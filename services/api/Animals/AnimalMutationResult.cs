namespace ogarniamy_zwierzaki_api.Animals;

// Animal is set only when the outcome is Updated.
public sealed record AnimalMutationResult(AnimalMutationOutcome Outcome, OwnedAnimal? Animal = null);
