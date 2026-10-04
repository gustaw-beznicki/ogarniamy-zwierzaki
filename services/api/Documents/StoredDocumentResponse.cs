namespace ogarniamy_zwierzaki_api.Documents;

// A Stored document as returned by completion and by the detail route. EventDate is the user's date of the
// veterinary event; UploadedAt is when the capture was completed (UTC).
public sealed record StoredDocumentResponse(
    Guid Id,
    Guid AnimalId,
    string AnimalName,
    DateOnly EventDate,
    DateTimeOffset UploadedAt,
    IReadOnlyList<StoredDocumentFileResponse> Files)
{
    public static StoredDocumentResponse From(DocumentDetails document) => new(
        document.Id,
        document.AnimalId,
        document.AnimalName,
        document.EventDate,
        document.UploadedAt,
        document.Files.Select(file => StoredDocumentFileResponse.From(document.Id, file)).ToList());
}
