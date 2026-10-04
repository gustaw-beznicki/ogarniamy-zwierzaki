using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.Documents;
using ogarniamy_zwierzaki_api.Storage;

namespace ogarniamy_zwierzaki_api.Tests;

// Ambiguous outcomes between Blob Storage and PostgreSQL: retries reconcile them into exactly one complete document,
// and nothing partial is ever visible. Failures are injected at the storage adapter and EF Core boundaries.
public sealed class DocumentRecoveryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task An_original_stored_before_a_database_failure_is_reconciled_on_retry()
    {
        await using var host = CreateFaultyHost(out _, out var database);
        using var client = await host.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var png = TestOriginal.Png();
        var operationId = await CreateOperationAsync(client, animalId, png);

        database.FailNextReceiptUpdate = true;
        using (var failed = await client.PutFileAsync(operationId, 0, png))
        {
            await failed.AssertProblemAsync(HttpStatusCode.ServiceUnavailable, "storage_unavailable");
        }

        var slot = await SingleSlotAsync(host, operationId);
        Assert.Null(slot.Receipt);
        Assert.NotNull(await host.Services.GetRequiredService<IOriginalStorage>().GetReceiptAsync(slot.BlobKey, CancellationToken.None));
        Assert.Empty(await client.ListDocumentIdsAsync(animalId));

        using (var retried = await client.PutFileAsync(operationId, 0, png))
        {
            Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
            Assert.True((await retried.ReadJsonAsync()).GetProperty("stored").GetBoolean());
        }

        await AssertSingleStoredDocumentAsync(client, animalId, operationId, png);
    }

    [Fact]
    public async Task Completion_records_a_receipt_missing_after_a_database_failure()
    {
        await using var host = CreateFaultyHost(out _, out var database);
        using var client = await host.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var pdf = TestOriginal.Pdf();
        var operationId = await CreateOperationAsync(client, animalId, pdf);

        database.FailNextReceiptUpdate = true;
        using (var failed = await client.PutFileAsync(operationId, 0, pdf))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        }

        // The client goes straight to completion; the durable original's receipt is recovered from storage.
        await AssertSingleStoredDocumentAsync(client, animalId, operationId, pdf);
    }

    [Fact]
    public async Task A_lost_storage_response_after_the_write_is_reconciled_on_retry()
    {
        await using var host = CreateFaultyHost(out var storage, out _);
        using var client = await host.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var jpeg = TestOriginal.Jpeg();
        var operationId = await CreateOperationAsync(client, animalId, jpeg);

        storage.LoseNextCreateResponse = true;
        using (var failed = await client.PutFileAsync(operationId, 0, jpeg))
        {
            await failed.AssertProblemAsync(HttpStatusCode.ServiceUnavailable, "storage_unavailable");
        }

        using (var retried = await client.PutFileAsync(operationId, 0, jpeg))
        {
            Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        }

        await AssertSingleStoredDocumentAsync(client, animalId, operationId, jpeg);
    }

    [Fact]
    public async Task A_lost_completion_response_is_recovered_without_further_side_effects()
    {
        await using var host = CreateFaultyHost(out _, out var database);
        using var client = await host.CreateCaptureClientAsync();
        var firstAnimal = await client.CreateAnimalIdAsync("Czarek");
        var secondAnimal = await client.CreateAnimalIdAsync("Burek");
        var png = TestOriginal.Png();
        var operationId = await CreateOperationAsync(client, secondAnimal, png);
        using (var uploaded = await client.PutFileAsync(operationId, 0, png))
        {
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        }

        database.LoseNextCompletionCommit = true;
        using (var lost = await client.CompleteAsync(operationId))
        {
            await lost.AssertProblemAsync(HttpStatusCode.ServiceUnavailable, "storage_unavailable");
        }

        // The commit happened: the document is listed and the preference moved once.
        Assert.Equal([operationId], await client.ListDocumentIdsAsync(secondAnimal));
        var stored = await client.GetJsonAsync($"/api/documents/{operationId}");
        Assert.Equal(secondAnimal, (await client.GetJsonAsync("/api/capture-defaults")).GetProperty("defaultAnimalId").GetGuid());

        // Another capture moves the preference; a late retry of the first completion must not move it back.
        await client.CaptureAsync(firstAnimal, TestOriginal.Png());
        using var retried = await client.CompleteAsync(operationId);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal(stored.GetRawText(), (await retried.ReadJsonAsync()).GetRawText());
        Assert.Equal(firstAnimal, (await client.GetJsonAsync("/api/capture-defaults")).GetProperty("defaultAnimalId").GetGuid());
        Assert.Equal([operationId], await client.ListDocumentIdsAsync(secondAnimal));
    }

    [Fact]
    public async Task Concurrent_retries_of_every_step_produce_one_document()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var jpeg = TestOriginal.Jpeg(400_000);
        var png = TestOriginal.Png(300_000);
        var operationId = Guid.NewGuid();
        var manifest = DocumentApi.Manifest(animalId, jpeg, png);

        var creates = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.PutManifestAsync(operationId, manifest)));
        Assert.Equal(1, creates.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(creates, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK }));
        var fileIds = await Task.WhenAll(creates.Select(async r => (await r.ReadJsonAsync()).FileIds()));
        Assert.Single(fileIds.Select(ids => string.Join(',', ids)).Distinct());
        DisposeAll(creates);

        var uploads = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(i => client.PutFileAsync(operationId, i % 2, i % 2 == 0 ? jpeg : png)));
        Assert.All(uploads, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        DisposeAll(uploads);

        var completions = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.CompleteAsync(operationId)));
        Assert.All(completions, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var bodies = await Task.WhenAll(completions.Select(r => r.Content.ReadAsStringAsync()));
        Assert.Single(bodies.Distinct());
        DisposeAll(completions);

        Assert.Equal([operationId], await client.ListDocumentIdsAsync(animalId));
        var document = await client.GetJsonAsync($"/api/documents/{operationId}");
        var urls = document.OriginalUrls();
        Assert.Equal(jpeg.Bytes, await client.GetOriginalBytesAsync(urls[0]));
        Assert.Equal(png.Bytes, await client.GetOriginalBytesAsync(urls[1]));
    }

    [Fact]
    public async Task A_different_original_already_in_storage_is_a_conflict_and_is_never_overwritten()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var png = TestOriginal.Png();
        var operationId = await CreateOperationAsync(client, animalId, png);
        var slot = await SingleSlotAsync(factory, operationId);
        var storage = factory.Services.GetRequiredService<IOriginalStorage>();
        var squatter = TestOriginal.Png(png.Bytes.Length).Bytes;
        OriginalFileReceipt? squatterReceipt;
        using (var content = new MemoryStream(squatter))
        {
            squatterReceipt = await storage.CreateIfAbsentAsync(slot.BlobKey, content, "image/png", CancellationToken.None);
        }

        Assert.NotNull(squatterReceipt);

        using (var upload = await client.PutFileAsync(operationId, 0, png))
        {
            await upload.AssertProblemAsync(HttpStatusCode.Conflict, "upload_conflict");
        }

        using (var complete = await client.CompleteAsync(operationId))
        {
            await complete.AssertProblemAsync(HttpStatusCode.Conflict, "upload_conflict");
        }

        await using var stored = await storage.OpenReadAsync(
            slot.BlobKey, squatterReceipt.Length, squatterReceipt.Sha256, CancellationToken.None);
        Assert.NotNull(stored);
        using var copy = new MemoryStream();
        await stored.CopyToAsync(copy);
        Assert.Equal(squatter, copy.ToArray());
        Assert.Empty(await client.ListDocumentIdsAsync(animalId));
    }

    [Fact]
    public async Task A_storage_outage_returns_a_retryable_error_and_keeps_every_record()
    {
        await using var host = CreateFaultyHost(out var storage, out _);
        using var client = await host.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var stored = await client.CaptureAsync(animalId, TestOriginal.Png());
        var png = TestOriginal.Png();
        var operationId = await CreateOperationAsync(client, animalId, png);

        storage.Unavailable = true;
        using (var upload = await client.PutFileAsync(operationId, 0, png))
        {
            await upload.AssertProblemAsync(HttpStatusCode.ServiceUnavailable, "storage_unavailable");
        }

        using (var original = await client.GetAsync(Assert.Single(stored.OriginalUrls())))
        {
            await original.AssertProblemAsync(HttpStatusCode.ServiceUnavailable, "storage_unavailable");
        }

        Assert.Equal([stored.GetProperty("id").GetGuid()], await client.ListDocumentIdsAsync(animalId));

        storage.Unavailable = false;
        using (var upload = await client.PutFileAsync(operationId, 0, png))
        {
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        }

        using var completed = await client.CompleteAsync(operationId);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(2, (await client.ListDocumentIdsAsync(animalId)).Length);
    }

    [Fact]
    public async Task Completion_during_a_storage_outage_is_retryable_and_keeps_the_operation()
    {
        await using var host = CreateFaultyHost(out var storage, out var database);
        using var client = await host.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var png = TestOriginal.Png();
        var operationId = await CreateOperationAsync(client, animalId, png);

        // The original is durable but its receipt is not recorded, so completion has to ask storage for it.
        database.FailNextReceiptUpdate = true;
        using (var failed = await client.PutFileAsync(operationId, 0, png))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        }

        storage.Unavailable = true;
        using (var outage = await client.CompleteAsync(operationId))
        {
            await outage.AssertProblemAsync(HttpStatusCode.ServiceUnavailable, "storage_unavailable");
        }

        Assert.Empty(await client.ListDocumentIdsAsync(animalId));
        Assert.Null((await SingleSlotAsync(host, operationId)).Receipt);

        storage.Unavailable = false;
        await AssertSingleStoredDocumentAsync(client, animalId, operationId, png);
    }

    [Fact]
    public async Task Originals_are_retrievable_after_an_api_restart()
    {
        var pdf = SampleOriginals.Pdf(pages: 2);
        var photo = SampleOriginals.Jpeg();
        string email;
        JsonElement pdfDocument;
        JsonElement photoDocument;
        await using (var before = factory.WithWebHostBuilder(_ => { }))
        {
            var (client, signedUp) = await before.CreateSignedInClientAsync();
            using (client)
            {
                email = signedUp;
                await client.UseAntiforgeryTokenAsync();
                var animalId = await client.CreateAnimalIdAsync();
                pdfDocument = await client.CaptureAsync(animalId, pdf);
                photoDocument = await client.CaptureAsync(animalId, photo);
            }
        }

        // A new process on the same database and storage, reached through a fresh sign-in.
        await using var after = factory.WithWebHostBuilder(_ => { });
        using var reader = after.CreateClient();
        using (var login = await reader.LoginAsync(email, TestAccounts.ValidPassword))
        {
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        }

        var pdfId = pdfDocument.GetProperty("id").GetGuid();
        Assert.Equal(pdfDocument.GetRawText(), (await reader.GetJsonAsync($"/api/documents/{pdfId}")).GetRawText());
        Assert.Equal(pdf.Bytes, await reader.GetOriginalBytesAsync(Assert.Single(pdfDocument.OriginalUrls())));
        Assert.Equal(photo.Bytes, await reader.GetOriginalBytesAsync(Assert.Single(photoDocument.OriginalUrls())));
    }

    [Fact]
    public async Task Logs_carry_ids_and_failure_codes_but_never_file_names_or_contents()
    {
        var logs = new CapturedLogs();
        await using var host = CreateFaultyHost(out var storage, out _, logs);
        using var client = await host.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        const string fileName = "Rabies certificate Burek 2024.pdf";
        const string contentMarker = "CONFIDENTIAL-CONTENT-MARKER";
        var pdf = new TestOriginal(
            fileName, "application/pdf", Encoding.ASCII.GetBytes($"%PDF-1.7\n% {contentMarker}\n%%EOF\n"));
        var operationId = await CreateOperationAsync(client, animalId, pdf);

        storage.Unavailable = true;
        using (var outage = await client.PutFileAsync(operationId, 0, pdf))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, outage.StatusCode);
        }

        storage.Unavailable = false;
        using (var mismatch = await client.PutBytesAsync(operationId, 0, TestOriginal.Pdf(pdf.Bytes.Length).Bytes))
        {
            Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        }

        using (var uploaded = await client.PutFileAsync(operationId, 0, pdf))
        {
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        }

        await AssertSingleStoredDocumentAsync(client, animalId, operationId, pdf);
        var slot = await SingleSlotAsync(host, operationId);
        var fileId = slot.Id;
        await host.Services.GetRequiredService<BlobContainerClient>().DeleteBlobAsync(slot.BlobKey);
        using (var missing = await client.GetAsync($"/api/documents/{operationId}/files/{fileId}/original"))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, missing.StatusCode);
        }

        var entries = logs.Entries.ToArray();
        Assert.Contains(entries, e => e.Contains(operationId.ToString()) && e.Contains("StorageUnavailable"));
        Assert.Contains(entries, e => e.Contains(fileId.ToString()) && e.Contains("FileMismatch"));
        Assert.Contains(entries, e => e.Contains(operationId.ToString()) && e.Contains("is stored"));
        Assert.Contains(entries, e => e.StartsWith("Error:") && e.Contains(fileId.ToString()));
        Assert.All(entries, e =>
        {
            Assert.DoesNotContain(fileName, e);
            Assert.DoesNotContain("Rabies", e);
            Assert.DoesNotContain(contentMarker, e);
            Assert.DoesNotContain(TestAccounts.ValidPassword, e);
        });
    }

    [Fact]
    public async Task A_missing_original_is_reported_as_unavailable_without_removing_the_document()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var document = await client.CaptureAsync(animalId, TestOriginal.Png());
        var documentId = document.GetProperty("id").GetGuid();
        var slot = await SingleSlotAsync(factory, documentId);
        await factory.Services.GetRequiredService<BlobContainerClient>().DeleteBlobAsync(slot.BlobKey);

        using (var original = await client.GetAsync(Assert.Single(document.OriginalUrls())))
        {
            await original.AssertProblemAsync(HttpStatusCode.ServiceUnavailable, "original_unavailable");
        }

        Assert.Equal([documentId], await client.ListDocumentIdsAsync(animalId));
        Assert.Equal(document.GetRawText(), (await client.GetJsonAsync($"/api/documents/{documentId}")).GetRawText());
    }

    // A test host on the same containers whose storage adapter and database commands can be made to fail. Its log
    // entries also go to logs, when given, under the app's normal log filters.
    private WebApplicationFactory<Program> CreateFaultyHost(
        out FaultyOriginalStorage storage, out InjectedDatabaseFaults database, CapturedLogs? logs = null)
    {
        var faults = new InjectedDatabaseFaults();
        var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IOriginalStorage>(serviceProvider =>
                    new FaultyOriginalStorage(new BlobOriginalStorage(serviceProvider.GetRequiredService<BlobContainerClient>())));
                services.ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(faults));
            });
            if (logs is not null)
            {
                builder.ConfigureLogging(logging => logging.AddProvider(logs));
            }
        });
        storage = (FaultyOriginalStorage)host.Services.GetRequiredService<IOriginalStorage>();
        database = faults;
        return host;
    }

    private static async Task<Guid> CreateOperationAsync(HttpClient client, Guid animalId, params TestOriginal[] files)
    {
        var operationId = Guid.NewGuid();
        using var created = await client.PutManifestAsync(operationId, DocumentApi.Manifest(animalId, files));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return operationId;
    }

    // The operation's only file slot, read straight from the database.
    private static async Task<DocumentFile> SingleSlotAsync(WebApplicationFactory<Program> host, Guid operationId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.DocumentFiles.AsNoTracking().SingleAsync(f => f.DocumentId == operationId);
    }

    private static async Task AssertSingleStoredDocumentAsync(
        HttpClient client, Guid animalId, Guid operationId, TestOriginal original)
    {
        using var completed = await client.CompleteAsync(operationId);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var document = await completed.ReadJsonAsync();
        Assert.Equal([operationId], await client.ListDocumentIdsAsync(animalId));
        Assert.Equal(original.Bytes, await client.GetOriginalBytesAsync(Assert.Single(document.OriginalUrls())));
    }

    private static void DisposeAll(IEnumerable<HttpResponseMessage> responses)
    {
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }
}
