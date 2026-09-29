using System.Net;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ogarniamy_zwierzaki_api.Auth;
using ogarniamy_zwierzaki_api.Data;

namespace ogarniamy_zwierzaki_api.Tests;

public sealed class AnimalTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Create_trims_the_name_and_returns_the_animal_with_its_location()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            using var response = await client.CreateAnimalAsync("  Czarek  ");

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.ReadJsonAsync();
            var id = body.GetProperty("id").GetGuid();
            Assert.Equal("Czarek", body.GetProperty("name").GetString());
            Assert.Equal($"/api/animals/{id}", response.Headers.Location?.OriginalString);

            using var fetched = await client.GetAsync($"/api/animals/{id}");
            Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
            Assert.Equal("Czarek", (await fetched.ReadJsonAsync()).GetProperty("name").GetString());
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Create_requires_a_name(string? name)
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            using var response = await client.CreateAnimalAsync(name);

            await response.AssertProblemAsync(HttpStatusCode.BadRequest, "name_required");
        }
    }

    [Fact]
    public async Task Create_accepts_100_characters_and_rejects_101()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            using var tooLong = await client.CreateAnimalAsync(new string('a', 101));
            await tooLong.AssertProblemAsync(HttpStatusCode.BadRequest, "name_too_long");

            using var longest = await client.CreateAnimalAsync(new string('a', 100));
            Assert.Equal(HttpStatusCode.Created, longest.StatusCode);
        }
    }

    [Fact]
    public async Task Create_accepts_json_bodies_only()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            using var content = new StringContent("name=Czarek", Encoding.UTF8, "application/x-www-form-urlencoded");

            using var response = await client.PostAsync("/api/animals", content);

            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        }
    }

    [Fact]
    public async Task Creating_an_animal_makes_the_user_its_owner()
    {
        var (client, email) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            using var response = await client.CreateAnimalAsync("Czarek");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var id = (await response.ReadJsonAsync()).GetProperty("id").GetGuid();

            Assert.True((await client.GetMeAsync()).GetProperty("hasAnimals").GetBoolean());

            await using var scope = factory.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await users.FindByEmailAsync(email);
            Assert.NotNull(user);
            var roles = await db.Database
                .SqlQuery<string>($"SELECT role AS \"Value\" FROM animal_members WHERE animal_id = {id} AND user_id = {user.Id}")
                .ToListAsync();
            Assert.Equal(["owner"], roles);
        }
    }

    [Fact]
    public async Task List_returns_the_users_animals_in_creation_order_and_allows_duplicate_names()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            foreach (var name in new[] { "Czarek", "Burek", "Czarek" })
            {
                using var created = await client.CreateAnimalAsync(name);
                Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            }

            using var response = await client.GetAsync("/api/animals");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var names = (await response.ReadJsonAsync()).EnumerateArray()
                .Select(a => a.GetProperty("name").GetString() ?? string.Empty)
                .ToArray();
            Assert.Equal(["Czarek", "Burek", "Czarek"], names);
        }
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_animal()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            using var response = await client.GetAsync($"/api/animals/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
