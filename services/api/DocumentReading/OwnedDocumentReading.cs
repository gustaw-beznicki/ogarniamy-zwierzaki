using Microsoft.EntityFrameworkCore;
using ogarniamy_zwierzaki_api.Animals;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.Documents;

namespace ogarniamy_zwierzaki_api.DocumentReading;

// Owner-facing reading status and Retry. Every operation reaches the job only through a Stored document of an animal
// the user owns (animal_members rows with the owner role), whether or not the animal is active; another account's,
// missing and Uploading documents are indistinguishable (null / NotFound). Unlike DocumentReadingJobs (the trusted
// worker boundary) it never addresses a job without a user, and it exposes no text, file names, storage keys or
// provider operation IDs.
//
// Retry limit: at most MaxOwnerRetries accepted retries per document in a window of RetryWindow that starts with the
// first accepted retry; the counter and window start live on the job row and are compared with database time, so the
// limit survives restarts and holds across replicas.
public sealed class OwnedDocumentReading(AppDbContext db)
{
    public const int MaxOwnerRetries = 3;

    public static readonly TimeSpan RetryWindow = TimeSpan.FromHours(1);

    // The reading status of the user's Stored document. Null when it is missing, foreign or still Uploading.
    public async Task<DocumentReadingResponse?> FindAsync(
        string userId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var row = await OwnedJob(userId, documentId)
            .Select(j => new
            {
                j.State,
                j.FailureCode,
                j.CompletedAt,
                j.OwnerRetryCount,
                j.OwnerRetryWindowStartedAt,
                // Database time, like the retry route's limit check.
                Now = DateTimeOffset.UtcNow,
            })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : ToResponse(row.State, row.FailureCode, row.CompletedAt, row.OwnerRetryCount, row.OwnerRetryWindowStartedAt, row.Now);
    }

    // An owner Retry under the job row lock: only a failed job is queued again, in a new run generation with a fresh
    // attempt budget. Run files of earlier generations are left as they are, so pending provider cleanup is kept (and
    // becomes due). A queued, reading or read job is unchanged, so repeated clicks coalesce. A failed job stays failed
    // when the hourly limit is reached.
    public async Task<DocumentReadingRetryResult> RetryAsync(
        string userId, Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var locked = await db.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM document_reading_jobs AS j
            JOIN documents AS d ON d.id = j.document_id
            WHERE j.document_id = {documentId} AND d.storage_state = 'stored'
                AND EXISTS (SELECT 1 FROM animal_members AS m
                    WHERE m.animal_id = d.animal_id AND m.user_id = {userId} AND m.role = 'owner')
            FOR UPDATE OF j
            """).ToListAsync(cancellationToken);
        if (locked.Count == 0)
        {
            return new DocumentReadingRetryResult(DocumentReadingRetryOutcome.NotFound, null);
        }

        var window = RetryWindow;
        var limit = MaxOwnerRetries;
        var accepted = await db.Database.ExecuteSqlAsync($"""
            UPDATE document_reading_jobs
            SET state = 'queued', run_generation = run_generation + 1, attempt_count = 0, due_at = now(),
                failure_code = NULL, completed_at = NULL, updated_at = now(),
                owner_retry_count = CASE
                    WHEN owner_retry_window_started_at IS NULL OR owner_retry_window_started_at + {window} <= now() THEN 1
                    ELSE owner_retry_count + 1 END,
                owner_retry_window_started_at = CASE
                    WHEN owner_retry_window_started_at IS NULL OR owner_retry_window_started_at + {window} <= now() THEN now()
                    ELSE owner_retry_window_started_at END
            WHERE document_id = {documentId} AND state = 'failed'
                AND (owner_retry_window_started_at IS NULL OR owner_retry_window_started_at + {window} <= now()
                    OR owner_retry_count < {limit})
            """, cancellationToken);

        var status = await FindAsync(userId, documentId, cancellationToken)
            ?? throw new InvalidOperationException($"The locked reading job of document {documentId} disappeared.");
        await transaction.CommitAsync(cancellationToken);

        var outcome = accepted == 1
            ? DocumentReadingRetryOutcome.Accepted
            : status.State == StateName(DocumentReadingState.Failed)
                ? DocumentReadingRetryOutcome.RateLimited
                : DocumentReadingRetryOutcome.Unchanged;
        return new DocumentReadingRetryResult(outcome, status);
    }

    private static DocumentReadingResponse ToResponse(
        DocumentReadingState state, string? failureCode, DateTimeOffset? completedAt, int ownerRetryCount,
        DateTimeOffset? windowStartedAt, DateTimeOffset now)
    {
        var windowEnd = windowStartedAt + RetryWindow;
        var limited = ownerRetryCount >= MaxOwnerRetries && windowEnd > now;
        var failed = state == DocumentReadingState.Failed;
        return new DocumentReadingResponse(
            StateName(state),
            failed ? failureCode : null,
            failed && !limited,
            failed && limited ? windowEnd : null,
            completedAt);
    }

    private static string StateName(DocumentReadingState state) => state switch
    {
        DocumentReadingState.Queued => "queued",
        DocumentReadingState.Reading => "reading",
        DocumentReadingState.Read => "read",
        DocumentReadingState.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown reading state."),
    };

    // The job of the user's Stored document, through the owner memberships only.
    private IQueryable<DocumentReadingJob> OwnedJob(string userId, Guid documentId)
    {
        var memberships = db.AnimalMembers.Where(m => m.UserId == userId && m.Role == AnimalRole.Owner);
        return db.DocumentReadingJobs
            .AsNoTracking()
            .Where(j => j.DocumentId == documentId && db.Documents.Any(d =>
                d.Id == j.DocumentId
                && d.StorageState == DocumentStorageState.Stored
                && memberships.Any(m => m.AnimalId == d.AnimalId)));
    }
}
