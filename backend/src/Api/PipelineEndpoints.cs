namespace Ats.Api;

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Ats.Service.Common;
using Ats.Service.Pipeline;
using Ats.Service.Pipeline.Dtos;
using Ats.Shared.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

/// <summary>Stage configuration (endpoints 1-5), Application move/reject (6-7), staff pipeline
/// board (8) and transition history (9) — api.md §2/§3. `RecruiterOnly` guards every write;
/// `StaffOnly` guards every read reachable by a HiringManager (FR-19, FR-20).</summary>
public static class PipelineEndpoints
{
    public static IEndpointRouteBuilder MapPipelineEndpoints(this IEndpointRouteBuilder app)
    {
        var stagesGroup = app.MapGroup("/api/requisitions/{requisitionId:guid}/stages");

        stagesGroup.MapPost("/", async (
            Guid requisitionId, [FromBody] AddStageRequestDto dto, IPipelineService service, CancellationToken ct) =>
        {
            var result = await service.AddStageAsync(requisitionId, dto, ct);
            return result.IsSuccess
                ? Results.Created($"/api/requisitions/{requisitionId}/stages/{result.Value!.Id}", result.Value)
                : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.RecruiterOnly);

        stagesGroup.MapGet("/", async (
            Guid requisitionId, IPipelineService service, CancellationToken ct) =>
        {
            var result = await service.GetStagesAsync(requisitionId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.StaffOnly);

        stagesGroup.MapPut("/reorder", async (
            Guid requisitionId, [FromBody] ReorderStagesRequestDto dto, IPipelineService service, CancellationToken ct) =>
        {
            var result = await service.ReorderStagesAsync(requisitionId, dto, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.RecruiterOnly);

        stagesGroup.MapPut("/{stageId:guid}", async (
            Guid requisitionId, Guid stageId, [FromBody] RenameStageRequestDto dto, IPipelineService service, CancellationToken ct) =>
        {
            var result = await service.RenameStageAsync(requisitionId, stageId, dto, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.RecruiterOnly);

        stagesGroup.MapDelete("/{stageId:guid}", async (
            Guid requisitionId, Guid stageId, IPipelineService service, CancellationToken ct) =>
        {
            var result = await service.RemoveStageAsync(requisitionId, stageId, ct);
            return result.IsSuccess ? Results.NoContent() : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.RecruiterOnly);

        var requisitionGroup = app.MapGroup("/api/requisitions");

        requisitionGroup.MapGet("/{requisitionId:guid}/pipeline", async (
            Guid requisitionId, IPipelineService service, CancellationToken ct) =>
        {
            var result = await service.GetPipelineBoardAsync(requisitionId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.StaffOnly);

        var applicationGroup = app.MapGroup("/api/applications");

        applicationGroup.MapPost("/{applicationId:guid}/move", async (
            Guid applicationId,
            [FromBody] MoveApplicationRequestDto dto,
            ClaimsPrincipal principal,
            IPipelineService service,
            CancellationToken ct) =>
        {
            var actingUserId = GetUserId(principal);
            if (actingUserId == null)
            {
                return Result.Unauthorized(
                    "application.move.missing-claim", "Authentication token is missing or invalid.").ToProblemResult();
            }

            var result = await service.MoveApplicationAsync(applicationId, dto, actingUserId.Value, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.RecruiterOnly);

        applicationGroup.MapPost("/{applicationId:guid}/reject", async (
            Guid applicationId,
            [FromBody] RejectApplicationRequestDto dto,
            ClaimsPrincipal principal,
            IPipelineService service,
            CancellationToken ct) =>
        {
            var actingUserId = GetUserId(principal);
            if (actingUserId == null)
            {
                return Result.Unauthorized(
                    "application.reject.missing-claim", "Authentication token is missing or invalid.").ToProblemResult();
            }

            var result = await service.RejectApplicationAsync(applicationId, dto, actingUserId.Value, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.RecruiterOnly);

        applicationGroup.MapGet("/{applicationId:guid}/transitions", async (
            Guid applicationId, IPipelineService service, CancellationToken ct) =>
        {
            var result = await service.GetTransitionHistoryAsync(applicationId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.StaffOnly);

        return app;
    }

    // Duplicated from ApplicationEndpoints.GetUserId rather than extracted to a shared helper —
    // both are private to their own file, consistent with how 0004 did not extract one either
    // (LLD §4).
    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
