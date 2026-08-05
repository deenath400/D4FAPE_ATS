namespace Ats.Api;

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Ats.Service.Auth;
using Ats.Service.Auth.Dtos;
using Ats.Service.Common;
using Ats.Shared.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async ([FromBody] RegisterRequestDto dto, IAuthService authService, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password) ||
                string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
            {
                return CreateProblemDetails(
                    StatusCodes.Status400BadRequest,
                    "auth.register.validation-failed",
                    "Validation failed",
                    "Email, password, firstName, and lastName are required.");
            }

            var result = await authService.RegisterCandidateAsync(dto, ct);
            return result.IsSuccess ? Results.Created($"/api/auth/me", result.Value) : result.ToProblemResult();
        }).AllowAnonymous();

        group.MapPost("/login", async ([FromBody] LoginRequestDto dto, IAuthService authService, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return CreateProblemDetails(
                    StatusCodes.Status400BadRequest,
                    "auth.login.invalid-input",
                    "Validation failed",
                    "Email and password are required.");
            }

            var result = await authService.AuthenticateAsync(dto, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).AllowAnonymous();

        group.MapPost("/refresh", async ([FromBody] RefreshTokenRequestDto dto, IAuthService authService, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(dto.RefreshToken))
            {
                return CreateProblemDetails(
                    StatusCodes.Status400BadRequest,
                    "auth.refresh.invalid-input",
                    "Validation failed",
                    "Refresh token is required.");
            }

            var result = await authService.RefreshTokenAsync(dto, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).AllowAnonymous();

        group.MapPost("/logout", async ([FromBody] RefreshTokenRequestDto dto, IAuthService authService, CancellationToken ct) =>
        {
            await authService.RevokeTokenAsync(dto.RefreshToken, ct);
            return Results.Ok(new { message = "Successfully logged out." });
        }).RequireAuthorization();

        group.MapGet("/me", async (ClaimsPrincipal principal, IAuthService authService, CancellationToken ct) =>
        {
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return CreateProblemDetails(
                    StatusCodes.Status401Unauthorized,
                    "auth.bearer.missing-or-invalid",
                    "Unauthorized",
                    "Authentication token is missing or invalid.");
            }

            var result = await authService.GetCurrentUserAsync(userId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemResult();
        }).RequireAuthorization();

        group.MapGet("/staff-test", () => Results.Ok(new { message = "Staff access granted." }))
            .RequireAuthorization(AuthConstants.Policies.StaffOnly);

        return app;
    }

    public static IResult ToProblemResult(this Result result)
    {
        var status = result.Status switch
        {
            ResultStatus.Validation => StatusCodes.Status400BadRequest,
            ResultStatus.Unauthorized => StatusCodes.Status401Unauthorized,
            ResultStatus.Forbidden => StatusCodes.Status403Forbidden,
            ResultStatus.NotFound => StatusCodes.Status404NotFound,
            ResultStatus.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return CreateProblemDetails(
            status,
            result.ErrorCode ?? "auth.error",
            result.Status.ToString(),
            result.ErrorMessage ?? "An authentication error occurred.",
            result.ValidationErrors);
    }

    private static IResult CreateProblemDetails(
        int statusCode,
        string code,
        string title,
        string detail,
        System.Collections.Generic.IDictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails
        {
            Type = $"https://d4fape.ats/errors/{code.Replace('.', '-')}",
            Title = title,
            Status = statusCode,
            Detail = detail
        };

        problem.Extensions["code"] = code;

        if (errors != null)
        {
            problem.Extensions["errors"] = errors;
        }

        return Results.Json(problem, statusCode: statusCode, contentType: "application/problem+json");
    }
}
