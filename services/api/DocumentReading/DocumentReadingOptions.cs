namespace ogarniamy_zwierzaki_api.DocumentReading;

// The "DocumentReading" configuration section.
public sealed class DocumentReadingOptions
{
    public const string SectionName = "DocumentReading";

    public DocumentReadingAuthMode Auth { get; init; } = DocumentReadingAuthMode.Disabled;

    // The resource's custom-subdomain endpoint, for example https://<name>.cognitiveservices.azure.com/. Not a secret.
    public Uri? Endpoint { get; init; }

    public string ModelId { get; init; } = "prebuilt-read";

    public string ApiVersion { get; init; } = "2024-11-30";

    public string TokenScope { get; init; } = "https://cognitiveservices.azure.com/.default";

    // Whether the background reading worker runs. Separate from Auth, so a configured endpoint does not start
    // processing by itself.
    public bool WorkerEnabled { get; init; }

    // Per HTTP call to the provider. There is no automatic retry: a replayed Analyze request could submit twice.
    public TimeSpan HttpTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
