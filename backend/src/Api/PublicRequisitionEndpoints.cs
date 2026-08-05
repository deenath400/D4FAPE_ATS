namespace Ats.Api;

using System;
using System.Threading;
using System.Threading.Tasks;
using Ats.Service.Requisition;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

public static class PublicRequisitionEndpoints
{
    private const int DefaultPageSize = 20;

    public static IEndpointRouteBuilder MapPublicRequisitionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/public/requisitions");

        group.MapGet("/", async (
            string? keyword,
            string? page,
            string? pageSize,
            IRequisitionService service,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var resolvedPage = 1;
            if (!string.IsNullOrWhiteSpace(page))
            {
                if (!int.TryParse(page, out resolvedPage) || resolvedPage < 1)
                {
                    return CreateProblemDetails(
                        StatusCodes.Status400BadRequest,
                        "requisition.list.invalid-page",
                        "Bad Request",
                        "page must be a positive integer.");
                }
            }

            var defaultPageSize = int.TryParse(configuration["Requisitions:DefaultPageSize"], out var configuredDefault)
                ? configuredDefault
                : DefaultPageSize;
            var resolvedPageSize = !string.IsNullOrWhiteSpace(pageSize) && int.TryParse(pageSize, out var parsedPageSize)
                ? parsedPageSize
                : defaultPageSize;

            var result = await service.SearchPublicAsync(keyword, resolvedPage, resolvedPageSize, ct);
            return Results.Ok(result.Value);
        }).AllowAnonymous();

        group.MapGet("/{id:guid}", async (Guid id, IRequisitionService service, CancellationToken ct) =>
        {
            var result = await service.GetPublicByIdAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).AllowAnonymous();

        return app;
    }

    private static IResult CreateProblemDetails(int statusCode, string code, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Type = $"https://d4fape.ats/errors/{code.Replace('.', '-')}",
            Title = title,
            Status = statusCode,
            Detail = detail
        };

        problem.Extensions["code"] = code;

        return Results.Json(problem, statusCode: statusCode, contentType: "application/problem+json");
    }
}
