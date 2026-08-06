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
using Ats.Service.Auth.Dtos;
using Ats.Service.Pipeline.Dtos;
using Ats.Service.Requisition.Dtos;
using Ats.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class StageEndpointsTests
{
    private const string Password = "Password123!";

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

    private static async Task<Guid> CreateDraftRequisitionAsync(HttpClient client, string recruiterToken, string title = "Senior Engineer")
    {
        var originalAuth = client.DefaultRequestHeaders.Authorization;
        Authorize(client, recruiterToken);

        var created = await (await client.PostAsJsonAsync(
                "/api/requisitions", new CreateRequisitionRequestDto(title, "Description")))
            .Content.ReadFromJsonAsync<RequisitionDto>();

        client.DefaultRequestHeaders.Authorization = originalAuth;
        return created!.Id;
    }

    [Fact]
    public async Task POST_stages_AsRecruiter_Returns201()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter1@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreateDraftRequisitionAsync(client, recruiterToken);
        Authorize(client, recruiterToken);

        var response = await client.PostAsJsonAsync(
            $"/api/requisitions/{requisitionId}/stages", new AddStageRequestDto("Technical Screen", 1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<StageDto>();
        Assert.NotNull(dto);
        Assert.Equal("Technical Screen", dto!.Name);
        Assert.Equal(1, dto.SortOrder);
        Assert.Contains($"/api/requisitions/{requisitionId}/stages/{dto.Id}", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task POST_stages_AsHiringManagerOrCandidate_Returns403(bool asHiringManager)
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter2@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreateDraftRequisitionAsync(client, recruiterToken);

        var token = asHiringManager
            ? await CreateStaffUserAndLoginAsync(factory, client, "hm1@example.com", AuthConstants.Roles.HiringManager)
            : await CreateCandidateAndLoginAsync(client, "candidate1@example.com");
        Authorize(client, token);

        var response = await client.PostAsJsonAsync(
            $"/api/requisitions/{requisitionId}/stages", new AddStageRequestDto("Technical Screen", null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GET_stages_AsStaff_ReturnsPipelineOrder()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter3@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreateDraftRequisitionAsync(client, recruiterToken);
        Authorize(client, recruiterToken);

        var response = await client.GetAsync($"/api/requisitions/{requisitionId}/stages");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stages = await response.Content.ReadFromJsonAsync<StageDto[]>();
        Assert.NotNull(stages);
        // FR-5: every Requisition is created with the default 4-Stage set, in pipeline order.
        Assert.Equal(new[] { "Applied", "Screening", "Interview", "Offer" }, stages!.Select(s => s.Name));
    }

    [Fact]
    public async Task GET_stages_MissingRequisition_Returns404()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter4@example.com", AuthConstants.Roles.Recruiter);
        Authorize(client, recruiterToken);

        var response = await client.GetAsync($"/api/requisitions/{Guid.NewGuid()}/stages");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("stage.list.requisition-not-found", content);
    }

    [Fact]
    public async Task PUT_stages_id_DuplicateName_Returns409()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter5@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreateDraftRequisitionAsync(client, recruiterToken);
        Authorize(client, recruiterToken);
        var stages = await (await client.GetAsync($"/api/requisitions/{requisitionId}/stages"))
            .Content.ReadFromJsonAsync<StageDto[]>();
        var screening = stages!.Single(s => s.Name == "Screening");

        var response = await client.PutAsJsonAsync(
            $"/api/requisitions/{requisitionId}/stages/{screening.Id}", new RenameStageRequestDto("applied"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("stage.rename.duplicate-name", content);
    }

    [Fact]
    public async Task PUT_stages_reorder_ReturnsNewOrder()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter6@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreateDraftRequisitionAsync(client, recruiterToken);
        Authorize(client, recruiterToken);
        var stages = await (await client.GetAsync($"/api/requisitions/{requisitionId}/stages"))
            .Content.ReadFromJsonAsync<StageDto[]>();
        var newOrder = stages!.OrderByDescending(s => s.SortOrder).Select(s => s.Id).ToArray();

        var response = await client.PutAsJsonAsync(
            $"/api/requisitions/{requisitionId}/stages/reorder", new ReorderStagesRequestDto(newOrder));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reordered = await response.Content.ReadFromJsonAsync<StageDto[]>();
        Assert.Equal(newOrder, reordered!.Select(s => s.Id));
        Assert.Equal(new[] { "Offer", "Interview", "Screening", "Applied" }, reordered!.Select(s => s.Name));

        // AC-4: a subsequent GET reflects the new order.
        var listed = await (await client.GetAsync($"/api/requisitions/{requisitionId}/stages"))
            .Content.ReadFromJsonAsync<StageDto[]>();
        Assert.Equal(new[] { "Offer", "Interview", "Screening", "Applied" }, listed!.Select(s => s.Name));
    }

    [Fact]
    public async Task PUT_stages_reorder_MismatchedSet_Returns400()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter7@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreateDraftRequisitionAsync(client, recruiterToken);
        Authorize(client, recruiterToken);

        var response = await client.PutAsJsonAsync(
            $"/api/requisitions/{requisitionId}/stages/reorder", new ReorderStagesRequestDto(new[] { Guid.NewGuid() }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("stage.reorder.invalid-set", content);
    }

    [Fact]
    public async Task DELETE_stages_id_Unoccupied_Returns204()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter8@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreateDraftRequisitionAsync(client, recruiterToken);
        Authorize(client, recruiterToken);
        var stages = await (await client.GetAsync($"/api/requisitions/{requisitionId}/stages"))
            .Content.ReadFromJsonAsync<StageDto[]>();
        var offer = stages!.Single(s => s.Name == "Offer");

        var response = await client.DeleteAsync($"/api/requisitions/{requisitionId}/stages/{offer.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var remaining = await (await client.GetAsync($"/api/requisitions/{requisitionId}/stages"))
            .Content.ReadFromJsonAsync<StageDto[]>();
        Assert.DoesNotContain(remaining!, s => s.Id == offer.Id);
    }

    [Fact]
    public async Task DELETE_stages_id_Occupied_Returns409()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter9@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreateDraftRequisitionAsync(client, recruiterToken);
        Authorize(client, recruiterToken);
        var stages = await (await client.GetAsync($"/api/requisitions/{requisitionId}/stages"))
            .Content.ReadFromJsonAsync<StageDto[]>();
        var applied = stages!.Single(s => s.Name == "Applied");
        await client.PostAsync($"/api/requisitions/{requisitionId}/publish", null);

        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate2@example.com");
        Authorize(client, candidateToken);
        using var pdfContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4 fake"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        pdfContent.Add(fileContent, "cv", "resume.pdf");
        var submitResponse = await client.PostAsync($"/api/requisitions/{requisitionId}/applications", pdfContent);
        Assert.Equal(HttpStatusCode.Created, submitResponse.StatusCode);

        Authorize(client, recruiterToken);
        var response = await client.DeleteAsync($"/api/requisitions/{requisitionId}/stages/{applied.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("stage.remove.occupied", content);
    }

    [Fact]
    public async Task ANY_stagesEndpoint_OnClosedRequisition_Returns409()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter10@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreateDraftRequisitionAsync(client, recruiterToken);
        Authorize(client, recruiterToken);
        await client.PostAsync($"/api/requisitions/{requisitionId}/publish", null);
        await client.PostAsync($"/api/requisitions/{requisitionId}/close", null);

        var response = await client.PostAsJsonAsync(
            $"/api/requisitions/{requisitionId}/stages", new AddStageRequestDto("Extra Stage", null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("stage.add.requisition-closed", content);
    }

    [Fact]
    public async Task POST_stages_MissingRequisition_Returns404()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter11@example.com", AuthConstants.Roles.Recruiter);
        Authorize(client, recruiterToken);

        var response = await client.PostAsJsonAsync(
            $"/api/requisitions/{Guid.NewGuid()}/stages", new AddStageRequestDto("Applied", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("stage.add.requisition-not-found", content);
    }

    [Fact]
    public async Task POST_stages_Anonymous_Returns401()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter12@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreateDraftRequisitionAsync(client, recruiterToken);

        var response = await client.PostAsJsonAsync(
            $"/api/requisitions/{requisitionId}/stages", new AddStageRequestDto("Applied", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
