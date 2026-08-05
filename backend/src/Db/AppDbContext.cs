namespace Ats.Db;

using Microsoft.EntityFrameworkCore;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Intentionally no DbSet<T> members. Schema empty by design.
}
