using Pgvector;

namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// One embedded fragment of a published text revision: a slice of one page with its provenance and vector. The set of
// chunks of a document is replaced as a whole when its embedding job completes. Chunks are derived data; the page text
// they were cut from stays in document_text_pages.
public sealed class DocumentChunk
{
    public Guid DocumentId { get; set; }

    // Zero-based, in reading order across the whole document.
    public int ChunkIndex { get; set; }

    // The reading run generation whose pages this chunk was cut from.
    public int RunGeneration { get; set; }

    public Guid DocumentFileId { get; set; }

    // The original's zero-based position in the document, copied from the page.
    public int FilePosition { get; set; }

    // One-based page number within the original.
    public int SourcePageNumber { get; set; }

    // Character offsets of Text within the page text: StartOffset inclusive, EndOffset exclusive.
    public int StartOffset { get; set; }

    public int EndOffset { get; set; }

    // The page text slice shown as the fragment (without the animal name that was embedded with it).
    public string Text { get; set; } = string.Empty;

    public Vector Embedding { get; set; } = null!;

    // Chunker version, model, dimensions and input scheme (see EmbeddingInput.Version).
    public string EmbeddingVersion { get; set; } = string.Empty;
}
