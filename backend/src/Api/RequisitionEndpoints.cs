namespace Ats.Api;

using System;
using System.Threading;
using System.Threading.Tasks;
using Ats.Service.Requisition;
using Ats.Service.Requisition.Dtos;
using Ats.Shared.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

public static class RequisitionEndpoints
{
    public static IEndpointRouteBuilder MapRequisitionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/requisitions");

        group.MapPost("/", async ([FromBody] CreateRequisitionRequestDto dto, IRequisitionService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(dto, ct);
            return result.IsSuccess
                ? Results.Created($"/api/requisitions/{result.Value!.Id}", result.Value)
                : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.RecruiterOnly);

        group.MapGet("/", async (IRequisitionService service, CancellationToken ct) =>
        {
            var result = await service.ListAsync(ct);
            return Results.Ok(result.Value);
        }).RequireAuthorization(AuthConstants.Policies.StaffOnly);

        group.MapGet("/{id:guid}", async (Guid id, IRequisitionService service, CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.StaffOnly);

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateRequisitionRequestDto dto, IRequisitionService service, CancellationToken ct) =>
        {
            var result = await service.UpdateContentAsync(id, dto, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.RecruiterOnly);

        group.MapPost("/{id:guid}/publish", async (Guid id, IRequisitionService service, CancellationToken ct) =>
        {
            var result = await service.PublishAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.RecruiterOnly);

        group.MapPost("/{id:guid}/unpublish", async (Guid id, IRequisitionService service, CancellationToken ct) =>
        {
            var result = await service.UnpublishAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.RecruiterOnly);

        group.MapPost("/{id:guid}/close", async (Guid id, IRequisitionService service, CancellationToken ct) =>
        {
            var result = await service.CloseAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).RequireAuthorization(AuthConstants.Policies.RecruiterOnly);

        return app;
    }
}
