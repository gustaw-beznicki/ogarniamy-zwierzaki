using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.DocumentReading;
using ogarniamy_zwierzaki_api.Documents;

namespace ogarniamy_zwierzaki_api.Tests;

// Completing a capture schedules its reading job in the same transaction: exactly once, never for an Uploading
// document, and never without the completion (or the completion without it). The completion response stays stable.
public sealed class DocumentReadingSchedulingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateOnly EventDate = new(2026, 9, 1);

    [Fact]
    public async Task Completion_schedules_one_queued_job_and_a_repeated_completion_leaves_it_unchanged()
    {
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var operationId = await factory.AddUploadAsync(
            userId, DocumentTestData.NewManifest(animalId, EventDate, DocumentFile.JpegContentType));
        Assert.Empty(await JobsAsync(operationId));

        await factory.RecordAllReceiptsAsync(userId, operationId);
        Assert.Empty(await JobsAsync(operationId));
        var stored = await factory.CompleteAsync(userId, operationId);
        Assert.NotNull(stored);

        var job = Assert.Single(await JobsAsync(operationId));
        Assert.Equal(DocumentReadingState.Queued, job.State);
        Assert.Equal(1, job.RunGeneration);
        Assert.Equal(0, job.AttemptCount);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.FailureCode);
        Assert.Null(job.CompletedAt);
        Assert.Null(job.PublishedRunGeneration);
        Assert.Equal(0, job.OwnerRetryCount);
        Assert.True(job.DueAt <= DateTimeOffset.UtcNow.AddMinutes(1));

        // A worker is reading it when a late completion retry arrives: the job and its lease stay as they are.
        await UseDatabaseAsync(db => db.Database.ExecuteSqlAsync($"""
            UPDATE document_reading_jobs SET due_at = now() - interval '1 day' WHERE document_id = {operationId}
            """));
        DocumentReadingClaim? claim;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            claim = await scope.ServiceProvider.GetRequiredService<DocumentReadingJobs>()
                .ClaimNextAsync(TimeSpan.FromMinutes(5));
        }

        Assert.Equal(operationId, claim?.DocumentId);
        var claimed = Assert.Single(await JobsAsync(operationId));

        var repeated = await factory.CompleteAsync(userId, operationId);
        Assert.NotNull(repeated);
        Assert.Equal(JsonSerializer.Serialize(stored), JsonSerializer.Serialize(repeated));
        var after = Assert.Single(await JobsAsync(operationId));
        Assert.Equal(JsonSerializer.Serialize(claimed), JsonSerializer.Serialize(after));
        Assert.Equal(DocumentReadingState.Reading, after.State);
    }

    [Fact]
    public async Task Concurrent_completions_schedule_exactly_one_job()
    {
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var operationId = await factory.AddUploadAsync(
            userId, DocumentTestData.NewManifest(animalId, EventDate, DocumentFile.PdfContentType));
        await factory.RecordAllReceiptsAsync(userId, operationId);

        var completions = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => factory.CompleteAsync(userId, operationId)));

        Assert.All(completions, Assert.NotNull);
        Assert.Single(completions.Select(c => JsonSerializer.Serialize(c)).Distinct());
        Assert.Equal(DocumentReadingState.Queued, Assert.Single(await JobsAsync(operationId)).State);
    }

    [Fact]
    public async Task A_failure_to_schedule_the_job_rolls_the_completion_back_and_a_retry_completes_once()
    {
        var faults = new InjectedDatabaseFaults();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(faults))));
        using var client = await host.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var png = TestOriginal.Png();
        var operationId = Guid.NewGuid();
        using (var created = await client.PutManifestAsync(operationId, DocumentApi.Manifest(animalId, png)))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        using (var uploaded = await client.PutFileAsync(operationId, 0, png))
        {
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        }

        faults.FailNextReadingJobInsert = true;
        using (var failed = await client.CompleteAsync(operationId))
        {
            await failed.AssertProblemAsync(HttpStatusCode.ServiceUnavailable, "storage_unavailable");
        }

        Assert.False(faults.FailNextReadingJobInsert);
        Assert.Empty(await client.ListDocumentIdsAsync(animalId));
        Assert.Empty(await JobsAsync(operationId));
        var document = await UseDatabaseAsync(db => db.Documents.AsNoTracking().SingleAsync(d => d.Id == operationId));
        Assert.Equal(DocumentStorageState.Uploading, document.StorageState);
        Assert.Null(document.UploadedAt);

        using var completed = await client.CompleteAsync(operationId);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var stored = await completed.ReadJsonAsync();
        Assert.Equal([operationId], await client.ListDocumentIdsAsync(animalId));
        Assert.Equal(DocumentReadingState.Queued, Assert.Single(await JobsAsync(operationId)).State);
        Assert.Equal(png.Bytes, await client.GetOriginalBytesAsync(Assert.Single(stored.OriginalUrls())));
    }

    private Task<List<DocumentReadingJob>> JobsAsync(Guid documentId) =>
        UseDatabaseAsync(db => db.DocumentReadingJobs.AsNoTracking().Where(j => j.DocumentId == documentId).ToListAsync());

    private async Task<T> UseDatabaseAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
