namespace ogarniamy_zwierzaki_api.DocumentReading;

// A worker's claim on a job. Every later write for the job must present the token and run generation; once the lease
// expires or another worker claims the job, those writes are refused.
public sealed record DocumentReadingClaim(
    Guid DocumentId,
    int RunGeneration,
    Guid LeaseToken,
    DateTimeOffset LeaseExpiresAt,
    int AttemptCount);
