using ogarniamy_zwierzaki_api.Storage;

namespace ogarniamy_zwierzaki_api.Documents;

// One original of a document, in its confirmed position. The manifest fields are frozen at creation; only the
// storage receipt is filled in later, once the original is durably stored.
public sealed class DocumentFile
{
    public const string PdfContentType = "application/pdf";
    public const string JpegContentType = "image/jpeg";
    public const string PngContentType = "image/png";

    public const long MaxByteLength = 10_485_760;
    public const int OriginalNameMaxLength = 255;
    public const int Sha256Length = 64;
    public const int ContentTypeMaxLength = 100;
    public const int BlobKeyMaxLength = 100;
    public const int ReceiptEtagMaxLength = 200;

    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    // Zero-based order of the original within the document.
    public int Position { get; set; }

    // The sanitized file name supplied by the user; display only, never used as a storage key.
    public string OriginalName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long ByteLength { get; set; }

    // Lowercase hex SHA-256 of the original's bytes.
    public string Sha256 { get; set; } = string.Empty;

    // Opaque, deterministic storage key derived from the document and file IDs (see BlobKeyFor).
    public string BlobKey { get; set; } = string.Empty;

    // The storage receipt; all three are null until the original is durably stored, then they match the manifest.
    public long? ReceiptLength { get; set; }

    public string? ReceiptSha256 { get; set; }

    public string? ReceiptEtag { get; set; }

    public bool IsStored => ReceiptEtag is not null;

    public static bool IsAllowedContentType(string contentType) =>
        contentType is PdfContentType or JpegContentType or PngContentType;

    // Never contains user input, so a file name can never influence where an original is stored.
    public static string BlobKeyFor(Guid documentId, Guid fileId) => $"documents/{documentId:D}/{fileId:D}";

    public OriginalFileReceipt? Receipt =>
        ReceiptLength is { } length && ReceiptSha256 is { } sha256 && ReceiptEtag is { } etag
            ? new OriginalFileReceipt(length, sha256, etag)
            : null;
}
