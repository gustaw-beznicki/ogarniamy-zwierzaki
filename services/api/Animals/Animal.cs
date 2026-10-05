namespace ogarniamy_zwierzaki_api.Animals;

public sealed class Animal
{
    public const int NameMaxLength = 100;

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    // Inactive animals stay readable but are no longer offered for new captures. Reversible.
    public bool IsActive { get; set; } = true;

    // Opaque edit version: replaced by a fresh UUID whenever the name or activity actually changes, so a client holding
    // an older copy cannot overwrite a newer edit. The database generates it (gen_random_uuid()) on insert.
    public Guid Version { get; set; }
}
