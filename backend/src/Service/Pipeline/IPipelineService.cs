namespace Ats.Service.Pipeline;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ats.Service.Common;
using Ats.Service.Pipeline.Dtos;

/// <summary>
/// Stage configuration and Application pipeline progression (FR-1..FR-16). <c>actingUserId</c> is
/// a plain <see cref="Guid"/>, resolved from <c>ClaimsPrincipal</c> at the API layer exactly as
/// <c>ApplicationEndpoints.GetUserId</c> already does — this contract never sees a
/// <c>ClaimsPrincipal</c> (layering rule #3/#4).
/// </summary>
public interface IPipelineService
{
    Task<Result<StageDto>> AddStageAsync(Guid requisitionId, AddStageRequestDto dto, CancellationToken ct = default);

    Task<Result<IReadOnlyList<StageDto>>> GetStagesAsync(Guid requisitionId, CancellationToken ct = default);

    Task<Result<StageDto>> RenameStageAsync(
        Guid requisitionId, Guid stageId, RenameStageRequestDto dto, CancellationToken ct = default);

    Task<Result<IReadOnlyList<StageDto>>> ReorderStagesAsync(
        Guid requisitionId, ReorderStagesRequestDto dto, CancellationToken ct = default);

    Task<Result> RemoveStageAsync(Guid requisitionId, Guid stageId, CancellationToken ct = default);

    Task<Result<ApplicationTransitionDto>> MoveApplicationAsync(
        Guid applicationId, MoveApplicationRequestDto dto, Guid actingUserId, CancellationToken ct = default);

    Task<Result<ApplicationTransitionDto>> RejectApplicationAsync(
        Guid applicationId, RejectApplicationRequestDto dto, Guid actingUserId, CancellationToken ct = default);

    Task<Result<PipelineBoardDto>> GetPipelineBoardAsync(Guid requisitionId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<StageTransitionDto>>> GetTransitionHistoryAsync(
        Guid applicationId, CancellationToken ct = default);

    Task<Result> SystemMoveToNextStageAsync(
        Guid applicationId, string? note = null, CancellationToken ct = default);
}
