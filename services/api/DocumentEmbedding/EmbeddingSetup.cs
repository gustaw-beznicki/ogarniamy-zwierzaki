namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

public static class EmbeddingSetup
{
    // The named HttpClient for Azure OpenAI.
    public const string HttpClientName = "AzureOpenAiEmbeddings";

    private static readonly TimeSpan FallbackHttpTimeout = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddDocumentEmbedding(this IServiceCollection services)
    {
        // Resolved lazily so configuration added after Program.cs (for example by the test host) is honoured.
        // Never throws: invalid settings make embedding unavailable instead of stopping the API.
        services.AddSingleton(serviceProvider => EmbeddingAvailability.Resolve(
            serviceProvider.GetRequiredService<IConfiguration>(),
            serviceProvider.GetRequiredService<IHostEnvironment>()));

        // The trusted worker boundary over embedding jobs; never used by HTTP handlers (see DocumentEmbeddingJobs).
        services.AddScoped<DocumentEmbeddingJobs>();

        // No resilience or retry handler: the worker owns retries and their schedule.
        services.AddHttpClient(HttpClientName)
            .ConfigureHttpClient((serviceProvider, client) =>
            {
                var timeout = serviceProvider.GetRequiredService<EmbeddingAvailability>().Options.HttpTimeout;
                client.Timeout = timeout > TimeSpan.Zero ? timeout : FallbackHttpTimeout;
            })
            // The client below is a singleton holding one HttpClient, so the factory never rotates its handler; recycling
            // pooled connections instead lets a changed endpoint address take effect without a restart.
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                UseCookies = false,
            });

        // With embedding unavailable the client has no credential and fails every call with provider_config; the worker
        // does not run then anyway.
        services.AddSingleton<IEmbeddingClient>(serviceProvider =>
        {
            var availability = serviceProvider.GetRequiredService<EmbeddingAvailability>();
            return new AzureOpenAiEmbeddingClient(
                serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName),
                availability.Credential,
                availability.Options,
                serviceProvider.GetRequiredService<TimeProvider>());
        });
        services.AddSingleton<DocumentEmbeddingWakeUp>();

        // Hosted services start after Program.cs has migrated the database. The worker exits at once (with one log
        // line) unless embedding is available and Embedding:WorkerEnabled is true.
        services.AddHostedService<DocumentEmbeddingWorker>();
        return services;
    }

    // One startup line, so a deployment with embedding misconfigured says what to fix. The health check stays
    // database-only: embedding being unavailable does not make the API unhealthy.
    public static void LogEmbeddingConfiguration(this WebApplication app)
    {
        var availability = app.Services.GetRequiredService<EmbeddingAvailability>();
        var worker = availability.Options.WorkerEnabled ? "enabled" : "disabled";
        if (availability.IsAvailable)
        {
            app.Logger.LogInformation("Embedding is available: {Setup} Worker {WorkerState}.", availability.Reason, worker);
        }
        else if (availability.IsDisabled)
        {
            app.Logger.LogInformation("Embedding is disabled: {Reason} Worker {WorkerState}.", availability.Reason, worker);
        }
        else
        {
            app.Logger.LogWarning(
                "Embedding is unavailable: {Reason} Documents are still read and their originals stored, but none is embedded.",
                availability.Reason);
        }
    }
}
