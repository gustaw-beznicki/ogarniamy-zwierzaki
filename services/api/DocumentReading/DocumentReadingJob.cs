namespace ogarniamy_zwierzaki_api.DocumentReading;

// The durable reading work of one Stored document: exactly one per document, created in the transaction that completes
// the capture (or by the DocumentReading migration's backfill). Derived state only; the original is never changed.
// Written through DocumentReadingJobs (workers) and, for scheduling, OwnedDocuments.CompleteAsync.
public sealed class DocumentReadingJob
{
    public const int FailureCodeMaxLength = 64;

    public Guid DocumentId { get; set; }

    public DocumentReadingState State { get; set; }

    // Starts at 1; incremented only by an owner Retry. A reclaimed (expired) job keeps its generation, so its run files
    // stay resumable.
    public int RunGeneration { get; set; }

    // When a Queued job may be claimed (database time).
    public DateTimeOffset DueAt { get; set; }

    // Claims made in the current run generation, including reclaims of expired leases.
    public int AttemptCount { get; set; }

    // Both set exactly while the job is Reading.
    public Guid? LeaseToken { get; set; }

    public DateTimeOffset? LeaseExpiresAt { get; set; }

    // A sanitized code (lowercase letters, digits, underscores), set exactly while the job is Failed. Never a provider
    // message or content.
    public string? FailureCode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    // When the job last reached Read or Failed; null otherwise.
    public DateTimeOffset? CompletedAt { get; set; }

    // The run generation whose pages are the current published revision. Kept until a later generation publishes.
    public int? PublishedRunGeneration { get; set; }

    // Accepted owner retries in the window starting at OwnerRetryWindowStartedAt; the limit is enforced by the retry
    // route, durably, so it survives restarts and replicas.
    public int OwnerRetryCount { get; set; }

    public DateTimeOffset? OwnerRetryWindowStartedAt { get; set; }
}
