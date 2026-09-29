using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace ogarniamy_zwierzaki_api.Data;

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
