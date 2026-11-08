namespace ogarniamy_zwierzaki_api.Tests;

// A signed-in test account: its HTTP client (session cookie, and usually an antiforgery token) and its user ID.
public sealed record SearchAccount(HttpClient Client, string UserId) : IDisposable
{
    public void Dispose() => Client.Dispose();
}
