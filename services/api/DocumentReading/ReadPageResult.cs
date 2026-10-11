namespace ogarniamy_zwierzaki_api.DocumentReading;

// The text of one page of one original, as a reader produced it: the one-based page number in the original (1 for a
// photo) and how it was read. Also the checkpoint format of a run file's fetched text (a JSON array of these).
public sealed record ReadPageResult(int SourcePageNumber, DocumentTextExtractionMethod Method, string Text);
