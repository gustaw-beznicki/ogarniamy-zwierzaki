using Microsoft.EntityFrameworkCore;

namespace ogarniamy_zwierzaki_api.Data;

// Empty until the account and animal entities are added; the naming convention is set where the context is configured.
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
