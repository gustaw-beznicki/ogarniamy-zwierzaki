namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// The durable embedding work of one read document: at most one per document, created (or re-queued) in the same
// transaction that publishes its text or renames its animal, or by the DocumentEmbedding migration's backfill. Derived
// state only; the original and its text are never changed. Written through DocumentEmbeddingJobs.
public sealed class DocumentEmbeddingJob
{
    public const int FailureCodeMaxLength = 64;

    public Guid DocumentId { get; set; }

    public DocumentEmbeddingState State { get; set; }

    // When a Queued job may be claimed (database time).
    public DateTimeOffset DueAt { get; set; }

    // Claims since the job was last queued, including reclaims of expired leases.
    public int AttemptCount { get; set; }

    // Both set exactly while the job is Embedding.
    public Guid? LeaseToken { get; set; }

    public DateTimeOffset? LeaseExpiresAt { get; set; }

    // A sanitized code (lowercase letters, digits, underscores), set exactly while the job is Failed. Never a provider
    // message or content.
    public string? FailureCode { get; set; }

    // When the job was last queued by an enqueue point (database time).
    public DateTimeOffset RequestedAt { get; set; }

    // The reading run generation and the animal name the stored chunks were computed for. Set exactly while Embedded;
    // kept (stale) after a re-queue so the previous result stays visible until it is replaced.
    public int? EmbeddedRunGeneration { get; set; }

    public string? EmbeddedAnimalName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
