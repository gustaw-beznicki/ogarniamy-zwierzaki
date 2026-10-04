using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.Documents;
using ogarniamy_zwierzaki_api.Storage;

namespace ogarniamy_zwierzaki_api.Tests;

// OwnedDocuments on the real database: owner isolation, pending exclusion, ordering/pagination and capture defaults.
public sealed class DocumentRepositoryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateOnly EventDate = new(2026, 9, 1);

    [Fact]
    public async Task Uploading_documents_are_reachable_only_through_their_upload_operation()
    {
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var operationId = await factory.AddUploadAsync(
            userId,
            DocumentTestData.NewManifest(animalId, EventDate, DocumentFile.JpegContentType, DocumentFile.PngContentType));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var documents = Documents(scope);
            var upload = await documents.FindUploadAsync(userId, operationId);
            Assert.NotNull(upload);
            Assert.Equal(DocumentStorageState.Uploading, upload.StorageState);
            Assert.Null(upload.UploadedAt);
            Assert.Equal([0, 1], upload.Files.Select(f => f.Position));
            Assert.All(upload.Files, f => Assert.Equal($"documents/{operationId}/{f.Id}", f.BlobKey));

            var page = await documents.ListStoredAsync(userId, animalId, 0, OwnedDocuments.DefaultPageSize);
            Assert.NotNull(page);
            Assert.Empty(page.Items);
            Assert.False(page.HasMore);
            Assert.Null(await documents.FindStoredAsync(userId, operationId));
            Assert.Null(await documents.FindStoredFileAsync(userId, operationId, upload.Files[0].Id));
        }

        // Completion needs every original's receipt.
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.CompleteAsync(userId, operationId));
        await factory.RecordAllReceiptsAsync(userId, operationId);
        var stored = await factory.CompleteAsync(userId, operationId);

        Assert.NotNull(stored);
        Assert.Equal(animalId, stored.AnimalId);
        Assert.Equal("Czarek", stored.AnimalName);
        Assert.Equal(EventDate, stored.EventDate);
        Assert.Equal(
            [DocumentFile.JpegContentType, DocumentFile.PngContentType], stored.Files.Select(f => f.ContentType));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var page = await Documents(scope).ListStoredAsync(userId, animalId, 0, OwnedDocuments.DefaultPageSize);
            Assert.NotNull(page);
            Assert.Equal([operationId], page.Items.Select(d => d.Id));
            Assert.Equal(2, page.Items[0].FileCount);
            var file = await Documents(scope).FindStoredFileAsync(userId, operationId, stored.Files[1].Id);
            Assert.Equal(stored.Files[1], file);
        }
    }

    [Fact]
    public async Task Another_accounts_documents_and_animals_are_indistinguishable_from_missing_ones()
    {
        var ownerId = await factory.CreateUserAsync();
        var otherId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(ownerId);
        var stored = await factory.AddStoredAsync(ownerId, animalId, EventDate);
        var pendingId = await factory.AddUploadAsync(
            ownerId, DocumentTestData.NewManifest(animalId, EventDate, DocumentFile.PdfContentType));

        await using var scope = factory.Services.CreateAsyncScope();
        var documents = Documents(scope);

        Assert.Null(await documents.ListStoredAsync(otherId, animalId, 0, OwnedDocuments.DefaultPageSize));
        Assert.Null(await documents.ListStoredAsync(otherId, Guid.NewGuid(), 0, OwnedDocuments.DefaultPageSize));
        Assert.Null(await documents.FindStoredAsync(otherId, stored.Id));
        Assert.Null(await documents.FindStoredFileAsync(otherId, stored.Id, stored.Files[0].Id));
        Assert.Null(await documents.FindUploadAsync(otherId, pendingId));
        Assert.Null(await documents.FindUploadAsync(otherId, stored.Id));
        Assert.Null(await documents.CompleteAsync(otherId, pendingId));
        Assert.False(await documents.AddUploadAsync(
            otherId, DocumentTestData.NewManifest(animalId, EventDate, DocumentFile.PngContentType)));

        // The owner still sees exactly what was there, and the other account's preference is untouched.
        Assert.Equal(DocumentStorageState.Uploading, (await documents.FindUploadAsync(ownerId, pendingId))?.StorageState);
        Assert.Null((await documents.GetCaptureDefaultsAsync(otherId)).DefaultAnimalId);
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Documents
            .CountAsync(d => d.AnimalId == animalId && d.StorageState == DocumentStorageState.Uploading));
    }

    [Fact]
    public async Task A_taken_operation_id_is_rejected_without_changing_the_existing_document()
    {
        var ownerId = await factory.CreateUserAsync();
        var otherId = await factory.CreateUserAsync();
        var ownerAnimal = await factory.AddAnimalAsync(ownerId);
        var otherAnimal = await factory.AddAnimalAsync(otherId);
        var operationId = await factory.AddUploadAsync(
            ownerId, DocumentTestData.NewManifest(ownerAnimal, EventDate, DocumentFile.PngContentType));

        await using var scope = factory.Services.CreateAsyncScope();
        var documents = Documents(scope);
        var reused = DocumentTestData.NewManifest(otherAnimal, EventDate, DocumentFile.PdfContentType);
        reused.Id = operationId;

        await Assert.ThrowsAsync<DbUpdateException>(() => documents.AddUploadAsync(otherId, reused));
        Assert.Null(await documents.FindUploadAsync(otherId, operationId));
        var existing = await documents.FindUploadAsync(ownerId, operationId);
        Assert.Equal(ownerAnimal, existing?.AnimalId);
        Assert.Equal([DocumentFile.PngContentType], existing?.Files.Select(f => f.ContentType));
    }

    [Theory]
    [InlineData("")]
    [InlineData("application/pdf,application/pdf")]
    [InlineData("application/pdf,image/jpeg")]
    [InlineData("image/heic")]
    public async Task Invalid_file_sets_are_refused_before_anything_is_written(string contentTypes)
    {
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var manifest = DocumentTestData.NewManifest(
            animalId, EventDate, contentTypes.Split(',', StringSplitOptions.RemoveEmptyEntries));

        await using var scope = factory.Services.CreateAsyncScope();
        var documents = Documents(scope);

        await Assert.ThrowsAsync<ArgumentException>(() => documents.AddUploadAsync(userId, manifest));
        Assert.Null(await documents.FindUploadAsync(userId, manifest.Id));
    }

    [Fact]
    public async Task One_pdf_or_one_to_ten_images_are_accepted_and_eleven_are_not()
    {
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var ten = Enumerable.Repeat(DocumentFile.JpegContentType, 9).Append(DocumentFile.PngContentType).ToArray();

        await factory.AddUploadAsync(userId, DocumentTestData.NewManifest(animalId, EventDate, DocumentFile.PdfContentType));
        await factory.AddUploadAsync(userId, DocumentTestData.NewManifest(animalId, EventDate, DocumentFile.JpegContentType));
        await factory.AddUploadAsync(userId, DocumentTestData.NewManifest(animalId, EventDate, ten));

        await using var scope = factory.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<ArgumentException>(() => Documents(scope).AddUploadAsync(
            userId, DocumentTestData.NewManifest(animalId, EventDate, [.. ten, DocumentFile.JpegContentType])));
    }

    [Fact]
    public async Task Stored_documents_list_by_event_date_then_upload_time_then_id_in_stable_pages()
    {
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var otherAnimalId = await factory.AddAnimalAsync(userId, "Burek");
        var older = await factory.AddStoredAsync(userId, animalId, new DateOnly(2025, 1, 1));
        var newest = await factory.AddStoredAsync(userId, animalId, new DateOnly(2026, 9, 2));
        var tiedA = await factory.AddStoredAsync(userId, animalId, EventDate);
        var tiedB = await factory.AddStoredAsync(userId, animalId, EventDate);
        var laterUpload = await factory.AddStoredAsync(userId, animalId, EventDate);
        await factory.AddStoredAsync(userId, otherAnimalId, EventDate);
        await factory.AddUploadAsync(userId, DocumentTestData.NewManifest(animalId, new DateOnly(2026, 9, 3), DocumentFile.PngContentType));

        // Same event date and upload time for two documents, so only the ID separates them.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tie = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
            await db.Database.ExecuteSqlAsync(
                $"UPDATE documents SET uploaded_at = {tie} WHERE id IN ({tiedA.Id}, {tiedB.Id})");
            await db.Database.ExecuteSqlAsync(
                $"UPDATE documents SET uploaded_at = {tie.AddMinutes(1)} WHERE id = {laterUpload.Id}");
        }

        // PostgreSQL orders UUIDs by their bytes, which matches the ordinal order of their canonical text.
        var tiedHigh = string.CompareOrdinal(tiedA.Id.ToString(), tiedB.Id.ToString()) > 0 ? tiedA.Id : tiedB.Id;
        var tiedLow = tiedHigh == tiedA.Id ? tiedB.Id : tiedA.Id;
        Guid[] expected = [newest.Id, laterUpload.Id, tiedHigh, tiedLow, older.Id];

        await using var reading = factory.Services.CreateAsyncScope();
        var documents = Documents(reading);
        var all = await documents.ListStoredAsync(userId, animalId, 0, OwnedDocuments.MaxPageSize);
        Assert.NotNull(all);
        Assert.Equal(expected, all.Items.Select(d => d.Id));
        Assert.False(all.HasMore);

        var paged = new List<Guid>();
        for (var offset = 0; ; offset += 2)
        {
            var page = await documents.ListStoredAsync(userId, animalId, offset, 2);
            Assert.NotNull(page);
            paged.AddRange(page.Items.Select(d => d.Id));
            Assert.Equal(offset + 2 < expected.Length, page.HasMore);
            if (!page.HasMore)
            {
                break;
            }
        }

        Assert.Equal(expected, paged);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => documents.ListStoredAsync(userId, animalId, 0, OwnedDocuments.MaxPageSize + 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => documents.ListStoredAsync(userId, animalId, 0, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => documents.ListStoredAsync(userId, animalId, -1, 10));
    }

    [Fact]
    public async Task Capture_defaults_follow_the_accounts_last_completed_capture()
    {
        var userId = await factory.CreateUserAsync();
        var otherUserId = await factory.CreateUserAsync();

        // No animals: onboarding.
        var none = await DefaultsAsync(userId);
        Assert.Empty(none.Animals);
        Assert.Null(none.DefaultAnimalId);

        var first = await factory.AddAnimalAsync(userId, "Czarek");
        var second = await factory.AddAnimalAsync(userId, "Burek");
        var otherAnimal = await factory.AddAnimalAsync(otherUserId, "Reks");

        // Before any capture: the earliest-created animal.
        var initial = await DefaultsAsync(userId);
        Assert.Equal([first, second], initial.Animals.Select(a => a.Id));
        Assert.Equal(first, initial.DefaultAnimalId);

        // Creating an operation does not change the default; completing it does.
        var earlier = await factory.AddUploadAsync(
            userId, DocumentTestData.NewManifest(first, EventDate, DocumentFile.PngContentType));
        var later = await factory.AddUploadAsync(
            userId, DocumentTestData.NewManifest(second, EventDate, DocumentFile.PngContentType));
        await factory.RecordAllReceiptsAsync(userId, earlier);
        await factory.RecordAllReceiptsAsync(userId, later);
        Assert.Equal(first, (await DefaultsAsync(userId)).DefaultAnimalId);

        var completedLater = await factory.CompleteAsync(userId, later);
        Assert.Equal(second, (await DefaultsAsync(userId)).DefaultAnimalId);

        var completedEarlier = await factory.CompleteAsync(userId, earlier);
        Assert.Equal(first, (await DefaultsAsync(userId)).DefaultAnimalId);

        // A repeated completion returns the same document and has no further side effect.
        var repeated = await factory.CompleteAsync(userId, later);
        Assert.NotNull(completedLater);
        Assert.NotNull(repeated);
        Assert.Equal(completedLater.UploadedAt, repeated.UploadedAt);
        Assert.Equal(completedLater.Files, repeated.Files);
        Assert.Equal(first, (await DefaultsAsync(userId)).DefaultAnimalId);
        Assert.NotNull(completedEarlier);

        // The preference is per account.
        Assert.Equal(otherAnimal, (await DefaultsAsync(otherUserId)).DefaultAnimalId);

        // A saved animal that is no longer the user's falls back to the earliest eligible one.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlAsync(
                $"DELETE FROM animal_members WHERE animal_id = {first} AND user_id = {userId}");
        }

        var fallback = await DefaultsAsync(userId);
        Assert.Equal([second], fallback.Animals.Select(a => a.Id));
        Assert.Equal(second, fallback.DefaultAnimalId);
    }

    [Fact]
    public async Task Concurrent_completions_store_the_document_once()
    {
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var operationId = await factory.AddUploadAsync(
            userId, DocumentTestData.NewManifest(animalId, EventDate, DocumentFile.PdfContentType));
        await factory.RecordAllReceiptsAsync(userId, operationId);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => factory.CompleteAsync(userId, operationId)));

        Assert.All(results, result => Assert.NotNull(result));
        Assert.Single(results.Select(r => r!.UploadedAt).Distinct());
        Assert.Equal(animalId, (await DefaultsAsync(userId)).DefaultAnimalId);
    }

    [Fact]
    public async Task A_recorded_receipt_is_never_replaced()
    {
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var operationId = await factory.AddUploadAsync(
            userId, DocumentTestData.NewManifest(animalId, EventDate, DocumentFile.PngContentType));

        await using var scope = factory.Services.CreateAsyncScope();
        var documents = Documents(scope);
        var file = (await documents.FindUploadAsync(userId, operationId))!.Files[0];
        var receipt = new OriginalFileReceipt(file.ByteLength, file.Sha256, "\"first\"");

        Assert.True(await documents.RecordReceiptAsync(file, receipt));
        Assert.True(await documents.RecordReceiptAsync(file, receipt));
        Assert.False(await documents.RecordReceiptAsync(file, receipt with { ETag = "\"second\"" }));
        Assert.Equal(receipt, file.Receipt);
        await Assert.ThrowsAsync<ArgumentException>(
            () => documents.RecordReceiptAsync(file, receipt with { Length = file.ByteLength + 1 }));
    }

    private static OwnedDocuments Documents(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<OwnedDocuments>();

    private async Task<CaptureDefaults> DefaultsAsync(string userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await Documents(scope).GetCaptureDefaultsAsync(userId);
    }
}
