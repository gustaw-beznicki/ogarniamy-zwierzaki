using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ogarniamy_zwierzaki_api.Documents;

// Manifest field checks and screening of uploaded originals. Formats are recognised by their leading signature, never
// by file extension or submitted Content-Type. This is format screening, not a parser: it does not promise to detect
// every malformed or hostile PDF or image.
public static class DocumentFileValidator
{
    private static ReadOnlySpan<byte> PdfSignature => "%PDF-"u8;

    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // The display name to store: the last path segment without control, invisible formatting or separator characters
    // (which could hide a real extension), NFC-normalized and trimmed. Null when nothing is left or the result is
    // longer than 255 characters.
    public static string? SanitizeName(string? name)
    {
        if (name is null)
        {
            return null;
        }

        var lastSegment = name[(name.LastIndexOfAny(['/', '\\']) + 1)..];
        var builder = new StringBuilder(lastSegment.Length);
        foreach (var rune in lastSegment.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) is not (UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.Surrogate))
            {
                builder.Append(rune.ToString());
            }
        }

        var sanitized = builder.ToString().Normalize(NormalizationForm.FormC).Trim();
        return sanitized.Length is > 0 and <= DocumentFile.OriginalNameMaxLength ? sanitized : null;
    }

    // The supported content type in canonical form, or null when it is not PDF, JPEG or PNG.
    public static string? NormalizeContentType(string? contentType)
    {
        var normalized = contentType?.Trim().ToLowerInvariant();
        return normalized is not null && DocumentFile.IsAllowedContentType(normalized) ? normalized : null;
    }

    // The SHA-256 in the stored lowercase form, or null when it is not 64 hex characters.
    public static string? NormalizeSha256(string? sha256) =>
        sha256 is { Length: DocumentFile.Sha256Length } && sha256.All(char.IsAsciiHexDigit)
            ? sha256.ToLowerInvariant()
            : null;

    // The format recognised from the content's signature, or null when it is none of PDF, JPEG and PNG.
    public static string? DetectContentType(ReadOnlySpan<byte> content)
    {
        if (content.StartsWith(PdfSignature))
        {
            return DocumentFile.PdfContentType;
        }

        if (content.StartsWith(JpegSignature))
        {
            return DocumentFile.JpegContentType;
        }

        return content.StartsWith(PngSignature) ? DocumentFile.PngContentType : null;
    }

    // Checks uploaded bytes against their manifest slot: non-empty, the declared length and SHA-256, and a signature
    // of the declared format. Null when they match.
    public static DocumentCaptureFailure? Verify(ReadOnlySpan<byte> content, DocumentFile slot)
    {
        if (content.IsEmpty)
        {
            return DocumentCaptureFailure.EmptyFile;
        }

        if (content.Length != slot.ByteLength
            || Convert.ToHexStringLower(SHA256.HashData(content)) != slot.Sha256)
        {
            return DocumentCaptureFailure.FileMismatch;
        }

        return DetectContentType(content) switch
        {
            null => DocumentCaptureFailure.UnsupportedFileType,
            var detected when detected == slot.ContentType => null,
            _ => DocumentCaptureFailure.FileMismatch,
        };
    }
}
