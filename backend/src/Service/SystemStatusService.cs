namespace Ats.Service;

using System;
using System.Threading;
using System.Threading.Tasks;
using Ats.Db;

public sealed class SystemStatusService : ISystemStatusService
{
    private readonly IDatabaseHealthCheck _health;
    private readonly IVersionProvider _version;

    public SystemStatusService(IDatabaseHealthCheck health, IVersionProvider version)
    {
        _health = health ?? throw new ArgumentNullException(nameof(health));
        _version = version ?? throw new ArgumentNullException(nameof(version));
    }

    public async Task<SystemStatusResult> GetStatusAsync(CancellationToken ct = default)
    {
        var health = await _health.CheckAsync(ct);
        var version = _version.GetVersion();
        return new SystemStatusResult(version, health.Reachable, health.SchemaCurrent);
    }
}
