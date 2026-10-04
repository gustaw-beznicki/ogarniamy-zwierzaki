using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.Documents;

namespace ogarniamy_zwierzaki_api.Tests;

// The document schema and its relational constraints, on the real PostgreSQL migrations. Rows are written with SQL
// here, bypassing OwnedDocuments, so the database itself is shown to refuse invalid data.
public sealed class DocumentModelTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string PreviousMigration = "20260929184113_AccountsAndAnimals";

    private static readonly DateOnly EventDate = new(2026, 9, 1);

    [Fact]
    public async Task The_document_migration_only_adds_schema()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var migrations = db.GetService<IMigrationsAssembly>();
        var (_, type) = Assert.Single(migrations.Migrations, m => m.Key.EndsWith("_DocumentOriginals", StringComparison.Ordinal));

        var migration = migrations.CreateMigration(type, db.Database.ProviderName!);

        Assert.All(migration.UpOperations, operation => Assert.True(
            operation is CreateTableOperation or AddColumnOperation or CreateIndexOperation or AddForeignKeyOperation,
            $"Unexpected {operation.GetType().Name} in an additive migration."));
    }

    [Fact]
    public async Task Upgrading_an_existing_database_keeps_accounts_animals_and_session_keys()
    {
        var connection = new NpgsqlConnectionStringBuilder(
            factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("Default"))
        {
            Database = $"upgrade_{Guid.NewGuid():N}",
        };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new AppDbContext(options);
        var migrator = db.GetService<IMigrator>();
        var userId = Guid.NewGuid().ToString();
        var animalId = Guid.NewGuid();

        // The schema before this change, with data in it.
        await migrator.MigrateAsync(PreviousMigration);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO users (id, user_name, email, email_confirmed, phone_number_confirmed, two_factor_enabled,
                lockout_enabled, access_failed_count)
            VALUES ({userId}, 'existing@example.test', 'existing@example.test', false, false, false, true, 0)
            """);
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO animals (id, name, created_at) VALUES ({animalId}, 'Czarek', now())");
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO animal_members (animal_id, user_id, role, created_at) VALUES ({animalId}, {userId}, 'owner', now())");
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO data_protection_keys (friendly_name, xml) VALUES ('key', '<key />')");

        await migrator.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(1, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM users"));
        Assert.Equal(1, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM animals"));
        Assert.Equal(1, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM animal_members"));
        Assert.Equal(1, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM data_protection_keys"));
        Assert.Equal(1, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM users WHERE last_capture_animal_id IS NULL"));
        Assert.Equal(0, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM documents"));

        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task File_lengths_from_one_byte_to_ten_mebibytes_are_accepted()
    {
        var documentId = await AddDocumentAsync();

        await InsertFileAsync(documentId, position: 1, byteLength: 1);
        await InsertFileAsync(documentId, position: 2, byteLength: DocumentFile.MaxByteLength);

        await AssertRejectedAsync("ck_document_files_byte_length", InsertFileAsync(documentId, position: 3, byteLength: 0));
        await AssertRejectedAsync(
            "ck_document_files_byte_length", InsertFileAsync(documentId, position: 3, byteLength: DocumentFile.MaxByteLength + 1));
    }

    [Theory]
    [InlineData("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("abcdef0123456789abcdef0123456789abcdef0123456789abcdef012345678")]
    [InlineData("abcdef0123456789abcdef0123456789abcdef0123456789abcdef012345678g")]
    public async Task Hashes_must_be_64_lowercase_hex_characters(string sha256)
    {
        var documentId = await AddDocumentAsync();

        await AssertRejectedAsync("ck_document_files_sha256", InsertFileAsync(documentId, position: 1, sha256: sha256));
    }

    [Theory]
    [InlineData("image/heic")]
    [InlineData("text/plain")]
    [InlineData("IMAGE/PNG")]
    public async Task Only_pdf_jpeg_and_png_originals_are_stored(string contentType)
    {
        var documentId = await AddDocumentAsync();

        await AssertRejectedAsync(
            "ck_document_files_content_type", InsertFileAsync(documentId, position: 1, contentType: contentType));
    }

    [Fact]
    public async Task Positions_are_zero_to_nine_and_unique_within_a_document()
    {
        var documentId = await AddDocumentAsync();

        await InsertFileAsync(documentId, position: 9);

        await AssertRejectedAsync("ck_document_files_position", InsertFileAsync(documentId, position: 10));
        await AssertRejectedAsync("ck_document_files_position", InsertFileAsync(documentId, position: -1));
        await AssertRejectedAsync("ix_document_files_document_id_position", InsertFileAsync(documentId, position: 0));
        await AssertRejectedAsync(
            "ck_document_files_position",
            InsertFileAsync(documentId, position: 1, contentType: DocumentFile.PdfContentType));

        // The same position is free in another document.
        await InsertFileAsync(await AddDocumentAsync(), position: 9);
    }

    [Fact]
    public async Task Blob_keys_are_derived_from_ids_and_unique()
    {
        var documentId = await AddDocumentAsync();

        await AssertRejectedAsync(
            "ck_document_files_blob_key", InsertFileAsync(documentId, position: 1, blobKey: "documents/scan.pdf"));
        Assert.Contains(
            "CREATE UNIQUE INDEX ix_document_files_blob_key",
            await IndexDefinitionAsync("ix_document_files_blob_key"));
    }

    [Fact]
    public async Task A_receipt_is_complete_and_matches_the_manifest()
    {
        var documentId = await AddDocumentAsync();
        var sha256 = DocumentTestData.RandomSha256();

        await InsertFileAsync(documentId, position: 1, sha256: sha256, receipt: (1_000, sha256, "\"etag\""));

        await AssertRejectedAsync(
            "ck_document_files_receipt",
            InsertFileAsync(documentId, position: 2, sha256: sha256, receipt: (999, sha256, "\"etag\"")));
        await AssertRejectedAsync(
            "ck_document_files_receipt",
            InsertFileAsync(documentId, position: 3, receipt: (1_000, DocumentTestData.RandomSha256(), "\"etag\"")));
        await AssertRejectedAsync(
            "ck_document_files_receipt",
            InsertFileAsync(documentId, position: 4, sha256: sha256, receipt: (1_000, sha256, null)));
    }

    [Fact]
    public async Task Uploaded_at_is_set_exactly_for_stored_documents()
    {
        var documentId = await AddDocumentAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await AssertRejectedAsync(
            "ck_documents_uploaded_at",
            db.Database.ExecuteSqlAsync($"UPDATE documents SET storage_state = 'stored' WHERE id = {documentId}"));
        await AssertRejectedAsync(
            "ck_documents_uploaded_at",
            db.Database.ExecuteSqlAsync($"UPDATE documents SET uploaded_at = now() WHERE id = {documentId}"));
        await AssertRejectedAsync(
            "ck_documents_storage_state",
            db.Database.ExecuteSqlAsync($"UPDATE documents SET storage_state = 'deleted' WHERE id = {documentId}"));
    }

    [Fact]
    public async Task Documents_need_an_existing_animal_and_keep_their_animal_and_files_from_deletion()
    {
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var documentId = await factory.AddUploadAsync(
            userId, DocumentTestData.NewManifest(animalId, EventDate, DocumentFile.PngContentType));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await AssertRejectedAsync(
            "fk_documents_animals_animal_id",
            db.Database.ExecuteSqlAsync($"""
                INSERT INTO documents (id, animal_id, event_date, capture_time_zone, storage_state, created_at)
                VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, {EventDate}, 'Europe/Warsaw', 'uploading', now())
                """));
        await AssertRejectedAsync(
            "fk_documents_animals_animal_id",
            db.Database.ExecuteSqlAsync($"DELETE FROM animals WHERE id = {animalId}"));
        await AssertRejectedAsync(
            "fk_document_files_documents_document_id",
            db.Database.ExecuteSqlAsync($"DELETE FROM documents WHERE id = {documentId}"));
        await AssertRejectedAsync(
            "fk_users_animals_last_capture_animal_id",
            db.Database.ExecuteSqlAsync(
                $"UPDATE users SET last_capture_animal_id = {Guid.NewGuid()} WHERE id = {userId}"));
    }

    // An Uploading document with one PNG at position 0.
    private async Task<Guid> AddDocumentAsync()
    {
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        return await factory.AddUploadAsync(
            userId, DocumentTestData.NewManifest(animalId, EventDate, DocumentFile.PngContentType));
    }

    private async Task InsertFileAsync(
        Guid documentId,
        int position,
        string contentType = DocumentFile.JpegContentType,
        long byteLength = 1_000,
        string? sha256 = null,
        string? blobKey = null,
        (long Length, string Sha256, string? ETag)? receipt = null)
    {
        var id = Guid.NewGuid();
        sha256 ??= DocumentTestData.RandomSha256();
        blobKey ??= DocumentFile.BlobKeyFor(documentId, id);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO document_files (id, document_id, position, original_name, content_type, byte_length, sha256,
                blob_key, receipt_length, receipt_sha256, receipt_etag)
            VALUES ({id}, {documentId}, {position}, 'page', {contentType}, {byteLength}, {sha256},
                {blobKey}, {receipt?.Length}, {receipt?.Sha256}, {receipt?.ETag})
            """);
    }

    private async Task<string> IndexDefinitionAsync(string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE indexname = {name}")
            .SingleAsync();
    }

    private static Task<long> CountAsync(AppDbContext db, string sql) =>
        db.Database.SqlQueryRaw<long>(sql).SingleAsync();

    private static async Task AssertRejectedAsync(string constraint, Task statement)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(() => statement);
        Assert.Equal(constraint, exception.ConstraintName);
    }
}
