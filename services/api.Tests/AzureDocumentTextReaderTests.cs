using System.Net;
using System.Text;
using ogarniamy_zwierzaki_api.DocumentReading;

namespace ogarniamy_zwierzaki_api.Tests;

// The OCR adapter's response limits and result-not-found handling, against the scripted provider.
public sealed class AzureDocumentTextReaderTests
{
    [Fact]
    public async Task A_result_beyond_the_cap_fails_for_good_even_without_a_content_length()
    {
        var body = FakeOcrProvider.Succeeded((1, new string('x', 4_000)));
        var provider = new FakeOcrProvider { BeforePoll = _ => Task.FromResult<HttpResponseMessage?>(Unsized(body)) };

        var failure = await Assert.ThrowsAsync<DocumentReadFailure>(
            () => Reader(provider, maxResultBytes: 1_024).GetAnalysisAsync("op-large", CancellationToken.None));

        Assert.Equal(DocumentReadFailure.ResultLimitExceeded, failure.Code);
        Assert.False(failure.IsTransient);
        Assert.True(failure.AbandonsOperation);
    }

    [Fact]
    public async Task A_result_within_the_cap_is_parsed_from_the_stream()
    {
        var body = FakeOcrProvider.Succeeded((1, "Line one\nLine two"), (2, ""));
        var provider = new FakeOcrProvider { BeforePoll = _ => Task.FromResult<HttpResponseMessage?>(Unsized(body)) };

        var analysis = await Reader(provider, maxResultBytes: body.Length).GetAnalysisAsync("op-small", CancellationToken.None);

        Assert.True(analysis.IsComplete);
        Assert.Equal([(1, "Line one\nLine two"), (2, "")], analysis.Pages.Select(p => (p.SourcePageNumber, p.Text)));
    }

    [Fact]
    public async Task A_deleted_result_reported_with_the_top_level_not_found_body_counts_as_deleted()
    {
        // The body the real service returns for a result that does not exist (observed 2026-10-11).
        var provider = new FakeOcrProvider
        {
            Delete = _ => FakeOcrProvider.Json(HttpStatusCode.NotFound, """{"code":"NotFound","message":"Analyze result does not exist."}"""),
        };

        await Reader(provider).DeleteResultAsync("op-gone", CancellationToken.None);

        Assert.Equal(["op-gone"], provider.DeletedIds);
    }

    [Fact]
    public async Task An_absolute_retry_after_date_is_measured_against_the_injected_clock()
    {
        var now = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var provider = new FakeOcrProvider
        {
            BeforePoll = _ =>
            {
                var response = FakeOcrProvider.Error(HttpStatusCode.ServiceUnavailable, "ServiceUnavailable");
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(now.AddMinutes(7));
                return Task.FromResult<HttpResponseMessage?>(response);
            },
        };
        var reader = new AzureDocumentTextReader(
            new HttpClient(provider), new FakeTokenCredential(),
            new DocumentReadingOptions { Auth = DocumentReadingAuthMode.AzureManagedIdentity, Endpoint = new Uri(FakeOcrProvider.Endpoint) },
            new FixedTimeProvider(now));

        var failure = await Assert.ThrowsAsync<DocumentReadFailure>(
            () => reader.GetAnalysisAsync("op-throttled", CancellationToken.None));

        Assert.True(failure.IsTransient);
        Assert.Equal(TimeSpan.FromMinutes(7), failure.RetryAfter);
    }

    private static AzureDocumentTextReader Reader(FakeOcrProvider provider, long maxResultBytes = 64L * 1024 * 1024) =>
        new(new HttpClient(provider), new FakeTokenCredential(),
            new DocumentReadingOptions { Auth = DocumentReadingAuthMode.AzureManagedIdentity, Endpoint = new Uri(FakeOcrProvider.Endpoint) })
        {
            MaxResultBytes = maxResultBytes,
        };

    // A chunked response: the body stream is not seekable, so no Content-Length is sent and only the streaming cap applies.
    private static HttpResponseMessage Unsized(string json)
    {
        var content = new StreamContent(new LengthLimitedStream(
            new MemoryStream(Encoding.UTF8.GetBytes(json)), long.MaxValue, () => new InvalidOperationException()));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        Assert.Null(content.Headers.ContentLength);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }
}
