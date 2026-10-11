namespace ogarniamy_zwierzaki_api.DocumentReading;

// The outcome of an owner Retry and the reading status after it (null only when NotFound).
public sealed record DocumentReadingRetryResult(DocumentReadingRetryOutcome Outcome, DocumentReadingResponse? Status);
