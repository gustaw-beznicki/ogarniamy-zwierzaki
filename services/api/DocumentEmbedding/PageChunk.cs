namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// A slice of one page's text. Text is exactly the page text from StartOffset (inclusive) to EndOffset (exclusive).
public sealed record PageChunk(
    Guid DocumentFileId, int FilePosition, int SourcePageNumber, int StartOffset, int EndOffset, string Text);
