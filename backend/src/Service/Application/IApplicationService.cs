namespace Ats.Service.Application;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ats.Service.Application.Dtos;
using Ats.Service.Common;

/// <summary>
/// Submission and read-access for Applications (FR-1..FR-13). <see cref="Stream"/> is
/// <c>System.IO.Stream</c>, not an ASP.NET Core HTTP type — the API layer extracts a plain
/// stream and primitives from the bound <c>IFormFile</c> before calling <see cref="SubmitAsync"/>
/// (LLD §3.1), keeping this contract free of <c>Microsoft.AspNetCore.Http</c>.
/// </summary>
public interface IApplicationService
{
    Task<Result<ApplicationDto>> SubmitAsync(
        Guid requisitionId, Guid candidateId,
        Stream cvContent, string cvFileName, string cvContentType, long cvSizeBytes,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<CandidateApplicationListItemDto>>> ListMineAsync(
        Guid candidateId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<StaffApplicationListItemDto>>> ListForRequisitionAsync(
        Guid requisitionId, CancellationToken ct = default);

    Task<Result<CvDownloadResult>> GetCvAsync(
        Guid applicationId, Guid requestingUserId, bool requesterIsStaff, CancellationToken ct = default);
}
