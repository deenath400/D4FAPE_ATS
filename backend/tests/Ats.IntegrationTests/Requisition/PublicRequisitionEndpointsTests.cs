namespace Ats.IntegrationTests.Requisition;

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Ats.IntegrationTests;
using Ats.Service.Requisition.Dtos;
using Ats.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class PublicRequisitionEndpointsTests
{
    private const string Password = "Password123!";

    private static async Task<HttpClient> CreateAuthorizedRecruiterClientAsync(CustomWebApplicationFactory factory, string email)
    {
        var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            FirstName = "Test",
            LastName = "Recruiter",
            CreatedAtUtc = DateTime.UtcNow
        };
        var createResult = await userManager.CreateAsync(user, Password);
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors));
        var roleResult = await userManager.AddToRoleAsync(user, AuthConstants.Roles.Recruiter);
        Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors));

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new Ats.Service.Auth.Dtos.LoginRequestDto(email, Password));
        var authDto = await loginResp.Content.ReadFromJsonAsync<Ats.Service.Auth.Dtos.AuthResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authDto!.AccessToken);

        return client;
    }

    private static async Task<Guid> CreateRequisitionAsync(HttpClient recruiterClient, string title, string description, bool publish)
    {
        var created = await (await recruiterClient.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequestDto(title, description)))
            .Content.ReadFromJsonAsync<RequisitionDto>();

        if (publish)
        {
            await recruiterClient.PostAsync($"/api/requisitions/{created!.Id}/publish", null);
        }

        return created!.Id;
    }

    [Fact]
    public async Task GET_public_requisitions_KeywordEngineer_ReturnsOnlyMatchingPublished()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var recruiterClient = await CreateAuthorizedRecruiterClientAsync(factory, "pr-recruiter1@example.com");
        using var anonymousClient = factory.CreateClient();

        await CreateRequisitionAsync(recruiterClient, "Senior Engineer", "We build backend systems.", publish: true);
        await CreateRequisitionAsync(recruiterClient, "Sales Manager", "A different kind of role.", publish: true);
        await CreateRequisitionAsync(recruiterClient, "Draft Engineer", "This one stays a draft.", publish: false);

        var response = await anonymousClient.GetAsync("/api/public/requisitions?keyword=engineer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = paged.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("Senior Engineer", items[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task GET_public_requisitions_NoMatch_Returns200EmptyList()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var recruiterClient = await CreateAuthorizedRecruiterClientAsync(factory, "pr-recruiter2@example.com");
        using var anonymousClient = factory.CreateClient();

        await CreateRequisitionAsync(recruiterClient, "Senior Engineer", "We build backend systems.", publish: true);

        var response = await anonymousClient.GetAsync("/api/public/requisitions?keyword=nonexistentkeyword");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, paged.GetProperty("items").GetArrayLength());
        Assert.Equal(0, paged.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task GET_public_requisitions_NoPageParam_ReturnsFirst20WithMetadata()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var recruiterClient = await CreateAuthorizedRecruiterClientAsync(factory, "pr-recruiter3@example.com");
        using var anonymousClient = factory.CreateClient();

        for (var i = 0; i < 25; i++)
        {
            await CreateRequisitionAsync(recruiterClient, $"Role {i}", "Description", publish: true);
        }

        var response = await anonymousClient.GetAsync("/api/public/requisitions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(20, paged.GetProperty("items").GetArrayLength());
        Assert.Equal(1, paged.GetProperty("page").GetInt32());
        Assert.Equal(20, paged.GetProperty("pageSize").GetInt32());
        Assert.Equal(25, paged.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task GET_public_requisitions_PageBeyondLast_Returns200EmptyWithTrueTotal()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var recruiterClient = await CreateAuthorizedRecruiterClientAsync(factory, "pr-recruiter4@example.com");
        using var anonymousClient = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            await CreateRequisitionAsync(recruiterClient, $"Role {i}", "Description", publish: true);
        }

        var response = await anonymousClient.GetAsync("/api/public/requisitions?page=99");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, paged.GetProperty("items").GetArrayLength());
        Assert.Equal(5, paged.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task GET_public_requisitions_KeywordAndPage_FiltersThenPaginates()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var recruiterClient = await CreateAuthorizedRecruiterClientAsync(factory, "pr-recruiter5@example.com");
        using var anonymousClient = factory.CreateClient();

        for (var i = 0; i < 25; i++)
        {
            await CreateRequisitionAsync(recruiterClient, $"Engineer {i}", "Backend role.", publish: true);
        }
        await CreateRequisitionAsync(recruiterClient, "Sales Manager", "A different kind of role.", publish: true);

        var firstPageResponse = await anonymousClient.GetAsync("/api/public/requisitions?keyword=engineer&page=1");
        var secondPageResponse = await anonymousClient.GetAsync("/api/public/requisitions?keyword=engineer&page=2");

        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<JsonElement>();
        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(20, firstPage.GetProperty("items").GetArrayLength());
        Assert.Equal(25, firstPage.GetProperty("total").GetInt32());
        Assert.Equal(5, secondPage.GetProperty("items").GetArrayLength());
        Assert.Equal(25, secondPage.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task GET_public_requisitions_id_Published_Returns200()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var recruiterClient = await CreateAuthorizedRecruiterClientAsync(factory, "pr-recruiter6@example.com");
        using var anonymousClient = factory.CreateClient();

        var id = await CreateRequisitionAsync(recruiterClient, "Senior Engineer", "We build backend systems.", publish: true);

        var response = await anonymousClient.GetAsync($"/api/public/requisitions/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PublicRequisitionDto>();
        Assert.Equal("Senior Engineer", dto!.Title);
    }

    [Fact]
    public async Task GET_public_requisitions_id_DraftOrClosed_Returns404()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var recruiterClient = await CreateAuthorizedRecruiterClientAsync(factory, "pr-recruiter7@example.com");
        using var anonymousClient = factory.CreateClient();

        var draftId = await CreateRequisitionAsync(recruiterClient, "Draft Role", "Description", publish: false);
        var closedId = await CreateRequisitionAsync(recruiterClient, "Closed Role", "Description", publish: true);
        await recruiterClient.PostAsync($"/api/requisitions/{closedId}/close", null);

        var draftResponse = await anonymousClient.GetAsync($"/api/public/requisitions/{draftId}");
        var closedResponse = await anonymousClient.GetAsync($"/api/public/requisitions/{closedId}");
        var missingResponse = await anonymousClient.GetAsync($"/api/public/requisitions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, draftResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, closedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("0")]
    public async Task GET_public_requisitions_InvalidPageParam_Returns400WithoutQuerying(string page)
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var anonymousClient = factory.CreateClient();

        var response = await anonymousClient.GetAsync($"/api/public/requisitions?page={page}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("requisition.list.invalid-page", content);
    }

    [Fact]
    public async Task GET_public_requisitions_PageSize1000_Returns50Items()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var recruiterClient = await CreateAuthorizedRecruiterClientAsync(factory, "pr-recruiter8@example.com");
        using var anonymousClient = factory.CreateClient();

        for (var i = 0; i < 55; i++)
        {
            await CreateRequisitionAsync(recruiterClient, $"Role {i}", "Description", publish: true);
        }

        var response = await anonymousClient.GetAsync("/api/public/requisitions?pageSize=1000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(50, paged.GetProperty("pageSize").GetInt32());
        Assert.Equal(50, paged.GetProperty("items").GetArrayLength());
    }
}
