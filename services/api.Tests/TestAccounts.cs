using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ogarniamy_zwierzaki_api.Tests;

// Shared helpers: every test uses its own accounts, so tests do not depend on each other.
public static class TestAccounts
{
    public const string ValidPassword = "long-enough-1";

    public const string AntiforgeryHeader = "X-CSRF-TOKEN";

    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.test";

    // A cookie-aware client that never follows redirects, so a redirect would show up as a failed assertion.
    public static HttpClient CreateClient(this ApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public static Task<HttpResponseMessage> RegisterAsync(this HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/register", new { email, password });

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    // Animal mutations need an antiforgery token: a client without a default token header gets a real one per request.
    public static Task<HttpResponseMessage> CreateAnimalAsync(this HttpClient client, string? name) =>
        client.SendJsonAsync(HttpMethod.Post, "/api/animals", new { name });

    // Sends a JSON body with an antiforgery token (unless the client already sends one by default) and optional headers.
    public static async Task<HttpResponseMessage> SendJsonAsync(
        this HttpClient client, HttpMethod method, string url, object body, string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        if (!client.DefaultRequestHeaders.Contains(AntiforgeryHeader))
        {
            request.Headers.Add(AntiforgeryHeader, await client.GetAntiforgeryTokenAsync());
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await client.SendAsync(request);
    }

    // Registers a fresh account; the returned client carries its session cookie.
    public static async Task<(HttpClient Client, string Email)> CreateSignedInClientAsync(
        this WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        using var response = await client.RegisterAsync(email, ValidPassword);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (client, email);
    }

    // Fetches an antiforgery request token for the client's session; its paired cookie lands in the client's cookies.
    public static async Task<string> GetAntiforgeryTokenAsync(this HttpClient client)
    {
        using var response = await client.GetAsync("/api/antiforgery");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(AntiforgeryHeader, body.GetProperty("headerName").GetString());
        return body.GetProperty("token").GetString() ?? throw new InvalidOperationException("No antiforgery token.");
    }

    // Sends a fresh antiforgery token with every later request of the client.
    public static async Task UseAntiforgeryTokenAsync(this HttpClient client)
    {
        var token = await client.GetAntiforgeryTokenAsync();
        client.DefaultRequestHeaders.Remove(AntiforgeryHeader);
        client.DefaultRequestHeaders.Add(AntiforgeryHeader, token);
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
