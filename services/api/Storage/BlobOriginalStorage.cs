using System.Security.Cryptography;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ogarniamy_zwierzaki_api.Storage;

// IOriginalStorage on a private Blob container: Azure Storage in production, Azurite locally and in tests.
public sealed class BlobOriginalStorage(BlobContainerClient container) : IOriginalStorage
{
    // Blob metadata entry holding the content's SHA-256, so a receipt can be read without downloading the blob.
    private const string Sha256MetadataKey = "sha256";

    public async Task<OriginalFileReceipt?> CreateIfAbsentAsync(
        string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        if (!content.CanSeek)
        {
            throw new ArgumentException("The content must be seekable.", nameof(content));
        }

        var start = content.Position;
        var sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(content, cancellationToken));
        var length = content.Position - start;
        content.Position = start;

        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            Metadata = new Dictionary<string, string> { [Sha256MetadataKey] = sha256 },
            // If-None-Match: * makes the service reject the write when the blob exists, so it is never overwritten.
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
        };

        try
        {
            var response = await container.GetBlobClient(key).UploadAsync(content, options, cancellationToken);
            return new OriginalFileReceipt(length, sha256, response.Value.ETag.ToString());
        }
        catch (RequestFailedException exception) when (IsAlreadyExists(exception))
        {
            return null;
        }
    }

    public async Task<OriginalFileReceipt?> GetReceiptAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        try
        {
            var properties = (await container.GetBlobClient(key).GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
            if (!properties.Metadata.TryGetValue(Sha256MetadataKey, out var sha256) || string.IsNullOrEmpty(sha256))
            {
                // Every original is written with its hash, so a missing one means the blob was not written by this adapter.
                throw new InvalidOperationException($"The original '{key}' has no '{Sha256MetadataKey}' metadata.");
            }

            return new OriginalFileReceipt(properties.ContentLength, sha256, properties.ETag.ToString());
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        try
        {
            // allowModifications: false pins the ETag, so a changed blob fails the read instead of mixing versions.
            return await container.GetBlobClient(key)
                .OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    // Azure answers a conditional Put Blob on an existing blob with 409 BlobAlreadyExists; 412 covers a failed condition.
    private static bool IsAlreadyExists(RequestFailedException exception) =>
        exception.ErrorCode == BlobErrorCode.BlobAlreadyExists
        || exception.Status == StatusCodes.Status412PreconditionFailed;
}
