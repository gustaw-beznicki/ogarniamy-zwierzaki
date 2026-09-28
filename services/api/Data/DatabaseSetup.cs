using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace ogarniamy_zwierzaki_api.Data;

// How the API authenticates to PostgreSQL. Selected by the Database:Auth setting.
public enum DatabaseAuthMode
{
    // The password is part of ConnectionStrings:Default (local, tests, self-hosted).
    Password,

    // The password is a periodically refreshed Entra token of the App Service managed identity.
    AzureManagedIdentity,
}

public static class DatabaseSetup
{
    private const string ConnectionStringName = "Default";
    private const string EntraTokenScope = "https://ossrdbms-aad.database.windows.net/.default";

    public static IServiceCollection AddAppDatabase(this IServiceCollection services)
    {
        // Resolved lazily so configuration added after Program.cs (for example by the test host) is honoured.
        services.AddSingleton(serviceProvider => CreateDataSource(serviceProvider.GetRequiredService<IConfiguration>()));
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            ConfigureContext(options, serviceProvider.GetRequiredService<NpgsqlDataSource>()));
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
        return services;
    }

    // Applies pending migrations before the app serves requests; a failure stops the app.
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
    }

    internal static void ConfigureContext(DbContextOptionsBuilder options, NpgsqlDataSource dataSource) =>
        options.UseNpgsql(dataSource).UseSnakeCaseNamingConvention();

    private static NpgsqlDataSource CreateDataSource(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string 'ConnectionStrings:{ConnectionStringName}' is not configured.");
        }

        var authMode = configuration.GetValue("Database:Auth", DatabaseAuthMode.Password);
        var builder = new NpgsqlDataSourceBuilder(connectionString);

        switch (authMode)
        {
            case DatabaseAuthMode.Password:
                break;
            case DatabaseAuthMode.AzureManagedIdentity:
                var credential = new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
                builder.UsePeriodicPasswordProvider(async (_, ct) =>
                    (await credential.GetTokenAsync(
                        new TokenRequestContext([EntraTokenScope]), ct)).Token,
                    TimeSpan.FromMinutes(55), TimeSpan.FromSeconds(5));
                break;
            default:
                throw new InvalidOperationException($"Unsupported Database:Auth value '{authMode}'.");
        }

        return builder.Build();
    }
}

// Used only by `dotnet ef`. Reads ConnectionStrings:Default from user secrets or the environment (Password mode);
// without one, a host-only placeholder is enough to scaffold migrations, which need no live database.
public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<DesignTimeAppDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("Default") ?? "Host=localhost;Database=ogarniamy";

        var dataSource = new NpgsqlDataSourceBuilder(connectionString).Build();
        var options = new DbContextOptionsBuilder<AppDbContext>();
        DatabaseSetup.ConfigureContext(options, dataSource);
        return new AppDbContext(options.Options);
    }
}
