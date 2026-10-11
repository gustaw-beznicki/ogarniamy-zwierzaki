using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.DocumentReading;
using ogarniamy_zwierzaki_api.Storage;

namespace ogarniamy_zwierzaki_api.Tests;

// Document reading (S-03) and animal activity/versioned edits (S-06) together, on a database migrated through both
// migrations (AnimalActivity, then DocumentReading). Animals change only through their HTTP routes with If-Match, and
// the worker runs single jobs with a fake OCR provider, so each test crosses both features the way the product does.
public sealed class DocumentReadingAcceptanceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task An_inactive_animals_document_keeps_its_original_and_is_read_failed_retried_and_read_again()
    {
        await ParkExistingJobsAsync();
        using var owner = await factory.CreateCaptureClientAsync();
        using var other = await factory.CreateCaptureClientAsync();
        var animal = await CreateAnimalAsync(owner);
        var animalId = animal.GetProperty("id").GetGuid();
        var png = SampleOriginals.Png();
        var stored = await owner.CaptureAsync(animalId, png);
        var documentId = stored.GetProperty("id").GetGuid();
        var originalUrl = Assert.Single(stored.OriginalUrls());

        var inactive = await SetActivityAsync(owner, animalId, false, Quoted(animal));
        Assert.False(inactive.GetProperty("isActive").GetBoolean());
        Assert.Equal(1, inactive.GetProperty("storedDocumentCount").GetInt32());

        // The archive stays reachable: list, detail, original and reading status.
        Assert.Equal([documentId], await owner.ListDocumentIdsAsync(animalId));
        Assert.Equal(stored.GetRawText(), (await owner.GetJsonAsync($"/api/documents/{documentId}")).GetRawText());
        Assert.Equal(png.Bytes, await owner.GetOriginalBytesAsync(originalUrl));
        Assert.Equal("queued", await StateAsync(owner, documentId));

        // The worker has no activity predicate: the first attempt reaches the provider and fails for good.
        var failing = new FakeOcrProvider { Analyze = (_, _) => FakeOcrProvider.Failed("InvalidRequest", "InvalidContent") };
        Assert.True(await Worker(failing).RunOnceAsync(CancellationToken.None));
        var failed = await owner.GetJsonAsync(StatusPath(documentId));
        Assert.Equal("failed", failed.GetProperty("state").GetString());
        Assert.Equal("unreadable_content", failed.GetProperty("failureCode").GetString());
        Assert.True(failed.GetProperty("canRetry").GetBoolean());
        Assert.Equal(png.Bytes, await owner.GetOriginalBytesAsync(originalUrl));

        // Another account sees neither the status nor the original, and cannot retry.
        foreach (var path in new[] { StatusPath(documentId), originalUrl })
        {
            using var response = await other.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using (var response = await other.PostAsync(RetryPath(documentId), null))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // The owner's Retry of the inactive animal's document is accepted and the next attempt publishes the text.
        using (var retried = await owner.PostAsync(RetryPath(documentId), null))
        {
            Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
            Assert.Equal("queued", (await retried.ReadJsonAsync()).GetProperty("state").GetString());
        }

        var succeeding = new FakeOcrProvider { Analyze = (_, _) => FakeOcrProvider.Succeeded((1, "synthetic acceptance text")) };
        Assert.True(await Worker(succeeding).RunOnceAsync(CancellationToken.None));
        Assert.Equal("read", await StateAsync(owner, documentId));
        var job = await JobAsync(documentId);
        Assert.Equal((2, 2), (job.RunGeneration, job.PublishedRunGeneration));
        Assert.Equal(1, job.OwnerRetryCount);
        Assert.Equal(png.Bytes, await owner.GetOriginalBytesAsync(originalUrl));
        Assert.False((await GetAnimalAsync(owner, animalId)).GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task A_stale_animal_edit_is_rejected_and_leaves_its_documents_and_reading_jobs_unchanged()
    {
        await ParkExistingJobsAsync();
        using var owner = await factory.CreateCaptureClientAsync();
        var original = await CreateAnimalAsync(owner);
        var animalId = original.GetProperty("id").GetGuid();
        var stored = await owner.CaptureAsync(animalId, TestOriginal.Png());
        var documentId = stored.GetProperty("id").GetGuid();
        var jobBefore = await JobAsync(documentId);

        using (var renamed = await owner.SendJsonAsync(
            HttpMethod.Put, $"/api/animals/{animalId}/name", new { name = "Burek" }, Quoted(original)))
        {
            Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        }

        // Both edits still carry the version from before the rename.
        using (var staleActivity = await owner.SendJsonAsync(
            HttpMethod.Put, $"/api/animals/{animalId}/activity", new { isActive = false }, Quoted(original)))
        {
            await staleActivity.AssertProblemAsync(HttpStatusCode.PreconditionFailed, "animal_changed");
        }

        using (var staleRename = await owner.SendJsonAsync(
            HttpMethod.Put, $"/api/animals/{animalId}/name", new { name = "Stale" }, Quoted(original)))
        {
            await staleRename.AssertProblemAsync(HttpStatusCode.PreconditionFailed, "animal_changed");
        }

        var current = await GetAnimalAsync(owner, animalId);
        Assert.Equal("Burek", current.GetProperty("name").GetString());
        Assert.True(current.GetProperty("isActive").GetBoolean());
        Assert.Equal(1, current.GetProperty("storedDocumentCount").GetInt32());

        // The document shows the current animal name; its reading job was not touched by any edit.
        var detail = await owner.GetJsonAsync($"/api/documents/{documentId}");
        Assert.Equal("Burek", detail.GetProperty("animalName").GetString());
        Assert.Equal(JsonSerializer.Serialize(jobBefore), JsonSerializer.Serialize(await JobAsync(documentId)));
        Assert.Equal("queued", await StateAsync(owner, documentId));
    }

    [Fact]
    public async Task Repeating_a_completion_after_the_animal_became_inactive_returns_the_same_document_and_keeps_one_job()
    {
        await ParkExistingJobsAsync();
        using var owner = await factory.CreateCaptureClientAsync();
        var animal = await CreateAnimalAsync(owner);
        var animalId = animal.GetProperty("id").GetGuid();
        var png = SampleOriginals.Png();
        var operationId = Guid.NewGuid();
        using (var created = await owner.PutManifestAsync(operationId, DocumentApi.Manifest(animalId, png)))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        using (var uploaded = await owner.PutFileAsync(operationId, 0, png))
        {
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        }

        var first = await CompleteJsonAsync(owner, operationId);
        var jobAfterFirst = await JobAsync(operationId);
        Assert.Equal(DocumentReadingState.Queued, jobAfterFirst.State);

        // A completion retry after the animal was archived: same response, no second job, no requeue.
        await SetActivityAsync(owner, animalId, false, Quoted(animal));
        var repeated = await CompleteJsonAsync(owner, operationId);
        Assert.Equal(first.GetRawText(), repeated.GetRawText());
        Assert.Equal(JsonSerializer.Serialize(jobAfterFirst), JsonSerializer.Serialize(await JobAsync(operationId)));
        Assert.Equal(1, await JobCountAsync(operationId));

        // And after the job was read: the published revision stays, nothing is queued again.
        var provider = new FakeOcrProvider { Analyze = (_, _) => FakeOcrProvider.Succeeded((1, "synthetic acceptance text")) };
        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));
        var readJob = await JobAsync(operationId);
        Assert.Equal(DocumentReadingState.Read, readJob.State);

        var afterRead = await CompleteJsonAsync(owner, operationId);
        Assert.Equal(first.GetRawText(), afterRead.GetRawText());
        Assert.Equal(JsonSerializer.Serialize(readJob), JsonSerializer.Serialize(await JobAsync(operationId)));
        Assert.Equal(1, await JobCountAsync(operationId));
        Assert.Single(provider.Posts);
        Assert.Equal(png.Bytes, await owner.GetOriginalBytesAsync(Assert.Single(first.OriginalUrls())));
    }

    private static string StatusPath(Guid documentId) => $"/api/documents/{documentId}/reading";

    private static string RetryPath(Guid documentId) => $"/api/documents/{documentId}/reading/retry";

    private static string Quoted(JsonElement animal) => $"\"{animal.GetProperty("version").GetGuid()}\"";

    private static async Task<JsonElement> CreateAnimalAsync(HttpClient client)
    {
        using var response = await client.CreateAnimalAsync("Czarek");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    private static Task<JsonElement> GetAnimalAsync(HttpClient client, Guid animalId) =>
        client.GetJsonAsync($"/api/animals/{animalId}");

    private static async Task<JsonElement> SetActivityAsync(HttpClient client, Guid animalId, bool isActive, string ifMatch)
    {
        using var response = await client.SendJsonAsync(
            HttpMethod.Put, $"/api/animals/{animalId}/activity", new { isActive }, ifMatch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    private static async Task<JsonElement> CompleteJsonAsync(HttpClient client, Guid operationId)
    {
        using var response = await client.CompleteAsync(operationId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    private static async Task<string?> StateAsync(HttpClient client, Guid documentId) =>
        (await client.GetJsonAsync(StatusPath(documentId))).GetProperty("state").GetString();

    private DocumentReadingWorker Worker(FakeOcrProvider provider)
    {
        var reader = new AzureDocumentTextReader(
            new HttpClient(provider), new FakeTokenCredential(),
            new DocumentReadingOptions { Auth = DocumentReadingAuthMode.AzureManagedIdentity, Endpoint = new Uri(FakeOcrProvider.Endpoint) });
        var scopes = factory.Services.GetRequiredService<IServiceScopeFactory>();
        return new DocumentReadingWorker(
            scopes,
            DocumentReadingAvailability.Resolve(new DocumentReadingOptions(), isDevelopment: false),
            reader,
            new PdfTextReader(),
            new DocumentReadingCleanup(scopes, reader, NullLogger<DocumentReadingCleanup>.Instance),
            factory.Services.GetRequiredService<IOriginalStorage>(),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<DocumentReadingWorker>.Instance)
        {
            AnalysisPollInterval = TimeSpan.Zero,
            HeartbeatInterval = TimeSpan.FromMilliseconds(200),
        };
    }

    // Jobs of earlier tests in this class would otherwise compete for claims.
    private Task ParkExistingJobsAsync() => UseDatabaseAsync(db => db.Database.ExecuteSqlRawAsync("""
        UPDATE document_reading_jobs
        SET state = 'queued', lease_token = NULL, lease_expires_at = NULL, due_at = now() + interval '100 years'
        WHERE state IN ('queued', 'reading')
        """));

    private Task<DocumentReadingJob> JobAsync(Guid documentId) =>
        UseDatabaseAsync(db => db.DocumentReadingJobs.AsNoTracking().SingleAsync(j => j.DocumentId == documentId));

    private Task<int> JobCountAsync(Guid documentId) =>
        UseDatabaseAsync(db => db.DocumentReadingJobs.CountAsync(j => j.DocumentId == documentId));

    private async Task<T> UseDatabaseAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
