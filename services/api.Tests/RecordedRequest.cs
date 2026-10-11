namespace ogarniamy_zwierzaki_api.Tests;

// One request seen by FakeDocumentIntelligence, with the body read at send time.
public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string? ContentType, byte[]? Body);
