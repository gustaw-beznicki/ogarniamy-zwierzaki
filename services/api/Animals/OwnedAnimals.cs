using Microsoft.EntityFrameworkCore;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.Documents;

namespace ogarniamy_zwierzaki_api.Animals;

// The only code allowed to read or create animals. Every query starts from the user's animal_members rows,
// so no caller can reach another account's animals.
public sealed class OwnedAnimals(AppDbContext db)
{
    // Every owned animal, active or not, in creation order.
    public async Task<IReadOnlyList<OwnedAnimal>> ListAsync(string userId, CancellationToken cancellationToken = default) =>
        await Project(ForUser(userId)).ToListAsync(cancellationToken);

    // Only the owned animals that are active.
    public async Task<IReadOnlyList<OwnedAnimal>> ListActiveAsync(string userId, CancellationToken cancellationToken = default) =>
        await Project(ForUser(userId).Where(a => a.IsActive)).ToListAsync(cancellationToken);

    // Null when the animal does not exist or is not the user's. Active or inactive.
    public Task<OwnedAnimal?> FindAsync(string userId, Guid animalId, CancellationToken cancellationToken = default) =>
        Project(ForUser(userId).Where(a => a.Id == animalId)).FirstOrDefaultAsync(cancellationToken);

    // All owned animals, including inactive ones: onboarding depends on owning any animal, not an active one.
    public Task<int> CountAsync(string userId, CancellationToken cancellationToken = default) =>
        ForUser(userId).CountAsync(cancellationToken);

    // Inserts the animal and the user's owner membership in one SaveChanges.
    public async Task<OwnedAnimal> CreateAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var animal = new Animal { Id = Guid.CreateVersion7(), Name = name, CreatedAt = now };
        db.Animals.Add(animal);
        db.AnimalMembers.Add(new AnimalMember
        {
            Animal = animal,
            UserId = userId,
            Role = AnimalRole.Owner,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        // A new animal has no documents yet.
        return new OwnedAnimal(animal.Id, animal.Name, animal.IsActive, animal.Version, 0);
    }

    // Renames the animal when expectedVersion is still current. Re-sending the current name keeps the version.
    public Task<AnimalMutationResult> RenameAsync(
        string userId, Guid animalId, Guid expectedVersion, string name, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, animalId, expectedVersion, animal =>
        {
            if (animal.Name == name)
            {
                return false;
            }

            animal.Name = name;
            return true;
        }, cancellationToken);

    // Marks the animal active or inactive when expectedVersion is still current. Never touches the name.
    public Task<AnimalMutationResult> SetActivityAsync(
        string userId, Guid animalId, Guid expectedVersion, bool isActive, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, animalId, expectedVersion, animal =>
        {
            if (animal.IsActive == isActive)
            {
                return false;
            }

            animal.IsActive = isActive;
            return true;
        }, cancellationToken);

    // Locks the owned animal row (FOR UPDATE), so concurrent edits of one animal run one after another and the second
    // sees the first one's new version. Fresh upload manifests lock the same row, which serializes them with activity
    // changes. Ownership is resolved before the version is compared, so a foreign animal's version is never disclosed.
    private async Task<AnimalMutationResult> MutateAsync(
        string userId, Guid animalId, Guid expectedVersion, Func<Animal, bool> apply, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var locked = await db.Animals
            .FromSql($"""
                SELECT * FROM animals
                WHERE id = {animalId} AND id IN (SELECT animal_id FROM animal_members WHERE user_id = {userId})
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);
        var animal = locked.SingleOrDefault();
        if (animal is null)
        {
            return new AnimalMutationResult(AnimalMutationOutcome.NotFound);
        }

        if (animal.Version != expectedVersion)
        {
            return new AnimalMutationResult(AnimalMutationOutcome.Stale);
        }

        if (apply(animal))
        {
            animal.Version = Guid.NewGuid();
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var current = await FindAsync(userId, animalId, cancellationToken);
        return new AnimalMutationResult(AnimalMutationOutcome.Updated, current);
    }

    // Counts Stored documents inside the same scoped query, so a list never reads documents per animal.
    private IQueryable<OwnedAnimal> Project(IQueryable<Animal> animals) =>
        animals
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Select(a => new OwnedAnimal(
                a.Id,
                a.Name,
                a.IsActive,
                a.Version,
                db.Documents.Count(d => d.AnimalId == a.Id && d.StorageState == DocumentStorageState.Stored)));

    private IQueryable<Animal> ForUser(string userId) =>
        db.AnimalMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.Animal);
}
