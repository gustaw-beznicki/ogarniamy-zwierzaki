namespace ogarniamy_zwierzaki_api.Data;

// How the API authenticates to PostgreSQL. Selected by the Database:Auth setting.
public enum DatabaseAuthMode
{
    // The password is part of ConnectionStrings:Default (local, tests, self-hosted).
    Password,

    // The password is a periodically refreshed Entra token of the App Service managed identity.
    AzureManagedIdentity,
}
