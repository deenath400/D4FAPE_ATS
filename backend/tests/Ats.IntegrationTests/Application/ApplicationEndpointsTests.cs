namespace Ats.IntegrationTests.Application;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ats.IntegrationTests;
using Ats.Service.Application.Dtos;
using Ats.Service.Auth.Dtos;
using Ats.Service.Pipeline.Dtos;
using Ats.Service.Requisition.Dtos;
using Ats.Shared.Auth;
using Ats.Shared.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

public class ApplicationEndpointsTests
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

    private static void ClearAuthorization(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
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

    private static MultipartFormDataContent CreatePdfFormContent(
        byte[] bytes, string fileName = "resume.pdf", string contentType = "application/pdf")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "cv", fileName);
        return content;
    }

    private static byte[] ValidPdfBytes() => Encoding.UTF8.GetBytes(PdfContent);

    [Fact]
    public async Task POST_applications_AsCandidateValidPdf_Returns201()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter1@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);

        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate1@example.com");
        Authorize(client, candidateToken);

        var response = await client.PostAsync(
            $"/api/requisitions/{requisitionId}/applications", CreatePdfFormContent(ValidPdfBytes()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ApplicationDto>();
        Assert.NotNull(dto);
        Assert.Equal(requisitionId, dto!.RequisitionId);
        Assert.Equal("resume.pdf", dto.Cv.FileName);
        Assert.Equal("application/pdf", dto.Cv.ContentType);
    }

    [Fact]
    public async Task POST_applications_NoFile_Returns400()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter2@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);

        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate2@example.com");
        Authorize(client, candidateToken);

        // A well-formed multipart body with no "cv" part — a totally empty multipart body is
        // itself malformed per RFC 7578 and would 500 before reaching the handler at all.
        using var noFileContent = new MultipartFormDataContent { { new StringContent("n/a"), "note" } };

        var response = await client.PostAsync($"/api/requisitions/{requisitionId}/applications", noFileContent);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("application.submit.cv-required", content);
    }

    [Fact]
    public async Task POST_applications_NonPdf_Returns400()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter3@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);

        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate3@example.com");
        Authorize(client, candidateToken);

        var response = await client.PostAsync(
            $"/api/requisitions/{requisitionId}/applications",
            CreatePdfFormContent(Encoding.UTF8.GetBytes("fake docx"), "resume.docx", "application/msword"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("application.submit.invalid-file-type", content);
    }

    [Fact]
    public async Task POST_applications_OversizedPdf_Returns400()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter4@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);

        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate4@example.com");
        Authorize(client, candidateToken);

        var oversized = new byte[5 * 1024 * 1024 + 1];
        Encoding.UTF8.GetBytes("%PDF-1.4").CopyTo(oversized, 0);

        var response = await client.PostAsync(
            $"/api/requisitions/{requisitionId}/applications", CreatePdfFormContent(oversized));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("application.submit.file-too-large", content);
    }

    [Fact]
    public async Task POST_applications_DraftRequisition_Returns404()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter5@example.com", AuthConstants.Roles.Recruiter);
        Authorize(client, recruiterToken);
        var draft = await (await client.PostAsJsonAsync(
                "/api/requisitions", new CreateRequisitionRequestDto("Draft Role", "Description")))
            .Content.ReadFromJsonAsync<RequisitionDto>();

        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate5@example.com");
        Authorize(client, candidateToken);

        var response = await client.PostAsync(
            $"/api/requisitions/{draft!.Id}/applications", CreatePdfFormContent(ValidPdfBytes()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("application.submit.requisition-not-found", content);
    }

    [Fact]
    public async Task POST_applications_ClosedRequisition_Returns404IdenticalToDraft()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter6@example.com", AuthConstants.Roles.Recruiter);
        Authorize(client, recruiterToken);
        var created = await (await client.PostAsJsonAsync(
                "/api/requisitions", new CreateRequisitionRequestDto("Closed Role", "Description")))
            .Content.ReadFromJsonAsync<RequisitionDto>();
        await client.PostAsync($"/api/requisitions/{created!.Id}/publish", null);
        await client.PostAsync($"/api/requisitions/{created.Id}/close", null);

        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate6@example.com");
        Authorize(client, candidateToken);

        var response = await client.PostAsync(
            $"/api/requisitions/{created.Id}/applications", CreatePdfFormContent(ValidPdfBytes()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("application.submit.requisition-not-found", content);
    }

    [Fact]
    public async Task POST_applications_MissingRequisition_Returns404IdenticalToDraft()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate7@example.com");
        Authorize(client, candidateToken);

        var response = await client.PostAsync(
            $"/api/requisitions/{Guid.NewGuid()}/applications", CreatePdfFormContent(ValidPdfBytes()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("application.submit.requisition-not-found", content);
    }

    [Fact]
    public async Task POST_applications_SecondSubmission_Returns409()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter7@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);

        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate8@example.com");
        Authorize(client, candidateToken);

        var first = await client.PostAsync(
            $"/api/requisitions/{requisitionId}/applications", CreatePdfFormContent(ValidPdfBytes()));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsync(
            $"/api/requisitions/{requisitionId}/applications", CreatePdfFormContent(ValidPdfBytes()));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var content = await second.Content.ReadAsStringAsync();
        Assert.Contains("application.submit.duplicate", content);
    }

    [Fact]
    public async Task POST_applications_Anonymous_Returns401()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter8@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);
        ClearAuthorization(client);

        var response = await client.PostAsync(
            $"/api/requisitions/{requisitionId}/applications", CreatePdfFormContent(ValidPdfBytes()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task POST_applications_AsRecruiterOrHiringManager_Returns403(bool asHiringManager)
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter9@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);

        var staffToken = asHiringManager
            ? await CreateStaffUserAndLoginAsync(factory, client, "hm1@example.com", AuthConstants.Roles.HiringManager)
            : recruiterToken;
        Authorize(client, staffToken);

        var response = await client.PostAsync(
            $"/api/requisitions/{requisitionId}/applications", CreatePdfFormContent(ValidPdfBytes()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GET_applications_mine_ReturnsOwnApplicationsOnly()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter10@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken, "Backend Engineer");

        var candidate1Token = await CreateCandidateAndLoginAsync(client, "candidate9@example.com");
        Authorize(client, candidate1Token);
        await client.PostAsync($"/api/requisitions/{requisitionId}/applications", CreatePdfFormContent(ValidPdfBytes()));

        var emptyMineResponse = await client.GetAsync("/api/applications/mine");

        var candidate2Token = await CreateCandidateAndLoginAsync(client, "candidate10@example.com");
        Authorize(client, candidate2Token);
        var candidate2MineBeforeSubmit = await client.GetAsync("/api/applications/mine");

        Authorize(client, candidate1Token);
        var mineResponse = await client.GetAsync("/api/applications/mine");

        Assert.Equal(HttpStatusCode.OK, mineResponse.StatusCode);
        var mine = await mineResponse.Content.ReadFromJsonAsync<CandidateApplicationListItemDto[]>();
        Assert.NotNull(mine);
        Assert.Single(mine!);
        Assert.Equal("Backend Engineer", mine![0].RequisitionTitle);

        Assert.Equal(HttpStatusCode.OK, candidate2MineBeforeSubmit.StatusCode);
        var candidate2Mine = await candidate2MineBeforeSubmit.Content.ReadFromJsonAsync<CandidateApplicationListItemDto[]>();
        Assert.Empty(candidate2Mine!);
    }

    [Fact]
    public async Task GET_applications_mine_IncludesStatus_ExcludesNote()
    {
        // AC-22/AC-23/AC-30 (0005 FR-17, FR-23): the Candidate's own-list rows carry the real
        // current status (a Stage name for an active Application, isRejected + retained Stage
        // name for a rejected one), and the staff-only transition note never appears anywhere
        // in this response.
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter22@example.com", AuthConstants.Roles.Recruiter);
        var activeRequisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken, "Backend Engineer");
        var rejectedRequisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken, "Frontend Engineer");

        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate22@example.com");
        Authorize(client, candidateToken);
        var activeSubmit = await client.PostAsync(
            $"/api/requisitions/{activeRequisitionId}/applications", CreatePdfFormContent(ValidPdfBytes()));
        var activeApplication = await activeSubmit.Content.ReadFromJsonAsync<ApplicationDto>();
        var rejectedSubmit = await client.PostAsync(
            $"/api/requisitions/{rejectedRequisitionId}/applications", CreatePdfFormContent(ValidPdfBytes()));
        var rejectedApplication = await rejectedSubmit.Content.ReadFromJsonAsync<ApplicationDto>();

        Authorize(client, recruiterToken);
        var rejectResponse = await client.PostAsJsonAsync(
            $"/api/applications/{rejectedApplication!.Id}/reject",
            new RejectApplicationRequestDto("Not enough backend depth."));
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

        Authorize(client, candidateToken);
        var mineResponse = await client.GetAsync("/api/applications/mine");

        Assert.Equal(HttpStatusCode.OK, mineResponse.StatusCode);
        var body = await mineResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Not enough backend depth.", body);

        var mine = await mineResponse.Content.ReadFromJsonAsync<CandidateApplicationListItemDto[]>();
        Assert.NotNull(mine);
        Assert.Equal(2, mine!.Length);

        var active = mine.Single(i => i.Id == activeApplication!.Id);
        Assert.Equal("Applied", active.CurrentStageName);
        Assert.False(active.IsRejected);

        var rejected = mine.Single(i => i.Id == rejectedApplication.Id);
        Assert.Equal("Applied", rejected.CurrentStageName);
        Assert.True(rejected.IsRejected);
    }

    [Fact]
    public async Task GET_applications_id_cv_AsOwner_Returns200WithPdfBytes()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter11@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);

        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate11@example.com");
        Authorize(client, candidateToken);
        var submitResponse = await client.PostAsync(
            $"/api/requisitions/{requisitionId}/applications", CreatePdfFormContent(ValidPdfBytes()));
        var submitted = await submitResponse.Content.ReadFromJsonAsync<ApplicationDto>();

        var response = await client.GetAsync($"/api/applications/{submitted!.Id}/cv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(ValidPdfBytes(), bytes);
    }

    [Fact]
    public async Task GET_applications_id_cv_AsNonOwnerCandidate_Returns403()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter12@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);

        var ownerToken = await CreateCandidateAndLoginAsync(client, "candidate12@example.com");
        Authorize(client, ownerToken);
        var submitResponse = await client.PostAsync(
            $"/api/requisitions/{requisitionId}/applications", CreatePdfFormContent(ValidPdfBytes()));
        var submitted = await submitResponse.Content.ReadFromJsonAsync<ApplicationDto>();

        var otherToken = await CreateCandidateAndLoginAsync(client, "candidate13@example.com");
        Authorize(client, otherToken);

        var response = await client.GetAsync($"/api/applications/{submitted!.Id}/cv");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("application.cv.forbidden", content);
    }

    [Fact]
    public async Task GET_requisitions_id_applications_AsRecruiter_Returns200()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter13@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);

        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate14@example.com");
        Authorize(client, candidateToken);
        await client.PostAsync($"/api/requisitions/{requisitionId}/applications", CreatePdfFormContent(ValidPdfBytes()));

        Authorize(client, recruiterToken);
        var response = await client.GetAsync($"/api/requisitions/{requisitionId}/applications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<StaffApplicationListItemDto[]>();
        Assert.NotNull(list);
        Assert.Single(list!);
        Assert.Equal("candidate14@example.com", list![0].Candidate.Email);
        Assert.Equal($"/api/applications/{list[0].Id}/cv", list[0].CvDownloadUrl);
    }

    [Fact]
    public async Task GET_requisitions_id_applications_AsHiringManager_Returns200()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter14@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);

        var hmToken = await CreateStaffUserAndLoginAsync(factory, client, "hm2@example.com", AuthConstants.Roles.HiringManager);
        Authorize(client, hmToken);

        var response = await client.GetAsync($"/api/requisitions/{requisitionId}/applications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GET_requisitions_id_applications_NoApplications_Returns200EmptyList()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter15@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);
        Authorize(client, recruiterToken);

        var response = await client.GetAsync($"/api/requisitions/{requisitionId}/applications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<StaffApplicationListItemDto[]>();
        Assert.NotNull(list);
        Assert.Empty(list!);
    }

    [Fact]
    public async Task GET_requisitions_id_applications_AsCandidate_Returns403()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter16@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);

        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate15@example.com");
        Authorize(client, candidateToken);

        var response = await client.GetAsync($"/api/requisitions/{requisitionId}/applications");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GET_applications_id_cv_AsRecruiter_Returns200()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter17@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(client, recruiterToken);

        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate16@example.com");
        Authorize(client, candidateToken);
        var submitResponse = await client.PostAsync(
            $"/api/requisitions/{requisitionId}/applications", CreatePdfFormContent(ValidPdfBytes()));
        var submitted = await submitResponse.Content.ReadFromJsonAsync<ApplicationDto>();

        Authorize(client, recruiterToken);
        var response = await client.GetAsync($"/api/applications/{submitted!.Id}/cv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(ValidPdfBytes(), bytes);
    }

    [Fact]
    public async Task GET_applications_id_cv_MissingId_Returns404()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var candidateToken = await CreateCandidateAndLoginAsync(client, "candidate17@example.com");
        Authorize(client, candidateToken);

        var response = await client.GetAsync($"/api/applications/{Guid.NewGuid()}/cv");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("application.cv.not-found", content);
    }

    [Fact]
    public async Task GET_requisitions_id_applications_MissingRequisition_Returns404()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var client = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, client, "recruiter18@example.com", AuthConstants.Roles.Recruiter);
        Authorize(client, recruiterToken);

        var response = await client.GetAsync($"/api/requisitions/{Guid.NewGuid()}/applications");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("application.list.requisition-not-found", content);
    }

    [Fact]
    public async Task POST_applications_SlowFileWrite_DoesNotExtendSqliteWriteLockDuration()
    {
        // NFR-3 dedicated verification (T-43): "the Application submission database transaction
        // remains open only for the row insert itself — the CV file write does not extend how
        // long the SQLite write lock is held." Proven structurally, not by code inspection: the
        // CV write for one submission is made artificially slow, and — while that submission is
        // still in flight — a second, wholly unrelated database write (a candidate registration,
        // which never touches IFileStorage) is issued against the same SQLite file. If the write
        // transaction wrapped the file write, SQLite's single-writer lock would force the
        // unrelated write to wait behind it for the full artificial delay. It finishing quickly
        // instead proves the lock was never held during the file write.
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();

        var fileWriteDelay = TimeSpan.FromMilliseconds(1000);
        using var slowFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFileStorage>();
                services.AddSingleton<IFileStorage>(sp =>
                    new DelayedFileStorage(new LocalDiskFileStorage(sp.GetRequiredService<IConfiguration>()), fileWriteDelay));
            });
        });

        using var setupClient = slowFactory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, setupClient, "recruiter19@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(setupClient, recruiterToken);
        var candidateToken = await CreateCandidateAndLoginAsync(setupClient, "candidate18@example.com");

        using var submitClient = slowFactory.CreateClient();
        Authorize(submitClient, candidateToken);
        using var unrelatedClient = slowFactory.CreateClient();

        var submitStopwatch = Stopwatch.StartNew();
        var submitTask = submitClient.PostAsync(
            $"/api/requisitions/{requisitionId}/applications", CreatePdfFormContent(ValidPdfBytes()));

        // Give the slow submission a head start into its (artificially delayed) file write
        // before issuing the unrelated write, so the unrelated write's timing genuinely measures
        // whether it had to wait behind the submission.
        await Task.Delay(150);

        var unrelatedStopwatch = Stopwatch.StartNew();
        var unrelatedResponse = await unrelatedClient.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequestDto("candidate19@example.com", Password, "Grace", "Hopper"));
        unrelatedStopwatch.Stop();

        var submitResponse = await submitTask;
        submitStopwatch.Stop();

        Assert.Equal(HttpStatusCode.Created, submitResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, unrelatedResponse.StatusCode);

        // The unrelated write finished comfortably before the slow submission's file write would
        // have released a held SQLite write lock — proof the lock was never held across it.
        Assert.True(
            unrelatedStopwatch.Elapsed < fileWriteDelay,
            $"Unrelated registration took {unrelatedStopwatch.ElapsedMilliseconds}ms, not less " +
            $"than the {fileWriteDelay.TotalMilliseconds}ms artificial file-write delay — " +
            "suggests the SQLite write lock was held across the CV file write (NFR-3 violation).");
        Assert.True(
            submitStopwatch.Elapsed >= fileWriteDelay,
            "The submission finished faster than the artificial file-write delay — the delayed " +
            "IFileStorage decorator was not actually exercised by this request.");
    }

    [Fact]
    public async Task POST_applications_TwoNearSimultaneousSubmissions_ExactlyOneSurvives()
    {
        // E-1 regression test (T-44): "the losing request receives HTTP 409, enforced
        // structurally (e.g. a uniqueness constraint), not by application-level check timing
        // alone." Fires two near-simultaneous submissions from the same Candidate against the
        // same Requisition over two independent HttpClients sharing one TestServer/database, and
        // asserts exactly one Application row survives regardless of how the two requests
        // actually interleaved.
        using var factory = new CustomWebApplicationFactory();
        factory.InitializeDatabase();
        using var setupClient = factory.CreateClient();
        var recruiterToken = await CreateStaffUserAndLoginAsync(factory, setupClient, "recruiter20@example.com", AuthConstants.Roles.Recruiter);
        var requisitionId = await CreatePublishedRequisitionAsync(setupClient, recruiterToken);
        var candidateToken = await CreateCandidateAndLoginAsync(setupClient, "candidate20@example.com");

        using var client1 = factory.CreateClient();
        Authorize(client1, candidateToken);
        using var client2 = factory.CreateClient();
        Authorize(client2, candidateToken);

        var task1 = client1.PostAsync($"/api/requisitions/{requisitionId}/applications", CreatePdfFormContent(ValidPdfBytes()));
        var task2 = client2.PostAsync($"/api/requisitions/{requisitionId}/applications", CreatePdfFormContent(ValidPdfBytes()));

        var responses = await Task.WhenAll(task1, task2);

        var createdCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, createdCount);
        Assert.Equal(1, conflictCount);

        Authorize(setupClient, candidateToken);
        var mineResponse = await setupClient.GetAsync("/api/applications/mine");
        var mine = await mineResponse.Content.ReadFromJsonAsync<CandidateApplicationListItemDto[]>();
        Assert.NotNull(mine);
        Assert.Single(mine!);
    }

    /// <summary>
    /// Wraps a real <see cref="IFileStorage"/> with an artificial delay around the write, used by
    /// <see cref="POST_applications_SlowFileWrite_DoesNotExtendSqliteWriteLockDuration"/> (NFR-3)
    /// to widen the CV file-write window so a concurrent, unrelated database write can prove it
    /// was never blocked behind it.
    /// </summary>
    private sealed class DelayedFileStorage : IFileStorage
    {
        private readonly IFileStorage _inner;
        private readonly TimeSpan _delay;

        public DelayedFileStorage(IFileStorage inner, TimeSpan delay)
        {
            _inner = inner;
            _delay = delay;
        }

        public async Task SaveAsync(string storageKey, Stream content, CancellationToken ct = default)
        {
            await Task.Delay(_delay, ct);
            await _inner.SaveAsync(storageKey, content, ct);
        }

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default) =>
            _inner.OpenReadAsync(storageKey, ct);

        public Task DeleteAsync(string storageKey, CancellationToken ct = default) =>
            _inner.DeleteAsync(storageKey, ct);
    }
}
