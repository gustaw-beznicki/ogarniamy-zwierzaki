using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ogarniamy_zwierzaki_api.Animals;
using ogarniamy_zwierzaki_api.Auth;

namespace ogarniamy_zwierzaki_api.Data;

// Identity accounts, data-protection keys and animals. The naming convention is set where the context is configured.
// IdentityUserContext leaves out Identity's account-level roles: the product's roles are per animal (animal_members.role).
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<AppUser>(options), IDataProtectionKeyContext
{
    // Read and written only through OwnedAnimals, which scopes every query to the current user.
    public DbSet<Animal> Animals => Set<Animal>();

    public DbSet<AnimalMember> AnimalMembers => Set<AnimalMember>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Identity sets explicit AspNet* table and index names, which the snake_case convention does not rename.
        builder.Entity<AppUser>(user =>
        {
            user.ToTable("users");
            user.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ix_users_normalized_email");
            user.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ix_users_normalized_user_name");
        });
        builder.Entity<IdentityUserClaim<string>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<string>>().ToTable("user_tokens");

        builder.Entity<Animal>(animal =>
        {
            animal.HasKey(a => a.Id);
            animal.Property(a => a.Name).HasMaxLength(Animal.NameMaxLength).IsRequired();
        });

        builder.Entity<AnimalMember>(member =>
        {
            member.ToTable(table => table.HasCheckConstraint("ck_animal_members_role", "role IN ('owner')"));
            member.HasKey(m => new { m.AnimalId, m.UserId });
            member.HasIndex(m => m.UserId);
            member.Property(m => m.Role)
                .HasConversion(role => ToDatabase(role), value => FromDatabase(value))
                .HasColumnType("text")
                .IsRequired();
            member.HasOne(m => m.Animal).WithMany().HasForeignKey(m => m.AnimalId);
            member.HasOne<AppUser>().WithMany().HasForeignKey(m => m.UserId);
        });
    }

    private static string ToDatabase(AnimalRole role) => role switch
    {
        AnimalRole.Owner => "owner",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown animal role."),
    };

    private static AnimalRole FromDatabase(string value) => value switch
    {
        "owner" => AnimalRole.Owner,
        _ => throw new InvalidOperationException($"Unknown animal role '{value}'."),
    };
}
