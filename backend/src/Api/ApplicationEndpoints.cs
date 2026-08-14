namespace Ats.Api;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Ats.Service.Application;
using Ats.Service.Application.Dtos;
using Ats.Service.Common;
using Ats.Shared.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

public static class ApplicationEndpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var requisitionGroup = app.MapGroup("/api/requisitions");

        // multipart/form-data — the only endpoint in this spec that isn't application/json
        // (api.md §1). DisableAntiforgery() because this API is stateless JWT-bearer, not
        // cookie/form-session based; ASP.NET Core's default form-binding antiforgery
        // requirement does not apply here.
        requisitionGroup.MapPost("/{requisitionId:guid}/applications", async (
            Guid requisitionId,
            IFormFile? cv,
            ClaimsPrincipal principal,
            IApplicationService service,
            CancellationToken ct) =>
        {
            var candidateId = GetUserId(principal);
            if (candidateId == null)
            {
                return Result.Unauthorized(
                    "application.submit.missing-claim", "Authentication token is missing or invalid.").ToProblemResult();
            }

            if (cv == null || cv.Length == 0)
            {
                var validation = Result<ApplicationDto>.Validation(
                    new Dictionary<string, string[]> { ["cv"] = new[] { "A CV file is required." } },
                    "application.submit.cv-required", "Validation failed.");
                return validation.ToProblemResult();
            }

            // Buffered into a seekable MemoryStream — the magic-byte check in
            // ApplicationService.SubmitAsync needs to seek, and IFormFile.OpenReadStream() is
            // forward-only (LLD §12).
            await using var buffered = new MemoryStream();
            await using (var formStream = cv.OpenReadStream())
            {
                await formStream.CopyToAsync(buffered, ct);
            }

            buffered.Position = 0;

            var result = await service.SubmitAsync(
                requisitionId, candidateId.Value, buffered, cv.FileName, cv.ContentType, cv.Length, ct);

            return result.IsSuccess
                ? Results.Created($"/api/applications/{result.Value!.Id}", result.Value)
                : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.CandidateOnly).DisableAntiforgery();

        requisitionGroup.MapGet("/{requisitionId:guid}/applications", async (
            Guid requisitionId, IApplicationService service, CancellationToken ct) =>
        {
            var result = await service.ListForRequisitionAsync(requisitionId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.StaffOnly);

        var applicationGroup = app.MapGroup("/api/applications");

        applicationGroup.MapGet("/mine", async (
            ClaimsPrincipal principal, IApplicationService service, CancellationToken ct) =>
        {
            var candidateId = GetUserId(principal);
            if (candidateId == null)
            {
                return Result.Unauthorized(
                    "application.list-mine.missing-claim", "Authentication token is missing or invalid.").ToProblemResult();
            }

            var result = await service.ListMineAsync(candidateId.Value, ct);
            return Results.Ok(result.Value);
        }).RequireAuthorization(AuthConstants.Policies.CandidateOnly);

        applicationGroup.MapGet("/{id:guid}/cv", async (
            Guid id, ClaimsPrincipal principal, IApplicationService service, CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId == null)
            {
                return Result.Unauthorized(
                    "application.cv.missing-claim", "Authentication token is missing or invalid.").ToProblemResult();
            }

            var isStaff = principal.IsInRole(AuthConstants.Roles.Recruiter) ||
                          principal.IsInRole(AuthConstants.Roles.HiringManager);

            var result = await service.GetCvAsync(id, userId.Value, isStaff, ct);
            if (!result.IsSuccess)
            {
                return result.ToProblemResult();
            }

            // Results.File sets Content-Disposition: attachment; filename="..." automatically.
            var download = result.Value!;
            return Results.File(download.Content, download.ContentType, download.FileName);
        }).RequireAuthorization();

        var staffApplicationGroup = app.MapGroup("/api/staff/applications");

        staffApplicationGroup.MapGet("/{id:guid}/screening-report", async (
            Guid id,
            Ats.Service.Screening.IScreeningOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            var result = await orchestrator.GetScreeningReportAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.StaffOnly);

        staffApplicationGroup.MapPost("/{id:guid}/screen", async (
            Guid id,
            Ats.Service.Screening.IScreeningOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            var result = await orchestrator.RunScreeningAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.RecruiterOnly);

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
