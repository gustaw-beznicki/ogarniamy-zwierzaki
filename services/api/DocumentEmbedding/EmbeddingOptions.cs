namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// The "Embedding" configuration section.
public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    // The size of the stored vector column (document_chunks.embedding); the only supported Dimensions value.
    public const int StoredDimensions = 1536;

    public EmbeddingAuthMode Auth { get; init; } = EmbeddingAuthMode.Disabled;

    // The Azure OpenAI resource's custom-subdomain endpoint, for example https://<name>.openai.azure.com/. Not a secret.
    public Uri? Endpoint { get; init; }

    // The deployment name, sent as the request's "model".
    public string Deployment { get; init; } = "text-embedding-3-large";

    public int Dimensions { get; init; } = StoredDimensions;

    public string TokenScope { get; init; } = "https://cognitiveservices.azure.com/.default";

    // Whether the background embedding worker runs. Separate from Auth, so a configured endpoint does not start
    // processing by itself.
    public bool WorkerEnabled { get; init; }

    // Per HTTP call to the provider.
    public TimeSpan HttpTimeout { get; init; } = TimeSpan.FromSeconds(30);

    // The estimated tokens one provider request may carry. Keep it well below the deployment's tokens-per-minute
    // limit: the provider rejects a request whose estimate does not fit the remaining per-minute allowance.
    public int MaxEstimatedTokensPerRequest { get; init; } = 16_000;

    // The estimated tokens per minute the background worker may send; the rest of the deployment's limit is left to
    // search. 0 turns pacing off.
    public int WorkerTokensPerMinute { get; init; } = 40_000;
}
