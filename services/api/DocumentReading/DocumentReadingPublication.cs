namespace ogarniamy_zwierzaki_api.DocumentReading;

// The outcome of DocumentReadingJobs.PublishAsync. Nothing is written unless it is Published.
public enum DocumentReadingPublication
{
    // The pages and the Read state were committed together.
    Published,

    // The claim's lease token or run generation is no longer current, or the lease expired.
    LeaseLost,

    // No page has any non-whitespace text: an entirely empty document is not content-read.
    EmptyDocument,
}
