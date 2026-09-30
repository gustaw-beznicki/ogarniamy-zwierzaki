using Microsoft.EntityFrameworkCore;
using ogarniamy_zwierzaki_api.Data;

namespace ogarniamy_zwierzaki_api.Animals;

// The only code allowed to read or create animals. Every query starts from the user's animal_members rows,
// so no caller can reach another account's animals.
public sealed class OwnedAnimals(AppDbContext db)
{
    public async Task<IReadOnlyList<OwnedAnimal>> ListAsync(string userId, CancellationToken cancellationToken = default) =>
        await ForUser(userId)
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Select(a => new OwnedAnimal(a.Id, a.Name))
            .ToListAsync(cancellationToken);

    // Null when the animal does not exist or is not the user's.
    public Task<OwnedAnimal?> FindAsync(string userId, Guid animalId, CancellationToken cancellationToken = default) =>
        ForUser(userId)
            .Where(a => a.Id == animalId)
            .Select(a => new OwnedAnimal(a.Id, a.Name))
            .FirstOrDefaultAsync(cancellationToken);

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
        return new OwnedAnimal(animal.Id, animal.Name);
    }

    private IQueryable<Animal> ForUser(string userId) =>
        db.AnimalMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.Animal);
}
