namespace Ats.IntegrationTests.Pipeline;

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Ats.IntegrationTests;
using Ats.Service.Application.Dtos;
using Ats.Service.Auth.Dtos;
using Ats.Service.Pipeline.Dtos;
using Ats.Service.Requisition.Dtos;
using Ats.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class TransitionEndpointsTests
{
    private const string Password = "Password123!";
    private const string PdfContent = "%PDF-1.4 fake pdf content for integration testing";

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
            FirstName = "Test",
            LastName = "User",
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
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto(email, Password, "Ada", "Lovelace"));
        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto(email, Password));
        var authDto = await loginResp.Content.ReadFromJsonAsync<AuthResponseDto>();
        return authDto!.AccessToken;
    }

    private static void Authorize(HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static async Task<Guid> CreatePublishedRequisitionAsync(HttpClient client, string recruiterToken, string title = "Senior Engineer")
    {
        var originalAuth = client.DefaultRequestHeaders.Authorization;
        Authorize(client, recruiterToken);

        var created = await (await client.PostAsJsonAsync(
                "/api/requisitions", new CreateRequisitionRequestDto(title, "Description")))
            .Content.ReadFromJsonAsync<RequisitionDto>();
        await client.PostAsync($"/api/requisitions/{created!.Id}/publish", null);

        client.DefaultRequestHeaders.Authorization = originalAuth;
        return created.Id;
    }

    private static async Task<StageDto[]> GetStagesAsync(HttpClient client, string recruiterToken, Guid requisitionId)
    {
        var originalAuth = client.DefaultRequestHeaders.Authorization;
        Authorize(client, recruiterToken);

        var stages = await (await client.GetAsync($"/api/requisitions/{requisitionId}/stages"))
            .Content.ReadFromJsonAsync<StageDto[]>();

        client.DefaultRequestHeaders.Authorization = originalAuth;
        return stages!;
    }

    private static MultipartFormDataContent CreatePdfFormContent()
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(PdfContent));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "cv", "resume.pdf");
        return content;
    }

    private static async Task<Guid> SubmitApplicationAsync(HttpClient client, Guid requisitionId, string candidateEmail)
    {
        var candidateToken = await CreateCandidateAndLoginAsync(client, candidateEmail);
        var originalAuth = client.DefaultRequestHeaders.Authorization;
        Authorize(client, candidateToken);

        var response = await client.PostAsync($"/api/requisitions/{requisitionId}/applications", CreatePdfFormContent());
        var dto = await response.Content.ReadFromJsonAsync<ApplicationDto>();

        client.DefaultRequestHeaders.Authorization = originalAuth;
        return dto!.Id;
    }

    [Fact]
    public async Task POST_applications_id_move_AsRecruiter_Returns200()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter1@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);
        var stages = await GetStagesAsync(client, recruiterToken, requisitionId);
        var applied = stages.Single(s => s.Name == "Applied");
        var screening = stages.Single(s => s.Name == "Screening");
        var applicationId = await SubmitApplicationAsync(client, requisitionId, "candidate1@example.com");

        Authorize(client, recruiterToken);
        var response = await client.PostAsJsonAsync(
            $"/api/applications/{applicationId}/move",
            new MoveApplicationRequestDto(screening.Id, applied.Id, "Looks strong."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ApplicationTransitionDto>();
        Assert.NotNull(dto);
        Assert.Equal(screening.Id, dto!.CurrentStageId);
        Assert.Equal("Screening", dto.CurrentStageName);
        Assert.False(dto.IsRejected);
        Assert.Equal("move", dto.Transition.Kind);
        Assert.Equal("Applied", dto.Transition.FromStageName);
        Assert.Equal("Screening", dto.Transition.ToStageName);

        // AC-12: a subsequent backward move within the same pipeline also succeeds.
        var backward = await client.PostAsJsonAsync(
            $"/api/applications/{applicationId}/move",
            new MoveApplicationRequestDto(applied.Id, screening.Id, null));
        Assert.Equal(HttpStatusCode.OK, backward.StatusCode);
        var backwardDto = await backward.Content.ReadFromJsonAsync<ApplicationTransitionDto>();
        Assert.Equal(applied.Id, backwardDto!.CurrentStageId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task POST_applications_id_move_AsHiringManagerOrCandidate_Returns403(bool asHiringManager)
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter2@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);
        var stages = await GetStagesAsync(client, recruiterToken, requisitionId);
        var applied = stages.Single(s => s.Name == "Applied");
        var screening = stages.Single(s => s.Name == "Screening");
        var applicationId = await SubmitApplicationAsync(client, requisitionId, "candidate2@example.com");

        var token = asHiringManager
            ? await CreateStaffUserAndLoginAsync(factory, client, "hm1@example.com", AuthConstants.Roles.HiringManager)
            : await CreateCandidateAndLoginAsync(client, "candidate3@example.com");
        Authorize(client, token);

        var response = await client.PostAsJsonAsync(
            $"/api/applications/{applicationId}/move",
            new MoveApplicationRequestDto(screening.Id, applied.Id, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task POST_applications_id_move_ForeignStage_Returns404()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter3@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);
        var otherRequisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken, "Other Role");
        var stages = await GetStagesAsync(client, recruiterToken, requisitionId);
        var applied = stages.Single(s => s.Name == "Applied");
        var otherStages = await GetStagesAsync(client, recruiterToken, otherRequisitionId);
        var foreignStage = otherStages.Single(s => s.Name == "Applied");
        var applicationId = await SubmitApplicationAsync(client, requisitionId, "candidate4@example.com");

        Authorize(client, recruiterToken);
        var response = await client.PostAsJsonAsync(
            $"/api/applications/{applicationId}/move",
            new MoveApplicationRequestDto(foreignStage.Id, applied.Id, null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("application.move.not-found", content);
    }

    [Fact]
    public async Task POST_applications_id_move_StaleExpected_Returns409WithActualStage()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter4@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);
        var stages = await GetStagesAsync(client, recruiterToken, requisitionId);
        var applied = stages.Single(s => s.Name == "Applied");
        var screening = stages.Single(s => s.Name == "Screening");
        var interview = stages.Single(s => s.Name == "Interview");
        var applicationId = await SubmitApplicationAsync(client, requisitionId, "candidate5@example.com");

        Authorize(client, recruiterToken);
        var firstMove = await client.PostAsJsonAsync(
            $"/api/applications/{applicationId}/move", new MoveApplicationRequestDto(screening.Id, applied.Id, null));
        Assert.Equal(HttpStatusCode.OK, firstMove.StatusCode);

        var response = await client.PostAsJsonAsync(
            $"/api/applications/{applicationId}/move", new MoveApplicationRequestDto(interview.Id, applied.Id, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("application.move.conflict", content);
        Assert.Contains(screening.Id.ToString(), content);
        Assert.Contains("Screening", content);
    }

    [Fact]
    public async Task POST_applications_id_reject_Returns200()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter5@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);
        var applicationId = await SubmitApplicationAsync(client, requisitionId, "candidate6@example.com");

        Authorize(client, recruiterToken);
        var response = await client.PostAsJsonAsync(
            $"/api/applications/{applicationId}/reject", new RejectApplicationRequestDto("Not enough depth."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ApplicationTransitionDto>();
        Assert.NotNull(dto);
        Assert.True(dto!.IsRejected);
        Assert.Equal("reject", dto.Transition.Kind);
        Assert.Null(dto.Transition.ToStageId);
    }

    [Fact]
    public async Task POST_applications_id_reject_Twice_SecondReturns409()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter6@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);
        var applicationId = await SubmitApplicationAsync(client, requisitionId, "candidate7@example.com");

        Authorize(client, recruiterToken);
        var first = await client.PostAsJsonAsync(
            $"/api/applications/{applicationId}/reject", new RejectApplicationRequestDto(null));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync(
            $"/api/applications/{applicationId}/reject", new RejectApplicationRequestDto(null));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var content = await second.Content.ReadAsStringAsync();
        Assert.Contains("application.reject.already-rejected", content);
    }

    [Fact]
    public async Task GET_requisitions_id_pipeline_GroupsAndCounts()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter7@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);
        var applicationId1 = await SubmitApplicationAsync(client, requisitionId, "candidate8@example.com");
        var applicationId2 = await SubmitApplicationAsync(client, requisitionId, "candidate9@example.com");

        Authorize(client, recruiterToken);
        var rejectResponse = await client.PostAsJsonAsync(
            $"/api/applications/{applicationId2}/reject", new RejectApplicationRequestDto(null));
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

        var response = await client.GetAsync($"/api/requisitions/{requisitionId}/pipeline");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var board = await response.Content.ReadFromJsonAsync<PipelineBoardDto>();
        Assert.NotNull(board);
        Assert.Equal(4, board!.Stages.Count);
        var appliedGroup = board.Stages.Single(s => s.StageName == "Applied");
        Assert.Equal(1, appliedGroup.Count);
        Assert.Equal(applicationId1, appliedGroup.Applications.Single().ApplicationId);
        Assert.Equal(1, board.Rejected.Count);
        Assert.Equal(applicationId2, board.Rejected.Applications.Single().ApplicationId);
    }

    [Fact]
    public async Task GET_requisitions_id_pipeline_ZeroApplications_EveryStageZeroCount()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter14@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);

        Authorize(client, recruiterToken);
        var response = await client.GetAsync($"/api/requisitions/{requisitionId}/pipeline");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var board = await response.Content.ReadFromJsonAsync<PipelineBoardDto>();
        Assert.NotNull(board);
        Assert.Equal(4, board!.Stages.Count);
        Assert.All(board.Stages, s => Assert.Equal(0, s.Count));
        Assert.Empty(board.Rejected.Applications);
    }

    [Fact]
    public async Task GET_requisitions_id_pipeline_AsCandidate_Returns403()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter8@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);

        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate10@example.com");
        Authorize(client, candidateToken);

        var response = await client.GetAsync($"/api/requisitions/{requisitionId}/pipeline");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GET_requisitions_id_pipeline_AsHiringManager_Returns200()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter9@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);

        var hmToken = await CreateStaffUserAndLoginAsync(factory, client, "hm2@example.com", AuthConstants.Roles.HiringManager);
        Authorize(client, hmToken);

        var response = await client.GetAsync($"/api/requisitions/{requisitionId}/pipeline");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GET_applications_id_transitions_ChronologicalOrder()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter10@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);
        var stages = await GetStagesAsync(client, recruiterToken, requisitionId);
        var applied = stages.Single(s => s.Name == "Applied");
        var screening = stages.Single(s => s.Name == "Screening");
        var interview = stages.Single(s => s.Name == "Interview");
        var applicationId = await SubmitApplicationAsync(client, requisitionId, "candidate11@example.com");

        Authorize(client, recruiterToken);
        await client.PostAsJsonAsync(
            $"/api/applications/{applicationId}/move", new MoveApplicationRequestDto(screening.Id, applied.Id, null));
        await client.PostAsJsonAsync(
            $"/api/applications/{applicationId}/move", new MoveApplicationRequestDto(interview.Id, screening.Id, null));

        var response = await client.GetAsync($"/api/applications/{applicationId}/transitions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transitions = await response.Content.ReadFromJsonAsync<StageTransitionDto[]>();
        Assert.NotNull(transitions);
        Assert.Equal(2, transitions!.Length);
        Assert.Equal("Screening", transitions[0].ToStageName);
        Assert.Equal("Interview", transitions[1].ToStageName);
    }

    [Fact]
    public async Task GET_applications_id_transitions_NoTransitions_Returns200EmptyList()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter11@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);
        var applicationId = await SubmitApplicationAsync(client, requisitionId, "candidate12@example.com");

        Authorize(client, recruiterToken);
        var response = await client.GetAsync($"/api/applications/{applicationId}/transitions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transitions = await response.Content.ReadFromJsonAsync<StageTransitionDto[]>();
        Assert.NotNull(transitions);
        Assert.Empty(transitions!);
    }

    [Fact]
    public async Task GET_applications_id_transitions_AsCandidate_Returns403()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter12@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);
        var applicationId = await SubmitApplicationAsync(client, requisitionId, "candidate13@example.com");

        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate14@example.com");
        Authorize(client, candidateToken);

        var response = await client.GetAsync($"/api/applications/{applicationId}/transitions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ANY_transitionEndpoint_OnClosedRequisition_Returns409()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter13@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);
        var stages = await GetStagesAsync(client, recruiterToken, requisitionId);
        var applied = stages.Single(s => s.Name == "Applied");
        var screening = stages.Single(s => s.Name == "Screening");
        var applicationId = await SubmitApplicationAsync(client, requisitionId, "candidate15@example.com");

        Authorize(client, recruiterToken);
        await client.PostAsync($"/api/requisitions/{requisitionId}/close", null);

        var response = await client.PostAsJsonAsync(
            $"/api/applications/{applicationId}/move", new MoveApplicationRequestDto(screening.Id, applied.Id, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("application.move.requisition-closed", content);
    }
}
