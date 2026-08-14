namespace Ats.Service.Screening;

using System;
using System.Threading;
using System.Threading.Tasks;
using Ats.Service.Common;
using Ats.Service.Screening.Dtos;

public interface IScreeningOrchestrator
{
    Task<Result<ScreeningReportDto>> RunScreeningAsync(Guid applicationId, CancellationToken ct = default);

    Task<Result<ScreeningReportDto>> GetScreeningReportAsync(Guid applicationId, CancellationToken ct = default);
}
