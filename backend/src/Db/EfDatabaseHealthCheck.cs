namespace Ats.Db;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class EfDatabaseHealthCheck : IDatabaseHealthCheck
{
    private readonly AppDbContext _db;
    private readonly ILogger<EfDatabaseHealthCheck> _logger;

    public EfDatabaseHealthCheck(AppDbContext db, ILogger<EfDatabaseHealthCheck> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DatabaseHealth> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            var reachable = await _db.Database.CanConnectAsync(ct);
            if (!reachable)
            {
                return new DatabaseHealth(Reachable: false, SchemaCurrent: false);
            }

            var pendingMigrations = await _db.Database.GetPendingMigrationsAsync(ct);
            var schemaCurrent = !pendingMigrations.Any();

            return new DatabaseHealth(Reachable: true, SchemaCurrent: schemaCurrent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database health check failed.");
            return new DatabaseHealth(Reachable: false, SchemaCurrent: false);
        }
    }
}
