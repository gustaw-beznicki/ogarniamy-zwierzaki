using System.Net;
using System.Text.Json;

namespace ogarniamy_zwierzaki_api.Tests;

// Acceptance of the animal profile lifecycle across two synthetic accounts, used the way the browser uses the API: real
// session cookies from registration or sign-in, a real antiforgery token per mutation and If-Match edit versions.
// Covers create, list, detail, rename, activity, capture, history and original routes end to end.
public sealed class AnimalLifecycleTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string Quoted(JsonElement animal) => $"\"{animal.GetProperty("version").GetGuid()}\"";

    private static Task<HttpResponseMessage> RenameAsync(HttpClient client, Guid id, string name, string ifMatch) =>
        client.SendJsonAsync(HttpMethod.Put, $"/api/animals/{id}/name", new { name }, ifMatch);

    private static Task<HttpResponseMessage> SetActivityAsync(HttpClient client, Guid id, bool isActive, string ifMatch) =>
        client.SendJsonAsync(HttpMethod.Put, $"/api/animals/{id}/activity", new { isActive }, ifMatch);

    private static async Task<JsonElement> CreateAsync(HttpClient client, string name)
    {
        using var response = await client.CreateAnimalAsync(name);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    private static async Task<JsonElement> ExpectOkAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    // Captures one document with the given event date through manifest, upload and completion.
    private static async Task<JsonElement> CaptureOnAsync(
        HttpClient client, Guid animalId, string eventDate, params TestOriginal[] files)
    {
        var operationId = Guid.NewGuid();
        using (var created = await client.PutManifestAsync(
                   operationId, DocumentApi.Manifest(animalId, eventDate, DocumentApi.TimeZone, files)))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        for (var position = 0; position < files.Length; position++)
        {
            using var uploaded = await client.PutFileAsync(operationId, position, files[position]);
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        }

        return await ExpectOkAsync(client.CompleteAsync(operationId));
    }

    private static string[] Names(JsonElement list) =>
        list.EnumerateArray().Select(a => a.GetProperty("name").GetString() ?? string.Empty).ToArray();

    [Fact]
    public async Task Two_accounts_manage_their_own_animals_histories_and_originals_only()
    {
        using var owner = await factory.CreateCaptureClientAsync();
        using var other = await factory.CreateCaptureClientAsync();
        var czarek = await CreateAsync(owner, "Czarek");
        var burek = await CreateAsync(owner, "Burek");
        var reks = await CreateAsync(other, "Reks");
        var czarekId = czarek.GetProperty("id").GetGuid();
        var burekId = burek.GetProperty("id").GetGuid();
        var reksId = reks.GetProperty("id").GetGuid();

        // History is ordered by event date, newest first, independently of upload order.
        var olderDocument = await CaptureOnAsync(owner, czarekId, "2023-02-10", TestOriginal.Png());
        var newerPdf = TestOriginal.Pdf();
        var newerDocument = await CaptureOnAsync(owner, czarekId, "2024-08-20", newerPdf);
        var burekJpeg = TestOriginal.Jpeg();
        var burekDocument = await CaptureOnAsync(owner, burekId, "2024-01-05", burekJpeg);
        await CaptureOnAsync(other, reksId, "2024-03-03", TestOriginal.Png());

        var history = (await owner.GetJsonAsync($"/api/animals/{czarekId}/documents")).GetProperty("items");
        Assert.Equal(
            [newerDocument.GetProperty("id").GetGuid(), olderDocument.GetProperty("id").GetGuid()],
            history.EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).ToArray());
        Assert.Equal(["2024-08-20", "2023-02-10"], history.EnumerateArray().Select(d => d.GetProperty("eventDate").GetString() ?? string.Empty).ToArray());
        Assert.Equal([burekDocument.GetProperty("id").GetGuid()], await owner.ListDocumentIdsAsync(burekId));

        // Rename one profile and inactivate the other; both remain readable with their documents.
        var renamed = await ExpectOkAsync(RenameAsync(owner, czarekId, "Czarek II", Quoted(czarek)));
        var inactive = await ExpectOkAsync(SetActivityAsync(owner, burekId, false, Quoted(burek)));
        Assert.True(renamed.GetProperty("isActive").GetBoolean());
        Assert.False(inactive.GetProperty("isActive").GetBoolean());
        Assert.Equal("Burek", inactive.GetProperty("name").GetString());

        Assert.Equal(["Czarek II"], Names(await owner.GetJsonAsync("/api/animals")));
        var all = await owner.GetJsonAsync("/api/animals?includeInactive=true");
        Assert.Equal(["Czarek II", "Burek"], Names(all));
        Assert.Equal([2, 1], all.EnumerateArray().Select(a => a.GetProperty("storedDocumentCount").GetInt32()).ToArray());
        Assert.Equal(["Reks"], Names(await other.GetJsonAsync("/api/animals?includeInactive=true")));

        var newerDetails = await owner.GetJsonAsync($"/api/documents/{newerDocument.GetProperty("id").GetGuid()}");
        Assert.Equal("Czarek II", newerDetails.GetProperty("animalName").GetString());
        Assert.Equal(newerPdf.Bytes, await owner.GetOriginalBytesAsync(Assert.Single(newerDetails.OriginalUrls())));
        var burekOriginal = Assert.Single(burekDocument.OriginalUrls());
        Assert.Equal(burekJpeg.Bytes, await owner.GetOriginalBytesAsync(burekOriginal));

        // The other account cannot read, edit, reactivate or capture into either owner profile, active or inactive.
        foreach (var path in new[]
                 {
                     $"/api/animals/{czarekId}", $"/api/animals/{burekId}",
                     $"/api/animals/{czarekId}/documents", $"/api/animals/{burekId}/documents",
                     $"/api/documents/{newerDocument.GetProperty("id").GetGuid()}",
                     $"/api/documents/{burekDocument.GetProperty("id").GetGuid()}", burekOriginal,
                 })
        {
            using var response = await other.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        foreach (var (id, current) in new[] { (czarekId, renamed), (burekId, inactive) })
        {
            using var rename = await RenameAsync(other, id, "Hijacked", Quoted(current));
            using var activity = await SetActivityAsync(other, id, true, Quoted(current));
            using var manifest = await other.PutManifestAsync(Guid.NewGuid(), DocumentApi.Manifest(id, TestOriginal.Png()));
            Assert.Equal(HttpStatusCode.NotFound, rename.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, activity.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, manifest.StatusCode);
        }

        // Nothing changed for the owner, and the other account's defaults only offer its own animal.
        Assert.Equal(Quoted(renamed), Quoted(await owner.GetJsonAsync($"/api/animals/{czarekId}")));
        Assert.Equal(Quoted(inactive), Quoted(await owner.GetJsonAsync($"/api/animals/{burekId}")));
        Assert.Equal(2, (await owner.ListDocumentIdsAsync(czarekId)).Length);
        var otherDefaults = await other.GetJsonAsync("/api/capture-defaults");
        Assert.Equal([reksId], otherDefaults.GetProperty("animals").EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).ToArray());
    }

    [Fact]
    public async Task An_edit_from_another_device_makes_the_first_device_refresh_before_reapplying()
    {
        var (firstDevice, email) = await factory.CreateSignedInClientAsync();
        using var secondDevice = factory.CreateClient();
        using (firstDevice)
        {
            using (var login = await secondDevice.LoginAsync(email, TestAccounts.ValidPassword))
            {
                Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
            }

            var created = await CreateAsync(firstDevice, "Czarek");
            var id = created.GetProperty("id").GetGuid();
            // Both devices open the edit form with the same version.
            var firstView = await firstDevice.GetJsonAsync($"/api/animals/{id}");
            var secondView = await secondDevice.GetJsonAsync($"/api/animals/{id}");
            Assert.Equal(Quoted(firstView), Quoted(secondView));

            var saved = await ExpectOkAsync(RenameAsync(secondDevice, id, "Burek", Quoted(secondView)));

            // The first device's rename and activity change are both stale and change nothing.
            using (var staleRename = await RenameAsync(firstDevice, id, "Reks", Quoted(firstView)))
            {
                await staleRename.AssertProblemAsync(HttpStatusCode.PreconditionFailed, "animal_changed");
            }

            using (var staleActivity = await SetActivityAsync(firstDevice, id, false, Quoted(firstView)))
            {
                await staleActivity.AssertProblemAsync(HttpStatusCode.PreconditionFailed, "animal_changed");
            }

            var refreshed = await firstDevice.GetJsonAsync($"/api/animals/{id}");
            Assert.Equal(Quoted(saved), Quoted(refreshed));
            Assert.Equal("Burek", refreshed.GetProperty("name").GetString());
            Assert.True(refreshed.GetProperty("isActive").GetBoolean());

            // After an explicit refresh the owner reapplies the edit with the current version.
            var reapplied = await ExpectOkAsync(RenameAsync(firstDevice, id, "Reks", Quoted(refreshed)));
            Assert.Equal("Reks", reapplied.GetProperty("name").GetString());
            Assert.True(reapplied.GetProperty("isActive").GetBoolean());

            // The second device now holds the stale version.
            using var nowStale = await SetActivityAsync(secondDevice, id, false, Quoted(saved));
            await nowStale.AssertProblemAsync(HttpStatusCode.PreconditionFailed, "animal_changed");
            Assert.True((await secondDevice.GetJsonAsync($"/api/animals/{id}")).GetProperty("isActive").GetBoolean());
        }
    }

    [Fact]
    public async Task With_every_animal_inactive_archives_stay_open_and_an_accepted_upload_still_finishes()
    {
        using var owner = await factory.CreateCaptureClientAsync();
        using var other = await factory.CreateCaptureClientAsync();
        var animal = await CreateAsync(owner, "Czarek");
        var animalId = animal.GetProperty("id").GetGuid();
        var archivedPdf = TestOriginal.Pdf();
        var archived = await CaptureOnAsync(owner, animalId, "2023-06-01", archivedPdf);

        // Accept a two-image upload, then inactivate the only animal before its files arrive.
        var first = TestOriginal.Png();
        var second = TestOriginal.Jpeg();
        var operationId = Guid.NewGuid();
        using (var accepted = await owner.PutManifestAsync(operationId, DocumentApi.Manifest(animalId, first, second)))
        {
            Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        }

        var inactive = await ExpectOkAsync(SetActivityAsync(owner, animalId, false, Quoted(animal)));

        // No fresh capture is possible, but the account is not sent back to onboarding.
        using (var fresh = await owner.PutManifestAsync(Guid.NewGuid(), DocumentApi.Manifest(animalId, TestOriginal.Png(900))))
        {
            await fresh.AssertProblemAsync(HttpStatusCode.Conflict, "animal_inactive");
        }

        var defaults = await owner.GetJsonAsync("/api/capture-defaults");
        Assert.Empty(defaults.GetProperty("animals").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, defaults.GetProperty("defaultAnimalId").ValueKind);
        Assert.True((await owner.GetMeAsync()).GetProperty("hasAnimals").GetBoolean());
        Assert.Empty((await owner.GetJsonAsync("/api/animals")).EnumerateArray());

        // The accepted upload still uploads and completes.
        using (var uploadFirst = await owner.PutFileAsync(operationId, 0, first))
        {
            Assert.Equal(HttpStatusCode.OK, uploadFirst.StatusCode);
        }

        using (var uploadSecond = await owner.PutFileAsync(operationId, 1, second))
        {
            Assert.Equal(HttpStatusCode.OK, uploadSecond.StatusCode);
        }

        var completed = await ExpectOkAsync(owner.CompleteAsync(operationId));
        Assert.Equal(operationId, completed.GetProperty("id").GetGuid());

        // The inactive profile, its history and every original remain available to the owner only.
        var all = await owner.GetJsonAsync("/api/animals?includeInactive=true");
        var listed = Assert.Single(all.EnumerateArray());
        Assert.False(listed.GetProperty("isActive").GetBoolean());
        Assert.Equal(2, listed.GetProperty("storedDocumentCount").GetInt32());
        Assert.Equal(Quoted(inactive), Quoted(listed));
        Assert.Equal(
            [operationId, archived.GetProperty("id").GetGuid()], await owner.ListDocumentIdsAsync(animalId));

        var details = await owner.GetJsonAsync($"/api/documents/{operationId}");
        var urls = details.OriginalUrls();
        Assert.Equal(first.Bytes, await owner.GetOriginalBytesAsync(urls[0]));
        Assert.Equal(second.Bytes, await owner.GetOriginalBytesAsync(urls[1]));
        Assert.Equal(archivedPdf.Bytes, await owner.GetOriginalBytesAsync(Assert.Single(archived.OriginalUrls())));
        foreach (var path in urls.Append($"/api/documents/{operationId}").Append($"/api/animals/{animalId}/documents"))
        {
            using var response = await other.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // Reactivating the animal makes it the capture default again and accepts a fresh capture.
        await ExpectOkAsync(SetActivityAsync(owner, animalId, true, Quoted(inactive)));
        Assert.Equal(animalId, (await owner.GetJsonAsync("/api/capture-defaults")).GetProperty("defaultAnimalId").GetGuid());
        await CaptureOnAsync(owner, animalId, "2024-02-02", TestOriginal.Png(900));
        Assert.Equal(3, (await owner.ListDocumentIdsAsync(animalId)).Length);
    }
}
