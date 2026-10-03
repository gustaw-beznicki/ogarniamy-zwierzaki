using System.Net;
using System.Security.Cryptography;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ogarniamy_zwierzaki_api.Storage;

namespace ogarniamy_zwierzaki_api.Tests;

// The production Blob adapter against Azurite. Every test writes under its own keys.
public sealed class OriginalStorageTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private IOriginalStorage Storage => factory.Services.GetRequiredService<IOriginalStorage>();

    [Fact]
    public async Task Create_stores_the_bytes_unchanged_and_returns_a_matching_receipt()
    {
        var key = UniqueKey();
        var bytes = RandomBytes(300_000);
        using var content = new MemoryStream(bytes);

        var created = await Storage.CreateIfAbsentAsync(key, content, "application/pdf", CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal(bytes.LongLength, created.Length);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), created.Sha256);
        Assert.False(string.IsNullOrEmpty(created.ETag));
        Assert.Equal(created, await Storage.GetReceiptAsync(key, CancellationToken.None));
        Assert.Equal(bytes, await ReadAllAsync(key));
    }

    [Fact]
    public async Task Create_hashes_from_the_current_position_of_the_stream()
    {
        var key = UniqueKey();
        var bytes = RandomBytes(1_000);
        using var content = new MemoryStream([.. RandomBytes(10), .. bytes]) { Position = 10 };

        var created = await Storage.CreateIfAbsentAsync(key, content, "image/png", CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal(bytes.LongLength, created.Length);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), created.Sha256);
        Assert.Equal(bytes, await ReadAllAsync(key));
    }

    [Fact]
    public async Task Create_never_overwrites_an_existing_original()
    {
        var key = UniqueKey();
        var original = RandomBytes(2_000);
        using (var first = new MemoryStream(original))
        {
            Assert.NotNull(await Storage.CreateIfAbsentAsync(key, first, "image/jpeg", CancellationToken.None));
        }

        var receipt = await Storage.GetReceiptAsync(key, CancellationToken.None);

        using var replacement = new MemoryStream(RandomBytes(3_000));
        Assert.Null(await Storage.CreateIfAbsentAsync(key, replacement, "image/jpeg", CancellationToken.None));

        using var same = new MemoryStream(original);
        Assert.Null(await Storage.CreateIfAbsentAsync(key, same, "image/jpeg", CancellationToken.None));

        Assert.Equal(receipt, await Storage.GetReceiptAsync(key, CancellationToken.None));
        Assert.Equal(original, await ReadAllAsync(key));
    }

    [Fact]
    public async Task A_missing_original_has_no_receipt_and_no_content()
    {
        var key = UniqueKey();

        Assert.Null(await Storage.GetReceiptAsync(key, CancellationToken.None));
        Assert.Null(await Storage.OpenReadAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task Reading_honours_cancellation()
    {
        var key = UniqueKey();
        using (var content = new MemoryStream(RandomBytes(1_000)))
        {
            Assert.NotNull(await Storage.CreateIfAbsentAsync(key, content, "image/png", CancellationToken.None));
        }

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Storage.OpenReadAsync(key, cancelled.Token));
    }

    [Fact]
    public async Task Originals_cannot_be_read_anonymously()
    {
        var key = UniqueKey();
        using (var content = new MemoryStream(RandomBytes(1_000)))
        {
            Assert.NotNull(await Storage.CreateIfAbsentAsync(key, content, "image/png", CancellationToken.None));
        }

        var container = factory.Services.GetRequiredService<BlobContainerClient>();
        Assert.Equal(PublicAccessType.None, (await container.GetAccessPolicyAsync()).Value.BlobPublicAccess);

        using var anonymous = new HttpClient();
        using var response = await anonymous.GetAsync(container.GetBlobClient(key).Uri);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
    }

    [Fact]
    public void Emulator_storage_is_refused_outside_development()
    {
        using var production = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var exception = Assert.Throws<InvalidOperationException>(() => production.Services);

        Assert.Contains("Storage:Auth", exception.Message);
    }

    private static string UniqueKey() => $"tests/{Guid.NewGuid():N}";

    private static byte[] RandomBytes(int length) => RandomNumberGenerator.GetBytes(length);

    private async Task<byte[]> ReadAllAsync(string key)
    {
        await using var stream = await Storage.OpenReadAsync(key, CancellationToken.None);
        Assert.NotNull(stream);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        return copy.ToArray();
    }
}
