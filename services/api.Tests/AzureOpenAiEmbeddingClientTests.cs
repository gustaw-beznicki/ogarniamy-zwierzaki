using System.Net;
using System.Text;
using System.Text.Json;
using ogarniamy_zwierzaki_api.DocumentEmbedding;

namespace ogarniamy_zwierzaki_api.Tests;

// The Azure OpenAI embeddings adapter against a scripted HTTP handler: request shape, batching limits, failure
// classification (including Retry-After) and validation of the returned vectors. No network, no Azure.
public sealed class AzureOpenAiEmbeddingClientTests
{
    private const string Endpoint = "https://oai-test.openai.azure.com/";

    private static readonly EmbeddingOptions Options = new()
    {
        Auth = EmbeddingAuthMode.AzureManagedIdentity,
        Endpoint = new Uri(Endpoint),
        Deployment = "test-deployment",
    };

    [Fact]
    public async Task A_request_posts_the_deployment_inputs_and_dimensions_with_a_bearer_token()
    {
        var handler = new ScriptedHandler(request => Vectors(request));
        var client = NewClient(handler);

        var vectors = await client.EmbedAsync(["first synthetic text", "second synthetic text"], CancellationToken.None);

        Assert.Equal(2, vectors.Count);
        Assert.All(vectors, v => Assert.Equal(EmbeddingOptions.StoredDimensions, v.Length));
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://oai-test.openai.azure.com/openai/v1/embeddings", request.Uri.ToString());
        Assert.Equal("Bearer test-token", request.Authorization);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("test-deployment", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(1536, body.RootElement.GetProperty("dimensions").GetInt32());
        Assert.Equal(
            ["first synthetic text", "second synthetic text"],
            body.RootElement.GetProperty("input").EnumerateArray().Select(i => i.GetString()));
    }

    [Fact]
    public async Task Vectors_are_returned_in_input_order_even_when_the_provider_answers_out_of_order()
    {
        var handler = new ScriptedHandler(_ => Json(HttpStatusCode.OK, Answer((1, 0.5f), (0, 0.25f))));
        var client = NewClient(handler);

        var vectors = await client.EmbedAsync(["a", "b"], CancellationToken.None);

        Assert.Equal(0.25f, vectors[0][0]);
        Assert.Equal(0.5f, vectors[1][0]);
    }

    [Fact]
    public async Task No_inputs_make_no_request()
    {
        var handler = new ScriptedHandler(request => Vectors(request));

        Assert.Empty(await NewClient(handler).EmbedAsync([], CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Inputs_are_split_at_the_provider_limit_on_count()
    {
        var handler = new ScriptedHandler(request => Vectors(request));
        var inputs = Enumerable.Range(0, AzureOpenAiEmbeddingClient.MaxInputsPerRequest + 5).Select(i => $"t{i}").ToList();

        var vectors = await NewClient(handler).EmbedAsync(inputs, CancellationToken.None);

        Assert.Equal(inputs.Count, vectors.Count);
        Assert.Equal([AzureOpenAiEmbeddingClient.MaxInputsPerRequest, 5], handler.Requests.Select(r => InputCount(r)));
    }

    [Fact]
    public async Task Inputs_are_split_to_stay_within_the_estimated_token_budget()
    {
        var handler = new ScriptedHandler(request => Vectors(request));
        var text = new string('x', 8_000);
        var perBatch = (int)(Options.MaxEstimatedTokensPerRequest / AzureOpenAiEmbeddingClient.EstimateTokens(text));
        var inputs = Enumerable.Repeat(text, perBatch + 3).ToList();

        await NewClient(handler).EmbedAsync(inputs, CancellationToken.None);

        Assert.Equal([perBatch, 3], handler.Requests.Select(r => InputCount(r)));
        Assert.All(handler.Requests, r =>
            Assert.True(InputCount(r) * AzureOpenAiEmbeddingClient.EstimateTokens(text) <= Options.MaxEstimatedTokensPerRequest));
    }

    [Fact]
    public void The_configured_budget_never_exceeds_the_hard_budget()
    {
        var text = new string('x', 8_000);
        var perBatch = (int)(AzureOpenAiEmbeddingClient.EstimatedTokenBudget / AzureOpenAiEmbeddingClient.EstimateTokens(text));
        var inputs = Enumerable.Repeat(text, perBatch + 3).ToList();

        var batches = AzureOpenAiEmbeddingClient.Batches(inputs, int.MaxValue).ToList();

        Assert.Equal([perBatch, 3], batches.Select(b => b.Count));
    }

    [Theory]
    [InlineData(429, true, false, EmbeddingFailure.ProviderUnavailable)]
    [InlineData(503, true, false, EmbeddingFailure.ProviderUnavailable)]
    [InlineData(408, true, false, EmbeddingFailure.ProviderUnavailable)]
    [InlineData(401, false, true, EmbeddingFailure.ProviderAuth)]
    [InlineData(403, false, true, EmbeddingFailure.ProviderAuth)]
    [InlineData(404, false, true, EmbeddingFailure.ProviderConfig)]
    [InlineData(400, false, false, EmbeddingFailure.ProviderRejected)]
    public async Task Failures_are_classified_by_status_with_sanitized_codes(
        int status, bool transient, bool configuration, string code)
    {
        var handler = new ScriptedHandler(_ => Json((HttpStatusCode)status, """{"error":{"message":"echo of SECRET-INPUT"}}"""));

        var failure = await Assert.ThrowsAsync<EmbeddingFailure>(
            () => NewClient(handler).EmbedAsync(["SECRET-INPUT"], CancellationToken.None));

        Assert.Equal(transient, failure.IsTransient);
        Assert.Equal(configuration, failure.IsConfiguration);
        Assert.Equal(code, failure.Code);
        Assert.DoesNotContain("SECRET-INPUT", failure.ToString());
    }

    [Fact]
    public async Task A_throttled_answer_carries_its_retry_after()
    {
        var handler = new ScriptedHandler(_ =>
        {
            var response = Json(HttpStatusCode.TooManyRequests, "{}");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(90));
            return response;
        });

        var failure = await Assert.ThrowsAsync<EmbeddingFailure>(() => NewClient(handler).EmbedAsync(["a"], CancellationToken.None));

        Assert.True(failure.IsTransient);
        Assert.Equal(TimeSpan.FromSeconds(90), failure.RetryAfter);
    }

    [Fact]
    public async Task A_very_long_retry_after_is_capped()
    {
        var handler = new ScriptedHandler(_ =>
        {
            var response = Json(HttpStatusCode.TooManyRequests, "{}");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromDays(3));
            return response;
        });

        var failure = await Assert.ThrowsAsync<EmbeddingFailure>(() => NewClient(handler).EmbedAsync(["a"], CancellationToken.None));

        Assert.Equal(AzureOpenAiEmbeddingClient.MaxRetryAfter, failure.RetryAfter);
    }

    [Fact]
    public async Task A_network_fault_is_transient()
    {
        var handler = new ScriptedHandler(_ => throw new HttpRequestException("synthetic network fault"));

        var failure = await Assert.ThrowsAsync<EmbeddingFailure>(() => NewClient(handler).EmbedAsync(["a"], CancellationToken.None));

        Assert.True(failure.IsTransient);
        Assert.Equal(EmbeddingFailure.ProviderUnavailable, failure.Code);
    }

    [Fact]
    public async Task A_wrong_vector_count_or_dimension_is_a_terminal_protocol_failure()
    {
        var wrongCount = new ScriptedHandler(_ => Json(HttpStatusCode.OK, Answer((0, 0.1f))));
        var shortVector = new ScriptedHandler(_ => Json(HttpStatusCode.OK, """{"data":[{"index":0,"embedding":[0.1,0.2]}]}"""));
        var notJson = new ScriptedHandler(_ => Json(HttpStatusCode.OK, "not json"));

        foreach (var (handler, inputs) in new[] { (wrongCount, 2), (shortVector, 1), (notJson, 1) })
        {
            var failure = await Assert.ThrowsAsync<EmbeddingFailure>(
                () => NewClient(handler).EmbedAsync(Enumerable.Repeat("a", inputs).ToList(), CancellationToken.None));
            Assert.False(failure.IsTransient);
            Assert.Equal(EmbeddingFailure.ProviderProtocol, failure.Code);
        }
    }

    [Fact]
    public async Task Without_a_credential_every_call_is_a_configuration_failure()
    {
        var client = new AzureOpenAiEmbeddingClient(new HttpClient(new ScriptedHandler(_ => Json(HttpStatusCode.OK, "{}"))), null, Options);

        var failure = await Assert.ThrowsAsync<EmbeddingFailure>(() => client.EmbedAsync(["a"], CancellationToken.None));

        Assert.True(failure.IsConfiguration);
        Assert.Equal(EmbeddingFailure.ProviderConfig, failure.Code);
    }

    private static AzureOpenAiEmbeddingClient NewClient(ScriptedHandler handler) =>
        new(new HttpClient(handler), new FakeTokenCredential(), Options);

    private static int InputCount(RecordedRequest request)
    {
        using var body = JsonDocument.Parse(request.Body!);
        return body.RootElement.GetProperty("input").GetArrayLength();
    }

    // One valid vector per input of the request.
    private static HttpResponseMessage Vectors(RecordedRequest request) =>
        Json(HttpStatusCode.OK, Answer(Enumerable.Range(0, InputCount(request)).Select(i => (i, 0.1f)).ToArray()));

    private static string Answer(params (int Index, float First)[] items) => JsonSerializer.Serialize(new
    {
        data = items.Select(item => new
        {
            index = item.Index,
            embedding = Enumerable.Range(0, EmbeddingOptions.StoredDimensions).Select(i => i == 0 ? item.First : 0f).ToArray(),
        }),
    });

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class ScriptedHandler(Func<RecordedRequest, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var recorded = new RecordedRequest(
                request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(),
                request.Content?.Headers.ContentType?.MediaType,
                request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken));
            Requests.Add(recorded);
            return respond(recorded);
        }
    }
}
