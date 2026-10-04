using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ogarniamy_zwierzaki_api.Animals;
using ogarniamy_zwierzaki_api.Auth;
using ogarniamy_zwierzaki_api.Documents;
using ogarniamy_zwierzaki_api.Storage;

namespace ogarniamy_zwierzaki_api.Tests;

// Document test data written straight through the services, without HTTP: accounts, owned animals and manifests.
public static class DocumentTestData
{
    public const string TimeZone = "Europe/Warsaw";

    public static async Task<string> CreateUserAsync(this ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email = TestAccounts.UniqueEmail();
        var user = new AppUser { UserName = email, Email = email };
        var result = await users.CreateAsync(user);
        Assert.True(result.Succeeded);
        return user.Id;
    }

    public static async Task<Guid> AddAnimalAsync(this ApiFactory factory, string userId, string name = "Czarek")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var animals = scope.ServiceProvider.GetRequiredService<OwnedAnimals>();
        return (await animals.CreateAsync(userId, name)).Id;
    }

    // A manifest with one file per content type, in order, with random hashes.
    public static Document NewManifest(Guid animalId, DateOnly eventDate, params string[] contentTypes) => new()
    {
        Id = Guid.NewGuid(),
        AnimalId = animalId,
        EventDate = eventDate,
        CaptureTimeZone = TimeZone,
        Files = contentTypes.Select((contentType, index) => new DocumentFile
        {
            OriginalName = $"page-{index + 1}",
            ContentType = contentType,
            ByteLength = 1_000 + index,
            Sha256 = RandomSha256(),
        }).ToList(),
    };

    public static string RandomSha256() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    // Adds the manifest as an Uploading document of the user's animal.
    public static async Task<Guid> AddUploadAsync(this ApiFactory factory, string userId, Document manifest)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var documents = scope.ServiceProvider.GetRequiredService<OwnedDocuments>();
        Assert.True(await documents.AddUploadAsync(userId, manifest));
        return manifest.Id;
    }

    // Records a receipt matching the manifest for every file, as a successful upload of each original would.
    public static async Task RecordAllReceiptsAsync(this ApiFactory factory, string userId, Guid operationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var documents = scope.ServiceProvider.GetRequiredService<OwnedDocuments>();
        var upload = await documents.FindUploadAsync(userId, operationId);
        Assert.NotNull(upload);
        foreach (var file in upload.Files)
        {
            Assert.True(await documents.RecordReceiptAsync(
                file, new OriginalFileReceipt(file.ByteLength, file.Sha256, $"\"etag-{file.Id:N}\"")));
        }
    }

    public static async Task<DocumentDetails?> CompleteAsync(this ApiFactory factory, string userId, Guid operationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OwnedDocuments>().CompleteAsync(userId, operationId);
    }

    // An Uploading document whose originals are all stored, then completed.
    public static async Task<DocumentDetails> AddStoredAsync(
        this ApiFactory factory, string userId, Guid animalId, DateOnly eventDate, params string[] contentTypes)
    {
        var manifest = NewManifest(
            animalId, eventDate, contentTypes.Length == 0 ? [DocumentFile.PngContentType] : contentTypes);
        var operationId = await factory.AddUploadAsync(userId, manifest);
        await factory.RecordAllReceiptsAsync(userId, operationId);
        var stored = await factory.CompleteAsync(userId, operationId);
        Assert.NotNull(stored);
        return stored;
    }
}
