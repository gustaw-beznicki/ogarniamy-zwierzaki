using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;

namespace ogarniamy_zwierzaki_api.DocumentReading;

// Development-only command that sends local files to Document Intelligence prebuilt-read and requests deletion of
// each analysis result: `dotnet run --project services/api -- ocr-probe [--print-text] <file>...`.
// It measures what the provider recovers, without the database or Blob Storage. It writes only to the console:
// extracted text is printed only with --print-text and never logged or saved. Ordinary tests use a fake handler.
public sealed class OcrProbe(HttpClient http, TokenCredential credential, DocumentReadingOptions options, TextWriter output)
{
    public const string Command = "ocr-probe";
    public const string PrintTextFlag = "--print-text";

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".pdf"] = "application/pdf",
    };

    // Used when the provider sends no Retry-After header.
    public TimeSpan DefaultPollInterval { get; init; } = TimeSpan.FromSeconds(1);

    // Upper bound on waiting for one analysis; the result is still deleted when it runs out.
    public TimeSpan MaxWait { get; init; } = TimeSpan.FromMinutes(2);

    public static bool IsRequested(string[] args) => args.Length > 0 && args[0] == Command;

    // Entry point from Program.cs, before the database and storage are configured. Returns the process exit code.
    public static async Task<int> RunCommandAsync(
        string[] args, IConfiguration configuration, IHostEnvironment environment, TextWriter output, TextWriter error)
    {
        var arguments = args.Skip(1).ToList();
        var printText = arguments.Remove(PrintTextFlag);
        if (arguments.Count == 0)
        {
            await error.WriteLineAsync($"Usage: dotnet run --project services/api -- {Command} [{PrintTextFlag}] <file>...");
            return 2;
        }

        if (!environment.IsDevelopment())
        {
            await error.WriteLineAsync($"{Command} runs only in the Development environment.");
            return 2;
        }

        var availability = DocumentReadingAvailability.Resolve(configuration, environment);
        if (!availability.IsAvailable || availability.Credential is null)
        {
            await error.WriteLineAsync($"Document reading is unavailable: {availability.Reason}");
            await error.WriteLineAsync(
                "Set DocumentReading:Auth to DeveloperCredential and DocumentReading:Endpoint with `dotnet user-secrets set " +
                "<key> <value> --project services/api`, then `az login` with an account that holds Cognitive Services User " +
                "on the resource. See docs/local-development.md.");
            return 2;
        }

        // A plain client: no retry handler and no HTTP logging of provider calls.
        using var http = new HttpClient { Timeout = availability.Options.HttpTimeout };
        var probe = new OcrProbe(http, availability.Credential, availability.Options, output);
        return await probe.RunAsync(arguments, printText, CancellationToken.None);
    }

    // Probes the files one by one, in order. Returns 0 when every file was analysed and its result deletion acknowledged.
    public async Task<int> RunAsync(IReadOnlyList<string> files, bool printText, CancellationToken cancellationToken)
    {
        if (printText)
        {
            await output.WriteLineAsync(
                "WARNING: extracted text is printed below. Do not commit it, paste it into notes or save it to the repository.");
        }

        var allSucceeded = true;
        for (var index = 0; index < files.Count; index++)
        {
            await output.WriteLineAsync($"[{index + 1}/{files.Count}]");
            allSucceeded &= await ProbeFileAsync(files[index], printText, cancellationToken);
        }

        await output.WriteLineAsync(allSucceeded ? "Probe finished: all files read." : "Probe finished with failures.");
        return allSucceeded ? 0 : 1;
    }

    private async Task<bool> ProbeFileAsync(string path, bool printText, CancellationToken cancellationToken)
    {
        // The file name is not printed, so the output can be summarised without identifying the source document.
        var extension = Path.GetExtension(path);
        if (!ContentTypes.TryGetValue(extension, out var contentType))
        {
            await output.WriteLineAsync($"  skipped: unsupported file type '{extension}' (use .jpg, .jpeg, .png or .pdf)");
            return false;
        }

        if (!File.Exists(path))
        {
            await output.WriteLineAsync("  skipped: file not found");
            return false;
        }

        var bytes = await ReadOriginalAsync(path, cancellationToken);
        var hashBefore = Convert.ToHexStringLower(SHA256.HashData(bytes));
        await output.WriteLineAsync($"  file: {extension.ToLowerInvariant()}, {bytes.Length} bytes, sha256 {hashBefore[..12]}...");

        string token;
        try
        {
            token = (await credential.GetTokenAsync(new TokenRequestContext([options.TokenScope]), cancellationToken)).Token;
        }
        catch (AuthenticationFailedException exception)
        {
            // The exception text is not printed: credential diagnostics can include account details.
            await output.WriteLineAsync(
                $"  token: {exception.GetType().Name}; run `az login` with an account that holds Cognitive Services User");
            return false;
        }

        var stopwatch = Stopwatch.StartNew();
        var succeeded = false;
        string? resultId = null;
        try
        {
            var operationLocation = await SubmitAsync(bytes, contentType, token, cancellationToken);
            if (operationLocation is not null)
            {
                resultId = ResultIdOf(operationLocation);
                if (resultId is null)
                {
                    await output.WriteLineAsync("  analysis: unexpected Operation-Location; result cannot be deleted early");
                }
                else
                {
                    succeeded = await PollAsync(operationLocation, token, printText, stopwatch, cancellationToken);
                }
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            await output.WriteLineAsync($"  analysis: failed ({exception.GetType().Name})");
        }
        finally
        {
            // Always attempt deletion once the provider has a result id, whether the analysis succeeded or not.
            if (resultId is not null)
            {
                succeeded &= await DeleteResultAsync(resultId, token);
            }
        }

        var hashAfter = Convert.ToHexStringLower(SHA256.HashData(await ReadOriginalAsync(path, CancellationToken.None)));
        var unchanged = hashAfter == hashBefore;
        await output.WriteLineAsync($"  original unchanged: {(unchanged ? "yes" : "NO")}");
        return succeeded && unchanged;
    }

    private static async Task<byte[]> ReadOriginalAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    // Sends the Analyze request exactly once. Any failure, including a timeout whose outcome is unknown, is reported
    // and not retried: a replay could create a second provider operation that is never deleted early.
    private async Task<Uri?> SubmitAsync(byte[] bytes, string contentType, string token, CancellationToken cancellationToken)
    {
        var uri = new Uri(
            $"{BaseUri}/documentModels/{Uri.EscapeDataString(options.ModelId)}:analyze" +
            $"?api-version={Uri.EscapeDataString(options.ApiVersion)}");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            await output.WriteLineAsync(
                $"  analysis: submission failed ({exception.GetType().Name}); outcome unknown, not retried");
            return null;
        }

        using (response)
        {
            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                await output.WriteLineAsync(
                    $"  analysis: submission rejected with {(int)response.StatusCode}{await ErrorCodeOfAsync(response)}");
                return null;
            }

            if (response.Headers.TryGetValues("Operation-Location", out var values)
                && Uri.TryCreate(values.FirstOrDefault(), UriKind.Absolute, out var operationLocation))
            {
                return operationLocation;
            }

            await output.WriteLineAsync("  analysis: accepted without an Operation-Location header");
            return null;
        }
    }

    private async Task<bool> PollAsync(
        Uri operationLocation, string token, bool printText, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, operationLocation);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                await output.WriteLineAsync(
                    $"  analysis: result retrieval failed with {(int)response.StatusCode}{await ErrorCodeOfAsync(response)}");
                return false;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            var status = document.RootElement.TryGetProperty("status", out var value) ? value.GetString() : null;
            switch (status)
            {
                case "succeeded":
                    await WriteSummaryAsync(document.RootElement, stopwatch.Elapsed, printText);
                    return true;
                case "notStarted" or "running":
                    break;
                default:
                    await output.WriteLineAsync(
                        $"  analysis: {status ?? "unknown status"} after {Seconds(stopwatch.Elapsed)}{ErrorCodeOf(document.RootElement)}");
                    return false;
            }

            var delay = response.Headers.RetryAfter?.Delta ?? DefaultPollInterval;
            if (stopwatch.Elapsed + delay > MaxWait)
            {
                await output.WriteLineAsync($"  analysis: still {status} after {Seconds(stopwatch.Elapsed)}; stopped waiting");
                return false;
            }

            await Task.Delay(delay, cancellationToken);
        }
    }

    // Counts only; the content itself is printed solely on request and never stored.
    private async Task WriteSummaryAsync(JsonElement root, TimeSpan elapsed, bool printText)
    {
        var result = root.TryGetProperty("analyzeResult", out var analyzeResult) ? analyzeResult : default;
        int pages = 0, lines = 0, words = 0;
        double confidenceSum = 0;
        var confidenceCount = 0;
        if (result.ValueKind == JsonValueKind.Object
            && result.TryGetProperty("pages", out var pageArray) && pageArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var page in pageArray.EnumerateArray())
            {
                pages++;
                lines += CountOf(page, "lines");
                if (page.TryGetProperty("words", out var wordArray) && wordArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var word in wordArray.EnumerateArray())
                    {
                        words++;
                        if (word.TryGetProperty("confidence", out var confidence) && confidence.ValueKind == JsonValueKind.Number)
                        {
                            confidenceSum += confidence.GetDouble();
                            confidenceCount++;
                        }
                    }
                }
            }
        }

        var content = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("content", out var contentValue)
            ? contentValue.GetString() ?? string.Empty
            : string.Empty;
        var averageConfidence = confidenceCount > 0
            ? (confidenceSum / confidenceCount).ToString("0.000", CultureInfo.InvariantCulture)
            : "n/a";
        await output.WriteLineAsync($"  analysis: succeeded in {Seconds(elapsed)}");
        await output.WriteLineAsync(
            $"  pages: {pages}, lines: {lines}, words: {words}, characters: {content.Length}, " +
            $"average word confidence: {averageConfidence}");
        if (printText)
        {
            await output.WriteLineAsync("  ----- extracted text (do not commit) -----");
            await output.WriteLineAsync(content);
            await output.WriteLineAsync("  ----- end of extracted text -----");
        }
    }

    // A 204 means the provider accepted the deletion request; it is not proof that the data is physically erased.
    private async Task<bool> DeleteResultAsync(string resultId, string token)
    {
        var uri = new Uri(
            $"{BaseUri}/documentModels/{Uri.EscapeDataString(options.ModelId)}/analyzeResults/{Uri.EscapeDataString(resultId)}" +
            $"?api-version={Uri.EscapeDataString(options.ApiVersion)}");
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await http.SendAsync(request, CancellationToken.None);
            var acknowledged = response.StatusCode == HttpStatusCode.NoContent;
            await output.WriteLineAsync(acknowledged
                ? "  result deletion: 204 acknowledged (a deletion request, not verified erasure)"
                : $"  result deletion: failed with {(int)response.StatusCode}{await ErrorCodeOfAsync(response)}; the provider keeps it up to 24 hours");
            return acknowledged;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            await output.WriteLineAsync(
                $"  result deletion: failed ({exception.GetType().Name}); the provider keeps it up to 24 hours");
            return false;
        }
    }

    private string BaseUri => $"{options.Endpoint!.AbsoluteUri.TrimEnd('/')}/documentintelligence";

    // The result id is the last path segment of .../analyzeResults/{resultId}.
    private static string? ResultIdOf(Uri operationLocation)
    {
        var segments = operationLocation.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 && segments[^2] == "analyzeResults" ? Uri.UnescapeDataString(segments[^1]) : null;
    }

    private static int CountOf(JsonElement element, string property) =>
        element.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array ? array.GetArrayLength() : 0;

    // Only the provider's error code is shown; its message can quote request details.
    private static async Task<string> ErrorCodeOfAsync(HttpResponseMessage response)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return ErrorCodeOf(document.RootElement);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string ErrorCodeOf(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object
        && error.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
            ? $" ({code.GetString()})"
            : string.Empty;

    private static string Seconds(TimeSpan elapsed) =>
        $"{elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s";
}
