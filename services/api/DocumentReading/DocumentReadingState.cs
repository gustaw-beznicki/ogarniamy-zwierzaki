namespace ogarniamy_zwierzaki_api.DocumentReading;

// Where reading a Stored document's content is. Separate from DocumentStorageState: an original stays retrievable
// whatever this says.
public enum DocumentReadingState
{
    // Waiting for a worker; due at DueAt.
    Queued,

    // Claimed by a worker that holds an unexpired lease (token and expiry). An expired lease can be reclaimed.
    Reading,

    // A complete text revision is published (PublishedRunGeneration).
    Read,

    // Reading stopped with a sanitized FailureCode. Only an owner Retry queues it again.
    Failed,
}
