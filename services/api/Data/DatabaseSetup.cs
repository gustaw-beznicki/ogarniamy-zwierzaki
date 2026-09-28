using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace ogarniamy_zwierzaki_api.Data;

public static class DatabaseSetup
{
    private const string ConnectionStringName = "Default";

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

        var options = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var builder = new NpgsqlDataSourceBuilder(connectionString);

        switch (options.Auth)
        {
            case DatabaseAuthMode.Password:
                break;
            case DatabaseAuthMode.AzureManagedIdentity:
                UseManagedIdentityToken(builder, options);
                break;
            default:
                throw new InvalidOperationException($"Unsupported Database:Auth value '{options.Auth}'.");
        }

        return builder.Build();
    }

    private static void UseManagedIdentityToken(NpgsqlDataSourceBuilder builder, DatabaseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.EntraTokenScope)
            || options.TokenRefreshInterval <= TimeSpan.Zero
            || options.TokenRetryInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "Database:EntraTokenScope, Database:TokenRefreshInterval and Database:TokenRetryInterval " +
                "must be configured for AzureManagedIdentity.");
        }

        var credential = new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
        builder.UsePeriodicPasswordProvider(async (_, ct) =>
            (await credential.GetTokenAsync(
                new TokenRequestContext([options.EntraTokenScope]), ct)).Token,
            options.TokenRefreshInterval, options.TokenRetryInterval);
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
