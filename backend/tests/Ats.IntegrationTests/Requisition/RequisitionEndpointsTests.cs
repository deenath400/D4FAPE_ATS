namespace Ats.IntegrationTests.Requisition;

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Ats.IntegrationTests;
using Ats.Service.Auth.Dtos;
using Ats.Service.Requisition.Dtos;
using Ats.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class RequisitionEndpointsTests
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

    [Fact]
    public async Task POST_requisitions_AsRecruiter_Returns201()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var token = await CreateStaffUserAndLoginAsync(factory, client, "recruiter1@example.com", AuthConstants.Roles.Recruiter);
        Authorize(client, token);

        var response = await client.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequestDto("Senior Backend Engineer", "We are looking for..."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<RequisitionDto>();
        Assert.NotNull(dto);
        Assert.Equal("draft", dto!.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task POST_requisitions_AsHiringManagerOrCandidate_Returns403(bool asHiringManager)
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var token = asHiringManager
            ? await CreateStaffUserAndLoginAsync(factory, client, "hm1@example.com", AuthConstants.Roles.HiringManager)
            : await CreateCandidateAndLoginAsync(client, "candidate1@example.com");
        Authorize(client, token);

        var response = await client.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequestDto("Title", "Description"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PUT_requisitions_AsRecruiterOnDraft_Returns200()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var token = await CreateStaffUserAndLoginAsync(factory, client, "recruiter2@example.com", AuthConstants.Roles.Recruiter);
        Authorize(client, token);

        var created = await (await client.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequestDto("Title", "Description")))
            .Content.ReadFromJsonAsync<RequisitionDto>();

        var response = await client.PutAsJsonAsync($"/api/requisitions/{created!.Id}", new UpdateRequisitionRequestDto("Updated Title", "Updated Description"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<RequisitionDto>();
        Assert.Equal("Updated Title", dto!.Title);
        Assert.Equal("draft", dto.Status);
    }

    [Fact]
    public async Task PUT_requisitions_AsRecruiterOnPublished_Returns200AndPortalReflectsIt()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var token = await CreateStaffUserAndLoginAsync(factory, client, "recruiter3@example.com", AuthConstants.Roles.Recruiter);
        Authorize(client, token);

        var created = await (await client.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequestDto("Title", "Description")))
            .Content.ReadFromJsonAsync<RequisitionDto>();
        await client.PostAsync($"/api/requisitions/{created!.Id}/publish", null);

        var putResponse = await client.PutAsJsonAsync($"/api/requisitions/{created.Id}", new UpdateRequisitionRequestDto("New Title", "New Description"));
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var portalResponse = await client.GetAsync($"/api/public/requisitions/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, portalResponse.StatusCode);
        var portalDto = await portalResponse.Content.ReadFromJsonAsync<PublicRequisitionDto>();
        Assert.Equal("New Title", portalDto!.Title);
    }

    [Fact]
    public async Task PUT_requisitions_OnClosed_Returns409()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var token = await CreateStaffUserAndLoginAsync(factory, client, "recruiter4@example.com", AuthConstants.Roles.Recruiter);
        Authorize(client, token);

        var created = await (await client.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequestDto("Title", "Description")))
            .Content.ReadFromJsonAsync<RequisitionDto>();
        await client.PostAsync($"/api/requisitions/{created!.Id}/publish", null);
        await client.PostAsync($"/api/requisitions/{created.Id}/close", null);

        var response = await client.PutAsJsonAsync($"/api/requisitions/{created.Id}", new UpdateRequisitionRequestDto("New Title", "New Description"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("requisition.update.closed", content);
    }

    [Fact]
    public async Task POST_publish_FromDraft_Returns200()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var token = await CreateStaffUserAndLoginAsync(factory, client, "recruiter5@example.com", AuthConstants.Roles.Recruiter);
        Authorize(client, token);

        var created = await (await client.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequestDto("Title", "Description")))
            .Content.ReadFromJsonAsync<RequisitionDto>();

        var response = await client.PostAsync($"/api/requisitions/{created!.Id}/publish", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<RequisitionDto>();
        Assert.Equal("published", dto!.Status);
    }

    [Fact]
    public async Task POST_unpublish_FromPublished_Returns200AndPortalDetail404s()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var token = await CreateStaffUserAndLoginAsync(factory, client, "recruiter6@example.com", AuthConstants.Roles.Recruiter);
        Authorize(client, token);

        var created = await (await client.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequestDto("Title", "Description")))
            .Content.ReadFromJsonAsync<RequisitionDto>();
        await client.PostAsync($"/api/requisitions/{created!.Id}/publish", null);

        var response = await client.PostAsync($"/api/requisitions/{created.Id}/unpublish", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<RequisitionDto>();
        Assert.Equal("draft", dto!.Status);

        var portalResponse = await client.GetAsync($"/api/public/requisitions/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, portalResponse.StatusCode);
    }

    [Fact]
    public async Task POST_publish_AfterUnpublishAndEdit_Returns200WithEditedContent()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var token = await CreateStaffUserAndLoginAsync(factory, client, "recruiter7@example.com", AuthConstants.Roles.Recruiter);
        Authorize(client, token);

        var created = await (await client.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequestDto("Title", "Description")))
            .Content.ReadFromJsonAsync<RequisitionDto>();
        await client.PostAsync($"/api/requisitions/{created!.Id}/publish", null);
        await client.PostAsync($"/api/requisitions/{created.Id}/unpublish", null);
        await client.PutAsJsonAsync($"/api/requisitions/{created.Id}", new UpdateRequisitionRequestDto("Edited Title", "Edited Description"));

        var response = await client.PostAsync($"/api/requisitions/{created.Id}/publish", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<RequisitionDto>();
        Assert.Equal("published", dto!.Status);
        Assert.Equal("Edited Title", dto.Title);
    }

    [Fact]
    public async Task POST_close_FromPublished_Returns200AndPortalRemovesIt()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var token = await CreateStaffUserAndLoginAsync(factory, client, "recruiter8@example.com", AuthConstants.Roles.Recruiter);
        Authorize(client, token);

        var created = await (await client.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequestDto("Title", "Description")))
            .Content.ReadFromJsonAsync<RequisitionDto>();
        await client.PostAsync($"/api/requisitions/{created!.Id}/publish", null);

        var response = await client.PostAsync($"/api/requisitions/{created.Id}/close", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<RequisitionDto>();
        Assert.Equal("closed", dto!.Status);

        var portalResponse = await client.GetAsync($"/api/public/requisitions/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, portalResponse.StatusCode);
    }

    [Fact]
    public async Task POST_close_FromDraft_Returns409()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var token = await CreateStaffUserAndLoginAsync(factory, client, "recruiter9@example.com", AuthConstants.Roles.Recruiter);
        Authorize(client, token);

        var created = await (await client.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequestDto("Title", "Description")))
            .Content.ReadFromJsonAsync<RequisitionDto>();

        var response = await client.PostAsync($"/api/requisitions/{created!.Id}/close", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("requisition.close.invalid-transition", content);
    }

    [Fact]
    public async Task POST_publishOrUnpublish_FromClosed_Returns409()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var token = await CreateStaffUserAndLoginAsync(factory, client, "recruiter10@example.com", AuthConstants.Roles.Recruiter);
        Authorize(client, token);

        var created = await (await client.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequestDto("Title", "Description")))
            .Content.ReadFromJsonAsync<RequisitionDto>();
        await client.PostAsync($"/api/requisitions/{created!.Id}/publish", null);
        await client.PostAsync($"/api/requisitions/{created.Id}/close", null);

        var publishResponse = await client.PostAsync($"/api/requisitions/{created.Id}/publish", null);
        var unpublishResponse = await client.PostAsync($"/api/requisitions/{created.Id}/unpublish", null);

        Assert.Equal(HttpStatusCode.Conflict, publishResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, unpublishResponse.StatusCode);
    }

    [Fact]
    public async Task GET_requisitions_AsHiringManager_Returns200AllStatuses()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter11@example.com", AuthConstants.Roles.Recruiter);
        Authorize(client, recruiterToken);

        var draft = await (await client.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequestDto("Draft Role", "Description")))
            .Content.ReadFromJsonAsync<RequisitionDto>();
        var published = await (await client.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequestDto("Published Role", "Description")))
            .Content.ReadFromJsonAsync<RequisitionDto>();
        await client.PostAsync($"/api/requisitions/{published!.Id}/publish", null);

        var hmToken = await CreateStaffUserAndLoginAsync(factory, client, "hm2@example.com", AuthConstants.Roles.HiringManager);
        Authorize(client, hmToken);

        var response = await client.GetAsync("/api/requisitions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<RequisitionDto[]>();
        Assert.NotNull(list);
        Assert.Contains(list, r => r.Id == draft!.Id && r.Status == "draft");
        Assert.Contains(list, r => r.Id == published.Id && r.Status == "published");
    }

    [Theory]
    [InlineData("GET", "/api/requisitions")]
    [InlineData("POST", "/api/requisitions")]
    public async Task ANY_requisitionsEndpoint_AsCandidate_Returns403(string method, string path)
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var token = await CreateCandidateAndLoginAsync(client, "candidate2@example.com");
        Authorize(client, token);

        var response = method == "GET"
            ? await client.GetAsync(path)
            : await client.PostAsJsonAsync(path, new CreateRequisitionRequestDto("Title", "Description"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
