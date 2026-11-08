using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ogarniamy_zwierzaki_api.DocumentEmbedding;

namespace ogarniamy_zwierzaki_api.Tests;

// POST /api/search for one owner: one result per document with its best fragment, ordering and the cap, the threshold,
// superseded revisions, the not-searchable count, provider failure, validation and logging. Documents are captured over
// HTTP and embedded by the real worker with a fake provider; the isolation guarantees are in SearchIsolationTests.
// Synthetic text only.
public sealed class SearchTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private SearchTestHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = new SearchTestHost(factory);
        await _host.ParkAllJobsAsync();
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task One_result_per_document_names_its_best_fragment_and_the_original()
    {
        using var owner = await _host.SignInAsync();
        var animalId = await owner.Client.CreateAnimalIdAsync("Czarek");
        var bestPage = "Synthetic velvet qzx7velvet quartz discharge instructions";
        var documentId = await _host.CaptureAndReadAsync(
            owner, animalId, "Synthetic cover page nothing else", bestPage, "Synthetic quartz discharge appendix");
        await _host.EmbedAllAsync();

        var body = await SearchTestHost.SearchOkAsync(owner.Client, "  qzx7velvet quartz discharge  ");

        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(0, body.GetProperty("notSearchableCount").GetInt32());
        Assert.Equal(documentId, item.GetProperty("documentId").GetGuid());
        Assert.Equal(animalId, item.GetProperty("animalId").GetGuid());
        Assert.Equal("Czarek", item.GetProperty("animalName").GetString());
        Assert.Equal(DocumentApi.PastEventDate, item.GetProperty("eventDate").GetString());
        Assert.Equal(0, item.GetProperty("filePosition").GetInt32());
        Assert.Equal(2, item.GetProperty("sourcePageNumber").GetInt32());
        Assert.Equal(3, item.GetProperty("filePageCount").GetInt32());
        Assert.Equal(bestPage, item.GetProperty("fragment").GetString());
        var fileId = item.GetProperty("fileId").GetGuid();
        Assert.Equal($"/api/documents/{documentId}/files/{fileId}/original", item.GetProperty("originalUrl").GetString());
        // No score or other ranking detail: exactly the contract's properties.
        Assert.Equal(
            new[]
            {
                "animalId", "animalName", "documentId", "eventDate", "fileId", "filePageCount", "filePosition", "fragment",
                "originalUrl", "sourcePageNumber",
            },
            item.EnumerateObject().Select(p => p.Name).Order().ToArray());

        // The link opens the stored original of that document.
        using var original = await owner.Client.GetAsync(item.GetProperty("originalUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);

        // The query was embedded as typed (trimmed), without the animal-name prefix chunks get.
        Assert.Equal("qzx7velvet quartz discharge", Assert.Single(_host.Provider.Calls[^1]));
    }

    [Fact]
    public async Task Documents_of_all_animals_are_ordered_by_similarity()
    {
        using var owner = await _host.SignInAsync();
        var first = await owner.Client.CreateAnimalIdAsync("Czarek");
        var second = await owner.Client.CreateAnimalIdAsync("Burek");
        var weaker = await _host.CaptureAndReadAsync(owner, second, "Synthetic quartz discharge letter");
        var stronger = await _host.CaptureAndReadAsync(owner, first, "Synthetic qzx7velvet quartz discharge");
        await _host.EmbedAllAsync();

        var body = await SearchTestHost.SearchOkAsync(owner.Client, "qzx7velvet quartz discharge");

        Assert.Equal([stronger, weaker], SearchTestHost.DocumentIds(body));
        Assert.Equal(
            ["Czarek", "Burek"],
            body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("animalName").GetString()));
    }

    [Fact]
    public async Task At_most_twenty_documents_are_returned()
    {
        using var owner = await _host.SignInAsync();
        var animalId = await owner.Client.CreateAnimalIdAsync("Czarek");
        for (var i = 0; i < 21; i++)
        {
            await _host.CaptureAndReadAsync(owner, animalId, $"Synthetic qzx7velvet capped note {i}");
        }

        await _host.EmbedAllAsync();

        var body = await SearchTestHost.SearchOkAsync(owner.Client, "qzx7velvet capped note");

        Assert.Equal(20, body.GetProperty("items").GetArrayLength());
        Assert.Equal(0, body.GetProperty("notSearchableCount").GetInt32());
    }

    [Fact]
    public async Task Nothing_above_the_threshold_is_an_empty_200()
    {
        using var owner = await _host.SignInAsync();
        var animalId = await owner.Client.CreateAnimalIdAsync("Czarek");
        await _host.CaptureAndReadAsync(owner, animalId, "Synthetic qzx7velvet quartz discharge");
        await _host.EmbedAllAsync();

        using var response = await SearchTestHost.SearchAsync(owner.Client, "unrelatedwordone unrelatedwordtwo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(JsonValueKind.Array, body.GetProperty("items").ValueKind);
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
        Assert.Equal(0, body.GetProperty("notSearchableCount").GetInt32());
    }

    [Fact]
    public async Task The_configured_minimum_similarity_decides_and_a_match_at_the_threshold_counts()
    {
        await using var strict = new SearchTestHost(factory, minSimilarity: 0.99);
        using var owner = await strict.SignInAsync();
        var animalId = await owner.Client.CreateAnimalIdAsync("Czarek");
        var documentId = await strict.CaptureAndReadAsync(owner, animalId, "Synthetic qzx7velvet quartz discharge");
        await strict.EmbedAllAsync();

        // A partial match is well above the default 0.30 (see the other tests) but below 0.99.
        var partial = await SearchTestHost.SearchOkAsync(owner.Client, "qzx7velvet quartz");
        Assert.Empty(partial.GetProperty("items").EnumerateArray());

        // The exact text the chunk was embedded as has similarity 1.
        var exact = await SearchTestHost.SearchOkAsync(owner.Client, "Czarek\n\nSynthetic qzx7velvet quartz discharge");
        Assert.Equal([documentId], SearchTestHost.DocumentIds(exact));
    }

    [Fact]
    public async Task Chunks_of_a_superseded_reading_generation_are_ignored_until_the_new_revision_is_embedded()
    {
        using var owner = await _host.SignInAsync();
        var animalId = await owner.Client.CreateAnimalIdAsync("Czarek");
        var documentId = await _host.CaptureAndReadAsync(owner, animalId, "Synthetic qzx7velvet old revision text");
        await _host.EmbedAllAsync();
        Assert.Equal([documentId], SearchTestHost.DocumentIds(await SearchTestHost.SearchOkAsync(owner.Client, "qzx7velvet old revision")));

        // The new revision is published (generation 2); its embedding is still queued, so only generation 1 chunks exist.
        await _host.RepublishAsync(documentId, "Synthetic zq9marble new revision text");

        var during = await SearchTestHost.SearchOkAsync(owner.Client, "qzx7velvet old revision");
        Assert.Empty(during.GetProperty("items").EnumerateArray());
        var duringNew = await SearchTestHost.SearchOkAsync(owner.Client, "zq9marble new revision");
        Assert.Empty(duringNew.GetProperty("items").EnumerateArray());
        Assert.Equal(1, duringNew.GetProperty("notSearchableCount").GetInt32());

        await _host.EmbedAllAsync();

        var after = await SearchTestHost.SearchOkAsync(owner.Client, "zq9marble new revision");
        var item = Assert.Single(after.GetProperty("items").EnumerateArray());
        Assert.Equal("Synthetic zq9marble new revision text", item.GetProperty("fragment").GetString());
        Assert.Equal(0, after.GetProperty("notSearchableCount").GetInt32());
        Assert.Empty((await SearchTestHost.SearchOkAsync(owner.Client, "qzx7velvet old revision")).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Not_searchable_counts_documents_being_read_or_not_yet_embedded_but_not_failed_readings()
    {
        using var owner = await _host.SignInAsync();
        using var other = await _host.SignInAsync();
        var animalId = await owner.Client.CreateAnimalIdAsync("Czarek");
        var otherAnimalId = await other.Client.CreateAnimalIdAsync("Burek");
        await _host.CaptureAndReadAsync(owner, animalId, "Synthetic qzx7velvet embedded note");
        await _host.EmbedAllAsync();
        // Read but its embedding job is still queued.
        await _host.CaptureAndReadAsync(owner, animalId, "Synthetic qzx7velvet waiting note");
        await _host.CaptureAndReadAsync(other, otherAnimalId, "Synthetic qzx7velvet foreign waiting note");
        // Reading queued, reading in progress and reading failed (the last is not counted).
        await _host.CaptureUnreadAsync(owner, animalId);
        var reading = await _host.CaptureUnreadAsync(owner, animalId);
        var failed = await _host.CaptureUnreadAsync(owner, animalId);
        await _host.CaptureUnreadAsync(other, otherAnimalId);
        await _host.ExecuteAsync($"""
            UPDATE document_reading_jobs
            SET state = 'reading', lease_token = gen_random_uuid(), lease_expires_at = now() + interval '1 hour'
            WHERE document_id = '{reading}'
            """);
        await _host.ExecuteAsync($"""
            UPDATE document_reading_jobs
            SET state = 'failed', failure_code = 'provider_rejected', completed_at = now()
            WHERE document_id = '{failed}'
            """);

        var body = await SearchTestHost.SearchOkAsync(owner.Client, "qzx7velvet embedded note");

        Assert.Single(body.GetProperty("items").EnumerateArray());
        // The waiting note, the queued and the in-progress readings; never another account's documents.
        Assert.Equal(3, body.GetProperty("notSearchableCount").GetInt32());
    }

    [Fact]
    public async Task A_provider_failure_is_a_503_search_unavailable_without_results()
    {
        using var owner = await _host.SignInAsync();
        var animalId = await owner.Client.CreateAnimalIdAsync("Czarek");
        await _host.CaptureAndReadAsync(owner, animalId, "Synthetic qzx7velvet quartz discharge");
        await _host.EmbedAllAsync();

        foreach (var failure in new[]
                 {
                     EmbeddingFailure.Transient(EmbeddingFailure.ProviderUnavailable),
                     EmbeddingFailure.Terminal(EmbeddingFailure.ProviderRejected),
                     EmbeddingFailure.Configuration(EmbeddingFailure.ProviderAuth),
                 })
        {
            _host.Provider.OnEmbed = (_, _) => throw failure;
            using var response = await SearchTestHost.SearchAsync(owner.Client, "qzx7velvet quartz discharge");
            await response.AssertProblemAsync(HttpStatusCode.ServiceUnavailable, "search_unavailable");
            var body = await response.ReadJsonAsync();
            Assert.False(body.TryGetProperty("items", out _));
        }

        // Search works again once the provider does.
        _host.Provider.OnEmbed = null;
        Assert.Single((await SearchTestHost.SearchOkAsync(owner.Client, "qzx7velvet quartz discharge")).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Unavailable_embedding_is_a_503_search_unavailable_and_calls_no_provider()
    {
        await using var unavailable = new SearchTestHost(factory, embeddingAvailable: false);
        using var owner = await unavailable.SignInAsync();

        using var response = await SearchTestHost.SearchAsync(owner.Client, "qzx7velvet quartz discharge");

        await response.AssertProblemAsync(HttpStatusCode.ServiceUnavailable, "search_unavailable");
        Assert.Empty(unavailable.Provider.Calls);
    }

    [Fact]
    public async Task Invalid_queries_are_400_invalid_query_and_reach_no_provider()
    {
        using var owner = await _host.SignInAsync();
        var callsBefore = _host.Provider.Calls.Count;

        foreach (var query in new string?[] { null, "", "   ", " \n\t ", new string('a', 501), " " + new string('a', 501) })
        {
            using var response = await SearchTestHost.SearchAsync(owner.Client, query);
            await response.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid_query");
        }

        using (var empty = await owner.Client.PostAsJsonAsync("/api/search", new { }))
        {
            await empty.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid_query");
        }

        using (var nullBody = await owner.Client.PostAsJsonAsync<object?>("/api/search", null))
        {
            await nullBody.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid_query");
        }

        Assert.Equal(callsBefore, _host.Provider.Calls.Count);

        // 500 characters after trimming is the longest valid query, and a single character the shortest.
        var longest = new string('a', 500);
        using (var ok = await SearchTestHost.SearchAsync(owner.Client, $"  {longest}  "))
        {
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        using (var shortest = await SearchTestHost.SearchAsync(owner.Client, "a"))
        {
            Assert.Equal(HttpStatusCode.OK, shortest.StatusCode);
        }

        Assert.Equal([[longest], ["a"]], _host.Provider.Calls.Skip(callsBefore).Select(c => c.ToArray()).ToArray());
    }

    [Fact]
    public async Task Query_text_and_fragments_never_reach_the_logs()
    {
        using var owner = await _host.SignInAsync();
        var animalId = await owner.Client.CreateAnimalIdAsync("Czarek");
        await _host.CaptureAndReadAsync(owner, animalId, "Synthetic fragmentprobe qzx7velvet quartz discharge");
        await _host.EmbedAllAsync();

        Assert.Single((await SearchTestHost.SearchOkAsync(owner.Client, "queryprobe qzx7velvet quartz discharge"))
            .GetProperty("items").EnumerateArray());
        _host.Provider.OnEmbed = (_, _) => throw EmbeddingFailure.Transient(EmbeddingFailure.ProviderUnavailable);
        using (var failed = await SearchTestHost.SearchAsync(owner.Client, "queryprobe qzx7velvet quartz discharge"))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        }

        Assert.Contains(_host.Logs.Entries, e => e.Contains("Search returned 1 documents"));
        Assert.Contains(_host.Logs.Entries, e => e.Contains("provider_unavailable"));
        Assert.DoesNotContain(_host.Logs.Entries, e =>
            e.Contains("queryprobe") || e.Contains("fragmentprobe") || e.Contains("qzx7velvet"));
    }
}
