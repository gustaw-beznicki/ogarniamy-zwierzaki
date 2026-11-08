using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using ogarniamy_zwierzaki_api.DocumentReading;

namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// IEmbeddingClient over the Azure OpenAI v1 REST API: POST {endpoint}openai/v1/embeddings with an Entra token. No retry
// handler: the worker decides about later attempts. Only status codes and the response's vectors are interpreted; the
// inputs, the provider's messages and its payloads are never logged or kept in a failure.
public sealed class AzureOpenAiEmbeddingClient(
    HttpClient http, TokenCredential? credential, EmbeddingOptions options, TimeProvider? time = null)
    : IEmbeddingClient
{
    // The provider's limits per request.
    public const int MaxInputsPerRequest = 2048;

    public const int MaxTokensPerRequest = 300_000;

    // Tokens are estimated, not counted: one token per two characters is pessimistic for prose in any European
    // language, and the hard budget leaves headroom below the provider's cap. Requests are normally much smaller:
    // EmbeddingOptions.MaxEstimatedTokensPerRequest keeps each one inside the deployment's per-minute limit.
    public const int CharactersPerEstimatedToken = 2;

    public const int EstimatedTokenBudget = 250_000;

    // A larger response is refused (2,048 vectors of 1,536 floats is about 60 MB of JSON).
    public long MaxResponseBytes { get; init; } = 96L * 1024 * 1024;

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var vectors = new List<float[]>(inputs.Count);
        foreach (var batch in Batches(inputs, options.MaxEstimatedTokensPerRequest))
        {
            vectors.AddRange(await EmbedBatchAsync(batch, cancellationToken));
        }

        return vectors;
    }

    // Consecutive slices of the inputs, each within the input-count limit and the estimated token budget (the smaller
    // of maxEstimatedTokens and EstimatedTokenBudget). A single input over the budget travels alone (the provider
    // rejects it if it is really too long).
    public static IEnumerable<IReadOnlyList<string>> Batches(IReadOnlyList<string> inputs, int maxEstimatedTokens)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEstimatedTokens, 1);
        var budget = Math.Min(maxEstimatedTokens, EstimatedTokenBudget);
        var start = 0;
        while (start < inputs.Count)
        {
            var count = 0;
            long tokens = 0;
            while (start + count < inputs.Count && count < MaxInputsPerRequest)
            {
                var estimate = EstimateTokens(inputs[start + count]);
                if (count > 0 && tokens + estimate > budget)
                {
                    break;
                }

                tokens += estimate;
                count++;
            }

            yield return inputs.Skip(start).Take(count).ToList();
            start += count;
        }
    }

    public static long EstimateTokens(string input) => (input.Length + CharactersPerEstimatedToken - 1) / CharactersPerEstimatedToken;

    private async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> batch, CancellationToken cancellationToken)
    {
        using var request = await RequestAsync(cancellationToken);
        var body = JsonSerializer.Serialize(new { model = options.Deployment, input = batch, dimensions = options.Dimensions });
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw FailureOf(response);
        }

        using var document = await ParseCappedAsync(response, cancellationToken);
        return VectorsOf(document.RootElement, batch.Count);
    }

    private Uri EmbeddingsUri => new($"{options.Endpoint!.AbsoluteUri.TrimEnd('/')}/openai/v1/embeddings");

    private async Task<HttpRequestMessage> RequestAsync(CancellationToken cancellationToken)
    {
        if (credential is null || options.Endpoint is null)
        {
            throw EmbeddingFailure.Configuration(EmbeddingFailure.ProviderConfig);
        }

        string token;
        try
        {
            token = (await credential.GetTokenAsync(new TokenRequestContext([options.TokenScope]), cancellationToken)).Token;
        }
        catch (AuthenticationFailedException)
        {
            // Includes CredentialUnavailableException. Its text can include account details, so it is not kept.
            throw EmbeddingFailure.Configuration(EmbeddingFailure.ProviderAuth);
        }
        catch (Azure.RequestFailedException)
        {
            // The token endpoint itself could not answer.
            throw EmbeddingFailure.Transient(EmbeddingFailure.ProviderUnavailable);
        }

        var request = new HttpRequestMessage(HttpMethod.Post, EmbeddingsUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw EmbeddingFailure.Transient(EmbeddingFailure.ProviderUnavailable);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw EmbeddingFailure.Transient(EmbeddingFailure.ProviderUnavailable);
        }
    }

    // The status alone decides; the error body (which may quote the input) is not read.
    private EmbeddingFailure FailureOf(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        if (status is 408 or 429 or >= 500)
        {
            return EmbeddingFailure.Transient(EmbeddingFailure.ProviderUnavailable, RetryAfterOf(response));
        }

        if (status is 401 or 403)
        {
            return EmbeddingFailure.Configuration(EmbeddingFailure.ProviderAuth);
        }

        if (status == 404)
        {
            // The endpoint or deployment does not exist.
            return EmbeddingFailure.Configuration(EmbeddingFailure.ProviderConfig);
        }

        return EmbeddingFailure.Terminal(EmbeddingFailure.ProviderRejected);
    }

    // The response's data[] in input order (by "index"), validated for count and dimension.
    private List<float[]> VectorsOf(JsonElement root, int expected)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() != expected)
        {
            throw EmbeddingFailure.Terminal(EmbeddingFailure.ProviderProtocol);
        }

        var byIndex = new SortedDictionary<int, float[]>();
        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("index", out var index) || !index.TryGetInt32(out var position)
                || position < 0 || position >= expected
                || !item.TryGetProperty("embedding", out var embedding) || embedding.ValueKind != JsonValueKind.Array
                || embedding.GetArrayLength() != options.Dimensions)
            {
                throw EmbeddingFailure.Terminal(EmbeddingFailure.ProviderProtocol);
            }

            var vector = new float[options.Dimensions];
            var i = 0;
            foreach (var value in embedding.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetSingle(out vector[i]) || !float.IsFinite(vector[i]))
                {
                    throw EmbeddingFailure.Terminal(EmbeddingFailure.ProviderProtocol);
                }

                i++;
            }

            if (!byIndex.TryAdd(position, vector))
            {
                throw EmbeddingFailure.Terminal(EmbeddingFailure.ProviderProtocol);
            }
        }

        return [.. byIndex.Values];
    }

    // Parses the response body straight from the network stream; the cap bounds what the parser may consume.
    private async Task<JsonDocument> ParseCappedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > MaxResponseBytes)
        {
            throw EmbeddingFailure.Terminal(EmbeddingFailure.ProviderProtocol);
        }

        try
        {
            await using var body = new LengthLimitedStream(
                await response.Content.ReadAsStreamAsync(cancellationToken), MaxResponseBytes,
                () => EmbeddingFailure.Terminal(EmbeddingFailure.ProviderProtocol));
            return await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            throw EmbeddingFailure.Terminal(EmbeddingFailure.ProviderProtocol);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException
                                              || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw EmbeddingFailure.Transient(EmbeddingFailure.ProviderUnavailable);
        }
    }

    // The longest provider Retry-After honoured; a larger value (or a far-off date) must not park a job for days.
    public static readonly TimeSpan MaxRetryAfter = TimeSpan.FromHours(1);

    // An absolute Retry-After date is measured against the injected clock; the result is capped at MaxRetryAfter.
    private TimeSpan? RetryAfterOf(HttpResponseMessage response)
    {
        var requested = response.Headers.RetryAfter is { } retryAfter
            ? retryAfter.Delta ?? (retryAfter.Date is { } date ? date - (time ?? TimeProvider.System).GetUtcNow() : null)
            : null;
        return requested > MaxRetryAfter ? MaxRetryAfter : requested;
    }
}
