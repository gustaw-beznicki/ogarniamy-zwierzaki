using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ogarniamy_zwierzaki_api.Tests;

// Shared helpers: every test uses its own accounts, so tests do not depend on each other.
public static class TestAccounts
{
    public const string ValidPassword = "long-enough-1";

    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.test";

    // A cookie-aware client that never follows redirects, so a redirect would show up as a failed assertion.
    public static HttpClient CreateClient(this ApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public static Task<HttpResponseMessage> RegisterAsync(this HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/register", new { email, password });

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    public static Task<HttpResponseMessage> CreateAnimalAsync(this HttpClient client, string? name) =>
        client.PostAsJsonAsync("/api/animals", new { name });

    // Registers a fresh account; the returned client carries its session cookie.
    public static async Task<(HttpClient Client, string Email)> CreateSignedInClientAsync(this ApiFactory factory)
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        using var response = await client.RegisterAsync(email, ValidPassword);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (client, email);
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    public static async Task AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    public static async Task<JsonElement> GetMeAsync(this HttpClient client)
    {
        using var response = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }
}
