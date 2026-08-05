namespace Ats.Db;

using System.Threading;
using System.Threading.Tasks;

public sealed record DatabaseHealth(bool Reachable, bool SchemaCurrent);

public interface IDatabaseHealthCheck
{
    Task<DatabaseHealth> CheckAsync(CancellationToken ct = default);
}
