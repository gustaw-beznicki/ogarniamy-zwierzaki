using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;

namespace ogarniamy_zwierzaki_api.DocumentReading;

// IDocumentTextReader over the Document Intelligence REST API (prebuilt-read): binary Analyze POST, polling of the
// saved result ID and Delete Analyze Result, with an Entra token. No retry handler: a submission is sent exactly once
// and the worker decides about later attempts. Only the provider's error codes are interpreted; its messages, the
// request and the result are never logged.
public sealed class AzureDocumentTextReader(
    HttpClient http, TokenCredential? credential, DocumentReadingOptions options, TimeProvider? time = null)
    : IDocumentTextReader
{
    // A larger analysis result is refused. The JSON is about 38 bytes per recognised character (words carry polygons and
    // spans; measured 2026-10-11), so 64 MiB holds several hundred dense pages while keeping the parsed document well
    // within F1 memory.
    public long MaxResultBytes { get; init; } = 64L * 1024 * 1024;

    public async Task<string> SubmitAsync(
        Stream content, string contentType, string? pages, CancellationToken cancellationToken)
    {
        var query = $"?api-version={Uri.EscapeDataString(options.ApiVersion)}";
        if (pages is not null)
        {
            query += $"&pages={Uri.EscapeDataString(pages)}";
        }

        using var request = await RequestAsync(
            HttpMethod.Post, $"{ModelUri}:analyze{query}", cancellationToken);
        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            if (response.Headers.TryGetValues("Operation-Location", out var values)
                && Uri.TryCreate(values.FirstOrDefault(), UriKind.Absolute, out var operationLocation)
                && ResultIdOf(operationLocation) is { Length: > 0 and <= DocumentReadingRunFile.ProviderOperationIdMaxLength } resultId)
            {
                return resultId;
            }

            // Accepted, but the result cannot be addressed: it stays with the provider until its retention ends.
            throw DocumentReadFailure.Terminal(DocumentReadFailure.ProviderProtocol);
        }

        throw await FailureOfAsync(response, cancellationToken);
    }

    public async Task<OcrAnalysis> GetAnalysisAsync(string operationId, CancellationToken cancellationToken)
    {
        using var request = await RequestAsync(HttpMethod.Get, ResultUri(operationId), cancellationToken);
        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // Expired or deleted: the saved operation cannot be resumed; submit again.
            throw DocumentReadFailure.Transient(DocumentReadFailure.ProviderResultMissing, abandonsOperation: true);
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await FailureOfAsync(response, cancellationToken);
        }

        using var document = await ParseCappedAsync(response, cancellationToken);
        var root = document.RootElement;
        var status = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("status", out var value)
            ? value.GetString()
            : null;
        return status switch
        {
            "notStarted" or "running" => OcrAnalysis.Running(RetryAfterOf(response)),
            "succeeded" => new OcrAnalysis(true, null, PagesOf(root)),
            "failed" => throw AnalysisFailure(root),
            _ => throw DocumentReadFailure.Terminal(DocumentReadFailure.ProviderProtocol, abandonsOperation: true),
        };
    }

    public async Task DeleteResultAsync(string operationId, CancellationToken cancellationToken)
    {
        using var request = await RequestAsync(HttpMethod.Delete, ResultUri(operationId), cancellationToken);
        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK)
        {
            return;
        }

        // Only the provider's own "result does not exist" counts as deleted. Any other 404 comes from a wrong endpoint or
        // path; acknowledging it would silently drop the obligation. (The 2024-11-30 API was observed answering 204 for
        // a missing result, so this is a safeguard.)
        if (response.StatusCode == HttpStatusCode.NotFound && await IsResultNotFoundAsync(response, cancellationToken))
        {
            return;
        }

        throw await FailureOfAsync(response, cancellationToken);
    }

    private string ModelUri =>
        $"{options.Endpoint!.AbsoluteUri.TrimEnd('/')}/documentintelligence/documentModels/{Uri.EscapeDataString(options.ModelId)}";

    private string ResultUri(string operationId) =>
        $"{ModelUri}/analyzeResults/{Uri.EscapeDataString(operationId)}?api-version={Uri.EscapeDataString(options.ApiVersion)}";

    private async Task<HttpRequestMessage> RequestAsync(HttpMethod method, string uri, CancellationToken cancellationToken)
    {
        if (credential is null || options.Endpoint is null)
        {
            throw DocumentReadFailure.Configuration(DocumentReadFailure.ProviderConfig);
        }

        string token;
        try
        {
            token = (await credential.GetTokenAsync(new TokenRequestContext([options.TokenScope]), cancellationToken)).Token;
        }
        catch (AuthenticationFailedException)
        {
            // Includes CredentialUnavailableException. Its text can include account details, so it is not kept.
            throw DocumentReadFailure.Configuration(DocumentReadFailure.ProviderAuth);
        }
        catch (Azure.RequestFailedException)
        {
            // The token endpoint itself could not answer.
            throw DocumentReadFailure.Transient(DocumentReadFailure.ProviderUnavailable);
        }

        var request = new HttpRequestMessage(method, new Uri(uri));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    // Network faults and timeouts are transient; for a submission their outcome is unknown, and the worker never
    // replays it within the attempt.
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw DocumentReadFailure.Transient(DocumentReadFailure.ProviderUnavailable);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw DocumentReadFailure.Transient(DocumentReadFailure.ProviderUnavailable);
        }
    }

    private async Task<DocumentReadFailure> FailureOfAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        if (status is 408 or 429 or >= 500)
        {
            return DocumentReadFailure.Transient(DocumentReadFailure.ProviderUnavailable, RetryAfterOf(response));
        }

        if (status is 401 or 403)
        {
            return DocumentReadFailure.Configuration(DocumentReadFailure.ProviderAuth);
        }

        if (status == 404)
        {
            // The endpoint or model does not exist.
            return DocumentReadFailure.Configuration(DocumentReadFailure.ProviderConfig);
        }

        if (status == 415)
        {
            return DocumentReadFailure.Terminal(DocumentReadFailure.UnreadableContent);
        }

        JsonElement root = default;
        try
        {
            using var document = await ParseCappedAsync(response, cancellationToken, maxBytes: 64 * 1024);
            root = document.RootElement.Clone();
        }
        catch (DocumentReadFailure)
        {
        }
        catch (JsonException)
        {
        }

        return ContentFailure(root) ?? DocumentReadFailure.Terminal(DocumentReadFailure.ProviderRejected);
    }

    // The provider's result-not-found body: {"code":"NotFound"} or {"error":{"code":"NotFound"}}.
    private async Task<bool> IsResultNotFoundAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await ParseCappedAsync(response, cancellationToken, maxBytes: 64 * 1024);
            var root = document.RootElement;
            var code = CodeOf(root) ?? (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error)
                ? CodeOf(error)
                : null);
            return code == "NotFound";
        }
        catch (DocumentReadFailure)
        {
            return false;
        }
    }

    // A failed analysis: unreadable content is terminal; a provider-side fault is transient but needs a new submission.
    private static DocumentReadFailure AnalysisFailure(JsonElement root)
    {
        var error = root.TryGetProperty("error", out var value) ? value : default;
        if (ContentFailure(root, abandonsOperation: true) is { } contentFailure)
        {
            return contentFailure;
        }

        var code = CodeOf(error);
        return code is "InternalServerError" or "ServiceUnavailable" or "Timeout" or null
            ? DocumentReadFailure.Transient(DocumentReadFailure.ProviderUnavailable, abandonsOperation: true)
            : DocumentReadFailure.Terminal(DocumentReadFailure.ProviderRejected, abandonsOperation: true);
    }

    // Recognises the provider's content errors (corrupted, unsupported, password-locked, too large) by inner code.
    private static DocumentReadFailure? ContentFailure(JsonElement root, bool abandonsOperation = false)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("error", out var error))
        {
            return null;
        }

        var codes = new List<string?> { CodeOf(error) };
        if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("innererror", out var inner))
        {
            codes.Add(CodeOf(inner));
        }

        if (error.ValueKind == JsonValueKind.Object
            && error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
        {
            foreach (var detail in details.EnumerateArray())
            {
                codes.Add(CodeOf(detail));
                if (detail.ValueKind == JsonValueKind.Object && detail.TryGetProperty("innererror", out var detailInner))
                {
                    codes.Add(CodeOf(detailInner));
                }
            }
        }

        if (codes.Contains("InvalidContentDimensions"))
        {
            return DocumentReadFailure.Terminal(DocumentReadFailure.UnsupportedImageDimensions, abandonsOperation);
        }

        if (codes.Contains("InvalidContentLength"))
        {
            return DocumentReadFailure.Terminal(DocumentReadFailure.ResultLimitExceeded, abandonsOperation);
        }

        if (codes.Contains("InvalidContent") || codes.Contains("UnsupportedContent"))
        {
            return DocumentReadFailure.Terminal(DocumentReadFailure.UnreadableContent, abandonsOperation);
        }

        return null;
    }

    private static string? CodeOf(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
            ? code.GetString()
            : null;

    // The text of each analysed page: its lines in the provider's reading order, one per line.
    private static List<ReadPageResult> PagesOf(JsonElement root)
    {
        if (!root.TryGetProperty("analyzeResult", out var result) || result.ValueKind != JsonValueKind.Object
            || !result.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
        {
            throw DocumentReadFailure.Terminal(DocumentReadFailure.ProviderProtocol, abandonsOperation: true);
        }

        var read = new List<ReadPageResult>();
        foreach (var page in pages.EnumerateArray())
        {
            if (!page.TryGetProperty("pageNumber", out var number) || !number.TryGetInt32(out var pageNumber) || pageNumber < 1)
            {
                throw DocumentReadFailure.Terminal(DocumentReadFailure.ProviderProtocol, abandonsOperation: true);
            }

            var lines = page.TryGetProperty("lines", out var lineArray) && lineArray.ValueKind == JsonValueKind.Array
                ? lineArray.EnumerateArray()
                    .Select(line => line.TryGetProperty("content", out var content) ? content.GetString() : null)
                    .Where(line => line is not null)
                : [];
            // A null character cannot be stored as text and carries no meaning.
            read.Add(new ReadPageResult(
                pageNumber, DocumentTextExtractionMethod.Ocr, string.Join('\n', lines).Replace("\0", string.Empty)));
        }

        return read;
    }

    // Parses the response body straight from the network stream; the cap bounds what the parser may consume, so no
    // copy of the body is buffered first.
    private async Task<JsonDocument> ParseCappedAsync(
        HttpResponseMessage response, CancellationToken cancellationToken, long? maxBytes = null)
    {
        var cap = maxBytes ?? MaxResultBytes;
        if (response.Content.Headers.ContentLength > cap)
        {
            throw DocumentReadFailure.Terminal(DocumentReadFailure.ResultLimitExceeded, abandonsOperation: true);
        }

        try
        {
            await using var body = new LengthLimitedStream(
                await response.Content.ReadAsStreamAsync(cancellationToken), cap,
                () => DocumentReadFailure.Terminal(DocumentReadFailure.ResultLimitExceeded, abandonsOperation: true));
            return await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            throw DocumentReadFailure.Terminal(DocumentReadFailure.ProviderProtocol, abandonsOperation: true);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException
                                              || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw DocumentReadFailure.Transient(DocumentReadFailure.ProviderUnavailable);
        }
    }

    // An absolute Retry-After date is measured against the injected clock.
    private TimeSpan? RetryAfterOf(HttpResponseMessage response) =>
        response.Headers.RetryAfter is { } retryAfter
            ? retryAfter.Delta ?? (retryAfter.Date is { } date ? date - (time ?? TimeProvider.System).GetUtcNow() : null)
            : null;

    // The result ID is the last path segment of .../analyzeResults/{resultId}.
    private static string? ResultIdOf(Uri operationLocation)
    {
        var segments = operationLocation.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 && segments[^2] == "analyzeResults" ? Uri.UnescapeDataString(segments[^1]) : null;
    }
}
