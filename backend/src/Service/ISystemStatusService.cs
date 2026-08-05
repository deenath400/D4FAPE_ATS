namespace Ats.Service;

using System.Threading;
using System.Threading.Tasks;

public interface ISystemStatusService
{
    Task<SystemStatusResult> GetStatusAsync(CancellationToken ct = default);
}
