namespace ogarniamy_zwierzaki_api.Storage;

// How the API authenticates to Blob Storage. Selected by the Storage:Auth setting.
public enum StorageAuthMode
{
    // ConnectionStrings:Storage, for the local Azurite emulator. Refused outside the Development environment.
    ConnectionString,

    // An Entra token of the App Service managed identity, against Storage:BlobServiceUri. No account keys.
    AzureManagedIdentity,
}
