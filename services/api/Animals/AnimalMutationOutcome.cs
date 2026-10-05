namespace ogarniamy_zwierzaki_api.Animals;

public enum AnimalMutationOutcome
{
    // The edit was applied, or was a no-op against the current version.
    Updated,

    // The animal does not exist or is not the user's.
    NotFound,

    // The animal is the user's, but the expected version is no longer current.
    Stale,
}
