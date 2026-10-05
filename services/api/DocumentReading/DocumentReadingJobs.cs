using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.Documents;

namespace ogarniamy_zwierzaki_api.DocumentReading;

// TRUSTED SYSTEM BOUNDARY. Job, run-file and text-page access for the background reading worker and provider cleanup.
// It deliberately bypasses owner scoping: it addresses jobs by document ID with no user at all. It must never be
// injected into an HTTP handler or used to answer a user's request; owner-facing reads go through OwnedDocuments (or a
// future owner-scoped reading service), which reaches documents only through the user's animal memberships. No
// animal-activity predicate applies here: documents of inactive animals are read like any other.
//
// Fencing: ClaimNextAsync hands out a unique lease token for the job's current run generation. Every later write for
// the job locks the job row and requires that token, that generation and an unexpired lease, all compared with database
// time (now()). A worker whose lease expired or was taken over gets false / LeaseLost and must stop. Each operation is
// a short transaction; no transaction is held across provider or file I/O.
//
// Provider operation IDs and cleanup obligations are never discarded: reclaiming an expired job keeps its run
// generation, so run files stay resumable, and a stale worker may still record an operation ID it learned (as an
// abandoned run file that only needs cleanup).
public sealed partial class DocumentReadingJobs(AppDbContext db)
{
    // Claims the next due job: a Queued job whose DueAt has passed, or a Reading job whose lease expired. Concurrent
    // claimers skip each other's locked rows, so each job goes to at most one of them. Increments AttemptCount.
    // Null when nothing is due.
    public async Task<DocumentReadingClaim?> ClaimNextAsync(TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        var token = Guid.NewGuid();

        // One statement in its own implicit transaction: the row lock lasts only for the claim itself.
        var claimed = await db.Database.SqlQuery<Guid>($"""
            UPDATE document_reading_jobs
            SET state = 'reading', lease_token = {token}, lease_expires_at = now() + {leaseDuration},
                attempt_count = attempt_count + 1, updated_at = now()
            WHERE document_id = (
                SELECT document_id FROM document_reading_jobs
                WHERE (state = 'queued' AND due_at <= now()) OR (state = 'reading' AND lease_expires_at <= now())
                ORDER BY due_at, document_id
                LIMIT 1
                FOR UPDATE SKIP LOCKED)
            RETURNING document_id AS "Value"
            """).ToListAsync(cancellationToken);
        if (claimed.Count == 0)
        {
            return null;
        }

        var documentId = claimed[0];
        return await db.DocumentReadingJobs
            .AsNoTracking()
            .Where(j => j.DocumentId == documentId && j.LeaseToken == token)
            .Select(j => new DocumentReadingClaim(
                j.DocumentId, j.RunGeneration, token, j.LeaseExpiresAt!.Value, j.AttemptCount))
            .SingleOrDefaultAsync(cancellationToken);
    }

    // Extends a still-valid lease to now() + leaseDuration. Null when the lease expired or was taken over: an expired
    // lease is never revived, because another worker may already be entitled to claim the job.
    public async Task<DocumentReadingClaim?> RenewLeaseAsync(
        DocumentReadingClaim claim, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        var renewed = await db.Database.SqlQuery<DateTimeOffset>($"""
            UPDATE document_reading_jobs
            SET lease_expires_at = now() + {leaseDuration}, updated_at = now()
            WHERE document_id = {claim.DocumentId} AND state = 'reading' AND lease_token = {claim.LeaseToken}
                AND run_generation = {claim.RunGeneration} AND lease_expires_at > now()
            RETURNING lease_expires_at AS "Value"
            """).ToListAsync(cancellationToken);
        return renewed.Count == 1 ? claim with { LeaseExpiresAt = renewed[0] } : null;
    }

    // The run files of the claim's run generation, oldest first, so a worker can resume saved provider operations
    // and checkpointed text. Empty when the lease is no longer current.
    public async Task<IReadOnlyList<DocumentReadingRunFile>> ListRunFilesAsync(
        DocumentReadingClaim claim, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockCurrentLeaseAsync(claim, cancellationToken))
        {
            return [];
        }

