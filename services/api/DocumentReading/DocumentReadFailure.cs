namespace ogarniamy_zwierzaki_api.DocumentReading;

// Why reading a document (or one of its originals) stopped, as a sanitized code (lowercase letters, digits and
// underscores) that may be stored as the job's failure code. Never carries extracted text, file names, provider
// messages or tokens, so it is safe to log.
public sealed class DocumentReadFailure : Exception
{
    private DocumentReadFailure(
        string code, bool isTransient, bool isConfiguration, TimeSpan? retryAfter, bool abandonsOperation,
        bool canFallBackToProvider)
        : base($"Document reading failed: {code}.")
    {
        Code = code;
        IsTransient = isTransient;
        IsConfiguration = isConfiguration;
        RetryAfter = retryAfter;
        AbandonsOperation = abandonsOperation;
        CanFallBackToProvider = canFallBackToProvider;
    }

    public string Code { get; }

    // Worth another attempt later (throttling, network, provider 5xx, storage outage, timeouts).
    public bool IsTransient { get; }

    // The provider rejected the API's identity or setup (auth, endpoint, model): the job fails with an explicit code
    // and a warning diagnostic instead of retrying endlessly.
    public bool IsConfiguration { get; }

    // The provider's Retry-After, when it sent one; a longer one than the retry schedule wins.
    public TimeSpan? RetryAfter { get; }

    // The saved provider operation cannot be resumed (its analysis failed or its result is gone): the run file is
    // abandoned, keeping its cleanup obligation, and a later attempt submits again.
    public bool AbandonsOperation { get; }

    // The local PDF parser could not read the file, but the provider may: read the whole file with OCR instead.
    public bool CanFallBackToProvider { get; }

    public static DocumentReadFailure Transient(string code, TimeSpan? retryAfter = null, bool abandonsOperation = false) =>
        new(code, true, false, retryAfter, abandonsOperation, false);

    public static DocumentReadFailure Terminal(string code, bool abandonsOperation = false) =>
        new(code, false, false, null, abandonsOperation, false);

    public static DocumentReadFailure Configuration(string code) => new(code, false, true, null, false, false);

    public static DocumentReadFailure LocalParserFailed() => new(PdfUnreadable, false, false, null, false, true);

    // Sanitized failure codes stored on failed jobs.
    public const string PdfUnreadable = "pdf_unreadable";
    public const string EncryptedPdf = "encrypted_pdf";
    public const string PageLimitExceeded = "page_limit_exceeded";
    public const string TextLimitExceeded = "text_limit_exceeded";
    public const string ResultLimitExceeded = "result_limit_exceeded";
    public const string EmptyDocument = "empty_document";
    public const string UnreadableContent = "unreadable_content";
    public const string UnsupportedImageDimensions = "unsupported_image_dimensions";
    public const string ProviderRejected = "provider_rejected";
    public const string ProviderProtocol = "provider_protocol_error";
    public const string ProviderUnavailable = "provider_unavailable";
    public const string ProviderResultMissing = "provider_result_missing";
    public const string ProviderAuth = "provider_auth";
    public const string ProviderConfig = "provider_config";
    public const string StorageUnavailable = "storage_unavailable";
    public const string OriginalUnavailable = "original_unavailable";
    public const string AttemptDeadlineExceeded = "attempt_deadline_exceeded";
    public const string AttemptsExhausted = "attempts_exhausted";
    public const string UnexpectedError = "unexpected_error";
}
