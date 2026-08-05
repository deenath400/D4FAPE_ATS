namespace Ats.Db;

using System;
using System.Globalization;
using System.IO;
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
            var cs = _db.Database.GetConnectionString();
            if (TryGetSqliteFilePath(cs, out var filePath))
            {
                if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
                {
                    return new DatabaseHealth(Reachable: false, SchemaCurrent: false);
                }
            }

            var reachable = await _db.Database.CanConnectAsync(ct);
            if (!reachable)
            {
                return new DatabaseHealth(Reachable: false, SchemaCurrent: false);
            }

            var conn = _db.Database.GetDbConnection();
            var wasOpen = conn.State == System.Data.ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync(ct);

            int appliedMigrationCount = 0;
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory';";
                var tableCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
                if (tableCount > 0)
                {
                    using var countCmd = conn.CreateCommand();
                    countCmd.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";";
                    appliedMigrationCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
                }
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }

            if (appliedMigrationCount == 0)
            {
                return new DatabaseHealth(Reachable: true, SchemaCurrent: false);
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

    private static bool TryGetSqliteFilePath(string? connectionString, out string filePath)
    {
        filePath = string.Empty;
        if (string.IsNullOrWhiteSpace(connectionString)) return false;

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 &&
                (kv[0].Trim().Equals("Data Source", StringComparison.OrdinalIgnoreCase) ||
                 kv[0].Trim().Equals("Filename", StringComparison.OrdinalIgnoreCase)))
            {
                filePath = kv[1].Trim();
                return !string.IsNullOrWhiteSpace(filePath) && !filePath.Equals(":memory:", StringComparison.OrdinalIgnoreCase);
            }
        }
        return false;
    }
}
