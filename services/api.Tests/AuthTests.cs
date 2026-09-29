using System.Net;

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
