using System.Net;

namespace ogarniamy_zwierzaki_api.Tests;

// FR-002: an account never sees another account's animals.
public sealed class IsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Accounts_see_only_their_own_animals()
    {
        var (clientA, _) = await factory.CreateSignedInClientAsync();
        var (clientB, _) = await factory.CreateSignedInClientAsync();
        using (clientA)
        using (clientB)
        {
            var idA = await CreateAsync(clientA, "Czarek");
            var idB = await CreateAsync(clientB, "Burek");

            Assert.Equal([idA], await ListIdsAsync(clientA));
            Assert.Equal([idB], await ListIdsAsync(clientB));

            using var foreign = await clientB.GetAsync($"/api/animals/{idA}");
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

            using var own = await clientA.GetAsync($"/api/animals/{idA}");
            Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        }
    }

    [Fact]
    public async Task Another_accounts_animals_do_not_count_towards_has_animals()
    {
        var (clientA, _) = await factory.CreateSignedInClientAsync();
        var (clientB, _) = await factory.CreateSignedInClientAsync();
        using (clientA)
        using (clientB)
        {
            await CreateAsync(clientA, "Czarek");
            await CreateAsync(clientA, "Burek");

            Assert.True((await clientA.GetMeAsync()).GetProperty("hasAnimals").GetBoolean());
            Assert.False((await clientB.GetMeAsync()).GetProperty("hasAnimals").GetBoolean());
            Assert.Empty(await ListIdsAsync(clientB));
        }
    }

    private static async Task<Guid> CreateAsync(HttpClient client, string name)
    {
        using var response = await client.CreateAnimalAsync(name);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid[]> ListIdsAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/animals");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadJsonAsync()).EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).ToArray();
    }
}
