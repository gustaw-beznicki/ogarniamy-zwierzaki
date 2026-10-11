using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.DocumentReading;
using ogarniamy_zwierzaki_api.Documents;

namespace ogarniamy_zwierzaki_api.Tests;

// DocumentReadingJobs on the real database: competing claims, lease expiry and recovery, token/generation fencing,
// complete and atomic publication, and cleanup obligations that survive all of them. No provider is called.
public sealed class DocumentReadingRepositoryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateOnly EventDate = new(2026, 9, 1);

    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task Competing_claims_never_hand_out_one_job_twice()
    {
        await ParkExistingJobsAsync();
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var first = await factory.AddStoredAsync(userId, animalId, EventDate);
        var second = await factory.AddStoredAsync(userId, animalId, EventDate, DocumentFile.PdfContentType);

        var concurrent = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => ClaimAsync()));
        var claims = concurrent.OfType<DocumentReadingClaim>().ToList();
        while (await ClaimAsync() is { } late)
        {
            claims.Add(late);
        }

        Assert.Equal(
            new[] { first.Id, second.Id }.Order(), claims.Select(c => c.DocumentId).Order());
        Assert.Equal(2, claims.Select(c => c.LeaseToken).Distinct().Count());
        Assert.All(claims, c =>
        {
            Assert.Equal(1, c.RunGeneration);
            Assert.Equal(1, c.AttemptCount);
        });
        Assert.All(await JobsAsync(first.Id, second.Id), job =>
        {
            Assert.Equal(DocumentReadingState.Reading, job.State);
            Assert.True(job.LeaseExpiresAt > DateTimeOffset.UtcNow);
        });
    }

    [Fact]
    public async Task An_expired_lease_is_reclaimed_and_the_old_holder_is_fenced_without_losing_provider_work()
    {
        await ParkExistingJobsAsync();
        var document = await StoredAsync(DocumentFile.PdfContentType);
        var fileId = document.Files[0].Id;
        var stale = Assert.IsType<DocumentReadingClaim>(await ClaimAsync());

        var submitted = await UseJobsAsync(jobs => jobs.StartRunFileAsync(stale, fileId, "1-2"));
        Assert.NotNull(submitted);
        Assert.True(await UseJobsAsync(jobs => jobs.RecordOperationAsync(stale, submitted.Value, "op-submitted")));
        var unsubmitted = await UseJobsAsync(jobs => jobs.StartRunFileAsync(stale, fileId, null));
        Assert.NotNull(unsubmitted);

        await ExpireLeaseAsync(document.Id);
        Assert.Null(await UseJobsAsync(jobs => jobs.RenewLeaseAsync(stale, Lease)));

        var current = Assert.IsType<DocumentReadingClaim>(await ClaimAsync());
        Assert.Equal(document.Id, current.DocumentId);
        Assert.NotEqual(stale.LeaseToken, current.LeaseToken);
        Assert.Equal(stale.RunGeneration, current.RunGeneration);
        Assert.Equal(2, current.AttemptCount);

        // The old holder can no longer write anything for the job...
        Assert.Null(await UseJobsAsync(jobs => jobs.StartRunFileAsync(stale, fileId, null)));
        Assert.False(await UseJobsAsync(jobs => jobs.RecordFetchedTextAsync(stale, submitted.Value, "stale text")));
        Assert.Equal(
            DocumentReadingPublication.LeaseLost,
            await UseJobsAsync(jobs => jobs.PublishAsync(stale, Pages(fileId, "stale text"))));
        Assert.False(await UseJobsAsync(jobs => jobs.RequeueAsync(stale, TimeSpan.Zero)));
        Assert.False(await UseJobsAsync(jobs => jobs.FailAsync(stale, "stale_failure")));
        Assert.Empty(await UseJobsAsync(jobs => jobs.ListRunFilesAsync(stale)));

        // ...except an operation ID it learned late, which is kept as an abandoned run file that still needs cleanup.
        Assert.False(await UseJobsAsync(jobs => jobs.RecordOperationAsync(stale, unsubmitted.Value, "op-late")));

        var runFiles = await UseJobsAsync(jobs => jobs.ListRunFilesAsync(current));
        Assert.Equal([submitted.Value, unsubmitted.Value], runFiles.Select(r => r.Id));
        Assert.Equal(DocumentReadingRunFileStage.Submitted, runFiles[0].Stage);
        Assert.Equal("op-submitted", runFiles[0].ProviderOperationId);
        Assert.Equal("1-2", runFiles[0].SelectedPageRanges);
        Assert.Null(runFiles[0].FetchedText);
        Assert.Equal(DocumentReadingRunFileStage.Failed, runFiles[1].Stage);
        Assert.Equal("op-late", runFiles[1].ProviderOperationId);
        Assert.Null(runFiles[1].SelectedPageRanges);
        Assert.All(runFiles, r => Assert.Equal(DocumentReadingCleanupState.Pending, r.CleanupState));

        // The new holder resumes the saved operation and publishes.
        Assert.True(await UseJobsAsync(jobs => jobs.RecordFetchedTextAsync(current, submitted.Value, "[\"checkpoint\"]")));
        Assert.Equal(
            DocumentReadingPublication.Published,
            await UseJobsAsync(jobs => jobs.PublishAsync(current, Pages(fileId, "Rabies vaccination", ""))));
        var job = Assert.Single(await JobsAsync(document.Id));
        Assert.Equal(DocumentReadingState.Read, job.State);
        Assert.Equal(2, job.AttemptCount);

        // Both provider results are now due for deletion, independently of any lease.
        var due = (await UseJobsAsync(jobs => jobs.ListDueCleanupsAsync(100))).Where(c => c.DocumentId == document.Id).ToList();
        Assert.Equal(["op-late", "op-submitted"], due.Select(c => c.ProviderOperationId).Order());
        Assert.True(await UseJobsAsync(jobs => jobs.AcknowledgeCleanupAsync(submitted.Value)));
        Assert.False(await UseJobsAsync(jobs => jobs.AcknowledgeCleanupAsync(submitted.Value)));
        Assert.True(await UseJobsAsync(jobs => jobs.DeferCleanupAsync(unsubmitted.Value, TimeSpan.FromMinutes(10))));
        Assert.DoesNotContain(
            await UseJobsAsync(jobs => jobs.ListDueCleanupsAsync(100)), c => c.DocumentId == document.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().DocumentReadingRunFiles
            .AsNoTracking()
            .Where(r => r.DocumentId == document.Id)
            .ToDictionaryAsync(r => r.Id);
        Assert.Equal(DocumentReadingCleanupState.Acknowledged, stored[submitted.Value].CleanupState);
        Assert.Equal(DocumentReadingCleanupState.Pending, stored[unsubmitted.Value].CleanupState);
        Assert.Equal(1, stored[unsubmitted.Value].CleanupAttemptCount);
    }

    [Fact]
    public async Task A_wrong_token_or_run_generation_is_fenced_from_every_write()
    {
        await ParkExistingJobsAsync();
        var document = await StoredAsync(DocumentFile.PngContentType);
        var fileId = document.Files[0].Id;
        var claim = Assert.IsType<DocumentReadingClaim>(await ClaimAsync());

        foreach (var forged in new[]
                 {
                     claim with { LeaseToken = Guid.NewGuid() },
                     claim with { RunGeneration = claim.RunGeneration + 1 },
                 })
        {
            Assert.Null(await UseJobsAsync(jobs => jobs.RenewLeaseAsync(forged, Lease)));
            Assert.Null(await UseJobsAsync(jobs => jobs.StartRunFileAsync(forged, fileId, null)));
            Assert.Equal(
                DocumentReadingPublication.LeaseLost,
                await UseJobsAsync(jobs => jobs.PublishAsync(forged, Pages(fileId, "text"))));
            Assert.False(await UseJobsAsync(jobs => jobs.RequeueAsync(forged, TimeSpan.Zero)));
            Assert.False(await UseJobsAsync(jobs => jobs.FailAsync(forged, "forged")));
        }

        var renewed = await UseJobsAsync(jobs => jobs.RenewLeaseAsync(claim, TimeSpan.FromMinutes(30)));
        Assert.NotNull(renewed);
        Assert.True(renewed.LeaseExpiresAt > claim.LeaseExpiresAt);
        var job = Assert.Single(await JobsAsync(document.Id));
        Assert.Equal(DocumentReadingState.Reading, job.State);
        Assert.Equal(claim.LeaseToken, job.LeaseToken);
        Assert.Empty(await PagesAsync(document.Id));
    }

    [Fact]
    public async Task Publication_writes_a_complete_revision_and_the_read_state_together_or_nothing()
    {
        await ParkExistingJobsAsync();
        var photos = await StoredAsync(DocumentFile.JpegContentType, DocumentFile.PngContentType);
        var claim = Assert.IsType<DocumentReadingClaim>(await ClaimAsync());
        var firstPhoto = photos.Files[0].Id;
        var secondPhoto = photos.Files[1].Id;

        // An incomplete or invalid revision is refused before anything is written.
        await Assert.ThrowsAsync<ArgumentException>(() => UseJobsAsync(jobs => jobs.PublishAsync(claim, Pages(firstPhoto, "a"))));
        await Assert.ThrowsAsync<ArgumentException>(() => UseJobsAsync(jobs => jobs.PublishAsync(
            claim, [.. Pages(firstPhoto, "a"), .. Pages(secondPhoto, "b", "c")])));
        await Assert.ThrowsAsync<ArgumentException>(() => UseJobsAsync(jobs => jobs.PublishAsync(
            claim, [.. Pages(firstPhoto, "a"), .. Pages(Guid.NewGuid(), "b")])));

        // A database failure halfway through publication leaves neither pages nor a Read state.
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => UseJobsAsync(jobs => jobs.PublishAsync(
            claim, [.. Pages(firstPhoto, "a"), .. Pages(secondPhoto, "invalid \0 text")])));
        Assert.Empty(await PagesAsync(photos.Id));
        Assert.Equal(DocumentReadingState.Reading, Assert.Single(await JobsAsync(photos.Id)).State);

        Assert.Equal(
            DocumentReadingPublication.Published,
            await UseJobsAsync(jobs => jobs.PublishAsync(claim, [.. Pages(secondPhoto, "Second"), .. Pages(firstPhoto, "")])));

        var pages = await PagesAsync(photos.Id);
        Assert.Equal([(0, 1, ""), (1, 1, "Second")], pages.Select(p => (p.FilePosition, p.SourcePageNumber, p.Text)));
        Assert.Equal([firstPhoto, secondPhoto], pages.Select(p => p.DocumentFileId));
        Assert.All(pages, p => Assert.Equal(claim.RunGeneration, p.RunGeneration));
        var job = Assert.Single(await JobsAsync(photos.Id));
        Assert.Equal(DocumentReadingState.Read, job.State);
        Assert.Equal(claim.RunGeneration, job.PublishedRunGeneration);
        Assert.NotNull(job.CompletedAt);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.LeaseExpiresAt);
        Assert.Null(job.FailureCode);

        // Publication ends the lease: a second publication by the same claim is refused.
        Assert.Equal(
            DocumentReadingPublication.LeaseLost,
            await UseJobsAsync(jobs => jobs.PublishAsync(claim, [.. Pages(firstPhoto, "x"), .. Pages(secondPhoto, "y")])));
        Assert.Equal(2, (await PagesAsync(photos.Id)).Count);
    }

    [Fact]
    public async Task An_entirely_empty_document_is_not_published_and_fails_with_a_sanitized_code()
    {
        await ParkExistingJobsAsync();
        var pdf = await StoredAsync(DocumentFile.PdfContentType);
        var fileId = pdf.Files[0].Id;
        var claim = Assert.IsType<DocumentReadingClaim>(await ClaimAsync());
        var runFile = await UseJobsAsync(jobs => jobs.StartRunFileAsync(claim, fileId, "1-3"));
        Assert.NotNull(runFile);
        Assert.True(await UseJobsAsync(jobs => jobs.RecordOperationAsync(claim, runFile.Value, "op-empty")));
        // Repeating the same ID is a no-op; a different one is a caller bug.
        Assert.True(await UseJobsAsync(jobs => jobs.RecordOperationAsync(claim, runFile.Value, "op-empty")));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => UseJobsAsync(jobs => jobs.RecordOperationAsync(claim, runFile.Value, "op-other")));

        Assert.Equal(
            DocumentReadingPublication.EmptyDocument,
            await UseJobsAsync(jobs => jobs.PublishAsync(claim, Pages(fileId, "", " \n\t", ""))));
        Assert.Empty(await PagesAsync(pdf.Id));
        Assert.Equal(DocumentReadingState.Reading, Assert.Single(await JobsAsync(pdf.Id)).State);

        await Assert.ThrowsAsync<ArgumentException>(
            () => UseJobsAsync(jobs => jobs.FailAsync(claim, "Provider said: page empty")));
        Assert.True(await UseJobsAsync(jobs => jobs.FailAsync(claim, "empty_document")));

        var job = Assert.Single(await JobsAsync(pdf.Id));
        Assert.Equal(DocumentReadingState.Failed, job.State);
        Assert.Equal("empty_document", job.FailureCode);
        Assert.NotNull(job.CompletedAt);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.PublishedRunGeneration);
        Assert.Null(await ClaimAsync());

        // The abandoned provider operation keeps its deletion obligation and is due for cleanup.
        Assert.Contains(
            await UseJobsAsync(jobs => jobs.ListDueCleanupsAsync(100)),
            c => c.RunFileId == runFile.Value && c.ProviderOperationId == "op-empty");
    }

    [Fact]
    public async Task A_transient_failure_requeues_the_same_generation_with_its_run_files()
    {
        await ParkExistingJobsAsync();
        var pdf = await StoredAsync(DocumentFile.PdfContentType);
        var fileId = pdf.Files[0].Id;
        var claim = Assert.IsType<DocumentReadingClaim>(await ClaimAsync());
        var runFile = await UseJobsAsync(jobs => jobs.StartRunFileAsync(claim, fileId, null));
        Assert.NotNull(runFile);
        Assert.True(await UseJobsAsync(jobs => jobs.RecordOperationAsync(claim, runFile.Value, "op-transient")));

        Assert.True(await UseJobsAsync(jobs => jobs.RequeueAsync(claim, TimeSpan.FromMinutes(10))));
        var queued = Assert.Single(await JobsAsync(pdf.Id));
        Assert.Equal(DocumentReadingState.Queued, queued.State);
        Assert.Null(queued.LeaseToken);
        Assert.Null(queued.FailureCode);
        Assert.True(queued.DueAt > DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.Null(await ClaimAsync());
        // The unfinished operation is resumable, so it is not yet due for cleanup.
        Assert.DoesNotContain(await UseJobsAsync(jobs => jobs.ListDueCleanupsAsync(100)), c => c.RunFileId == runFile.Value);

        await UseDatabaseAsync(db => db.Database.ExecuteSqlAsync(
            $"UPDATE document_reading_jobs SET due_at = now() - interval '1 second' WHERE document_id = {pdf.Id}"));
        var resumed = Assert.IsType<DocumentReadingClaim>(await ClaimAsync());
        Assert.Equal(claim.RunGeneration, resumed.RunGeneration);
        Assert.Equal(2, resumed.AttemptCount);
        var runFiles = await UseJobsAsync(jobs => jobs.ListRunFilesAsync(resumed));
        Assert.Equal(("op-transient", DocumentReadingRunFileStage.Submitted), (Assert.Single(runFiles).ProviderOperationId, runFiles[0].Stage));
    }

    [Fact]
    public async Task A_published_revision_is_kept_until_a_later_generation_replaces_it()
    {
        await ParkExistingJobsAsync();
        var pdf = await StoredAsync(DocumentFile.PdfContentType);
        var fileId = pdf.Files[0].Id;
        var first = Assert.IsType<DocumentReadingClaim>(await ClaimAsync());
        Assert.Equal(
            DocumentReadingPublication.Published,
            await UseJobsAsync(jobs => jobs.PublishAsync(first, Pages(fileId, "First revision"))));

        // A later generation (as a future owner Retry or reprocessing would start) fails: the first revision stays.
        await StartNextGenerationAsync(pdf.Id);
        var second = Assert.IsType<DocumentReadingClaim>(await ClaimAsync());
        Assert.Equal(first.RunGeneration + 1, second.RunGeneration);
        Assert.Equal(1, second.AttemptCount);
        Assert.True(await UseJobsAsync(jobs => jobs.FailAsync(second, "ocr_unavailable")));
        Assert.Equal(["First revision"], (await PagesAsync(pdf.Id)).Select(p => p.Text));
        Assert.Equal(first.RunGeneration, Assert.Single(await JobsAsync(pdf.Id)).PublishedRunGeneration);

        // A generation that publishes replaces it in the same transaction.
        await StartNextGenerationAsync(pdf.Id);
        var third = Assert.IsType<DocumentReadingClaim>(await ClaimAsync());
        Assert.Equal(
            DocumentReadingPublication.Published,
            await UseJobsAsync(jobs => jobs.PublishAsync(third, Pages(fileId, "Third revision", "page two"))));
        var pages = await PagesAsync(pdf.Id);
        Assert.Equal(["Third revision", "page two"], pages.Select(p => p.Text));
        Assert.All(pages, p => Assert.Equal(third.RunGeneration, p.RunGeneration));
        Assert.Equal(third.RunGeneration, Assert.Single(await JobsAsync(pdf.Id)).PublishedRunGeneration);
    }

    [Fact]
    public async Task The_database_rejects_inconsistent_job_and_page_rows()
    {
        await ParkExistingJobsAsync();
        var pdf = await StoredAsync(DocumentFile.PdfContentType);
        var other = await StoredAsync(DocumentFile.PngContentType);

        // Reading without a lease, Read without a published revision, Failed without a code, a lease while Queued.
        foreach (var update in new FormattableString[]
                 {
                     $"UPDATE document_reading_jobs SET state = 'reading' WHERE document_id = {pdf.Id}",
                     $"UPDATE document_reading_jobs SET state = 'read', completed_at = now() WHERE document_id = {pdf.Id}",
                     $"UPDATE document_reading_jobs SET state = 'failed', completed_at = now() WHERE document_id = {pdf.Id}",
                     $"UPDATE document_reading_jobs SET lease_token = gen_random_uuid() WHERE document_id = {pdf.Id}",
                 })
        {
            await Assert.ThrowsAsync<PostgresException>(() => UseDatabaseAsync(db => db.Database.ExecuteSqlAsync(update)));
        }

        // A page must reference an original of its own document.
        await Assert.ThrowsAsync<PostgresException>(() => UseDatabaseAsync(db => db.Database.ExecuteSqlAsync($"""
            INSERT INTO document_text_pages (document_id, run_generation, document_file_id, source_page_number,
                file_position, extraction_method, text)
            VALUES ({pdf.Id}, 1, {other.Files[0].Id}, 1, 0, 'ocr', 'x')
            """)));
    }

    private async Task<DocumentDetails> StoredAsync(params string[] contentTypes)
    {
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        return await factory.AddStoredAsync(userId, animalId, EventDate, contentTypes);
    }

    // Jobs left by earlier tests in this class would otherwise compete for claims.
    private Task ParkExistingJobsAsync() => UseDatabaseAsync(db => db.Database.ExecuteSqlRawAsync("""
        UPDATE document_reading_jobs
        SET state = 'queued', lease_token = NULL, lease_expires_at = NULL, due_at = now() + interval '100 years'
        WHERE state IN ('queued', 'reading')
        """));

    private Task ExpireLeaseAsync(Guid documentId) => UseDatabaseAsync(db => db.Database.ExecuteSqlAsync(
        $"UPDATE document_reading_jobs SET lease_expires_at = now() - interval '1 second' WHERE document_id = {documentId}"));

    // What an owner Retry will do (Phase 4): a finished job is queued again in the next run generation.
    private Task StartNextGenerationAsync(Guid documentId) => UseDatabaseAsync(db => db.Database.ExecuteSqlAsync($"""
        UPDATE document_reading_jobs
        SET state = 'queued', run_generation = run_generation + 1, attempt_count = 0, failure_code = NULL,
            completed_at = NULL, due_at = now() - interval '1 second'
        WHERE document_id = {documentId}
        """));

    private Task<DocumentReadingClaim?> ClaimAsync() => UseJobsAsync(jobs => jobs.ClaimNextAsync(Lease));

    private static List<DocumentTextPage> Pages(Guid fileId, params string[] texts) =>
        texts.Select((text, index) => new DocumentTextPage
        {
            DocumentFileId = fileId,
            SourcePageNumber = index + 1,
            ExtractionMethod = DocumentTextExtractionMethod.Ocr,
            Text = text,
        }).ToList();

    private Task<List<DocumentReadingJob>> JobsAsync(params Guid[] documentIds) =>
        UseDatabaseAsync(db => db.DocumentReadingJobs.AsNoTracking().Where(j => documentIds.Contains(j.DocumentId)).ToListAsync());

    private Task<List<DocumentTextPage>> PagesAsync(Guid documentId) =>
        UseDatabaseAsync(db => db.DocumentTextPages
            .AsNoTracking()
            .Where(p => p.DocumentId == documentId)
            .OrderBy(p => p.RunGeneration)
            .ThenBy(p => p.FilePosition)
            .ThenBy(p => p.SourcePageNumber)
            .ToListAsync());

    // Each call gets its own scope (and DbContext), as separate worker iterations would.
    private async Task<T> UseJobsAsync<T>(Func<DocumentReadingJobs, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<DocumentReadingJobs>());
    }

    private async Task<T> UseDatabaseAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task UseDatabaseAsync(Func<AppDbContext, Task<int>> action) => UseDatabaseAsync<int>(action);
}
