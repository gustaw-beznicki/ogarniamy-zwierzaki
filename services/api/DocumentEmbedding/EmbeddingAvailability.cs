using Azure.Core;
using Azure.Identity;

namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// Whether embedding can call the provider, resolved once from configuration. Misconfiguration never stops the API:
// embedding becomes unavailable with a reason that names the setting to fix, and reading and storing keep working.
public sealed class EmbeddingAvailability
{
    private EmbeddingAvailability(
        EmbeddingOptions options, bool isAvailable, bool isDisabled, string reason, TokenCredential? credential)
    {
        Options = options;
        IsAvailable = isAvailable;
        IsDisabled = isDisabled;
        Reason = reason;
        Credential = credential;
    }

    // The bound settings, or the defaults when the section could not be bound.
    public EmbeddingOptions Options { get; }

    public bool IsAvailable { get; }

    // Turned off on purpose (Embedding:Auth is Disabled), as opposed to misconfigured.
    public bool IsDisabled { get; }

    // Why embedding is unavailable, or a short description of the active setup. Never contains secrets.
    public string Reason { get; }

    // Set only when embedding is available.
    public TokenCredential? Credential { get; }

    public static EmbeddingAvailability Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        EmbeddingOptions options;
        try
        {
            options = configuration.GetSection(EmbeddingOptions.SectionName).Get<EmbeddingOptions>() ?? new EmbeddingOptions();
        }
        catch (InvalidOperationException)
        {
            // An unknown enum value or an unparsable URI/timespan; the binder's message may echo the raw value.
            return Unavailable(new EmbeddingOptions(),
                $"the {EmbeddingOptions.SectionName} section has an invalid value; check Auth " +
                $"({string.Join(", ", Enum.GetNames<EmbeddingAuthMode>())}), Endpoint, Dimensions, WorkerEnabled and HttpTimeout.");
        }

        return Resolve(options, environment.IsDevelopment());
    }

    public static EmbeddingAvailability Resolve(EmbeddingOptions options, bool isDevelopment)
    {
        if (options.Auth == EmbeddingAuthMode.Disabled)
        {
            return new EmbeddingAvailability(options, false, true, "Embedding:Auth is Disabled.", null);
        }

        if (options.Auth is not (EmbeddingAuthMode.AzureManagedIdentity or EmbeddingAuthMode.DeveloperCredential))
        {
            return Unavailable(options, $"unsupported Embedding:Auth value '{options.Auth}'.");
        }

        if (options.Auth == EmbeddingAuthMode.DeveloperCredential && !isDevelopment)
        {
            return Unavailable(options,
                $"Embedding:Auth '{EmbeddingAuthMode.DeveloperCredential}' is allowed only in the Development " +
                $"environment; configure '{EmbeddingAuthMode.AzureManagedIdentity}'.");
        }

        if (options.Endpoint is not { IsAbsoluteUri: true, Scheme: "https" })
        {
            return Unavailable(options,
                "Embedding:Endpoint must be an absolute https URI, for example https://<name>.openai.azure.com/.");
        }

        if (string.IsNullOrWhiteSpace(options.Deployment) || string.IsNullOrWhiteSpace(options.TokenScope))
        {
            return Unavailable(options, "Embedding:Deployment and Embedding:TokenScope must be set.");
        }

        if (options.Dimensions != EmbeddingOptions.StoredDimensions)
        {
            return Unavailable(options,
                $"Embedding:Dimensions must be {EmbeddingOptions.StoredDimensions}, the size of the stored vector column.");
        }

        if (options.HttpTimeout <= TimeSpan.Zero)
        {
            return Unavailable(options, "Embedding:HttpTimeout must be a positive hh:mm:ss value.");
        }

        TokenCredential credential = options.Auth == EmbeddingAuthMode.AzureManagedIdentity
            ? new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)
            : new AzureCliCredential();
        return new EmbeddingAvailability(options, true, false,
            $"{options.Auth} against {options.Endpoint.Host}, deployment {options.Deployment}, {options.Dimensions} dimensions.",
            credential);
    }

    private static EmbeddingAvailability Unavailable(EmbeddingOptions options, string reason) =>
        new(options, false, false, reason, null);
}
