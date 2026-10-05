namespace ogarniamy_zwierzaki_api.DocumentReading;

// One page of a published text revision, with its provenance: the original file, its position and the page number in
// that original (1 for a photo). A revision is complete: every page of every original is present, blank ones with
// empty Text. Not to be confused with Documents.DocumentPage, a pagination DTO.
public sealed class DocumentTextPage
{
    public Guid DocumentId { get; set; }

    public int RunGeneration { get; set; }

    public Guid DocumentFileId { get; set; }

    // The original's zero-based position in the document, copied from document_files for ordering.
    public int FilePosition { get; set; }

    // One-based page number within the original.
    public int SourcePageNumber { get; set; }

    public DocumentTextExtractionMethod ExtractionMethod { get; set; }

    public string Text { get; set; } = string.Empty;
}