        var runFiles = await db.DocumentReadingRunFiles
            .AsNoTracking()
            .Where(r => r.DocumentId == claim.DocumentId && r.RunGeneration == claim.RunGeneration)
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return runFiles;
    }

    // Starts reading one original of the claimed document in the claim's run generation. selectedPageRanges are the
    // original page numbers to send for OCR in the provider's "pages" syntax, or null for the whole file. Returns the
    // new run file's ID, or null (writing nothing) when the lease is no longer current. The file must belong to the
    // document; otherwise the database rejects it.
    public async Task<Guid?> StartRunFileAsync(
        DocumentReadingClaim claim, Guid documentFileId, string? selectedPageRanges,
        CancellationToken cancellationToken = default)
    {
        if (selectedPageRanges is not null
            && (selectedPageRanges.Length == 0 || selectedPageRanges.Length > DocumentReadingRunFile.SelectedPageRangesMaxLength))
        {
            throw new ArgumentOutOfRangeException(nameof(selectedPageRanges));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockCurrentLeaseAsync(claim, cancellationToken))
        {
            return null;
        }

        var id = Guid.CreateVersion7();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO document_reading_run_files (id, document_id, run_generation, document_file_id, stage,
                selected_page_ranges, cleanup_state, cleanup_attempt_count, created_at, updated_at)
            VALUES ({id}, {claim.DocumentId}, {claim.RunGeneration}, {documentFileId}, 'pending',
                NULLIF({selectedPageRanges ?? string.Empty}, ''), 'not_needed', 0, now(), now())
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    // Records the provider operation ID of a Pending run file as soon as it is known, together with its deletion
    // obligation. Returns true while the lease is current (the run file becomes Submitted and can be resumed). With a
    // lost lease the ID is still recorded, but the run file is abandoned (Failed) and only awaits cleanup; the method
    // then returns false and the worker must stop. Repeating the same ID is a no-op.
    public async Task<bool> RecordOperationAsync(
        DocumentReadingClaim claim, Guid runFileId, string providerOperationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerOperationId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            providerOperationId.Length, DocumentReadingRunFile.ProviderOperationIdMaxLength, nameof(providerOperationId));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var held = await LockCurrentLeaseAsync(claim, cancellationToken);
        var stage = held ? "submitted" : "failed";
        var updated = await db.Database.ExecuteSqlAsync($"""
            UPDATE document_reading_run_files
            SET stage = CASE WHEN stage = 'pending' THEN {stage} ELSE 'failed' END,
                provider_operation_id = {providerOperationId}, cleanup_state = 'pending', updated_at = now()
            WHERE id = {runFileId} AND document_id = {claim.DocumentId} AND run_generation = {claim.RunGeneration}
                AND provider_operation_id IS NULL
            """, cancellationToken);
        if (updated == 0 && !await HasOperationAsync(claim, runFileId, providerOperationId, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Run file {runFileId} of document {claim.DocumentId} is missing or already has another provider operation.");
        }

        await transaction.CommitAsync(cancellationToken);
        return held;
    }

    // Checkpoints the fetched output of a Pending or Submitted run file (Retrieved). False, writing nothing, when the
    // lease is no longer current; the next claim of the same generation can resume from the saved operation ID.
    public async Task<bool> RecordFetchedTextAsync(
        DocumentReadingClaim claim, Guid runFileId, string fetchedText, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fetchedText);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockCurrentLeaseAsync(claim, cancellationToken))
        {
            return false;
        }

        var updated = await db.Database.ExecuteSqlAsync($"""
            UPDATE document_reading_run_files
            SET stage = 'retrieved', fetched_text = {fetchedText}, updated_at = now()
            WHERE id = {runFileId} AND document_id = {claim.DocumentId} AND run_generation = {claim.RunGeneration}
                AND stage IN ('pending', 'submitted')
            """, cancellationToken);
        if (updated == 0)
        {
            throw new InvalidOperationException(
                $"Run file {runFileId} of document {claim.DocumentId} is missing or cannot take fetched text.");
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // Publishes the complete text revision of the claim's run generation and marks the job Read, in one transaction.
    // The pages must cover every original of the document: pages 1..n of the PDF, page 1 of each photo; blank pages are
    // included with empty text. DocumentId, RunGeneration and FilePosition are taken from the claim and document_files,
    // not from the arguments. Any earlier published revision is replaced in the same transaction, so a reader always sees
    // one complete revision. Nothing is written on LeaseLost or EmptyDocument (the caller fails the job instead).
    public async Task<DocumentReadingPublication> PublishAsync(
        DocumentReadingClaim claim, IReadOnlyCollection<DocumentTextPage> pages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockCurrentLeaseAsync(claim, cancellationToken))
        {
            return DocumentReadingPublication.LeaseLost;
        }

        var files = await db.DocumentFiles
            .AsNoTracking()
            .Where(f => f.DocumentId == claim.DocumentId)
            .Select(f => new { f.Id, f.Position, f.ContentType })
            .ToDictionaryAsync(f => f.Id, cancellationToken);
        EnsureCompleteRevision(pages, files.ToDictionary(f => f.Key, f => f.Value.ContentType == DocumentFile.PdfContentType));

        if (pages.All(p => string.IsNullOrWhiteSpace(p.Text)))
        {
            return DocumentReadingPublication.EmptyDocument;
        }

        try
        {
            await db.Database.ExecuteSqlAsync(
                $"DELETE FROM document_text_pages WHERE document_id = {claim.DocumentId}", cancellationToken);
            db.DocumentTextPages.AddRange(pages.Select(p => new DocumentTextPage
            {
                DocumentId = claim.DocumentId,
                RunGeneration = claim.RunGeneration,
                DocumentFileId = p.DocumentFileId,
                FilePosition = files[p.DocumentFileId].Position,
                SourcePageNumber = p.SourcePageNumber,
                ExtractionMethod = p.ExtractionMethod,
                Text = p.Text,
            }));
            await db.SaveChangesAsync(cancellationToken);
            await db.Database.ExecuteSqlAsync($"""
                UPDATE document_reading_jobs
                SET state = 'read', published_run_generation = run_generation, completed_at = now(), failure_code = NULL,
                    lease_token = NULL, lease_expires_at = NULL, updated_at = now()
                WHERE document_id = {claim.DocumentId}
                """, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }

        return DocumentReadingPublication.Published;
    }

    // A transient failure: the job goes back to Queued, due after delay (database time), keeping its run generation and
    // run files so saved operations and checkpoints can be resumed. False when the lease is no longer current.
    public async Task<bool> RequeueAsync(
        DocumentReadingClaim claim, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockCurrentLeaseAsync(claim, cancellationToken))
        {
            return false;
        }

        await db.Database.ExecuteSqlAsync($"""
            UPDATE document_reading_jobs
            SET state = 'queued', due_at = now() + {delay}, lease_token = NULL, lease_expires_at = NULL, updated_at = now()
            WHERE document_id = {claim.DocumentId}
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // A terminal failure: the job becomes Failed with a sanitized code (lowercase letters, digits and underscores, at
    // most 64; never a provider message). Unfinished run files of the generation are abandoned (Failed), which keeps
    // their cleanup obligations and makes them eligible for cleanup. Any earlier published revision is kept. False when
    // the lease is no longer current.
    public async Task<bool> FailAsync(
        DocumentReadingClaim claim, string failureCode, CancellationToken cancellationToken = default)
    {
        if (failureCode is null || !FailureCodePattern().IsMatch(failureCode))
        {
            throw new ArgumentException("A failure code is 1-64 lowercase letters, digits or underscores.", nameof(failureCode));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockCurrentLeaseAsync(claim, cancellationToken))
        {
            return false;
        }

        await db.Database.ExecuteSqlAsync($"""
            UPDATE document_reading_run_files
            SET stage = 'failed', updated_at = now()
            WHERE document_id = {claim.DocumentId} AND run_generation = {claim.RunGeneration}
                AND stage IN ('pending', 'submitted')
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            UPDATE document_reading_jobs
            SET state = 'failed', failure_code = {failureCode}, completed_at = now(),
                lease_token = NULL, lease_expires_at = NULL, updated_at = now()
            WHERE document_id = {claim.DocumentId}
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // Provider results due for deletion: a pending cleanup whose output is no longer needed (checkpointed, abandoned,
    // from an older run generation, or of a finished job) and whose retry time has passed. Cleanup is independent of
    // job leases; deleting a result twice is harmless, as the provider then reports it missing.
    public Task<List<DocumentReadingCleanupWork>> ListDueCleanupsAsync(int limit, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        return db.DocumentReadingRunFiles
            .FromSql($"""
                SELECT r.* FROM document_reading_run_files AS r
                JOIN document_reading_jobs AS j ON j.document_id = r.document_id
                WHERE r.cleanup_state = 'pending' AND (r.next_cleanup_at IS NULL OR r.next_cleanup_at <= now())
                    AND (r.stage IN ('retrieved', 'failed') OR r.run_generation < j.run_generation
                        OR j.state IN ('read', 'failed'))
                """)
            .AsNoTracking()
            .OrderBy(r => r.NextCleanupAt)
            .ThenBy(r => r.CreatedAt)
            .Take(limit)
            .Select(r => new DocumentReadingCleanupWork(r.Id, r.DocumentId, r.ProviderOperationId!, r.CleanupAttemptCount))
            .ToListAsync(cancellationToken);
    }

    // The provider acknowledged deletion of the run file's result (or reported it missing). Not proof of physical
    // erasure. False when the cleanup was not pending.
    public async Task<bool> AcknowledgeCleanupAsync(Guid runFileId, CancellationToken cancellationToken = default) =>
        await db.Database.ExecuteSqlAsync($"""
            UPDATE document_reading_run_files
            SET cleanup_state = 'acknowledged', next_cleanup_at = NULL, updated_at = now()
            WHERE id = {runFileId} AND cleanup_state = 'pending'
            """, cancellationToken) == 1;

    // A deletion attempt failed; the cleanup stays pending and is retried after delay (database time).
    public async Task<bool> DeferCleanupAsync(Guid runFileId, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        return await db.Database.ExecuteSqlAsync($"""
            UPDATE document_reading_run_files
            SET cleanup_attempt_count = cleanup_attempt_count + 1, next_cleanup_at = now() + {delay}, updated_at = now()
            WHERE id = {runFileId} AND cleanup_state = 'pending'
            """, cancellationToken) == 1;
    }

    [GeneratedRegex("^[a-z0-9_]{1,64}$")]
    private static partial Regex FailureCodePattern();

    // Inside the caller's transaction: locks the job row when the claim still holds the lease (same token and run
    // generation, not expired by database time). The lock keeps a reclaim from interleaving with the caller's writes.
    private async Task<bool> LockCurrentLeaseAsync(DocumentReadingClaim claim, CancellationToken cancellationToken) =>
        (await db.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM document_reading_jobs
            WHERE document_id = {claim.DocumentId} AND state = 'reading' AND lease_token = {claim.LeaseToken}
                AND run_generation = {claim.RunGeneration} AND lease_expires_at > clock_timestamp()
            FOR UPDATE
            """).ToListAsync(cancellationToken)).Count == 1;

    private Task<bool> HasOperationAsync(
        DocumentReadingClaim claim, Guid runFileId, string providerOperationId, CancellationToken cancellationToken) =>
        db.DocumentReadingRunFiles.AnyAsync(
            r => r.Id == runFileId && r.DocumentId == claim.DocumentId && r.ProviderOperationId == providerOperationId,
            cancellationToken);

    // A revision is complete when it has exactly pages 1..n (n >= 1) of every original, one page per photo, and nothing
    // else. A violation is a caller bug, not a reading outcome.
    private static void EnsureCompleteRevision(IReadOnlyCollection<DocumentTextPage> pages, Dictionary<Guid, bool> isPdfByFile)
    {
        foreach (var page in pages)
        {
            if (page is null || page.Text is null || !Enum.IsDefined(page.ExtractionMethod))
            {
                throw new ArgumentException("Every page needs text (possibly empty) and an extraction method.", nameof(pages));
            }

            if (!isPdfByFile.ContainsKey(page.DocumentFileId))
            {
                throw new ArgumentException($"File {page.DocumentFileId} is not an original of the document.", nameof(pages));
            }
        }

        foreach (var (fileId, isPdf) in isPdfByFile)
        {
            var numbers = pages.Where(p => p.DocumentFileId == fileId).Select(p => p.SourcePageNumber).Order().ToList();
            var expected = isPdf ? numbers.Count : 1;
            if (numbers.Count == 0 || numbers.Count != expected || !numbers.SequenceEqual(Enumerable.Range(1, expected)))
            {
                throw new ArgumentException(
                    $"The revision must contain pages 1..n of file {fileId} exactly once ({(isPdf ? "PDF" : "photo")}).",
                    nameof(pages));
            }
        }
    }
}
