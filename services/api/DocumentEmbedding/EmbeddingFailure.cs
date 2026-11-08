namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// Why embedding stopped, as a sanitized code (lowercase letters, digits and underscores) that may be stored as the
// job's failure code. Never carries document text, provider messages or tokens, so it is safe to log.
public sealed class EmbeddingFailure : Exception
{
    private EmbeddingFailure(string code, bool isTransient, bool isConfiguration, TimeSpan? retryAfter)
        : base($"Embedding failed: {code}.")
    {
        Code = code;
        IsTransient = isTransient;
        IsConfiguration = isConfiguration;
        RetryAfter = retryAfter;
    }

    public string Code { get; }

    // Worth another attempt later (throttling, network, provider 5xx, timeouts).
    public bool IsTransient { get; }

    // The provider rejected the API's identity or setup (auth, endpoint, deployment): the job fails with an explicit
    // code and a warning instead of retrying endlessly.
    public bool IsConfiguration { get; }

    // The provider's Retry-After, when it sent one; a longer one than the retry schedule wins.
    public TimeSpan? RetryAfter { get; }

    public static EmbeddingFailure Transient(string code, TimeSpan? retryAfter = null) => new(code, true, false, retryAfter);

    public static EmbeddingFailure Terminal(string code) => new(code, false, false, null);

    public static EmbeddingFailure Configuration(string code) => new(code, false, true, null);

    // Sanitized failure codes stored on failed jobs.
    public const string ProviderRejected = "provider_rejected";
    public const string ProviderProtocol = "provider_protocol_error";
    public const string ProviderUnavailable = "provider_unavailable";
    public const string ProviderAuth = "provider_auth";
    public const string ProviderConfig = "provider_config";
    public const string TextUnavailable = "text_unavailable";
    public const string AttemptDeadlineExceeded = "attempt_deadline_exceeded";
    public const string AttemptsExhausted = "attempts_exhausted";
    public const string UnexpectedError = "unexpected_error";
}
