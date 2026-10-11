namespace ogarniamy_zwierzaki_api.Tests;

// A fake Document Intelligence endpoint: answers the Analyze POST, then each poll in turn, then the DELETE, and records
// every request it receives. Nothing leaves the process.
public sealed class FakeDocumentIntelligence(
    Func<HttpRequestMessage, HttpResponseMessage> submit,
    IReadOnlyList<HttpResponseMessage> polls,
    Func<HttpRequestMessage, HttpResponseMessage> delete) : HttpMessageHandler
{
    private int _polls;

    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        Requests.Add(new RecordedRequest(
            request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(),
            request.Content?.Headers.ContentType?.MediaType, body));
        if (request.Method == HttpMethod.Post)
        {
            return submit(request);
        }

        return request.Method == HttpMethod.Get ? polls[_polls++] : delete(request);
    }
}
