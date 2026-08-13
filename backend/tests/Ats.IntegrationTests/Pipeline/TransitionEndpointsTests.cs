namespace Ats.IntegrationTests.Pipeline;

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ats.Db;
using Ats.IntegrationTests;
using Ats.Service.Application.Dtos;
using Ats.Service.Auth.Dtos;
using Ats.Service.Common;
using Ats.Service.Pipeline;
using Ats.Service.Pipeline.Dtos;
using Ats.Service.Requisition.Dtos;
using Ats.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ApplicationEntity = Ats.Db.Applications.Application;

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

    private static async Task<Guid> GetUserIdByEmailAsync(CustomWebApplicationFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        return user!.Id;
    }

    /// <summary>
    /// Seeds <paramref name="count"/> Applications directly against the database file backing
    /// <paramref name="factory"/>, bypassing HTTP registration/submission entirely (which would
    /// otherwise pay for real Identity password hashing per candidate). Used only to build volume
    /// for NFR-1's query-count verification (T-54) — the seeded rows are otherwise
    /// indistinguishable from ones a Candidate would have submitted for real.
    /// </summary>
    private static async Task SeedManyApplicationsDirectAsync(
        CustomWebApplicationFactory factory, Guid requisitionId, Guid[] stageIds, int count)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        for (var i = 0; i < count; i++)
        {
            var candidate = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = $"bulk-candidate-{i}@example.com",
                UserName = $"bulk-candidate-{i}@example.com",
                FirstName = "Bulk",
                LastName = $"Candidate{i}",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Users.Add(candidate);

            // Rejecting every 7th one exercises the separate Rejected group too, not just the
            // Stage columns.
            var application = ApplicationEntity.Create(requisitionId, candidate.Id, stageIds[i % stageIds.Length]);
            if (i % 7 == 0)
            {
                application.Reject();
            }

            db.Applications.Add(application);
        }

        await db.SaveChangesAsync();
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

    // ---- CP-4 Hardening ----

    [Fact]
    public async Task GetPipelineBoardAsync_At500Applications_IssuesExactlyTwoQueriesAndGroupsInMemory()
    {
        // NFR-1: "the staff pipeline board renders within 2 seconds at p95 for a Requisition
        // with up to 500 Applications, with no pagination." lld.md §3.2 designs this as exactly
        // two AsNoTracking() round trips (Requisition+Stages; Applications joined to Users), with
        // every Stage's grouping and count computed in memory afterward — never one query per
        // Stage. A DbCommandInterceptor counts every SELECT actually sent to SQLite while serving
        // a board for a Requisition holding 500 Applications across its 4 default Stages (with a
        // handful rejected, to exercise the separate Rejected group too).
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter16@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);
        var stages = await GetStagesAsync(client, recruiterToken, requisitionId);
        var stageIds = stages.OrderBy(s => s.SortOrder).Select(s => s.Id).ToArray();

        const int applicationCount = 500;
        await SeedManyApplicationsDirectAsync(factory, requisitionId, stageIds, applicationCount);

        var selectCounter = new SelectCountingInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={factory.DbFilePath}")
            .AddInterceptors(new SqlitePragmaConnectionInterceptor(), selectCounter)
            .Options;

        await using var instrumentedContext = new AppDbContext(options);
        var pipelineService = new PipelineService(instrumentedContext);

        var stopwatch = Stopwatch.StartNew();
        var result = await pipelineService.GetPipelineBoardAsync(requisitionId);
        stopwatch.Stop();

        Assert.True(result.IsSuccess);
        var totalOnBoard = result.Value!.Stages.Sum(s => s.Count) + result.Value.Rejected.Count;
        Assert.Equal(applicationCount, totalOnBoard);
        Assert.Equal(4, result.Value.Stages.Count);

        Assert.Equal(2, selectCounter.SelectCount);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"GetPipelineBoardAsync took {stopwatch.ElapsedMilliseconds}ms for {applicationCount} Applications, exceeding NFR-1's 2s p95 budget.");
    }

    [Fact]
    public async Task MoveApplicationAsync_TransactionSpansOnlyApplicationAndStageTransitionWrites()
    {
        // NFR-2: "consistent with architecture.md's documented SQLite single-writer constraint
        // ..., a stage move or rejection's database transaction remains open only for the
        // Application/StageTransition writes themselves." Every read MoveApplicationAsync needs
        // (the Application, the owning Requisition's status, the target Stage, the acting user's
        // name, the origin Stage's name) happens before SaveChangesAsync opens its implicit
        // transaction; only the final Application UPDATE and StageTransition INSERT should occur
        // while a transaction is open.
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterEmail = "recruiter17@example.com";
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, recruiterEmail, AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);
        var stages = await GetStagesAsync(client, recruiterToken, requisitionId);
        var applied = stages.Single(s => s.Name == "Applied");
        var screening = stages.Single(s => s.Name == "Screening");
        var applicationId = await SubmitApplicationAsync(client, requisitionId, "candidate16@example.com");
        var recruiterId = await GetUserIdByEmailAsync(factory, recruiterEmail);

        var window = new TransactionWindowState();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={factory.DbFilePath}")
            .AddInterceptors(new SqlitePragmaConnectionInterceptor(), new TransactionWindowTrackingInterceptor(window), new TransactionWindowCommandInterceptor(window))
            .Options;

        await using var instrumentedContext = new AppDbContext(options);
        var pipelineService = new PipelineService(instrumentedContext);

        var result = await pipelineService.MoveApplicationAsync(
            applicationId, new MoveApplicationRequestDto(screening.Id, applied.Id, "Looks strong."), recruiterId);

        Assert.True(result.IsSuccess);
        AssertTransactionScopedToApplicationAndStageTransitionWrites(window);
    }

    [Fact]
    public async Task RejectApplicationAsync_TransactionSpansOnlyApplicationAndStageTransitionWrites()
    {
        // NFR-2, reject side of the same guarantee — see the move test above for the full
        // rationale.
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterEmail = "recruiter18@example.com";
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, recruiterEmail, AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);
        var applicationId = await SubmitApplicationAsync(client, requisitionId, "candidate17@example.com");
        var recruiterId = await GetUserIdByEmailAsync(factory, recruiterEmail);

        var window = new TransactionWindowState();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={factory.DbFilePath}")
            .AddInterceptors(new SqlitePragmaConnectionInterceptor(), new TransactionWindowTrackingInterceptor(window), new TransactionWindowCommandInterceptor(window))
            .Options;

        await using var instrumentedContext = new AppDbContext(options);
        var pipelineService = new PipelineService(instrumentedContext);

        var result = await pipelineService.RejectApplicationAsync(
            applicationId, new RejectApplicationRequestDto("Not enough depth."), recruiterId);

        Assert.True(result.IsSuccess);
        AssertTransactionScopedToApplicationAndStageTransitionWrites(window);
    }

    private static void AssertTransactionScopedToApplicationAndStageTransitionWrites(TransactionWindowState window)
    {
        Assert.True(window.TransactionOpened, "A move/reject must open a transaction (the implicit one SaveChangesAsync wraps its writes in) to persist the Application and StageTransition together.");
        Assert.NotEmpty(window.CommandsInTransaction);
        Assert.All(window.CommandsInTransaction, sql =>
        {
            var upper = sql.ToUpperInvariant();
            var touchesApplications = upper.Contains("\"APPLICATIONS\"", StringComparison.Ordinal) || upper.Contains(" APPLICATIONS ", StringComparison.Ordinal);
            var touchesStageTransitions = upper.Contains("STAGETRANSITIONS", StringComparison.Ordinal);
            Assert.True(
                touchesApplications || touchesStageTransitions,
                $"NFR-2: only Application/StageTransition writes may occur while the transaction is open. Found: {sql}");
            Assert.DoesNotContain("REQUISITIONS", upper, StringComparison.Ordinal);
            Assert.DoesNotContain("ASPNETUSERS", upper, StringComparison.Ordinal);
            Assert.DoesNotContain("\"STAGES\"", upper, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task POST_applications_id_move_TwoConcurrentMoves_ExactlyOneSucceeds()
    {
        // E-2/AC-29 regression (T-56): "two Recruiters attempt to move the same Application
        // concurrently, each believing a different current Stage. The losing request is rejected
        // with 409 and told the Application's actual current Stage; no silent overwrite." Fires
        // two real, near-simultaneous HTTP move requests — both believing the Application is
        // still in "Applied" — over two independent HttpClients sharing one TestServer/database,
        // and asserts exactly one wins regardless of how the two requests actually interleaved.
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var setupClient = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, setupClient, "recruiter19@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(setupClient, recruiterToken);
        var stages = await GetStagesAsync(setupClient, recruiterToken, requisitionId);
        var applied = stages.Single(s => s.Name == "Applied");
        var screening = stages.Single(s => s.Name == "Screening");
        var interview = stages.Single(s => s.Name == "Interview");
        var applicationId = await SubmitApplicationAsync(setupClient, requisitionId, "candidate18@example.com");

        using var client1 = factory.CreateClient();
        Authorize(client1, recruiterToken);
        using var client2 = factory.CreateClient();
        Authorize(client2, recruiterToken);

        var task1 = client1.PostAsJsonAsync(
            $"/api/applications/{applicationId}/move", new MoveApplicationRequestDto(screening.Id, applied.Id, "Mover A"));
        var task2 = client2.PostAsJsonAsync(
            $"/api/applications/{applicationId}/move", new MoveApplicationRequestDto(interview.Id, applied.Id, "Mover B"));

        var responses = await Task.WhenAll(task1, task2);

        var okCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, okCount);
        Assert.Equal(1, conflictCount);

        var conflictResponse = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        var conflictContent = await conflictResponse.Content.ReadAsStringAsync();
        Assert.Contains("application.move.conflict", conflictContent);
        Assert.Contains("actualCurrentStageId", conflictContent);
        Assert.Contains("actualCurrentStageName", conflictContent);

        // FR-14 append-only: exactly one transition was recorded, not two, and no earlier
        // transition was overwritten or removed.
        Authorize(setupClient, recruiterToken);
        var historyResponse = await setupClient.GetAsync($"/api/applications/{applicationId}/transitions");
        var history = await historyResponse.Content.ReadFromJsonAsync<StageTransitionDto[]>();
        Assert.NotNull(history);
        Assert.Single(history!);
    }

    /// <summary>Counts every SELECT (<c>ReaderExecuting</c>) command sent to the database — used
    /// by <see cref="GetPipelineBoardAsync_At500Applications_IssuesExactlyTwoQueriesAndGroupsInMemory"/>
    /// to assert NFR-1's "exactly two queries, no per-stage round trip" claim directly against
    /// the commands actually issued, not just the code's shape.</summary>
    private sealed class SelectCountingInterceptor : DbCommandInterceptor
    {
        public int SelectCount { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            SelectCount++;
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            SelectCount++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    /// <summary>Shared state between <see cref="TransactionWindowTrackingInterceptor"/> and
    /// <see cref="TransactionWindowCommandInterceptor"/> — records whether a transaction was ever
    /// opened on the intercepted context, and the SQL text of every command issued while one was
    /// open, for <see cref="AssertTransactionScopedToApplicationAndStageTransitionWrites"/>
    /// (NFR-2, T-55).</summary>
    private sealed class TransactionWindowState
    {
        public bool InTransaction { get; set; }
        public bool TransactionOpened { get; set; }
        public List<string> CommandsInTransaction { get; } = new();
    }

    private sealed class TransactionWindowTrackingInterceptor : DbTransactionInterceptor
    {
        private readonly TransactionWindowState _state;

        public TransactionWindowTrackingInterceptor(TransactionWindowState state) => _state = state;

        public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
        {
            _state.InTransaction = true;
            _state.TransactionOpened = true;
            return base.TransactionStarted(connection, eventData, result);
        }

        public override ValueTask<DbTransaction> TransactionStartedAsync(
            DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
        {
            _state.InTransaction = true;
            _state.TransactionOpened = true;
            return base.TransactionStartedAsync(connection, eventData, result, cancellationToken);
        }

        public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        {
            _state.InTransaction = false;
            base.TransactionCommitted(transaction, eventData);
        }

        public override Task TransactionCommittedAsync(
            DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            _state.InTransaction = false;
            return base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
        }

        public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        {
            _state.InTransaction = false;
            base.TransactionRolledBack(transaction, eventData);
        }

        public override Task TransactionRolledBackAsync(
            DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            _state.InTransaction = false;
            return base.TransactionRolledBackAsync(transaction, eventData, cancellationToken);
        }
    }

    private sealed class TransactionWindowCommandInterceptor : DbCommandInterceptor
    {
        private readonly TransactionWindowState _state;

        public TransactionWindowCommandInterceptor(TransactionWindowState state) => _state = state;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Capture(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Capture(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Capture(command);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Capture(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Capture(DbCommand command)
        {
            if (_state.InTransaction)
            {
                _state.CommandsInTransaction.Add(command.CommandText);
            }
        }
    }
}
