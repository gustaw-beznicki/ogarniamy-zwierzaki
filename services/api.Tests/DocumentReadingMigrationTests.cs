using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.DocumentReading;

namespace ogarniamy_zwierzaki_api.Tests;

// The DocumentReading migration: applied to a fresh database, and on top of an S-02 schema (before AnimalActivity)
// that already holds Stored and Uploading documents, which must get reading work only for the Stored ones.
public sealed class DocumentReadingMigrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string S02Migration = "20261004120243_AuthSessions";

    private const string PreviousMigration = "20261005180733_AnimalActivity";

    [Fact]
    public async Task A_fresh_database_migrates_to_the_reading_schema()
    {
        await using var db = NewDatabase("reading_fresh");
        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Empty(await db.DocumentReadingJobs.ToListAsync());
        Assert.Empty(await db.DocumentReadingRunFiles.ToListAsync());
        Assert.Empty(await db.DocumentTextPages.ToListAsync());

        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Existing_stored_documents_get_one_queued_job_and_uploading_documents_none()
    {
        await using var db = NewDatabase("reading_backfill");
        var migrator = db.GetService<IMigrator>();
        var userId = Guid.NewGuid().ToString();
        var animalId = Guid.NewGuid();
        var storedDocument = Guid.NewGuid();
        var uploadingDocument = Guid.NewGuid();

        await migrator.MigrateAsync(S02Migration);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO users (id, user_name, email, email_confirmed, phone_number_confirmed, two_factor_enabled,
                lockout_enabled, access_failed_count)
            VALUES ({userId}, 'existing@example.test', 'existing@example.test', false, false, false, true, 0)
            """);
        await db.Database.ExecuteSqlAsync($"INSERT INTO animals (id, name, created_at) VALUES ({animalId}, 'Czarek', now())");
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO animal_members (animal_id, user_id, role, created_at) VALUES ({animalId}, {userId}, 'owner', now())
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO documents (id, animal_id, event_date, capture_time_zone, storage_state, created_at, uploaded_at)
            VALUES ({storedDocument}, {animalId}, '2026-09-01', 'Europe/Warsaw', 'stored', now(), now())
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO documents (id, animal_id, event_date, capture_time_zone, storage_state, created_at)
            VALUES ({uploadingDocument}, {animalId}, '2026-09-02', 'Europe/Warsaw', 'uploading', now())
            """);

        await migrator.MigrateAsync(PreviousMigration);
        await migrator.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        await AssertSingleQueuedJobAsync(db, storedDocument);
        Assert.Equal(2, await db.Documents.CountAsync());

        // Rolling the reading schema back and forward again keeps the originals and backfills the same work once.
        await migrator.MigrateAsync(PreviousMigration);
        Assert.Equal(2, await db.Documents.CountAsync());
        await migrator.MigrateAsync();
        await AssertSingleQueuedJobAsync(db, storedDocument);

        await db.Database.EnsureDeletedAsync();
    }

    private static async Task AssertSingleQueuedJobAsync(AppDbContext db, Guid documentId)
    {
        var job = Assert.Single(await db.DocumentReadingJobs.AsNoTracking().ToListAsync());
        Assert.Equal(documentId, job.DocumentId);
        Assert.Equal(DocumentReadingState.Queued, job.State);
        Assert.Equal(1, job.RunGeneration);
        Assert.Equal(0, job.AttemptCount);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.PublishedRunGeneration);
    }

    // A separate database on the class's PostgreSQL container, migrated by the test itself.
    private AppDbContext NewDatabase(string prefix)
    {
        var connection = new NpgsqlConnectionStringBuilder(
            factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("Default"))
        {
            Database = $"{prefix}_{Guid.NewGuid():N}",
        };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }
}
