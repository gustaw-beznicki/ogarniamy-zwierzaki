namespace ogarniamy_zwierzaki_api.Documents;

// One original of a capture manifest, in its confirmed position. Sha256 is the hex SHA-256 computed by the browser.
public sealed record DocumentUploadFileRequest(string? Name, string? ContentType, long? ByteLength, string? Sha256);
