namespace ogarniamy_zwierzaki_api.Documents;

// A Stored document with its originals in their confirmed order.
public sealed record DocumentDetails(
    Guid Id,
    Guid AnimalId,
    string AnimalName,
    DateOnly EventDate,
    DateTimeOffset UploadedAt,
    IReadOnlyList<DocumentFileDetails> Files);
