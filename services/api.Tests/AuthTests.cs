using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ogarniamy_zwierzaki_api.Tests;

public sealed class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Register_creates_the_account_and_signs_in()
    {
        using var client = factory.CreateClient();
        var email = TestAccounts.UniqueEmail();

        using var response = await client.RegisterAsync(email, TestAccounts.ValidPassword);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("oz_session=") && c.Contains("httponly"));
        var me = await client.GetMeAsync();
        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.False(me.GetProperty("hasAnimals").GetBoolean());
    }

    [Fact]
    public async Task Register_rejects_a_taken_email_in_any_letter_case()
    {
        using var client = factory.CreateClient();
        var email = TestAccounts.UniqueEmail();
        using var first = await client.RegisterAsync(email, TestAccounts.ValidPassword);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var sameCase = await factory.CreateClient().RegisterAsync(email, TestAccounts.ValidPassword);
        await sameCase.AssertProblemAsync(HttpStatusCode.Conflict, "email_taken");

        using var otherCase = await factory.CreateClient().RegisterAsync(email.ToUpperInvariant(), TestAccounts.ValidPassword);
        await otherCase.AssertProblemAsync(HttpStatusCode.Conflict, "email_taken");
    }

    [Fact]
    public async Task Register_requires_a_password_of_at_least_ten_characters()
    {
        using var client = factory.CreateClient();

        using var tooShort = await client.RegisterAsync(TestAccounts.UniqueEmail(), "abcdefghi");
        await tooShort.AssertProblemAsync(HttpStatusCode.BadRequest, "password_too_short");

        using var longEnough = await client.RegisterAsync(TestAccounts.UniqueEmail(), "abcdefghij");
        Assert.Equal(HttpStatusCode.Created, longEnough.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_an_invalid_email()
    {
        using var client = factory.CreateClient();

        using var response = await client.RegisterAsync("not-an-email", TestAccounts.ValidPassword);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid_email");
    }

    [Fact]
    public async Task Login_with_correct_credentials_sets_the_session_cookie()
    {
        var email = TestAccounts.UniqueEmail();
        using (var registering = factory.CreateClient())
        using (var registered = await registering.RegisterAsync(email, TestAccounts.ValidPassword))
        {
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        }

        using var client = factory.CreateClient();
        using var response = await client.LoginAsync(email, TestAccounts.ValidPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("oz_session="));
        var me = await client.GetMeAsync();
        Assert.Equal(email, me.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Login_with_a_wrong_password_is_rejected()
    {
        var (_, email) = await factory.CreateSignedInClientAsync();
        using var client = factory.CreateClient();

        using var response = await client.LoginAsync(email, "wrong-password-1");

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, "invalid_credentials");
    }

    [Fact]
    public async Task Login_with_an_unknown_email_is_rejected_as_invalid_credentials()
    {
        using var client = factory.CreateClient();

        using var response = await client.LoginAsync(TestAccounts.UniqueEmail(), TestAccounts.ValidPassword);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, "invalid_credentials");
    }

    [Fact]
    public async Task Login_locks_the_account_on_the_fifth_failed_attempt()
    {
        var (_, email) = await factory.CreateSignedInClientAsync();
        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            using var failed = await client.LoginAsync(email, "wrong-password-1");
            await failed.AssertProblemAsync(HttpStatusCode.Unauthorized, "invalid_credentials");
        }

        using var fifth = await client.LoginAsync(email, "wrong-password-1");
        await fifth.AssertProblemAsync(HttpStatusCode.Unauthorized, "locked_out");

        using var sixth = await client.LoginAsync(email, "wrong-password-1");
        await sixth.AssertProblemAsync(HttpStatusCode.Unauthorized, "locked_out");

        using var correct = await client.LoginAsync(email, TestAccounts.ValidPassword);
        await correct.AssertProblemAsync(HttpStatusCode.Unauthorized, "locked_out");
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            using var response = await client.PostAsync("/api/auth/logout", null);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            using var me = await client.GetAsync("/api/me");
            Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        }
    }

    [Fact]
    public async Task Logout_ends_the_session_even_if_a_copy_of_the_cookie_is_sent_again()
    {
        var email = TestAccounts.UniqueEmail();
        using var client = factory.CreateClient();
        using var registered = await client.RegisterAsync(email, TestAccounts.ValidPassword);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var sessionCookie = Assert.Single(registered.Headers.GetValues("Set-Cookie"), c => c.StartsWith("oz_session="))
            .Split(';')[0];

        using (var before = await SendWithCookieAsync(sessionCookie))
        {
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        }

        using var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // Simulates a proxy that dropped the expiring Set-Cookie: the browser would still send the old cookie.
        using var after = await SendWithCookieAsync(sessionCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Logout_ends_only_the_session_of_the_device_that_logged_out()
    {
        var (first, email) = await factory.CreateSignedInClientAsync();
        using (first)
        using (var second = factory.CreateClient())
        {
            using var login = await second.LoginAsync(email, TestAccounts.ValidPassword);
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

            using var logout = await first.PostAsync("/api/auth/logout", null);
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

            using var firstMe = await first.GetAsync("/api/me");
            Assert.Equal(HttpStatusCode.Unauthorized, firstMe.StatusCode);
            var me = await second.GetMeAsync();
            Assert.Equal(email, me.GetProperty("email").GetString());
        }
    }

    [Fact]
    public async Task Logout_without_a_session_succeeds()
    {
        using var client = factory.CreateClient();

        using var withoutCookie = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, withoutCookie.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("Cookie", "oz_session=not-a-valid-ticket");
        using var withInvalidCookie = await CreateClientWithoutCookies().SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, withInvalidCookie.StatusCode);
    }

    [Fact]
    public async Task Api_responses_are_not_stored()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            using var me = await client.GetAsync("/api/me");
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            Assert.True(me.Headers.CacheControl?.NoStore);
        }

        using var anonymous = await factory.CreateClient().GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.True(anonymous.Headers.CacheControl?.NoStore);
    }

    // A client without a cookie container, so a test controls exactly which cookie a request carries.
    private HttpClient CreateClientWithoutCookies() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private async Task<HttpResponseMessage> SendWithCookieAsync(string cookie)
    {
        using var client = CreateClientWithoutCookies();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request);
    }

    [Theory]
    [InlineData("/api/me")]
    [InlineData("/api/animals")]
    public async Task Requests_without_a_session_get_401_without_a_redirect(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }
}
