using Azure.Core;
using Azure.Identity;

namespace ogarniamy_zwierzaki_api.DocumentReading;

// Whether document reading can call the provider, resolved once from configuration. Misconfiguration never stops the
// API: reading becomes unavailable with a reason that names the setting to fix, and storing originals keeps working.
public sealed class DocumentReadingAvailability
{
    private DocumentReadingAvailability(
        DocumentReadingOptions options, bool isAvailable, bool isDisabled, string reason, TokenCredential? credential)
    {
        Options = options;
        IsAvailable = isAvailable;
        IsDisabled = isDisabled;
        Reason = reason;
        Credential = credential;
    }

    // The bound settings, or the defaults when the section could not be bound.
    public DocumentReadingOptions Options { get; }

    public bool IsAvailable { get; }

    // Turned off on purpose (DocumentReading:Auth is Disabled), as opposed to misconfigured.
    public bool IsDisabled { get; }

    // Why reading is unavailable, or a short description of the active setup. Never contains secrets.
    public string Reason { get; }

    // Set only when reading is available.
    public TokenCredential? Credential { get; }

    public static DocumentReadingAvailability Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        DocumentReadingOptions options;
        try
        {
            options = configuration.GetSection(DocumentReadingOptions.SectionName).Get<DocumentReadingOptions>()
                ?? new DocumentReadingOptions();
        }
        catch (InvalidOperationException)
        {
            // An unknown enum value or an unparsable URI/timespan; the binder's message may echo the raw value.
            return Unavailable(new DocumentReadingOptions(),
                $"the {DocumentReadingOptions.SectionName} section has an invalid value; check Auth " +
                $"({string.Join(", ", Enum.GetNames<DocumentReadingAuthMode>())}), Endpoint, WorkerEnabled and HttpTimeout.");
        }

        return Resolve(options, environment.IsDevelopment());
    }

    public static DocumentReadingAvailability Resolve(DocumentReadingOptions options, bool isDevelopment)
    {
        if (options.Auth == DocumentReadingAuthMode.Disabled)
        {
            return new DocumentReadingAvailability(options, false, true, "DocumentReading:Auth is Disabled.", null);
        }

        if (options.Auth is not (DocumentReadingAuthMode.AzureManagedIdentity or DocumentReadingAuthMode.DeveloperCredential))
        {
            return Unavailable(options, $"unsupported DocumentReading:Auth value '{options.Auth}'.");
        }

        if (options.Auth == DocumentReadingAuthMode.DeveloperCredential && !isDevelopment)
        {
            return Unavailable(options,
                $"DocumentReading:Auth '{DocumentReadingAuthMode.DeveloperCredential}' is allowed only in the Development " +
                $"environment; configure '{DocumentReadingAuthMode.AzureManagedIdentity}'.");
        }

        if (options.Endpoint is not { IsAbsoluteUri: true, Scheme: "https" })
        {
            return Unavailable(options,
                "DocumentReading:Endpoint must be an absolute https URI, for example https://<name>.cognitiveservices.azure.com/.");
        }

        if (string.IsNullOrWhiteSpace(options.ModelId)
            || string.IsNullOrWhiteSpace(options.ApiVersion)
            || string.IsNullOrWhiteSpace(options.TokenScope))
        {
            return Unavailable(options, "DocumentReading:ModelId, DocumentReading:ApiVersion and DocumentReading:TokenScope must be set.");
        }

        if (options.HttpTimeout <= TimeSpan.Zero)
        {
            return Unavailable(options, "DocumentReading:HttpTimeout must be a positive hh:mm:ss value.");
        }

        TokenCredential credential = options.Auth == DocumentReadingAuthMode.AzureManagedIdentity
            ? new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)
            : new AzureCliCredential();
        return new DocumentReadingAvailability(options, true, false,
            $"{options.Auth} against {options.Endpoint.Host}, model {options.ModelId}, API {options.ApiVersion}.",
            credential);
    }

    private static DocumentReadingAvailability Unavailable(DocumentReadingOptions options, string reason) =>
        new(options, false, false, reason, null);
}
