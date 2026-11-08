namespace ogarniamy_zwierzaki_api.Documents;

// One original of a Stored document, in its confirmed position. OriginalUrl is the authenticated API route;
// appending ?download=true asks for an attachment. No storage location is ever exposed.
public sealed record StoredDocumentFileResponse(
    Guid Id,
    int Position,
    string OriginalName,
    string ContentType,
    long ByteLength,
    string OriginalUrl)
{
    public static StoredDocumentFileResponse From(Guid documentId, DocumentFileDetails file) => new(
        file.Id,
        file.Position,
        file.OriginalName,
        file.ContentType,
        file.ByteLength,
        OriginalUrlFor(documentId, file.Id));

    // The one place the authenticated original route is spelled; search results use it too.
    public static string OriginalUrlFor(Guid documentId, Guid fileId) =>
        $"/api/documents/{documentId:D}/files/{fileId:D}/original";
}
