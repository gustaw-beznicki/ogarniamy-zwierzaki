namespace ogarniamy_zwierzaki_api.Animals;

public sealed class Animal
{
    public const int NameMaxLength = 100;

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
