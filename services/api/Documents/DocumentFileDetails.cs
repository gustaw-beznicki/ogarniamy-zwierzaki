using System.Text.Json.Serialization;

namespace ogarniamy_zwierzaki_api.Documents;

// One original of a Stored document, in its confirmed position. BlobKey and Sha256 are for reading the original
// through IOriginalStorage only; they are never serialized, so no response can reveal a storage location.
public sealed record DocumentFileDetails(
    Guid Id,
    int Position,
    string OriginalName,
    string ContentType,
    long ByteLength,
    [property: JsonIgnore] string BlobKey,
    [property: JsonIgnore] string Sha256);
