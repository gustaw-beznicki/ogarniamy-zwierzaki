namespace ogarniamy_zwierzaki_api.Animals;

// Links an account to an animal with a role. Primary key: (AnimalId, UserId).
public sealed class AnimalMember
{
    public Guid AnimalId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public AnimalRole Role { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Animal Animal { get; set; } = null!;
}
