namespace Ats.IntegrationTests.Screening;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Ats.IntegrationTests;
using Ats.Service.Application.Dtos;
using Ats.Service.Auth.Dtos;
using Ats.Service.Pipeline.Dtos;
using Ats.Service.Requisition.Dtos;
using Ats.Service.Screening.Dtos;
using Ats.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class ScreeningEndpointsTests
{
    private const string Password = "Password123!";
    private const string PdfContent = "%PDF-1.4 sample candidate resume with c# and dot net experience";

    private static async Task<string> CreateStaffUserAndLoginAsync(
        CustomWebApplicationFactory factory, HttpClient client, string email, string role)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            FirstName = "Staff",
            LastName = "Member",
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, Password);
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors));

        var roleResult = await userManager.AddToRoleAsync(user, role);
        Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors));

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto(email, Password));
        var authDto = await loginResp.Content.ReadFromJsonAsync<AuthResponseDto>();
        return authDto!.AccessToken;
    }

    private static async Task<string> CreateCandidateAndLoginAsync(HttpClient client, string email)
    {
        var regResp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto(
            email, Password, "Test", "Candidate"));
        Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto(email, Password));
        var authDto = await loginResp.Content.ReadFromJsonAsync<AuthResponseDto>();
        return authDto!.AccessToken;
    }

    private static async Task<(Guid RequisitionId, Guid ApplicationId)> CreateRequisitionAndApplicationAsync(
        CustomWebApplicationFactory factory, HttpClient client, string recruiterToken, string candidateToken)
    {
        // 1. Create and publish requisition
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", recruiterToken);
        var createReqResp = await client.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequestDto(
            "Senior Backend Engineer", "Build scalable services with C# .NET and SQLite"));
        Assert.Equal(HttpStatusCode.Created, createReqResp.StatusCode);
        var reqDto = await createReqResp.Content.ReadFromJsonAsync<RequisitionDto>();
        var requisitionId = reqDto!.Id;

        var publishResp = await client.PostAsync($"/api/requisitions/{requisitionId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishResp.StatusCode);

        // 2. Submit application as candidate
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", candidateToken);
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(PdfContent));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "cv", "resume.pdf");

        var submitResp = await client.PostAsync($"/api/requisitions/{requisitionId}/applications", content);
        Assert.Equal(HttpStatusCode.Created, submitResp.StatusCode);
        var appDto = await submitResp.Content.ReadFromJsonAsync<ApplicationDto>();

        return (requisitionId, appDto!.Id);
    }

    [Fact]
    public async Task GetReport_AsStaff_Returns200WithReport()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var recruiterToken = await CreateStaffUserAndLoginAsync(
            factory, client, $"recruiter-{Guid.NewGuid():N}@example.com", AuthConstants.Roles.Recruiter);
        var candidateToken = await CreateCandidateAndLoginAsync(
            client, $"cand-{Guid.NewGuid():N}@example.com");

        var (_, applicationId) = await CreateRequisitionAndApplicationAsync(
            factory, client, recruiterToken, candidateToken);

        // Allow background screening task to complete
        await Task.Delay(300);

        // Act - Recruiter GET report
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", recruiterToken);
        var getResp = await client.GetAsync($"/api/staff/applications/{applicationId}/screening-report");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var report = await getResp.Content.ReadFromJsonAsync<ScreeningReportDto>();
        Assert.NotNull(report);
        Assert.Equal(applicationId, report.ApplicationId);
        Assert.Contains(report.Status, new[] { "Completed", "Pending", "Failed" });
    }

    [Fact]
    public async Task GetReport_AsCandidate_Returns403()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var recruiterToken = await CreateStaffUserAndLoginAsync(
            factory, client, $"recruiter-{Guid.NewGuid():N}@example.com", AuthConstants.Roles.Recruiter);
        var candidateToken = await CreateCandidateAndLoginAsync(
            client, $"cand-{Guid.NewGuid():N}@example.com");

        var (_, applicationId) = await CreateRequisitionAndApplicationAsync(
            factory, client, recruiterToken, candidateToken);

        // Act - Candidate tries to access screening report
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", candidateToken);
        var getResp = await client.GetAsync($"/api/staff/applications/{applicationId}/screening-report");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, getResp.StatusCode);
    }

    [Fact]
    public async Task ReScreen_AsRecruiter_Returns200WithUpdatedReport()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var recruiterToken = await CreateStaffUserAndLoginAsync(
            factory, client, $"recruiter-{Guid.NewGuid():N}@example.com", AuthConstants.Roles.Recruiter);
        var candidateToken = await CreateCandidateAndLoginAsync(
            client, $"cand-{Guid.NewGuid():N}@example.com");

        var (_, applicationId) = await CreateRequisitionAndApplicationAsync(
            factory, client, recruiterToken, candidateToken);

        // Wait for background screening to finish first
        await Task.Delay(300);

        // Act - Recruiter re-screens application
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", recruiterToken);
        var screenResp = await client.PostAsync($"/api/staff/applications/{applicationId}/screen", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, screenResp.StatusCode);
        var report = await screenResp.Content.ReadFromJsonAsync<ScreeningReportDto>();
        Assert.NotNull(report);
        Assert.Equal(applicationId, report.ApplicationId);
    }

    [Fact]
    public async Task ReScreen_AsHiringManager_Returns403()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var recruiterToken = await CreateStaffUserAndLoginAsync(
            factory, client, $"recruiter-{Guid.NewGuid():N}@example.com", AuthConstants.Roles.Recruiter);
        var hmToken = await CreateStaffUserAndLoginAsync(
            factory, client, $"hm-{Guid.NewGuid():N}@example.com", AuthConstants.Roles.HiringManager);
        var candidateToken = await CreateCandidateAndLoginAsync(
            client, $"cand-{Guid.NewGuid():N}@example.com");

        var (_, applicationId) = await CreateRequisitionAndApplicationAsync(
            factory, client, recruiterToken, candidateToken);

        // Act - HiringManager tries to trigger screening
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hmToken);
        var screenResp = await client.PostAsync($"/api/staff/applications/{applicationId}/screen", null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, screenResp.StatusCode);
    }

    [Fact]
    public async Task ReScreen_AsCandidate_Returns403()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var recruiterToken = await CreateStaffUserAndLoginAsync(
            factory, client, $"recruiter-{Guid.NewGuid():N}@example.com", AuthConstants.Roles.Recruiter);
        var candidateToken = await CreateCandidateAndLoginAsync(
            client, $"cand-{Guid.NewGuid():N}@example.com");

        var (_, applicationId) = await CreateRequisitionAndApplicationAsync(
            factory, client, recruiterToken, candidateToken);

        // Act - Candidate tries to trigger screening
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", candidateToken);
        var screenResp = await client.PostAsync($"/api/staff/applications/{applicationId}/screen", null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, screenResp.StatusCode);
    }

    [Fact]
    public async Task ListMine_NoScreeningFieldsExposed()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var recruiterToken = await CreateStaffUserAndLoginAsync(
            factory, client, $"recruiter-{Guid.NewGuid():N}@example.com", AuthConstants.Roles.Recruiter);
        var candidateToken = await CreateCandidateAndLoginAsync(
            client, $"cand-{Guid.NewGuid():N}@example.com");

        var (_, applicationId) = await CreateRequisitionAndApplicationAsync(
            factory, client, recruiterToken, candidateToken);

        // Act - Candidate lists own applications
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", candidateToken);
        var resp = await client.GetAsync("/api/applications/mine");

        // Assert
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var rawJson = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("screeningScore", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("screeningRecommendation", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("screeningReport", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetReport_NonExistentApplication_Returns404()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var recruiterToken = await CreateStaffUserAndLoginAsync(
            factory, client, $"recruiter-{Guid.NewGuid():N}@example.com", AuthConstants.Roles.Recruiter);

        // Act
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", recruiterToken);
        var getResp = await client.GetAsync($"/api/staff/applications/{Guid.NewGuid()}/screening-report");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task PipelineBoard_IncludesScreeningBadges_ForStaff()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var recruiterToken = await CreateStaffUserAndLoginAsync(
            factory, client, $"recruiter-{Guid.NewGuid():N}@example.com", AuthConstants.Roles.Recruiter);
        var candidateToken = await CreateCandidateAndLoginAsync(
            client, $"cand-{Guid.NewGuid():N}@example.com");

        var (requisitionId, applicationId) = await CreateRequisitionAndApplicationAsync(
            factory, client, recruiterToken, candidateToken);

        // Allow background screening to complete
        await Task.Delay(300);

        // Act - Get pipeline board as recruiter
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", recruiterToken);
        var boardResp = await client.GetAsync($"/api/requisitions/{requisitionId}/pipeline");

        // Assert
        Assert.Equal(HttpStatusCode.OK, boardResp.StatusCode);
        var board = await boardResp.Content.ReadFromJsonAsync<PipelineBoardDto>();
        Assert.NotNull(board);

        // Check that stages contain the application
        var allApps = new System.Collections.Generic.List<PipelineBoardApplicationDto>();
        foreach (var stage in board.Stages)
        {
            allApps.AddRange(stage.Applications);
        }
        allApps.AddRange(board.Rejected.Applications);

        var targetApp = allApps.Find(a => a.ApplicationId == applicationId);
        Assert.NotNull(targetApp);
        Assert.NotNull(targetApp.ScreeningStatus);
    }
}
