using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.DocumentReading;
using ogarniamy_zwierzaki_api.Documents;

namespace ogarniamy_zwierzaki_api.Tests;

// Provider cleanup on the real database with a fake provider: deletion obligations are kept until the provider
// acknowledges them, retried with a backoff after failures, recovered by a fresh instance (as after a restart)
// independently of job state, and never erase run-file records.
public sealed class DocumentReadingCleanupTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateOnly EventDate = new(2026, 9, 1);

    [Fact]
    public async Task A_failed_deletion_stays_pending_with_a_backoff_and_is_acknowledged_on_a_later_pass()
    {
        await ParkExistingCleanupsAsync();
        var runFileId = await AbandonedOperationAsync("op-retry");
        var deleteStatus = HttpStatusCode.InternalServerError;
        var provider = new FakeOcrProvider { Delete = _ => new HttpResponseMessage(deleteStatus) };

        Assert.Equal(0, await Cleanup(provider).RunDueAsync(CancellationToken.None));

        var deferred = await RunFileAsync(runFileId);
        Assert.Equal(DocumentReadingCleanupState.Pending, deferred.CleanupState);
        Assert.Equal(1, deferred.CleanupAttemptCount);
        Assert.True(deferred.NextCleanupAt > await DatabaseNowAsync() + TimeSpan.FromSeconds(50));
        // Not due before its backoff has passed.
        Assert.Equal(0, await Cleanup(provider).RunDueAsync(CancellationToken.None));
        Assert.Single(provider.DeletedIds);

        await MakeCleanupDueAsync(runFileId);
        deleteStatus = HttpStatusCode.NoContent;
        Assert.Equal(1, await Cleanup(provider).RunDueAsync(CancellationToken.None));

        var acknowledged = await RunFileAsync(runFileId);
        Assert.Equal(DocumentReadingCleanupState.Acknowledged, acknowledged.CleanupState);
        Assert.Equal("op-retry", acknowledged.ProviderOperationId);
        Assert.Equal(["op-retry", "op-retry"], provider.DeletedIds);
        Assert.All(provider.Requests, r => Assert.Equal("Bearer test-token", r.Authorization));
    }

    [Fact]
    public async Task A_result_the_provider_no_longer_has_counts_as_acknowledged()
    {
        await ParkExistingCleanupsAsync();
        var runFileId = await AbandonedOperationAsync("op-gone");
        var provider = new FakeOcrProvider { Delete = _ => FakeOcrProvider.Error(HttpStatusCode.NotFound, "NotFound") };

        Assert.Equal(1, await Cleanup(provider).RunDueAsync(CancellationToken.None));

        Assert.Equal(DocumentReadingCleanupState.Acknowledged, (await RunFileAsync(runFileId)).CleanupState);
    }

    [Fact]
    public async Task A_not_found_from_a_wrong_endpoint_or_path_keeps_the_cleanup_pending()
    {
        await ParkExistingCleanupsAsync();
        var runFileId = await AbandonedOperationAsync("op-wrong-path");
        // The body observed from the real service for an unknown path (2026-10-11), unlike its result-not-found body.
        var provider = new FakeOcrProvider
        {
            Delete = _ => FakeOcrProvider.Json(HttpStatusCode.NotFound, """{"error":{"code":"404","message":"Resource not found"}}"""),
        };

        Assert.Equal(0, await Cleanup(provider).RunDueAsync(CancellationToken.None));

        var runFile = await RunFileAsync(runFileId);
        Assert.Equal((DocumentReadingCleanupState.Pending, 1), (runFile.CleanupState, runFile.CleanupAttemptCount));
    }

    [Fact]
    public async Task A_rejected_identity_does_not_acknowledge_anything()
    {
        await ParkExistingCleanupsAsync();
        var runFileId = await AbandonedOperationAsync("op-forbidden");
        var provider = new FakeOcrProvider { Delete = _ => FakeOcrProvider.Error(HttpStatusCode.Forbidden, "Forbidden") };

        Assert.Equal(0, await Cleanup(provider).RunDueAsync(CancellationToken.None));

        var runFile = await RunFileAsync(runFileId);
        Assert.Equal((DocumentReadingCleanupState.Pending, 1), (runFile.CleanupState, runFile.CleanupAttemptCount));
    }

    [Fact]
    public async Task A_fresh_instance_recovers_obligations_independently_of_the_job_and_keeps_every_record()
    {
        await ParkExistingCleanupsAsync();
        // A worker submitted, then its job failed for good (as after a crash and a terminal error): the obligation is
        // all that is left, and no worker will ever claim the job again.
        var runFileId = await AbandonedOperationAsync("op-after-restart");
        var jobBefore = await JobOfRunFileAsync(runFileId);
        Assert.Equal(DocumentReadingState.Failed, jobBefore.State);

        // A new process: new cleanup instance, new provider client.
        var provider = new FakeOcrProvider();
        Assert.Equal(1, await Cleanup(provider).RunDueAsync(CancellationToken.None));

        Assert.Equal(["op-after-restart"], provider.DeletedIds);
        var runFile = await RunFileAsync(runFileId);
        Assert.Equal(DocumentReadingCleanupState.Acknowledged, runFile.CleanupState);
        Assert.Equal(DocumentReadingRunFileStage.Failed, runFile.Stage);
        var jobAfter = await JobOfRunFileAsync(runFileId);
        Assert.Equal((jobBefore.State, jobBefore.FailureCode, jobBefore.UpdatedAt), (jobAfter.State, jobAfter.FailureCode, jobAfter.UpdatedAt));

        // Nothing is due any more; acknowledged work is not repeated.
        Assert.Equal(0, await Cleanup(provider).RunDueAsync(CancellationToken.None));
        Assert.Single(provider.DeletedIds);
    }

    [Fact]
    public async Task An_operation_that_can_still_be_resumed_is_not_deleted()
    {
        await ParkExistingCleanupsAsync();
        var document = await StoredAsync();
        DocumentReadingClaim claim;
        Guid runFileId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var jobs = scope.ServiceProvider.GetRequiredService<DocumentReadingJobs>();
            claim = await ClaimAsync(jobs, document.Id);
            runFileId = (await jobs.StartRunFileAsync(claim, document.Files[0].Id, null))!.Value;
            Assert.True(await jobs.RecordOperationAsync(claim, runFileId, "op-resumable"));
            Assert.True(await jobs.RequeueAsync(claim, TimeSpan.FromMinutes(10)));
        }

        var provider = new FakeOcrProvider();
        Assert.Equal(0, await Cleanup(provider).RunDueAsync(CancellationToken.None));

        Assert.Empty(provider.Requests);
        Assert.Equal(DocumentReadingCleanupState.Pending, (await RunFileAsync(runFileId)).CleanupState);
    }

    private DocumentReadingCleanup Cleanup(FakeOcrProvider provider) => new(
        factory.Services.GetRequiredService<IServiceScopeFactory>(),
        new AzureDocumentTextReader(
            new HttpClient(provider), new FakeTokenCredential(),
            new DocumentReadingOptions { Auth = DocumentReadingAuthMode.AzureManagedIdentity, Endpoint = new Uri(FakeOcrProvider.Endpoint) }),
        NullLogger<DocumentReadingCleanup>.Instance);

    // A run file whose provider operation was recorded, then the job failed: its cleanup is pending and due.
    private async Task<Guid> AbandonedOperationAsync(string operationId)
    {
        var document = await StoredAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<DocumentReadingJobs>();
        var claim = await ClaimAsync(jobs, document.Id);
        var runFileId = (await jobs.StartRunFileAsync(claim, document.Files[0].Id, null))!.Value;
        Assert.True(await jobs.RecordOperationAsync(claim, runFileId, operationId));
        Assert.True(await jobs.FailAsync(claim, "unreadable_content"));
        return runFileId;
    }

    private async Task<DocumentReadingClaim> ClaimAsync(DocumentReadingJobs jobs, Guid documentId)
    {
        await UseDatabaseAsync(db => db.Database.ExecuteSqlAsync($"""
            UPDATE document_reading_jobs SET due_at = CASE WHEN document_id = {documentId}
                THEN now() - interval '1 second' ELSE now() + interval '100 years' END
            WHERE state = 'queued'
            """));
        var claim = await jobs.ClaimNextAsync(TimeSpan.FromMinutes(5));
        Assert.Equal(documentId, claim?.DocumentId);
        return claim!;
    }

    private async Task<DocumentDetails> StoredAsync()
    {
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        return await factory.AddStoredAsync(userId, animalId, EventDate, DocumentFile.PngContentType);
    }

    // Obligations left by earlier tests in this class would otherwise be picked up first.
    private Task ParkExistingCleanupsAsync() => UseDatabaseAsync(db => db.Database.ExecuteSqlRawAsync("""
        UPDATE document_reading_run_files SET next_cleanup_at = now() + interval '100 years'
        WHERE cleanup_state = 'pending'
        """));

    private Task MakeCleanupDueAsync(Guid runFileId) => UseDatabaseAsync(db => db.Database.ExecuteSqlAsync(
        $"UPDATE document_reading_run_files SET next_cleanup_at = now() - interval '1 second' WHERE id = {runFileId}"));

    private Task<DocumentReadingRunFile> RunFileAsync(Guid runFileId) =>
        UseDatabaseAsync(db => db.DocumentReadingRunFiles.AsNoTracking().SingleAsync(r => r.Id == runFileId));

    private async Task<DocumentReadingJob> JobOfRunFileAsync(Guid runFileId)
    {
        var runFile = await RunFileAsync(runFileId);
        return await UseDatabaseAsync(db => db.DocumentReadingJobs.AsNoTracking().SingleAsync(j => j.DocumentId == runFile.DocumentId));
    }

    private Task<DateTimeOffset> DatabaseNowAsync() =>
        UseDatabaseAsync(db => db.Database.SqlQuery<DateTimeOffset>($"SELECT now() AS \"Value\"").SingleAsync());

    private async Task<T> UseDatabaseAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
