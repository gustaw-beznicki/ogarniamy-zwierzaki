namespace ogarniamy_zwierzaki_api.Documents;

// One manifest slot. Stored is true once the original has a durable storage receipt matching ByteLength and Sha256.
public sealed record DocumentUploadFileResponse(
    int Position,
    Guid FileId,
    string Name,
    string ContentType,
    long ByteLength,
    string Sha256,
    bool Stored);
