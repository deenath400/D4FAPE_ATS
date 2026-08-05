namespace Ats.Api;

using System.Threading;
using System.Threading.Tasks;
using Ats.Service;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

public static class SystemStatusEndpoints
{
    public static IEndpointRouteBuilder MapSystemStatus(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/system/status", async (ISystemStatusService svc, CancellationToken ct) =>
        {
            var result = await svc.GetStatusAsync(ct);
            var dto = new SystemStatusDto(result.Version,
                new DatabaseStatusDto(result.DatabaseReachable, result.DatabaseSchemaCurrent));

            if (result.DatabaseReachable && result.DatabaseSchemaCurrent)
            {
                return Results.Ok(dto);
            }

            var problem = new ProblemDetails
            {
                Type = "https://d4fape.ats/errors/system-status-unavailable",
                Title = "System status degraded",
                Status = StatusCodes.Status503ServiceUnavailable,
            };
            problem.Extensions["code"] = "system.status.database-unavailable";
            problem.Extensions["version"] = dto.Version;
            problem.Extensions["database"] = dto.Database;

            return Results.Json(problem, statusCode: StatusCodes.Status503ServiceUnavailable,
                contentType: "application/problem+json");
        }).AllowAnonymous();

        return app;
    }
}
