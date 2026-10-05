using System.Net;
using System.Security.Cryptography;
using System.Text;
using ogarniamy_zwierzaki_api.DocumentReading;

namespace ogarniamy_zwierzaki_api.Tests;

// The OCR probe against a fake provider: no network, no Azure and no containers.
public sealed class OcrProbeTests : IDisposable
{
    private const string Endpoint = "https://di-test.cognitiveservices.azure.com/";
    private const string ResultId = "3b1f6c0e-0000-4000-8000-000000000001";
    private const string OperationLocation =
        Endpoint + "documentintelligence/documentModels/prebuilt-read/analyzeResults/" + ResultId + "?api-version=2024-11-30";
    private const string ExtractedText = "SYNTHETIC TEST DOCUMENT Patient Rex";

    private readonly string _file = Path.Combine(Path.GetTempPath(), $"ocr-probe-{Guid.NewGuid():N}.png");
    private readonly byte[] _bytes = RandomNumberGenerator.GetBytes(4_096);

    public OcrProbeTests() => File.WriteAllBytes(_file, _bytes);

    public void Dispose() => File.Delete(_file);

    [Fact]
    public async Task Probe_submits_the_file_polls_the_result_and_then_requests_its_deletion()
    {
        var provider = new FakeDocumentIntelligence(
            _ => Accepted(),
            [Json("""{"status":"running"}"""), Json(SucceededBody)],
            _ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var output = new StringWriter();

        var exitCode = await RunAsync(provider, output, printText: false);

        Assert.Equal(0, exitCode);
        Assert.Collection(provider.Requests,
            post =>
            {
                Assert.Equal(HttpMethod.Post, post.Method);
                Assert.Equal(
                    Endpoint + "documentintelligence/documentModels/prebuilt-read:analyze?api-version=2024-11-30",
                    post.Uri.AbsoluteUri);
                Assert.Equal("image/png", post.ContentType);
                Assert.Equal("Bearer test-token", post.Authorization);
                Assert.Equal(_bytes, post.Body);
            },
            poll =>
            {
                Assert.Equal(HttpMethod.Get, poll.Method);
                Assert.Equal(OperationLocation, poll.Uri.AbsoluteUri);
                Assert.Equal("Bearer test-token", poll.Authorization);
            },
            poll => Assert.Equal(HttpMethod.Get, poll.Method),
            delete =>
            {
                Assert.Equal(HttpMethod.Delete, delete.Method);
                Assert.Equal(
                    Endpoint + $"documentintelligence/documentModels/prebuilt-read/analyzeResults/{ResultId}?api-version=2024-11-30",
                    delete.Uri.AbsoluteUri);
                Assert.Equal("Bearer test-token", delete.Authorization);
            });

        var text = output.ToString();
        Assert.Contains("pages: 1, lines: 2, words: 4, characters: 35, average word confidence: 0.900", text);
        Assert.Contains("result deletion: 204 acknowledged", text);
        Assert.Contains("original unchanged: yes", text);
        Assert.DoesNotContain(ExtractedText, text);
        Assert.DoesNotContain(Path.GetFileName(_file), text);
        Assert.Equal(_bytes, await File.ReadAllBytesAsync(_file));
    }

    [Fact]
    public async Task Probe_prints_the_extracted_text_only_when_asked()
    {
        var provider = new FakeDocumentIntelligence(
            _ => Accepted(), [Json(SucceededBody)], _ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var output = new StringWriter();

        Assert.Equal(0, await RunAsync(provider, output, printText: true));

        Assert.Contains(ExtractedText, output.ToString());
        Assert.Contains("do not commit", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Probe_requests_deletion_even_when_the_analysis_fails()
    {
        var provider = new FakeDocumentIntelligence(
            _ => Accepted(),
            [Json("""{"status":"failed","error":{"code":"InvalidRequest","message":"details"}}""")],
            _ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var output = new StringWriter();

        Assert.Equal(1, await RunAsync(provider, output, printText: false));

        Assert.Equal([HttpMethod.Post, HttpMethod.Get, HttpMethod.Delete], provider.Requests.Select(r => r.Method));
        Assert.Contains("analysis: failed", output.ToString());
        Assert.Contains("(InvalidRequest)", output.ToString());
        Assert.DoesNotContain("details", output.ToString());
    }

    [Fact]
    public async Task Probe_does_not_resubmit_after_a_server_error()
    {
        var provider = new FakeDocumentIntelligence(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable), [], _ => throw new InvalidOperationException());
        var output = new StringWriter();

        Assert.Equal(1, await RunAsync(provider, output, printText: false));

        Assert.Equal([HttpMethod.Post], provider.Requests.Select(r => r.Method));
        Assert.Contains("submission rejected with 503", output.ToString());
        Assert.Equal(_bytes, await File.ReadAllBytesAsync(_file));
    }

    [Fact]
    public async Task Probe_does_not_resubmit_after_a_timeout()
    {
        var provider = new FakeDocumentIntelligence(_ => throw new TaskCanceledException(), [], _ => throw new InvalidOperationException());
        var output = new StringWriter();

        Assert.Equal(1, await RunAsync(provider, output, printText: false));

        Assert.Equal([HttpMethod.Post], provider.Requests.Select(r => r.Method));
        Assert.Contains("outcome unknown, not retried", output.ToString());
    }

    [Fact]
    public async Task Probe_skips_unsupported_file_types_without_calling_the_provider()
    {
        var provider = new FakeDocumentIntelligence(_ => Accepted(), [], _ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var output = new StringWriter();
        using var http = new HttpClient(provider);
        var probe = new OcrProbe(http, new FakeTokenCredential(), ValidOptions(), output);

        Assert.Equal(1, await probe.RunAsync(["notes.txt"], printText: false, CancellationToken.None));

        Assert.Empty(provider.Requests);
        Assert.Contains("unsupported file type", output.ToString());
    }

    private const string SucceededBody = """
        {
          "status": "succeeded",
          "analyzeResult": {
            "content": "SYNTHETIC TEST DOCUMENT Patient Rex",
            "pages": [
              {
                "pageNumber": 1,
                "lines": [ { "content": "SYNTHETIC TEST DOCUMENT" }, { "content": "Patient Rex" } ],
                "words": [
                  { "content": "SYNTHETIC", "confidence": 0.9 },
                  { "content": "TEST", "confidence": 1.0 },
                  { "content": "DOCUMENT", "confidence": 0.8 },
                  { "content": "Patient", "confidence": 0.9 }
                ]
              }
            ]
          }
        }
        """;

    private async Task<int> RunAsync(FakeDocumentIntelligence provider, TextWriter output, bool printText)
    {
        using var http = new HttpClient(provider);
        var probe = new OcrProbe(http, new FakeTokenCredential(), ValidOptions(), output) { DefaultPollInterval = TimeSpan.Zero };
        return await probe.RunAsync([_file], printText, CancellationToken.None);
    }

    private static DocumentReadingOptions ValidOptions() => new()
    {
        Auth = DocumentReadingAuthMode.DeveloperCredential,
        Endpoint = new Uri(Endpoint),
    };

    private static HttpResponseMessage Accepted()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Accepted);
        response.Headers.Add("Operation-Location", OperationLocation);
        return response;
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
