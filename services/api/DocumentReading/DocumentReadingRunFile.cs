namespace ogarniamy_zwierzaki_api.DocumentReading;

// Reading of one original within one run generation of a job: what was selected, submitted and fetched, and whether
// the provider still holds data that must be deleted. A file may have several run files (one per abandoned attempt);
// rows are never deleted, so cleanup obligations survive retries and restarts.
public sealed class DocumentReadingRunFile
{
    public const int SelectedPageRangesMaxLength = 2_000;

    public const int ProviderOperationIdMaxLength = 200;

    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public int RunGeneration { get; set; }

    // An original of the same document.
    public Guid DocumentFileId { get; set; }

    public DocumentReadingRunFileStage Stage { get; set; }

    // The original page numbers sent for OCR, in the provider's "pages" syntax (for example "1-3,7"); null when the
    // whole file is read.
    public string? SelectedPageRanges { get; set; }

    // The provider's Analyze result ID, recorded as soon as it is known; set exactly when cleanup is not NotNeeded.
    public string? ProviderOperationId { get; set; }

    // The checkpointed per-page output of this file, serialized by the reader. Set when Retrieved. Before that, a PDF's
    // run file may hold the serialized PdfLocalCheckpoint taken before its OCR submission (kept if it is abandoned).
    public string? FetchedText { get; set; }

    public DocumentReadingCleanupState CleanupState { get; set; }

    public int CleanupAttemptCount { get; set; }

    // A pending cleanup is not retried before this time (database time); null means as soon as it is eligible.
    public DateTimeOffset? NextCleanupAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
