using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ogarniamy_zwierzaki_api.Animals;
using ogarniamy_zwierzaki_api.Data;

namespace ogarniamy_zwierzaki_api.Tests;

// The AnimalActivity migration, applied on top of a previous-schema database that already holds data.
public sealed class AnimalActivityMigrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string PreviousMigration = "20261004120243_AuthSessions";

    [Fact]
    public async Task Existing_animals_become_active_with_distinct_versions_and_keep_their_data()
    {
        var connection = new NpgsqlConnectionStringBuilder(
            factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("Default"))
        {
            Database = $"animal_activity_{Guid.NewGuid():N}",
        };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new AppDbContext(options);
        var migrator = db.GetService<IMigrator>();
        var userId = Guid.NewGuid().ToString();
        var firstAnimal = Guid.NewGuid();
        var secondAnimal = Guid.NewGuid();
        var storedDocument = Guid.NewGuid();
        var uploadingDocument = Guid.NewGuid();

        await migrator.MigrateAsync(PreviousMigration);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO users (id, user_name, email, email_confirmed, phone_number_confirmed, two_factor_enabled,
                lockout_enabled, access_failed_count)
            VALUES ({userId}, 'existing@example.test', 'existing@example.test', false, false, false, true, 0)
            """);
        foreach (var (id, name) in new[] { (firstAnimal, "Czarek"), (secondAnimal, "Burek") })
        {
            await db.Database.ExecuteSqlAsync($"INSERT INTO animals (id, name, created_at) VALUES ({id}, {name}, now())");
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO animal_members (animal_id, user_id, role, created_at) VALUES ({id}, {userId}, 'owner', now())
                """);
        }

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO documents (id, animal_id, event_date, capture_time_zone, storage_state, created_at, uploaded_at)
            VALUES ({storedDocument}, {firstAnimal}, '2026-09-01', 'Europe/Warsaw', 'stored', now(), now())
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO documents (id, animal_id, event_date, capture_time_zone, storage_state, created_at)
            VALUES ({uploadingDocument}, {firstAnimal}, '2026-09-02', 'Europe/Warsaw', 'uploading', now())
            """);

        await migrator.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(2, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM animals WHERE is_active"));
        Assert.Equal(2, await CountAsync(db, "SELECT count(DISTINCT version) AS \"Value\" FROM animals"));
        Assert.Equal(2, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM animal_members"));
        Assert.Equal(2, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM documents"));
        Assert.Equal(
            ["Burek", "Czarek"],
            await db.Database.SqlQueryRaw<string>("SELECT name AS \"Value\" FROM animals ORDER BY name").ToListAsync());

        // The migrated rows work through the owner repository: all owned animals still count for onboarding.
        var animals = new OwnedAnimals(db);
        Assert.Equal(2, await animals.CountAsync(userId));
        var listed = await animals.ListActiveAsync(userId);
        Assert.Equal(2, listed.Count);
        Assert.All(listed, animal => Assert.True(animal.IsActive));
        Assert.Equal(1, listed.Single(a => a.Id == firstAnimal).StoredDocumentCount);
        Assert.Equal(0, listed.Single(a => a.Id == secondAnimal).StoredDocumentCount);

        await db.Database.EnsureDeletedAsync();
    }

    private static Task<long> CountAsync(AppDbContext db, string sql) =>
        db.Database.SqlQueryRaw<long>(sql).SingleAsync();
}
