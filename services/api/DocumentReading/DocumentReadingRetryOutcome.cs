namespace ogarniamy_zwierzaki_api.DocumentReading;

// What an owner Retry did.
public enum DocumentReadingRetryOutcome
{
    // The document is missing, another account's or still Uploading.
    NotFound,

    // The failed job was queued again in a new run generation.
    Accepted,

    // The job was not failed (queued, reading or read): nothing changed.
    Unchanged,

    // The failed job stays failed: the hourly owner-retry limit is reached.
    RateLimited,
}
