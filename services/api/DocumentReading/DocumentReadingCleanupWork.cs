namespace ogarniamy_zwierzaki_api.DocumentReading;

// A provider result due for deletion. Carries IDs only, never content.
public sealed record DocumentReadingCleanupWork(Guid RunFileId, Guid DocumentId, string ProviderOperationId, int AttemptCount);
