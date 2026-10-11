namespace ogarniamy_zwierzaki_api.Documents;

// The result of OwnedDocuments.AddUploadAsync: whether a fresh upload manifest was accepted.
public enum UploadAcceptance
{
    // The manifest was stored as an Uploading document.
    Accepted,

    // The animal does not exist or is not the user's. Nothing was written.
    AnimalNotFound,

    // The user's animal is inactive, so it takes no fresh capture. Nothing was written.
    AnimalInactive,
}
