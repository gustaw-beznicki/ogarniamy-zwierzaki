using Azure;
using ogarniamy_zwierzaki_api.Storage;

namespace ogarniamy_zwierzaki_api.Tests;

// The production Blob adapter with failures injected on demand at the storage boundary. Test hosts only.
public sealed class FaultyOriginalStorage(IOriginalStorage inner) : IOriginalStorage
{
    // Every call fails as if Blob Storage could not be reached.
    public bool Unavailable { get; set; }

    // The next write succeeds in storage, but its response is lost on the way back.
    public bool LoseNextCreateResponse { get; set; }

    public async Task<OriginalFileReceipt?> CreateIfAbsentAsync(
        string key, Stream content, string contentType, CancellationToken cancellationToken, string? verifiedSha256 = null)
    {
        ThrowIfUnavailable();
        var receipt = await inner.CreateIfAbsentAsync(key, content, contentType, cancellationToken, verifiedSha256);
        if (LoseNextCreateResponse)
        {
            LoseNextCreateResponse = false;
            throw new RequestFailedException(0, "Injected: the response of a completed write was lost.");
        }

        return receipt;
    }

    public Task<OriginalFileReceipt?> GetReceiptAsync(string key, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        return inner.GetReceiptAsync(key, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(
        string key, long expectedLength, string expectedSha256, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        return inner.OpenReadAsync(key, expectedLength, expectedSha256, cancellationToken);
    }

    private void ThrowIfUnavailable()
    {
        if (Unavailable)
        {
            throw new RequestFailedException(503, "Injected: Blob Storage is unavailable.");
        }
    }
}
