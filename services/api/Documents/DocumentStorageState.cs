namespace ogarniamy_zwierzaki_api.Documents;

// Where a capture is in its lifecycle. Only Stored documents are listed or opened; an Uploading one is reachable
// only through its upload operation.
public enum DocumentStorageState
{
    // The manifest is frozen and its originals are being uploaded.
    Uploading,

    // Every original has a durable storage receipt and the capture was completed.
    Stored,
}
