namespace ogarniamy_zwierzaki_api.Storage;

// Proof that an original is durably stored: its byte length, lowercase hex SHA-256 and the blob's ETag.
public sealed record OriginalFileReceipt(long Length, string Sha256, string ETag);
