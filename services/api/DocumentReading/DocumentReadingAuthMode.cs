namespace ogarniamy_zwierzaki_api.DocumentReading;

// How the API authenticates to Azure Document Intelligence. Selected by the DocumentReading:Auth setting.
public enum DocumentReadingAuthMode
{
    // Reading is off; the API never calls the provider. The default, so local runs and tests never call Azure.
    Disabled,

    // An Entra token of the App Service system-assigned managed identity. No keys: the resource disables local auth.
    AzureManagedIdentity,

    // The developer's own `az login` identity, for the local probe. Refused outside the Development environment.
    DeveloperCredential,
}
