namespace ogarniamy_zwierzaki_api.DocumentReading;

// The provider-side deletion obligation of a run file. It is independent of reading success or failure and is never
// discarded by a retry.
public enum DocumentReadingCleanupState
{
    // Nothing was submitted to the provider, so there is nothing to delete.
    NotNeeded,

    // A provider operation exists and its result has not been acknowledged as deleted.
    Pending,

    // The provider acknowledged deletion (or reported the result missing). Not proof of physical erasure.
    Acknowledged,
}
