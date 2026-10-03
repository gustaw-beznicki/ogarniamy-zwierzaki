using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;

namespace ogarniamy_zwierzaki_api.Tests;

// Boots the API against throwaway PostgreSQL and Azurite containers; migrations and the originals container
// are created through normal startup. Each test class gets its own containers, so stored data never leaks between them.
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    // Pinned to the version used by compose.yaml; in-memory, as the data is discarded with the container.
    private readonly AzuriteContainer _azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.37.0")
        .WithInMemoryPersistence()
        .Build();

    public Task InitializeAsync() => Task.WhenAll(_postgres.StartAsync(), _azurite.StartAsync());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Added last, so it overrides any local user secrets or environment variables.
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
                ["Database:Auth"] = "Password",
                ["ConnectionStrings:Storage"] = _azurite.GetConnectionString(),
                ["Storage:Auth"] = "ConnectionString",
                ["Storage:OriginalsContainer"] = "originals",
            }));
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _azurite.DisposeAsync();
    }
}
