using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.DocumentReading;

namespace ogarniamy_zwierzaki_api.Tests;

// The owner's reading status and Retry routes: owner isolation (including inactive animals), antiforgery, coalescing of
// repeated retries, the durable hourly limit and the unchanged completion/detail responses.
public sealed class DocumentReadingStatusTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] StatusFields = ["state", "failureCode", "canRetry", "retryAfter", "completedAt"];

    [Fact]
    public async Task A_completed_capture_reports_queued_reading_without_changing_completion_or_detail_responses()
    {
        using var owner = await factory.CreateCaptureClientAsync();
        var animalId = await owner.CreateAnimalIdAsync();
        var stored = await owner.CaptureAsync(animalId, TestOriginal.Png());
        var documentId = stored.GetProperty("id").GetGuid();

        // Completion and detail stay the stable original DTO; reading status is fetched separately.
        string[] documentFields = ["id", "animalId", "animalName", "eventDate", "uploadedAt", "files"];
        Assert.Equal(documentFields, FieldsOf(stored));
        var detail = await owner.GetJsonAsync($"/api/documents/{documentId}");
        Assert.Equal(documentFields, FieldsOf(detail));
        Assert.Equal(stored.GetRawText(), detail.GetRawText());

        var status = await owner.GetJsonAsync(StatusPath(documentId));
        Assert.Equal(StatusFields, FieldsOf(status));
        Assert.Equal("queued", status.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("failureCode").ValueKind);
        Assert.False(status.GetProperty("canRetry").GetBoolean());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("retryAfter").ValueKind);
        Assert.Equal(JsonValueKind.Null, status.GetProperty("completedAt").ValueKind);
    }

    [Fact]
    public async Task A_failed_job_reports_only_its_sanitized_code_and_can_be_retried()
    {
        using var owner = await factory.CreateCaptureClientAsync();
        var documentId = await StoredDocumentAsync(owner);
        await FailAsync(documentId);

        var status = await owner.GetJsonAsync(StatusPath(documentId));

        Assert.Equal(StatusFields, FieldsOf(status));
        Assert.Equal("failed", status.GetProperty("state").GetString());
        Assert.Equal("provider_unavailable", status.GetProperty("failureCode").GetString());
        Assert.True(status.GetProperty("canRetry").GetBoolean());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("retryAfter").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, status.GetProperty("completedAt").ValueKind);
    }

    [Fact]
    public async Task Anonymous_requests_get_401_and_other_accounts_get_404_without_changing_the_job()
    {
        using var owner = await factory.CreateCaptureClientAsync();
        using var other = await factory.CreateCaptureClientAsync();
        await other.CreateAnimalIdAsync("Burek");
        var documentId = await StoredDocumentAsync(owner);
        await FailAsync(documentId);
        var before = await JobAsync(documentId);

        using var anonymous = TestAccounts.CreateClient(factory);
        anonymous.DefaultRequestHeaders.Add(TestAccounts.AntiforgeryHeader, await owner.GetAntiforgeryTokenAsync());
        using (var response = await anonymous.GetAsync(StatusPath(documentId)))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Null(response.Headers.Location);
        }

        using (var response = await anonymous.PostAsync(RetryPath(documentId), null))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using (var response = await other.GetAsync(StatusPath(documentId)))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using (var response = await other.PostAsync(RetryPath(documentId), null))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // A missing document looks the same as a foreign one.
        using (var response = await owner.GetAsync(StatusPath(Guid.NewGuid())))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using (var response = await owner.PostAsync(RetryPath(Guid.NewGuid()), null))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        var after = await JobAsync(documentId);
        Assert.Equal(DocumentReadingState.Failed, after.State);
        Assert.Equal(before.RunGeneration, after.RunGeneration);
        Assert.Equal(0, after.OwnerRetryCount);
    }

    [Fact]
    public async Task An_uploading_document_has_no_reading_status()
    {
        using var owner = await factory.CreateCaptureClientAsync();
        var animalId = await owner.CreateAnimalIdAsync();
        var operationId = Guid.NewGuid();
        using (var created = await owner.PutManifestAsync(operationId, DocumentApi.Manifest(animalId, TestOriginal.Png())))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        using (var response = await owner.GetAsync(StatusPath(operationId)))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using (var response = await owner.PostAsync(RetryPath(operationId), null))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task Retry_without_a_valid_antiforgery_token_is_rejected_and_changes_nothing()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using var owner = client;
        var animalId = await WithTokenAsync(owner, c => c.CreateAnimalIdAsync());
        var stored = await WithTokenAsync(owner, c => c.CaptureAsync(animalId, TestOriginal.Png()));
        var documentId = stored.GetProperty("id").GetGuid();
        await FailAsync(documentId);

        using (var missing = await owner.PostAsync(RetryPath(documentId), null))
        {
            await missing.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid_antiforgery_token");
        }

        using (var invalid = new HttpRequestMessage(HttpMethod.Post, RetryPath(documentId)))
        {
            invalid.Headers.Add(TestAccounts.AntiforgeryHeader, "not-a-token");
            using var response = await owner.SendAsync(invalid);
            await response.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid_antiforgery_token");
        }

        var job = await JobAsync(documentId);
        Assert.Equal(DocumentReadingState.Failed, job.State);
        Assert.Equal(1, job.RunGeneration);
        Assert.Equal(0, job.OwnerRetryCount);
    }

    [Fact]
    public async Task Retry_requeues_a_failed_job_in_a_new_generation_keeping_cleanup_and_repeats_coalesce()
    {
        using var owner = await factory.CreateCaptureClientAsync();
        var documentId = await StoredDocumentAsync(owner);
        await UseDatabaseAsync(db => db.Database.ExecuteSqlAsync(
            $"UPDATE document_reading_jobs SET attempt_count = 5 WHERE document_id = {documentId}"));
        await FailAsync(documentId);
        var runFileId = await AddPendingCleanupAsync(documentId);

        var retried = await RetryJsonAsync(owner, documentId);

        Assert.Equal(StatusFields, FieldsOf(retried));
        Assert.Equal("queued", retried.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, retried.GetProperty("failureCode").ValueKind);
        Assert.False(retried.GetProperty("canRetry").GetBoolean());
        Assert.Equal(JsonValueKind.Null, retried.GetProperty("completedAt").ValueKind);
        var job = await JobAsync(documentId);
        Assert.Equal(DocumentReadingState.Queued, job.State);
        Assert.Equal(2, job.RunGeneration);
        Assert.Equal(0, job.AttemptCount);
        Assert.Null(job.FailureCode);
        Assert.Null(job.CompletedAt);
        Assert.Equal(1, job.OwnerRetryCount);
        Assert.NotNull(job.OwnerRetryWindowStartedAt);
        Assert.True(job.DueAt <= DateTimeOffset.UtcNow.AddMinutes(1));
        var runFile = await UseDatabaseAsync(db => db.DocumentReadingRunFiles.AsNoTracking().SingleAsync(r => r.Id == runFileId));
        Assert.Equal(DocumentReadingCleanupState.Pending, runFile.CleanupState);
        Assert.Equal("op-retry-cleanup", runFile.ProviderOperationId);

        // A repeated click while queued is a no-op returning the current status.
        var repeated = await RetryJsonAsync(owner, documentId);
        Assert.Equal("queued", repeated.GetProperty("state").GetString());
        Assert.Equal(2, (await JobAsync(documentId)).RunGeneration);
        Assert.Equal(1, (await JobAsync(documentId)).OwnerRetryCount);

        // Also while a worker is reading it: the lease is untouched.
        await UseDatabaseAsync(db => db.Database.ExecuteSqlAsync($"""
            UPDATE document_reading_jobs SET state = 'reading', lease_token = gen_random_uuid(),
                lease_expires_at = now() + interval '5 minutes'
            WHERE document_id = {documentId}
            """));
        var leased = await JobAsync(documentId);
        var whileReading = await RetryJsonAsync(owner, documentId);
        Assert.Equal("reading", whileReading.GetProperty("state").GetString());
        var stillLeased = await JobAsync(documentId);
        Assert.Equal(leased.LeaseToken, stillLeased.LeaseToken);
        Assert.Equal(2, stillLeased.RunGeneration);
        Assert.Equal(1, stillLeased.OwnerRetryCount);

        // And a read job stays read.
        await UseDatabaseAsync(db => db.Database.ExecuteSqlAsync($"""
            UPDATE document_reading_jobs SET state = 'read', lease_token = NULL, lease_expires_at = NULL,
                published_run_generation = run_generation, completed_at = now()
            WHERE document_id = {documentId}
            """));
        var whileRead = await RetryJsonAsync(owner, documentId);
        Assert.Equal("read", whileRead.GetProperty("state").GetString());
        Assert.False(whileRead.GetProperty("canRetry").GetBoolean());
        Assert.Equal(DocumentReadingState.Read, (await JobAsync(documentId)).State);
        Assert.Equal(2, (await JobAsync(documentId)).RunGeneration);
    }

    [Fact]
    public async Task Concurrent_retries_of_one_failed_job_are_accepted_once()
    {
        using var owner = await factory.CreateCaptureClientAsync();
        var documentId = await StoredDocumentAsync(owner);
        await FailAsync(documentId);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => owner.PostAsync(RetryPath(documentId), null)));
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            response.Dispose();
        }

        var job = await JobAsync(documentId);
        Assert.Equal(DocumentReadingState.Queued, job.State);
        Assert.Equal(2, job.RunGeneration);
        Assert.Equal(1, job.OwnerRetryCount);
    }

    [Fact]
    public async Task The_hourly_retry_limit_is_durable_across_app_instances_and_ends_with_its_window()
    {
        var (owner, email) = await factory.CreateSignedInClientAsync();
        using (owner)
        {
            await owner.UseAntiforgeryTokenAsync();
            var documentId = await StoredDocumentAsync(owner);

            for (var retry = 1; retry <= OwnedDocumentReading.MaxOwnerRetries; retry++)
            {
                await FailAsync(documentId);
                Assert.Equal("queued", (await RetryJsonAsync(owner, documentId)).GetProperty("state").GetString());
            }

            await FailAsync(documentId);
            var limited = await owner.GetJsonAsync(StatusPath(documentId));
            Assert.Equal("failed", limited.GetProperty("state").GetString());
            Assert.False(limited.GetProperty("canRetry").GetBoolean());
            var retryAfter = limited.GetProperty("retryAfter").GetDateTimeOffset();
            var windowStart = (await JobAsync(documentId)).OwnerRetryWindowStartedAt!.Value;
            Assert.Equal(windowStart + OwnedDocumentReading.RetryWindow, retryAfter);

            using (var refused = await owner.PostAsync(RetryPath(documentId), null))
            {
                await AssertRetryLimitedAsync(refused, retryAfter);
            }

            // A fresh app instance (new host, new DI container, same database) enforces the same limit.
            await using (var restarted = factory.WithWebHostBuilder(_ => { }))
            {
                using var again = restarted.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
                using (var login = await again.LoginAsync(email, TestAccounts.ValidPassword))
                {
                    Assert.True(login.IsSuccessStatusCode);
                }

                await again.UseAntiforgeryTokenAsync();
                using var refusedAgain = await again.PostAsync(RetryPath(documentId), null);
                await AssertRetryLimitedAsync(refusedAgain, retryAfter);
            }

            var job = await JobAsync(documentId);
            Assert.Equal(DocumentReadingState.Failed, job.State);
            Assert.Equal(OwnedDocumentReading.MaxOwnerRetries + 1, job.RunGeneration);
            Assert.Equal(OwnedDocumentReading.MaxOwnerRetries, job.OwnerRetryCount);

            // Once the window has passed, a retry is accepted and starts a new window.
            await UseDatabaseAsync(db => db.Database.ExecuteSqlAsync($"""
                UPDATE document_reading_jobs
                SET owner_retry_window_started_at = now() - interval '1 hour' - interval '1 second'
                WHERE document_id = {documentId}
                """));
            Assert.True((await owner.GetJsonAsync(StatusPath(documentId))).GetProperty("canRetry").GetBoolean());
            Assert.Equal("queued", (await RetryJsonAsync(owner, documentId)).GetProperty("state").GetString());
            var renewed = await JobAsync(documentId);
            Assert.Equal(1, renewed.OwnerRetryCount);
            Assert.True(renewed.OwnerRetryWindowStartedAt > windowStart);
        }
    }

    [Fact]
    public async Task An_inactive_animals_document_keeps_its_status_and_retry()
    {
        using var owner = await factory.CreateCaptureClientAsync();
        var animalId = await owner.CreateAnimalIdAsync();
        var png = TestOriginal.Png();
        var stored = await owner.CaptureAsync(animalId, png);
        var documentId = stored.GetProperty("id").GetGuid();
        await FailAsync(documentId);
        await UseDatabaseAsync(db => db.Database.ExecuteSqlAsync(
            $"UPDATE animals SET is_active = false WHERE id = {animalId}"));

        Assert.Equal("failed", (await owner.GetJsonAsync(StatusPath(documentId))).GetProperty("state").GetString());
        Assert.Equal("queued", (await RetryJsonAsync(owner, documentId)).GetProperty("state").GetString());
        Assert.Equal(png.Bytes, await owner.GetOriginalBytesAsync(Assert.Single(stored.OriginalUrls())));
    }

    private static string StatusPath(Guid documentId) => $"/api/documents/{documentId}/reading";

    private static string RetryPath(Guid documentId) => $"/api/documents/{documentId}/reading/retry";

    private static string[] FieldsOf(JsonElement element) => element.EnumerateObject().Select(p => p.Name).ToArray();

    private static async Task<T> WithTokenAsync<T>(HttpClient client, Func<HttpClient, Task<T>> action)
    {
        await client.UseAntiforgeryTokenAsync();
        try
        {
            return await action(client);
        }
        finally
        {
            client.DefaultRequestHeaders.Remove(TestAccounts.AntiforgeryHeader);
        }
    }

    private static async Task<JsonElement> RetryJsonAsync(HttpClient client, Guid documentId)
    {
        using var response = await client.PostAsync(RetryPath(documentId), null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    private static async Task AssertRetryLimitedAsync(HttpResponseMessage response, DateTimeOffset retryAfter)
    {
        await response.AssertProblemAsync(HttpStatusCode.TooManyRequests, "retry_limited");
        var body = await response.ReadJsonAsync();
        Assert.Equal(retryAfter, body.GetProperty("retryAfter").GetDateTimeOffset());
        var delta = Assert.IsType<TimeSpan>(response.Headers.RetryAfter?.Delta);
        Assert.InRange(delta, TimeSpan.FromSeconds(1), OwnedDocumentReading.RetryWindow);
    }

    private async Task<Guid> StoredDocumentAsync(HttpClient owner)
    {
        var animalId = await owner.CreateAnimalIdAsync();
        var stored = await owner.CaptureAsync(animalId, TestOriginal.Png());
        return stored.GetProperty("id").GetGuid();
    }

    // What the worker's FailAsync leaves behind, without running a worker.
    private Task FailAsync(Guid documentId) =>
        UseDatabaseAsync(db => db.Database.ExecuteSqlAsync($"""
            UPDATE document_reading_jobs
            SET state = 'failed', failure_code = 'provider_unavailable', completed_at = now(),
                lease_token = NULL, lease_expires_at = NULL, updated_at = now()
            WHERE document_id = {documentId}
            """));

    // An abandoned provider operation of the current generation whose deletion is still pending.
    private async Task<Guid> AddPendingCleanupAsync(Guid documentId)
    {
        var id = Guid.CreateVersion7();
        await UseDatabaseAsync(db => db.Database.ExecuteSqlAsync($"""
            INSERT INTO document_reading_run_files (id, document_id, run_generation, document_file_id, stage,
                provider_operation_id, cleanup_state, cleanup_attempt_count, next_cleanup_at, created_at, updated_at)
            SELECT {id}, {documentId}, 1, f.id, 'failed', 'op-retry-cleanup', 'pending', 0,
                now() + interval '1 day', now(), now()
            FROM document_files AS f WHERE f.document_id = {documentId}
            """));
        return id;
    }

    private Task<DocumentReadingJob> JobAsync(Guid documentId) =>
        UseDatabaseAsync(db => db.DocumentReadingJobs.AsNoTracking().SingleAsync(j => j.DocumentId == documentId));

    private async Task<T> UseDatabaseAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
