namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// A worker's claim on an embedding job. Publishing and failing must present the token; once the lease expires, the job
// is re-queued or another worker claims it, those writes are refused.
public sealed record DocumentEmbeddingClaim(Guid DocumentId, Guid LeaseToken, DateTimeOffset LeaseExpiresAt, int AttemptCount);
