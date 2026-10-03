namespace ogarniamy_zwierzaki_api.Storage;

// The "Storage" configuration section.
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public StorageAuthMode Auth { get; init; } = StorageAuthMode.ConnectionString;

    // AzureManagedIdentity only: the account's Blob service endpoint, for example https://<account>.blob.core.windows.net/.
    public Uri? BlobServiceUri { get; init; }

    // The private container holding the originals. Azure provisions it through Bicep.
    public string OriginalsContainer { get; init; } = string.Empty;
}
