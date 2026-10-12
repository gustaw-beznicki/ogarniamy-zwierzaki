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

        // The trusted worker boundary over reading jobs; never used by HTTP handlers (see DocumentReadingJobs).
        services.AddScoped<DocumentReadingJobs>();

        // No resilience or retry handler: an Analyze request whose outcome is unknown must not be sent again.
        services.AddHttpClient(HttpClientName)
            .ConfigureHttpClient((serviceProvider, client) =>
            {
                var timeout = serviceProvider.GetRequiredService<DocumentReadingAvailability>().Options.HttpTimeout;
                client.Timeout = timeout > TimeSpan.Zero ? timeout : FallbackHttpTimeout;
            })
            // The adapter below is a singleton holding one client, so the factory never rotates its handler; recycling
            // pooled connections instead lets a changed endpoint address take effect without a restart.
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                UseCookies = false,
            });

        // The OCR adapter, the local PDF parser (a child process) and provider cleanup. With reading unavailable the
        // adapter has no credential and fails every call with provider_config; the worker does not run then anyway.
        services.AddSingleton<IDocumentTextReader>(serviceProvider =>
        {
            var availability = serviceProvider.GetRequiredService<DocumentReadingAvailability>();
            return new AzureDocumentTextReader(
                serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName),
                availability.Credential,
                availability.Options,
                serviceProvider.GetRequiredService<TimeProvider>());
        });
        services.AddSingleton<PdfTextReader>();
        services.AddSingleton<DocumentReadingCleanup>();
        services.AddSingleton<DocumentReadingWakeUp>();

        // Hosted services start after Program.cs has migrated the database. The worker exits at once (with one log
        // line) unless reading is available and DocumentReading:WorkerEnabled is true.
        services.AddHostedService<DocumentReadingWorker>();
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
