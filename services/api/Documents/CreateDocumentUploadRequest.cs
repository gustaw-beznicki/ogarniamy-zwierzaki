namespace ogarniamy_zwierzaki_api.Documents;

// The immutable capture manifest. EventDate is an ISO calendar date (yyyy-MM-dd) and TimeZone the capturing browser's
// IANA time zone. Every field is optional here so that DocumentCaptureService answers with a specific error code.
public sealed record CreateDocumentUploadRequest(
    Guid? AnimalId,
    string? EventDate,
    string? TimeZone,
    IReadOnlyList<DocumentUploadFileRequest?>? Files);
