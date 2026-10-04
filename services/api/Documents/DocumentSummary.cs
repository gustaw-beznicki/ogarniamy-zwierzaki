namespace ogarniamy_zwierzaki_api.Documents;

// A Stored document in an animal's list. EventDate is the user's date of the veterinary event; UploadedAt is when
// the capture was completed.
public sealed record DocumentSummary(Guid Id, Guid AnimalId, DateOnly EventDate, DateTimeOffset UploadedAt, int FileCount);
