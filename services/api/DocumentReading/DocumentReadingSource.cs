namespace ogarniamy_zwierzaki_api.DocumentReading;

// One original of a claimed document, as the worker needs it to read the content: where it is stored and the receipt
// it must match. Carries no file name.
public sealed record DocumentReadingSource(
    Guid DocumentFileId, int Position, string ContentType, string BlobKey, long ByteLength, string Sha256);
