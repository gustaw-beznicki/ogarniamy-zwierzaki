namespace ogarniamy_zwierzaki_api.Documents;

// An upload operation as frozen by its manifest. State is "uploading" or "stored".
public sealed record DocumentUploadResponse(
    Guid OperationId,
    Guid AnimalId,
    DateOnly EventDate,
    string TimeZone,
    string State,
    IReadOnlyList<DocumentUploadFileResponse> Files);
