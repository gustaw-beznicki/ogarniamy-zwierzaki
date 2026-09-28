namespace ogarniamy_zwierzaki_api.Data;

// How the API authenticates to PostgreSQL. Selected by the Database:Auth setting.
public enum DatabaseAuthMode
{
    // The password is part of ConnectionStrings:Default (local, tests, self-hosted).
    Password,

    // The password is a periodically refreshed Entra token of the App Service managed identity.
    AzureManagedIdentity,
}

// The "Database" configuration section.
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public DatabaseAuthMode Auth { get; init; } = DatabaseAuthMode.Password;

    // AzureManagedIdentity only: the Entra scope of the token used as the password.
    public string EntraTokenScope { get; init; } = string.Empty;

    // AzureManagedIdentity only: how often a fresh token is fetched (tokens are valid for about an hour).
    public TimeSpan TokenRefreshInterval { get; init; }

    // AzureManagedIdentity only: how soon a failed token fetch is retried.
    public TimeSpan TokenRetryInterval { get; init; }
}
