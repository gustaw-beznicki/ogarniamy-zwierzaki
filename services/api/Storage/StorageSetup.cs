using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace ogarniamy_zwierzaki_api.Storage;

public static class StorageSetup
{
    private const string ConnectionStringName = "Storage";

    public static IServiceCollection AddOriginalStorage(this IServiceCollection services)
    {
        services.AddOptions<StorageOptions>().BindConfiguration(StorageOptions.SectionName);
        // Resolved lazily so configuration added after Program.cs (for example by the test host) is honoured.
        services.AddSingleton(serviceProvider => CreateContainerClient(
            serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value,
            serviceProvider.GetRequiredService<IConfiguration>(),
            serviceProvider.GetRequiredService<IHostEnvironment>()));
        services.AddSingleton<IOriginalStorage, BlobOriginalStorage>();
        return services;
    }

    // Validates the storage configuration before the app serves requests; invalid configuration stops the app.
    // With the emulator, also creates the private originals container. Azure provisions it through Bicep, and the
    // API's container-scoped role cannot create containers.
    public static async Task PrepareOriginalStorageAsync(this WebApplication app)
    {
        var container = app.Services.GetRequiredService<BlobContainerClient>();
        var options = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
        if (options.Auth == StorageAuthMode.ConnectionString)
        {
            await container.CreateIfNotExistsAsync(PublicAccessType.None);
        }
    }

    private static BlobContainerClient CreateContainerClient(
        StorageOptions options, IConfiguration configuration, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(options.OriginalsContainer))
        {
            throw new InvalidOperationException("Storage:OriginalsContainer is not configured.");
        }

        var clientOptions = new BlobClientOptions();
        // Bounded retries keep a failing storage call well inside the 45-second Static Web Apps proxy window.
        clientOptions.Retry.MaxRetries = 2;
        clientOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(15);

        var service = options.Auth switch
        {
            StorageAuthMode.ConnectionString => CreateEmulatorClient(configuration, environment, clientOptions),
            StorageAuthMode.AzureManagedIdentity => CreateManagedIdentityClient(options, clientOptions),
            _ => throw new InvalidOperationException($"Unsupported Storage:Auth value '{options.Auth}'."),
        };
        return service.GetBlobContainerClient(options.OriginalsContainer);
    }

    // Development only, so a deployed API with missing storage settings fails at startup instead of using an emulator.
    private static BlobServiceClient CreateEmulatorClient(
        IConfiguration configuration, IHostEnvironment environment, BlobClientOptions clientOptions)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"Storage:Auth '{StorageAuthMode.ConnectionString}' is allowed only in the Development environment; " +
                $"configure '{StorageAuthMode.AzureManagedIdentity}' with Storage:BlobServiceUri.");
        }

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string 'ConnectionStrings:{ConnectionStringName}' is not configured.");
        }

        return new BlobServiceClient(connectionString, clientOptions);
    }

    private static BlobServiceClient CreateManagedIdentityClient(StorageOptions options, BlobClientOptions clientOptions)
    {
        if (options.BlobServiceUri is not { IsAbsoluteUri: true, Scheme: "https" })
        {
            throw new InvalidOperationException(
                "Storage:BlobServiceUri must be an absolute https URI for AzureManagedIdentity.");
        }

        return new BlobServiceClient(
            options.BlobServiceUri, new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned), clientOptions);
    }
}
