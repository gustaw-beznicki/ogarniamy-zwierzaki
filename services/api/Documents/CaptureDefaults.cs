using ogarniamy_zwierzaki_api.Animals;

namespace ogarniamy_zwierzaki_api.Documents;

// The active animals a fresh capture can be assigned to, in the existing earliest-created order, and the preselected
// one: the animal of the account's last completed capture while it is still active, otherwise the first active animal.
// DefaultAnimalId is null, and Animals empty, when the account has no active animal; that is not onboarding when the
// account still owns inactive animals (the animal list and its count say which).
public sealed record CaptureDefaults(IReadOnlyList<OwnedAnimal> Animals, Guid? DefaultAnimalId);
