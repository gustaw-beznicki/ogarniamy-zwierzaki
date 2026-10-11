using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ogarniamy_zwierzaki_api.Animals;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.Documents;

namespace ogarniamy_zwierzaki_api.Tests;

// Inactive animals take no fresh capture, while accepted operations and stored originals stay usable. The two race
// orders between a manifest insert and an inactivation are driven with real PostgreSQL row locks.
public sealed class AnimalCaptureEligibilityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateOnly EventDate = new(2026, 9, 1);

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task An_insert_that_locks_the_animal_first_is_accepted_and_the_inactivation_waits_for_it()
    {
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var gate = new DocumentInsertGate();
        await using var gated = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(gate))));
        var manifest = DocumentTestData.NewManifest(animalId, EventDate, DocumentFile.PngContentType);

        // The insert holds the animal row lock and is parked just before writing the document.
        var insert = Task.Run(async () =>
        {
            await using var scope = gated.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<OwnedDocuments>().AddUploadAsync(userId, manifest);
        });
        await gate.Reached.WaitAsync(WaitLimit);

        var current = await FindAnimalAsync(userId, animalId);
        var inactivation = Task.Run(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<OwnedAnimals>()
                .SetActivityAsync(userId, animalId, current.Version, false);
        });
        await WaitForBlockedAnimalLockAsync();
        Assert.False(inactivation.IsCompleted);

        gate.Release();
        Assert.Equal(UploadAcceptance.Accepted, await insert.WaitAsync(WaitLimit));
        Assert.Equal(AnimalMutationOutcome.Updated, (await inactivation.WaitAsync(WaitLimit)).Outcome);

        // The accepted operation survives the inactivation and can still finish.
        Assert.False((await FindAnimalAsync(userId, animalId)).IsActive);
        await factory.RecordAllReceiptsAsync(userId, manifest.Id);
        var stored = await factory.CompleteAsync(userId, manifest.Id);
        Assert.NotNull(stored);
        Assert.Equal(animalId, stored.AnimalId);
        var defaults = await DefaultsAsync(userId);
        Assert.Empty(defaults.Animals);
        Assert.Null(defaults.DefaultAnimalId);
    }

    [Fact]
    public async Task An_inactivation_that_locks_the_animal_first_rejects_the_fresh_manifest()
    {
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var manifest = DocumentTestData.NewManifest(animalId, EventDate, DocumentFile.PngContentType);

        // The inactivation holds the animal row lock in an open transaction.
        var dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var lockRow = new NpgsqlCommand("SELECT 1 FROM animals WHERE id = @id FOR UPDATE", connection, transaction))
        {
            lockRow.Parameters.AddWithValue("id", animalId);
            await lockRow.ExecuteScalarAsync();
        }

        var insert = Task.Run(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<OwnedDocuments>().AddUploadAsync(userId, manifest);
        });
        await WaitForBlockedAnimalLockAsync();
        Assert.False(insert.IsCompleted);

        await using (var inactivate = new NpgsqlCommand(
            "UPDATE animals SET is_active = false, version = gen_random_uuid() WHERE id = @id", connection, transaction))
        {
            inactivate.Parameters.AddWithValue("id", animalId);
            await inactivate.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();

        Assert.Equal(UploadAcceptance.AnimalInactive, await insert.WaitAsync(WaitLimit));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<OwnedDocuments>().FindUploadAsync(userId, manifest.Id));
    }

    [Fact]
    public async Task A_fresh_manifest_for_an_inactive_animal_is_refused_and_a_foreign_one_stays_not_found()
    {
        using var client = await factory.CreateCaptureClientAsync();
        using var other = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var foreignId = await other.CreateAnimalIdAsync("Reks");
        await SetActiveAsync(client, animalId, false);

        using var refused = await client.PutManifestAsync(
            Guid.NewGuid(), DocumentApi.Manifest(animalId, TestOriginal.Png()));
        await refused.AssertProblemAsync(HttpStatusCode.Conflict, "animal_inactive");
        using var foreign = await client.PutManifestAsync(
            Guid.NewGuid(), DocumentApi.Manifest(foreignId, TestOriginal.Png()));
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var missing = await client.PutManifestAsync(
            Guid.NewGuid(), DocumentApi.Manifest(Guid.NewGuid(), TestOriginal.Png()));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        await SetActiveAsync(client, animalId, true);
        using var accepted = await client.PutManifestAsync(
            Guid.NewGuid(), DocumentApi.Manifest(animalId, TestOriginal.Png()));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task An_accepted_manifest_is_retryable_uploadable_and_completable_after_the_animal_is_inactivated()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var png = TestOriginal.Png();
        var operationId = Guid.NewGuid();
        using (var created = await client.PutManifestAsync(operationId, DocumentApi.Manifest(animalId, png)))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        await SetActiveAsync(client, animalId, false);

        using (var retried = await client.PutManifestAsync(operationId, DocumentApi.Manifest(animalId, png)))
        {
            Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        }

        using (var changed = await client.PutManifestAsync(operationId, DocumentApi.Manifest(animalId, TestOriginal.Png(2_000))))
        {
            await changed.AssertProblemAsync(HttpStatusCode.Conflict, "upload_conflict");
        }

        using (var uploaded = await client.PutFileAsync(operationId, 0, png))
        {
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        }

        using (var completed = await client.CompleteAsync(operationId))
        {
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        }

        // Completion wrote the inactive animal as the last capture, which defaults ignore.
        var defaults = await client.GetJsonAsync("/api/capture-defaults");
        Assert.Empty(defaults.GetProperty("animals").EnumerateArray());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, defaults.GetProperty("defaultAnimalId").ValueKind);
        Assert.True((await client.GetMeAsync()).GetProperty("hasAnimals").GetBoolean());
    }

    [Fact]
    public async Task Defaults_fall_back_to_the_earliest_active_animal_and_return_to_a_reactivated_preference()
    {
        var userId = await factory.CreateUserAsync();
        var first = await factory.AddAnimalAsync(userId, "Czarek");
        var second = await factory.AddAnimalAsync(userId, "Burek");
        var third = await factory.AddAnimalAsync(userId, "Reks");
        await factory.AddStoredAsync(userId, third, EventDate);
        Assert.Equal(third, (await DefaultsAsync(userId)).DefaultAnimalId);

        // The saved animal is inactive: the earliest active animal, then nothing at all.
        await SetActivityAsync(userId, third, false);
        var fallback = await DefaultsAsync(userId);
        Assert.Equal([first, second], fallback.Animals.Select(a => a.Id));
        Assert.Equal(first, fallback.DefaultAnimalId);

        await SetActivityAsync(userId, first, false);
        var onlySecond = await DefaultsAsync(userId);
        Assert.Equal([second], onlySecond.Animals.Select(a => a.Id));
        Assert.Equal(second, onlySecond.DefaultAnimalId);

        await SetActivityAsync(userId, second, false);
        var none = await DefaultsAsync(userId);
        Assert.Empty(none.Animals);
        Assert.Null(none.DefaultAnimalId);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // Onboarding counts every owned animal, active or not.
            Assert.Equal(3, await scope.ServiceProvider.GetRequiredService<OwnedAnimals>().CountAsync(userId));
        }

        // Reactivating the saved animal makes it the default again.
        await SetActivityAsync(userId, third, true);
        Assert.Equal(third, (await DefaultsAsync(userId)).DefaultAnimalId);
    }

    [Fact]
    public async Task Pending_documents_stay_invisible_and_inactive_originals_stay_byte_identical()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var png = TestOriginal.Png();
        var stored = await client.CaptureAsync(animalId, png);
        var storedId = stored.GetProperty("id").GetGuid();
        var pendingId = Guid.NewGuid();
        using (var created = await client.PutManifestAsync(pendingId, DocumentApi.Manifest(animalId, TestOriginal.Pdf())))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        await SetActiveAsync(client, animalId, false);

        Assert.Equal([storedId], await client.ListDocumentIdsAsync(animalId));
        using (var pending = await client.GetAsync($"/api/documents/{pendingId}"))
        {
            Assert.Equal(HttpStatusCode.NotFound, pending.StatusCode);
        }

        var details = await client.GetJsonAsync($"/api/documents/{storedId}");
        Assert.Equal(png.Bytes, await client.GetOriginalBytesAsync(Assert.Single(details.OriginalUrls())));
    }

    private async Task<OwnedAnimal> FindAnimalAsync(string userId, Guid animalId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<OwnedAnimals>().FindAsync(userId, animalId))!;
    }

    private async Task SetActivityAsync(string userId, Guid animalId, bool isActive)
    {
        var current = await FindAnimalAsync(userId, animalId);
        await using var scope = factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<OwnedAnimals>()
            .SetActivityAsync(userId, animalId, current.Version, isActive);
        Assert.Equal(AnimalMutationOutcome.Updated, result.Outcome);
    }

    private static async Task SetActiveAsync(HttpClient client, Guid animalId, bool isActive)
    {
        var animal = await client.GetJsonAsync($"/api/animals/{animalId}");
        using var response = await client.SendJsonAsync(
            HttpMethod.Put, $"/api/animals/{animalId}/activity", new { isActive },
            $"\"{animal.GetProperty("version").GetGuid()}\"");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<CaptureDefaults> DefaultsAsync(string userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OwnedDocuments>().GetCaptureDefaultsAsync(userId);
    }

    // Polls pg_stat_activity until a session is waiting on a row lock taken by an animal lock query, so the test
    // knows the second transaction is really queued behind the first one.
    private async Task WaitForBlockedAnimalLockAsync()
    {
        var dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
        using var timeout = new CancellationTokenSource(WaitLimit);
        while (true)
        {
            await using var command = dataSource.CreateCommand("""
                SELECT count(*) FROM pg_stat_activity
                WHERE wait_event_type = 'Lock' AND query LIKE '%FROM animals%FOR UPDATE%'
                """);
            if ((long)(await command.ExecuteScalarAsync(timeout.Token))! > 0)
            {
                return;
            }

            await Task.Delay(50, timeout.Token);
        }
    }
}
