namespace ogarniamy_zwierzaki_api.Documents;

// One logical veterinary document: an immutable capture manifest and its ordered originals. It belongs to an animal,
// never directly to an account; access always goes through the account's animal memberships (OwnedDocuments).
public sealed class Document
{
    public const int TimeZoneMaxLength = 100;

    public const int MaxImageFiles = 10;

    // The client-generated upload operation ID doubles as the document ID, so a retried operation finds its document.
    public Guid Id { get; set; }

    public Guid AnimalId { get; set; }

    // The calendar date of the veterinary event, as chosen by the user. Distinct from UploadedAt.
    public DateOnly EventDate { get; set; }

    // The IANA time zone of the capturing device, used to validate EventDate against the user's local today.
    public string CaptureTimeZone { get; set; } = string.Empty;

    public DocumentStorageState StorageState { get; set; }

    // When the manifest was created (UTC).
    public DateTimeOffset CreatedAt { get; set; }

    // When the capture was completed (UTC); set exactly once, together with StorageState = Stored.
    public DateTimeOffset? UploadedAt { get; set; }

    public List<DocumentFile> Files { get; set; } = [];
}
