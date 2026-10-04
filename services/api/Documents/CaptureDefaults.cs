using ogarniamy_zwierzaki_api.Animals;

namespace ogarniamy_zwierzaki_api.Documents;

// The animals a capture can be assigned to, in the existing earliest-created order, and the preselected one:
// the animal of the account's last completed capture while it is still eligible, otherwise the first animal.
// DefaultAnimalId is null only when the account has no animals (onboarding).
public sealed record CaptureDefaults(IReadOnlyList<OwnedAnimal> Animals, Guid? DefaultAnimalId);
