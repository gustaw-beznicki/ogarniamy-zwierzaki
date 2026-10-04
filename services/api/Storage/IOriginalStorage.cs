namespace ogarniamy_zwierzaki_api.Storage;

// Write-once storage of original files under opaque keys. An existing original is never overwritten.
public interface IOriginalStorage
{
    // Stores the content under the key unless the key is already taken. Returns the new original's receipt,
    // or null when an original already exists under the key; that original is left unchanged, and the caller
    // compares it through GetReceiptAsync. The content must be seekable: it is hashed before the upload.
    Task<OriginalFileReceipt?> CreateIfAbsentAsync(
        string key, Stream content, string contentType, CancellationToken cancellationToken);

    // The receipt of the original stored under the key, or null when there is none.
    Task<OriginalFileReceipt?> GetReceiptAsync(string key, CancellationToken cancellationToken);

    // A seekable read-only stream of the original, fetched in chunks as it is read, or null when there is none.
    // The caller disposes it. Reading fails if the original changes after it was opened.
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);
}
