using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Pgvector.Npgsql;

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

        // The migration may have just created the vector extension; a pooled data source that loaded its type map
        // before that does not know the vector type yet, so reload it once.
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await connection.ReloadTypesAsync();
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    internal static void ConfigureContext(DbContextOptionsBuilder options, NpgsqlDataSource dataSource) =>
        options.UseNpgsql(dataSource, npgsql => npgsql.UseVector()).UseSnakeCaseNamingConvention();

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
        builder.UseVector();

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
