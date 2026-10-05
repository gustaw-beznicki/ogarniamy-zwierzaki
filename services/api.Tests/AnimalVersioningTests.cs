using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ogarniamy_zwierzaki_api.Tests;

// Activity, edit versions (If-Match), antiforgery protection and owner isolation of the animal routes.
public sealed class AnimalVersioningTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string Quoted(JsonElement animal) => $"\"{animal.GetProperty("version").GetGuid()}\"";

    private static Task<HttpResponseMessage> RenameAsync(HttpClient client, Guid id, object body, string? ifMatch) =>
        client.SendJsonAsync(HttpMethod.Put, $"/api/animals/{id}/name", body, ifMatch);

    private static Task<HttpResponseMessage> SetActivityAsync(HttpClient client, Guid id, object body, string? ifMatch) =>
        client.SendJsonAsync(HttpMethod.Put, $"/api/animals/{id}/activity", body, ifMatch);

    private static async Task<JsonElement> CreateAsync(HttpClient client, string name = "Czarek")
    {
        using var response = await client.CreateAnimalAsync(name);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync($"/api/animals/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    [Fact]
    public async Task A_new_animal_is_active_with_a_version_and_no_documents()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            var animal = await CreateAsync(client);

            Assert.True(animal.GetProperty("isActive").GetBoolean());
            Assert.NotEqual(Guid.Empty, animal.GetProperty("version").GetGuid());
            Assert.Equal(0, animal.GetProperty("storedDocumentCount").GetInt32());
        }
    }

    [Fact]
    public async Task Animal_mutations_require_an_antiforgery_token()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            var animal = await CreateAsync(client);
            var id = animal.GetProperty("id").GetGuid();

            using var create = await client.PostAsJsonAsync("/api/animals", new { name = "Burek" });
            await create.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid_antiforgery_token");

            using var rename = new HttpRequestMessage(HttpMethod.Put, $"/api/animals/{id}/name")
            {
                Content = JsonContent.Create(new { name = "Burek" }),
            };
            rename.Headers.TryAddWithoutValidation("If-Match", Quoted(animal));
            using var renamed = await client.SendAsync(rename);
            await renamed.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid_antiforgery_token");

            using var activity = new HttpRequestMessage(HttpMethod.Put, $"/api/animals/{id}/activity")
            {
                Content = JsonContent.Create(new { isActive = false }),
            };
            activity.Headers.TryAddWithoutValidation("If-Match", Quoted(animal));
            using var changed = await client.SendAsync(activity);
            await changed.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid_antiforgery_token");

            // Nothing was created or changed, and reads need no token.
            var current = await GetAsync(client, id);
            Assert.Equal("Czarek", current.GetProperty("name").GetString());
            Assert.True(current.GetProperty("isActive").GetBoolean());
            Assert.Single((await client.GetJsonAsync("/api/animals")).EnumerateArray());
        }
    }

    [Fact]
    public async Task Mutations_without_a_session_get_401()
    {
        using var client = factory.CreateClient();
        var id = Guid.NewGuid();

        using var create = await client.PostAsJsonAsync("/api/animals", new { name = "Burek" });
        using var rename = await client.PutAsJsonAsync($"/api/animals/{id}/name", new { name = "Burek" });
        using var activity = await client.PutAsJsonAsync($"/api/animals/{id}/activity", new { isActive = false });

        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, rename.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, activity.StatusCode);
    }

    [Fact]
    public async Task Rename_trims_applies_name_limits_and_changes_the_version()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            var animal = await CreateAsync(client);
            var id = animal.GetProperty("id").GetGuid();

            using (var empty = await RenameAsync(client, id, new { name = "  " }, Quoted(animal)))
            {
                await empty.AssertProblemAsync(HttpStatusCode.BadRequest, "name_required");
            }

            using (var tooLong = await RenameAsync(client, id, new { name = new string('a', 101) }, Quoted(animal)))
            {
                await tooLong.AssertProblemAsync(HttpStatusCode.BadRequest, "name_too_long");
            }

            using (var longest = await RenameAsync(client, id, new { name = new string('a', 100) }, Quoted(animal)))
            {
                Assert.Equal(HttpStatusCode.OK, longest.StatusCode);
                animal = await longest.ReadJsonAsync();
            }

            using var renamed = await RenameAsync(client, id, new { name = "  Burek  " }, Quoted(animal));
            Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
            var body = await renamed.ReadJsonAsync();
            Assert.Equal("Burek", body.GetProperty("name").GetString());
            Assert.True(body.GetProperty("isActive").GetBoolean());
            Assert.NotEqual(animal.GetProperty("version").GetGuid(), body.GetProperty("version").GetGuid());
            Assert.Equal(body.GetProperty("version").GetGuid(), (await GetAsync(client, id)).GetProperty("version").GetGuid());
        }
    }

    [Fact]
    public async Task Duplicate_names_are_allowed_on_create_and_rename()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            await CreateAsync(client, "Czarek");
            var second = await CreateAsync(client, "Burek");

            using var renamed = await RenameAsync(client, second.GetProperty("id").GetGuid(), new { name = "Czarek" }, Quoted(second));

            Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        }
    }

    [Fact]
    public async Task Activity_changes_keep_the_name_and_are_reversible()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            var animal = await CreateAsync(client);
            var id = animal.GetProperty("id").GetGuid();

            using var inactive = await SetActivityAsync(client, id, new { isActive = false }, Quoted(animal));
            Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);
            var afterInactive = await inactive.ReadJsonAsync();
            Assert.False(afterInactive.GetProperty("isActive").GetBoolean());
            Assert.Equal("Czarek", afterInactive.GetProperty("name").GetString());
            Assert.NotEqual(animal.GetProperty("version").GetGuid(), afterInactive.GetProperty("version").GetGuid());

            using var active = await SetActivityAsync(client, id, new { isActive = true }, Quoted(afterInactive));
            Assert.Equal(HttpStatusCode.OK, active.StatusCode);
            Assert.True((await active.ReadJsonAsync()).GetProperty("isActive").GetBoolean());
        }
    }

    [Fact]
    public async Task Lists_default_to_active_animals_and_can_include_inactive_ones_in_creation_order()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            var first = await CreateAsync(client, "A");
            var second = await CreateAsync(client, "B");
            var third = await CreateAsync(client, "C");
            using var inactive = await SetActivityAsync(client, second.GetProperty("id").GetGuid(), new { isActive = false }, Quoted(second));
            Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);

            static string[] Names(JsonElement list) =>
                list.EnumerateArray().Select(a => a.GetProperty("name").GetString() ?? string.Empty).ToArray();

            Assert.Equal(["A", "C"], Names(await client.GetJsonAsync("/api/animals")));
            Assert.Equal(["A", "C"], Names(await client.GetJsonAsync("/api/animals?includeInactive=false")));
            Assert.Equal(["A", "B", "C"], Names(await client.GetJsonAsync("/api/animals?includeInactive=true")));

            // The direct route returns either state, and onboarding still sees the owned animals.
            Assert.False((await GetAsync(client, second.GetProperty("id").GetGuid())).GetProperty("isActive").GetBoolean());
            Assert.True(first.GetProperty("isActive").GetBoolean() && third.GetProperty("isActive").GetBoolean());
        }
    }

    [Fact]
    public async Task Has_animals_stays_true_when_every_animal_is_inactive()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            var animal = await CreateAsync(client);
            using var inactive = await SetActivityAsync(client, animal.GetProperty("id").GetGuid(), new { isActive = false }, Quoted(animal));
            Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);

            Assert.Empty((await client.GetJsonAsync("/api/animals")).EnumerateArray());
            Assert.True((await client.GetMeAsync()).GetProperty("hasAnimals").GetBoolean());
        }
    }

    [Fact]
    public async Task Counts_include_stored_documents_only()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync("Czarek");
        var otherId = await client.CreateAnimalIdAsync("Burek");
        await client.CaptureAsync(animalId, TestOriginal.Png());
        await client.CaptureAsync(animalId, TestOriginal.Pdf());
        // An accepted manifest that was never completed is not counted.
        using (var pending = await client.PutManifestAsync(Guid.NewGuid(), DocumentApi.Manifest(animalId, TestOriginal.Png())))
        {
            Assert.Equal(HttpStatusCode.Created, pending.StatusCode);
        }

        var counts = (await client.GetJsonAsync("/api/animals")).EnumerateArray()
            .ToDictionary(a => a.GetProperty("id").GetGuid(), a => a.GetProperty("storedDocumentCount").GetInt32());

        Assert.Equal(2, counts[animalId]);
        Assert.Equal(0, counts[otherId]);
        Assert.Equal(2, (await GetAsync(client, animalId)).GetProperty("storedDocumentCount").GetInt32());
    }

    [Fact]
    public async Task Another_accounts_documents_are_not_counted()
    {
        using var owner = await factory.CreateCaptureClientAsync();
        using var other = await factory.CreateCaptureClientAsync();
        var ownerAnimal = await owner.CreateAnimalIdAsync();
        var otherAnimal = await other.CreateAnimalIdAsync();
        await owner.CaptureAsync(ownerAnimal, TestOriginal.Png());

        Assert.Equal(1, (await GetAsync(owner, ownerAnimal)).GetProperty("storedDocumentCount").GetInt32());
        Assert.Equal(0, (await GetAsync(other, otherAnimal)).GetProperty("storedDocumentCount").GetInt32());
    }

    [Fact]
    public async Task A_missing_if_match_header_gets_428()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            var id = (await CreateAsync(client)).GetProperty("id").GetGuid();

            using var rename = await RenameAsync(client, id, new { name = "Burek" }, null);
            using var activity = await SetActivityAsync(client, id, new { isActive = false }, null);

            await rename.AssertProblemAsync(HttpStatusCode.PreconditionRequired, "version_required");
            await activity.AssertProblemAsync(HttpStatusCode.PreconditionRequired, "version_required");
        }
    }

    [Theory]
    [InlineData("*")]
    [InlineData("not-a-version")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("\"not-a-guid\"")]
    [InlineData("W/\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("\"\"")]
    public async Task A_malformed_if_match_header_gets_400(string ifMatch)
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            var id = (await CreateAsync(client)).GetProperty("id").GetGuid();

            using var rename = await RenameAsync(client, id, new { name = "Burek" }, ifMatch);
            using var activity = await SetActivityAsync(client, id, new { isActive = false }, ifMatch);

            await rename.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid_version");
            await activity.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid_version");
        }
    }

    [Fact]
    public async Task A_malformed_activity_body_gets_400()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            var animal = await CreateAsync(client);
            var id = animal.GetProperty("id").GetGuid();

            foreach (var body in new object[] { new { }, new { isActive = "yes" }, new { isActive = (bool?)null } })
            {
                using var response = await SetActivityAsync(client, id, body, Quoted(animal));
                await response.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid_activity");
            }

            using var malformed = new HttpRequestMessage(HttpMethod.Put, $"/api/animals/{id}/activity")
            {
                Content = new StringContent("{", Encoding.UTF8, "application/json"),
            };
            malformed.Headers.Add(TestAccounts.AntiforgeryHeader, await client.GetAntiforgeryTokenAsync());
            malformed.Headers.TryAddWithoutValidation("If-Match", Quoted(animal));
            using var response2 = await client.SendAsync(malformed);
            await response2.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid_activity");
        }
    }

    [Fact]
    public async Task A_stale_version_gets_412_and_changes_nothing()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            var original = await CreateAsync(client);
            var id = original.GetProperty("id").GetGuid();
            using var renamed = await RenameAsync(client, id, new { name = "Burek" }, Quoted(original));
            Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

            using var staleRename = await RenameAsync(client, id, new { name = "Stale" }, Quoted(original));
            using var staleActivity = await SetActivityAsync(client, id, new { isActive = false }, Quoted(original));

            await staleRename.AssertProblemAsync(HttpStatusCode.PreconditionFailed, "animal_changed");
            await staleActivity.AssertProblemAsync(HttpStatusCode.PreconditionFailed, "animal_changed");
            var current = await GetAsync(client, id);
            Assert.Equal("Burek", current.GetProperty("name").GetString());
            Assert.True(current.GetProperty("isActive").GetBoolean());
        }
    }

    [Fact]
    public async Task A_rename_after_an_activity_change_with_the_old_version_is_stale()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            var original = await CreateAsync(client);
            var id = original.GetProperty("id").GetGuid();
            using var inactive = await SetActivityAsync(client, id, new { isActive = false }, Quoted(original));
            Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);

            using var stale = await RenameAsync(client, id, new { name = "Burek" }, Quoted(original));

            await stale.AssertProblemAsync(HttpStatusCode.PreconditionFailed, "animal_changed");
        }
    }

    [Fact]
    public async Task No_op_requests_keep_the_version_unless_the_version_is_stale()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            var original = await CreateAsync(client);
            var id = original.GetProperty("id").GetGuid();
            var version = original.GetProperty("version").GetGuid();

            using var sameName = await RenameAsync(client, id, new { name = " Czarek " }, Quoted(original));
            using var sameActivity = await SetActivityAsync(client, id, new { isActive = true }, Quoted(original));

            Assert.Equal(HttpStatusCode.OK, sameName.StatusCode);
            Assert.Equal(version, (await sameName.ReadJsonAsync()).GetProperty("version").GetGuid());
            Assert.Equal(HttpStatusCode.OK, sameActivity.StatusCode);
            Assert.Equal(version, (await sameActivity.ReadJsonAsync()).GetProperty("version").GetGuid());

            using var changed = await RenameAsync(client, id, new { name = "Burek" }, Quoted(original));
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

            // A no-op with an outdated version is still refused.
            using var staleNoOp = await RenameAsync(client, id, new { name = "Burek" }, Quoted(original));
            await staleNoOp.AssertProblemAsync(HttpStatusCode.PreconditionFailed, "animal_changed");
            using var staleActivityNoOp = await SetActivityAsync(client, id, new { isActive = true }, Quoted(original));
            await staleActivityNoOp.AssertProblemAsync(HttpStatusCode.PreconditionFailed, "animal_changed");
        }
    }

    [Fact]
    public async Task Competing_edits_with_one_version_apply_exactly_once()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            var original = await CreateAsync(client);
            var id = original.GetProperty("id").GetGuid();
            var ifMatch = Quoted(original);

            var responses = await Task.WhenAll(
                Enumerable.Range(0, 6).Select(async index =>
                {
                    using var response = index % 2 == 0
                        ? await RenameAsync(client, id, new { name = $"Name {index}" }, ifMatch)
                        : await SetActivityAsync(client, id, new { isActive = false }, ifMatch);
                    return response.StatusCode;
                }));

            Assert.Equal(1, responses.Count(status => status == HttpStatusCode.OK));
            Assert.Equal(5, responses.Count(status => status == HttpStatusCode.PreconditionFailed));
        }
    }

    [Fact]
    public async Task Foreign_and_missing_animals_look_the_same_and_stay_unchanged()
    {
        var (owner, _) = await factory.CreateSignedInClientAsync();
        var (other, _) = await factory.CreateSignedInClientAsync();
        using (owner)
        using (other)
        {
            var animal = await CreateAsync(owner);
            var id = animal.GetProperty("id").GetGuid();

            // Even with the right or a wrong version, a foreign account only ever sees 404.
            foreach (var ifMatch in new[] { Quoted(animal), $"\"{Guid.NewGuid()}\"" })
            {
                using var rename = await RenameAsync(other, id, new { name = "Hijacked" }, ifMatch);
                using var activity = await SetActivityAsync(other, id, new { isActive = false }, ifMatch);
                Assert.Equal(HttpStatusCode.NotFound, rename.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, activity.StatusCode);
            }

            using var missing = await RenameAsync(owner, Guid.NewGuid(), new { name = "Burek" }, Quoted(animal));
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

            var current = await GetAsync(owner, id);
            Assert.Equal("Czarek", current.GetProperty("name").GetString());
            Assert.True(current.GetProperty("isActive").GetBoolean());
            Assert.Equal(animal.GetProperty("version").GetGuid(), current.GetProperty("version").GetGuid());
            Assert.Empty((await other.GetJsonAsync("/api/animals?includeInactive=true")).EnumerateArray());
        }
    }

    [Fact]
    public async Task There_is_no_delete_route()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            var id = (await CreateAsync(client)).GetProperty("id").GetGuid();
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/animals/{id}");
            request.Headers.Add(TestAccounts.AntiforgeryHeader, await client.GetAntiforgeryTokenAsync());

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }
    }
}
