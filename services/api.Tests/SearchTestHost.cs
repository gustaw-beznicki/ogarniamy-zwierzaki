using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ogarniamy_zwierzaki_api.Animals;
using ogarniamy_zwierzaki_api.Auth;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.DocumentEmbedding;
using ogarniamy_zwierzaki_api.DocumentReading;
using ogarniamy_zwierzaki_api.Documents;

namespace ogarniamy_zwierzaki_api.Tests;

// A second API host over the same database for search tests: embedding counts as available (a never-used developer
// credential and an example endpoint) and the embedding client is a FakeEmbeddingProvider, so the endpoint embeds queries
// without leaving the process. Documents are captured over HTTP, their reading is published through the real
// publication path, and the real embedding worker (driven one job at a time) embeds them with the same fake provider.
// Synthetic text only.
public sealed class SearchTestHost : IAsyncDisposable
{
    private readonly ApiFactory _factory;

    public SearchTestHost(ApiFactory factory, double? minSimilarity = null, bool embeddingAvailable = true)
    {
        _factory = factory;
        Host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?>();
                if (embeddingAvailable)
                {
                    // Development-only credential type; nothing calls it because the client below is the fake.
                    settings["Embedding:Auth"] = "DeveloperCredential";
                    settings["Embedding:Endpoint"] = "https://embedding.example.test/";
                }

                if (minSimilarity is { } min)
                {
                    settings["Search:MinSimilarity"] = min.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                configuration.AddInMemoryCollection(settings);
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmbeddingClient>();
                services.AddSingleton<IEmbeddingClient>(Provider);
            });
            builder.ConfigureLogging(logging => logging.AddProvider(Logs).SetMinimumLevel(LogLevel.Trace));
        });
    }

    public WebApplicationFactory<Program> Host { get; }

    public FakeEmbeddingProvider Provider { get; } = new();

    public CapturedLogs Logs { get; } = new();

    public ValueTask DisposeAsync() => Host.DisposeAsync();

    // A client that never follows redirects, without a session.
    public HttpClient CreateAnonymousClient() =>
        Host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    // A fresh account, signed in, sending its antiforgery token with every request.
    public async Task<SearchAccount> SignInAsync()
    {
        var client = CreateAnonymousClient();
        var email = TestAccounts.UniqueEmail();
        using (var registered = await client.RegisterAsync(email, TestAccounts.ValidPassword))
        {
            Assert.Equal(System.Net.HttpStatusCode.Created, registered.StatusCode);
        }

        await client.UseAntiforgeryTokenAsync();

        await using var scope = Host.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByEmailAsync(email);
        return new SearchAccount(client, user!.Id);
    }

    // Reading and embedding jobs left by earlier tests of the class must not be claimed by this one.
    public async Task ParkAllJobsAsync()
    {
        await ExecuteAsync("""
            UPDATE document_reading_jobs
            SET state = 'queued', lease_token = NULL, lease_expires_at = NULL, completed_at = NULL,
                failure_code = NULL, due_at = now() + interval '100 years'
            WHERE state IN ('queued', 'reading')
            """);
        await ExecuteAsync("""
            UPDATE document_embedding_jobs
            SET state = 'queued', lease_token = NULL, lease_expires_at = NULL, due_at = now() + interval '100 years'
            WHERE state IN ('queued', 'embedding')
            """);
    }

    // Captures a real original over HTTP and publishes its reading: one text per page (several texts need a PDF of that
    // many pages). The embedding job is queued by the publication; run EmbedAllAsync to embed it.
    public async Task<Guid> CaptureAndReadAsync(SearchAccount account, Guid animalId, params string[] pageTexts)
    {
        var original = pageTexts.Length == 1 ? TestOriginal.Png() : SampleOriginals.Pdf(pageTexts.Length);
        var document = await account.Client.CaptureAsync(animalId, original);
        var documentId = document.GetProperty("id").GetGuid();
        var fileId = document.GetProperty("files")[0].GetProperty("id").GetGuid();
        await PublishAsync(documentId, fileId, pageTexts);
        return documentId;
    }

    // A Stored document that has not been read: its reading job stays queued.
    public async Task<Guid> CaptureUnreadAsync(SearchAccount account, Guid animalId)
    {
        var document = await account.Client.CaptureAsync(animalId, TestOriginal.Png());
        return document.GetProperty("id").GetGuid();
    }

    // What an owner Retry followed by a new read does: the next run generation publishes a new revision.
    public async Task RepublishAsync(Guid documentId, params string[] pageTexts)
    {
        await ExecuteAsync($"""
            UPDATE document_reading_jobs
            SET state = 'queued', run_generation = run_generation + 1, attempt_count = 0, failure_code = NULL,
                completed_at = NULL, due_at = now() - interval '1 second'
            WHERE document_id = '{documentId}'
            """);
        var fileId = await UseDatabaseAsync(db => db.DocumentFiles.AsNoTracking()
            .Where(f => f.DocumentId == documentId).Select(f => f.Id).FirstAsync());
        await PublishAsync(documentId, fileId, pageTexts);
    }

    public async Task EmbedAllAsync()
    {
        var loggers = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Information));
        var worker = new DocumentEmbeddingWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            EmbeddingAvailability.Resolve(new EmbeddingOptions(), isDevelopment: false),
            Provider,
            new DocumentEmbeddingWakeUp(),
            TimeProvider.System,
            loggers.CreateLogger<DocumentEmbeddingWorker>());
        while (await worker.RunOnceAsync(CancellationToken.None))
        {
        }
    }

    public Task DeactivateAnimalAsync(string userId, Guid animalId) => UseScopeAsync(async services =>
    {
        var animals = services.GetRequiredService<OwnedAnimals>();
        var animal = await animals.FindAsync(userId, animalId);
        await animals.SetActivityAsync(userId, animalId, animal!.Version, false);
    });

    public Task ExecuteAsync(string sql) => UseDatabaseAsync(db => db.Database.ExecuteSqlRawAsync(sql));

    public Task<T> UseDatabaseAsync<T>(Func<AppDbContext, Task<T>> action) =>
        UseScopeAsync(async services => await action(services.GetRequiredService<AppDbContext>()));

    public static Task<HttpResponseMessage> SearchAsync(HttpClient client, string? query) =>
        client.PostAsJsonAsync("/api/search", new { query });

    // The 200 body of a search.
    public static async Task<JsonElement> SearchOkAsync(HttpClient client, string query)
    {
        using var response = await SearchAsync(client, query);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    public static Guid[] DocumentIds(JsonElement body) =>
        body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("documentId").GetGuid()).ToArray();

    private async Task PublishAsync(Guid documentId, Guid fileId, string[] pageTexts)
    {
        var pages = pageTexts.Select((text, index) => new DocumentTextPage
        {
            DocumentFileId = fileId,
            FilePosition = 0,
            SourcePageNumber = index + 1,
            ExtractionMethod = DocumentTextExtractionMethod.Ocr,
            Text = text,
        }).ToList();
        var publication = await UseScopeAsync(async services =>
        {
            var jobs = services.GetRequiredService<DocumentReadingJobs>();
            var claim = Assert.IsType<DocumentReadingClaim>(await jobs.ClaimNextAsync(TimeSpan.FromMinutes(5)));
            Assert.Equal(documentId, claim.DocumentId);
            return await jobs.PublishAsync(claim, pages);
        });
        Assert.Equal(DocumentReadingPublication.Published, publication);
    }

    private async Task<T> UseScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private Task UseScopeAsync(Func<IServiceProvider, Task> action) =>
        UseScopeAsync<int>(async services =>
        {
            await action(services);
            return 0;
        });
}
