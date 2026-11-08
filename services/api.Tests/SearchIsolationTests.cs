using System.Net;
using System.Net.Http.Json;

namespace ogarniamy_zwierzaki_api.Tests;

// FR-002 for search: no session gets 401 without a redirect, a search needs a valid antiforgery token, and a search
// only ever returns the caller's own documents, with originals that no other account can open. Every assertion about
// "only their own" would turn red if the owner predicate of the search query were removed. Inactive animals' documents
// are still found. Synthetic text only.
public sealed class SearchIsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private const string SharedToken = "qzx7velvet";

    private SearchTestHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = new SearchTestHost(factory);
        await _host.ParkAllJobsAsync();
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task A_search_without_a_session_gets_401_and_reaches_no_provider()
    {
        using var owner = await _host.SignInAsync();
        var animalId = await owner.Client.CreateAnimalIdAsync("Czarek");
        await _host.CaptureAndReadAsync(owner, animalId, $"Synthetic {SharedToken} note");
        await _host.EmbedAllAsync();
        var callsBefore = _host.Provider.Calls.Count;
        // No redirects are followed, so a redirect to a sign-in page would fail the assertions.
        using var anonymous = _host.CreateAnonymousClient();
        anonymous.DefaultRequestHeaders.Add(TestAccounts.AntiforgeryHeader, await owner.Client.GetAntiforgeryTokenAsync());

        using var response = await SearchTestHost.SearchAsync(anonymous, SharedToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal(callsBefore, _host.Provider.Calls.Count);
    }

    [Fact]
    public async Task A_search_without_a_valid_antiforgery_token_is_refused_before_anything_runs()
    {
        using var other = await _host.SignInAsync();
        using var account = await _host.SignInAsync();
        var animalId = await account.Client.CreateAnimalIdAsync("Czarek");
        await _host.CaptureAndReadAsync(account, animalId, $"Synthetic {SharedToken} note");
        await _host.EmbedAllAsync();
        // From here the account sends no token by default; each request below chooses its own.
        account.Client.DefaultRequestHeaders.Remove(TestAccounts.AntiforgeryHeader);
        var callsBefore = _host.Provider.Calls.Count;
        var otherAccountsToken = await other.Client.GetAntiforgeryTokenAsync();

        foreach (var token in new string?[] { null, "forged", otherAccountsToken })
        {
            await AssertRejectedAsync(account.Client, token);
        }

        var validToken = await account.Client.GetAntiforgeryTokenAsync();
        foreach (var token in new string?[] { null, "", "forged", otherAccountsToken })
        {
            await AssertRejectedAsync(account.Client, token);
        }

        Assert.Equal(callsBefore, _host.Provider.Calls.Count);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/search")
        {
            Content = JsonContent.Create(new { query = SharedToken }),
        };
        request.Headers.Add(TestAccounts.AntiforgeryHeader, validToken);
        using var accepted = await account.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task Two_accounts_with_the_same_words_each_find_only_their_own_documents()
    {
        using var first = await _host.SignInAsync();
        using var second = await _host.SignInAsync();
        var firstAnimal = await first.Client.CreateAnimalIdAsync("Czarek");
        var secondAnimal = await second.Client.CreateAnimalIdAsync("Burek");
        var firstDocument = await _host.CaptureAndReadAsync(first, firstAnimal, $"Synthetic {SharedToken} firstowner note");
        var secondDocument = await _host.CaptureAndReadAsync(second, secondAnimal, $"Synthetic {SharedToken} secondowner note");
        await _host.EmbedAllAsync();

        var firstBody = await SearchTestHost.SearchOkAsync(first.Client, $"{SharedToken} note");
        var secondBody = await SearchTestHost.SearchOkAsync(second.Client, $"{SharedToken} note");

        Assert.Equal([firstDocument], SearchTestHost.DocumentIds(firstBody));
        Assert.Equal([secondDocument], SearchTestHost.DocumentIds(secondBody));
        Assert.Equal("Synthetic qzx7velvet firstowner note", Fragment(firstBody));
        Assert.Equal("Synthetic qzx7velvet secondowner note", Fragment(secondBody));
        Assert.Equal(firstAnimal, Assert.Single(firstBody.GetProperty("items").EnumerateArray()).GetProperty("animalId").GetGuid());
        Assert.Equal(secondAnimal, Assert.Single(secondBody.GetProperty("items").EnumerateArray()).GetProperty("animalId").GetGuid());

        // Words that exist only in the other account's text, and the other account's animal name, find nothing.
        Assert.Empty((await SearchTestHost.SearchOkAsync(first.Client, "secondowner")).GetProperty("items").EnumerateArray());
        Assert.Empty((await SearchTestHost.SearchOkAsync(first.Client, "Burek")).GetProperty("items").EnumerateArray());
        Assert.Empty((await SearchTestHost.SearchOkAsync(second.Client, "firstowner")).GetProperty("items").EnumerateArray());
        Assert.Single((await SearchTestHost.SearchOkAsync(second.Client, "Burek")).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task The_original_of_a_result_opens_for_its_owner_and_is_404_for_another_account()
    {
        using var owner = await _host.SignInAsync();
        using var other = await _host.SignInAsync();
        var animalId = await owner.Client.CreateAnimalIdAsync("Czarek");
        await other.Client.CreateAnimalIdAsync("Burek");
        await _host.CaptureAndReadAsync(owner, animalId, $"Synthetic {SharedToken} ownersnote");
        await _host.EmbedAllAsync();
        var item = Assert.Single(
            (await SearchTestHost.SearchOkAsync(owner.Client, $"{SharedToken} ownersnote")).GetProperty("items").EnumerateArray());
        var originalUrl = item.GetProperty("originalUrl").GetString()!;

        using (var asOwner = await owner.Client.GetAsync(originalUrl))
        {
            Assert.Equal(HttpStatusCode.OK, asOwner.StatusCode);
        }

        foreach (var url in new[] { originalUrl, $"{originalUrl}?download=true" })
        {
            using var asOther = await other.Client.GetAsync(url);
            Assert.Equal(HttpStatusCode.NotFound, asOther.StatusCode);
            Assert.Empty(await asOther.Content.ReadAsByteArrayAsync());
        }

        using var anonymous = _host.CreateAnonymousClient();
        using var asAnonymous = await anonymous.GetAsync(originalUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, asAnonymous.StatusCode);
    }

    [Fact]
    public async Task The_documents_of_an_inactive_animal_are_found_and_the_other_accounts_still_are_not()
    {
        using var owner = await _host.SignInAsync();
        using var other = await _host.SignInAsync();
        var activeAnimal = await owner.Client.CreateAnimalIdAsync("Czarek");
        var retiredAnimal = await owner.Client.CreateAnimalIdAsync("Reksio");
        var otherAnimal = await other.Client.CreateAnimalIdAsync("Burek");
        var activeDocument = await _host.CaptureAndReadAsync(owner, activeAnimal, $"Synthetic {SharedToken} active note");
        var retiredDocument = await _host.CaptureAndReadAsync(owner, retiredAnimal, $"Synthetic {SharedToken} retired note");
        var otherRetiredDocument = await _host.CaptureAndReadAsync(other, otherAnimal, $"Synthetic {SharedToken} foreign retired note");
        await _host.EmbedAllAsync();
        await _host.DeactivateAnimalAsync(owner.UserId, retiredAnimal);
        await _host.DeactivateAnimalAsync(other.UserId, otherAnimal);

        var ownerBody = await SearchTestHost.SearchOkAsync(owner.Client, $"{SharedToken} note");

        Assert.Equal(
            new[] { activeDocument, retiredDocument }.Order(),
            SearchTestHost.DocumentIds(ownerBody).Order());
        Assert.DoesNotContain(otherRetiredDocument, SearchTestHost.DocumentIds(ownerBody));
        var retired = ownerBody.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("documentId").GetGuid() == retiredDocument);
        Assert.Equal(retiredAnimal, retired.GetProperty("animalId").GetGuid());
        Assert.Equal("Reksio", retired.GetProperty("animalName").GetString());
        Assert.Equal([otherRetiredDocument], SearchTestHost.DocumentIds(await SearchTestHost.SearchOkAsync(other.Client, $"{SharedToken} note")));
    }

    private static string? Fragment(System.Text.Json.JsonElement body) =>
        Assert.Single(body.GetProperty("items").EnumerateArray()).GetProperty("fragment").GetString();

    private static async Task AssertRejectedAsync(HttpClient client, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/search")
        {
            Content = JsonContent.Create(new { query = SharedToken }),
        };
        if (token is not null)
        {
            request.Headers.Add(TestAccounts.AntiforgeryHeader, token);
        }

        using var response = await client.SendAsync(request);
        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid_antiforgery_token");
    }
}
