namespace ogarniamy_zwierzaki_api.DocumentReading;

// The owner's view of reading one Stored document. State is queued, reading, read or failed. FailureCode is the
// sanitized code while failed (never a provider message). CanRetry is true when an owner Retry would be accepted now;
// RetryAfter is when the next one will be, while the hourly limit blocks it. CompletedAt is when reading last reached
// read or failed. Never carries extracted text, file names, storage keys or provider operation IDs.
public sealed record DocumentReadingResponse(
    string State,
    string? FailureCode,
    bool CanRetry,
    DateTimeOffset? RetryAfter,
    DateTimeOffset? CompletedAt);
