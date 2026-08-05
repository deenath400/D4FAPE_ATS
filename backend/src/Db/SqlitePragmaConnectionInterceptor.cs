namespace Ats.Db;

using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

public sealed class SqlitePragmaConnectionInterceptor : DbConnectionInterceptor
{
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        EnsureDirectoryExists(connection);
        return base.ConnectionOpening(connection, eventData, result);
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        EnsureDirectoryExists(connection);
        return base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        ExecutePragmas(connection);
    }

    public override void ConnectionOpened(
        DbConnection connection, ConnectionEndEventData eventData)
    {
        base.ConnectionOpened(connection, eventData);
        ExecutePragmas(connection);
    }

    private static void EnsureDirectoryExists(DbConnection connection)
    {
        if (connection is SqliteConnection sqliteConn)
        {
            var builder = new SqliteConnectionStringBuilder(sqliteConn.ConnectionString);
            if (!string.IsNullOrWhiteSpace(builder.DataSource) && !builder.DataSource.Equals(":memory:", System.StringComparison.OrdinalIgnoreCase))
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
        }
    }

    private static void ExecutePragmas(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();
    }
}
