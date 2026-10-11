using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;

namespace ogarniamy_zwierzaki_api.Tests;

// A scripted Document Intelligence endpoint for worker and cleanup tests. Each Analyze POST gets a new result ID
// (IdOf(1), IdOf(2), ...; unique across instances, as real result IDs are) and its analysis is decided at submission time by Analyze; polls answer from that decision
// (after RunningPolls "running" answers); DELETE answers with Delete. Every request is recorded. Nothing leaves the
// process.
public sealed class FakeOcrProvider : HttpMessageHandler
{
    public const string Endpoint = "https://di-test.cognitiveservices.azure.com/";

    private readonly Dictionary<string, Func<HttpResponseMessage>> _results = [];
    private readonly Dictionary<string, int> _pollCounts = [];
    private readonly object _gate = new();
    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];
    private int _submissions;

    public List<RecordedRequest> Requests { get; } = [];

    // Called for every POST before anything else; may throw to simulate a crash or a lost response.
    public Func<RecordedRequest, int, HttpResponseMessage?>? BeforeSubmit { get; set; }

    // The analysis of a submission (its 1-based index): the result body to answer polls with.
    public Func<RecordedRequest, int, string> Analyze { get; set; } = (_, index) => Succeeded((1, $"OCR text {index}"));

    // Called for every poll before the scripted answer; may throw, block or return its own response.
    public Func<string, Task<HttpResponseMessage?>>? BeforePoll { get; set; }

    public int RunningPolls { get; set; }

    public Func<string, HttpResponseMessage> Delete { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NoContent);

    public IEnumerable<RecordedRequest> Posts => Snapshot().Where(r => r.Method == HttpMethod.Post);

    public IEnumerable<RecordedRequest> Polls => Snapshot().Where(r => r.Method == HttpMethod.Get);

    public IEnumerable<string> DeletedIds => Snapshot().Where(r => r.Method == HttpMethod.Delete).Select(r => ResultIdOf(r.Uri));

    public string IdOf(int submission) => $"op-{submission}-{_instance}";

    public static string? PagesOf(RecordedRequest request) => HttpUtility.ParseQueryString(request.Uri.Query)["pages"];

    public static string ResultIdOf(Uri uri) => Uri.UnescapeDataString(uri.AbsolutePath.Split('/')[^1]);

    public static string Succeeded(params (int Page, string Text)[] pages) => JsonSerializer.Serialize(new
    {
        status = "succeeded",
        analyzeResult = new
        {
            content = string.Join("\n", pages.Select(p => p.Text)),
            pages = pages.Select(p => new
            {
                pageNumber = p.Page,
                lines = p.Text.Length == 0 ? [] : p.Text.Split('\n').Select(line => new { content = line }).ToArray(),
            }),
        },
    });

    public static string Failed(string code, string? innerCode = null) => JsonSerializer.Serialize(new
    {
        status = "failed",
        error = new
        {
            code,
            message = "synthetic provider message",
            innererror = innerCode is null ? null : new { code = innerCode, message = "synthetic inner message" },
        },
    });

    public static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Error(HttpStatusCode status, string code, string? innerCode = null, TimeSpan? retryAfter = null)
    {
        var response = Json(status, JsonSerializer.Serialize(new
        {
            error = new
            {
                code,
                message = "synthetic provider message",
                innererror = innerCode is null ? null : new { code = innerCode, message = "synthetic inner message" },
            },
        }));
        if (retryAfter is { } delay)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(delay);
        }

        return response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var recorded = new RecordedRequest(
            request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(),
            request.Content?.Headers.ContentType?.MediaType, body);
        lock (_gate)
        {
            Requests.Add(recorded);
        }

        if (request.Method == HttpMethod.Post)
        {
            int index;
            lock (_gate)
            {
                index = ++_submissions;
            }

            if (BeforeSubmit?.Invoke(recorded, index) is { } early)
            {
                return early;
            }

            var resultBody = Analyze(recorded, index);
            var id = IdOf(index);
            lock (_gate)
            {
                _results[id] = () => Json(HttpStatusCode.OK, resultBody);
            }

            var accepted = new HttpResponseMessage(HttpStatusCode.Accepted);
            accepted.Headers.Add(
                "Operation-Location",
                $"{Endpoint}documentintelligence/documentModels/prebuilt-read/analyzeResults/{id}?api-version=2024-11-30");
            return accepted;
        }

        var resultId = ResultIdOf(request.RequestUri!);
        if (request.Method == HttpMethod.Delete)
        {
            return Delete(resultId);
        }

        if (BeforePoll is not null && await BeforePoll(resultId) is { } custom)
        {
            return custom;
        }

        Func<HttpResponseMessage>? result;
        int polls;
        lock (_gate)
        {
            _results.TryGetValue(resultId, out result);
            polls = _pollCounts[resultId] = _pollCounts.GetValueOrDefault(resultId) + 1;
        }

        if (result is null)
        {
            return Error(HttpStatusCode.NotFound, "NotFound");
        }

        return polls <= RunningPolls ? Json(HttpStatusCode.OK, """{"status":"running"}""") : result();
    }

    private List<RecordedRequest> Snapshot()
    {
        lock (_gate)
        {
            return [.. Requests];
        }
    }
}
