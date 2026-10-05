namespace ogarniamy_zwierzaki_api.DocumentReading;

public static class DocumentReadingSetup
{
    // The named HttpClient for Azure Document Intelligence.
    public const string HttpClientName = "DocumentIntelligence";

    private static readonly TimeSpan FallbackHttpTimeout = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddDocumentReading(this IServiceCollection services)
    {
        // Resolved lazily so configuration added after Program.cs (for example by the test host) is honoured.
        // Never throws: invalid settings make reading unavailable instead of stopping the API.
        services.AddSingleton(serviceProvider => DocumentReadingAvailability.Resolve(
            serviceProvider.GetRequiredService<IConfiguration>(),
            serviceProvider.GetRequiredService<IHostEnvironment>()));

        // No resilience or retry handler: an Analyze request whose outcome is unknown must not be sent again.
        services.AddHttpClient(HttpClientName)
            .ConfigureHttpClient((serviceProvider, client) =>
            {
                var timeout = serviceProvider.GetRequiredService<DocumentReadingAvailability>().Options.HttpTimeout;
                client.Timeout = timeout > TimeSpan.Zero ? timeout : FallbackHttpTimeout;
            });
        return services;
    }

    // One startup line, so a deployment with reading misconfigured says what to fix. The health check stays
    // database-only: reading being unavailable does not make the API unhealthy.
    public static void LogDocumentReadingConfiguration(this WebApplication app)
    {
        var availability = app.Services.GetRequiredService<DocumentReadingAvailability>();
        var worker = availability.Options.WorkerEnabled ? "enabled" : "disabled";
        if (availability.IsAvailable)
        {
            app.Logger.LogInformation(
                "Document reading is available: {Setup} Worker {WorkerState}.", availability.Reason, worker);
        }
        else if (availability.IsDisabled)
        {
            app.Logger.LogInformation(
                "Document reading is disabled: {Reason} Worker {WorkerState}.", availability.Reason, worker);
        }
        else
        {
            app.Logger.LogWarning(
                "Document reading is unavailable: {Reason} Originals are still stored and opened, but no document is read.",
                availability.Reason);
        }
    }
}
