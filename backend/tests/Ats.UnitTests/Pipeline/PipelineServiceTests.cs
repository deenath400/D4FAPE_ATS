namespace Ats.UnitTests.Pipeline;

using System;
using System.Linq;
using System.Threading.Tasks;
using Ats.Db;
using Ats.Service.Common;
using Ats.Service.Pipeline;
using Ats.Service.Pipeline.Dtos;
using Ats.Shared.Auth;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ApplicationEntity = Ats.Db.Applications.Application;
using RequisitionEntity = Ats.Db.Requisitions.Requisition;
using StageEntity = Ats.Db.Requisitions.Stage;

public class PipelineServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly PipelineService _service;

    public PipelineServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        _service = new PipelineService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<Guid> CreateDraftRequisitionAsync(string title = "Senior Engineer")
    {
        var requisition = RequisitionEntity.Create(title, "Description");
        _dbContext.Requisitions.Add(requisition);
        await _dbContext.SaveChangesAsync();
        return requisition.Id;
    }

    private async Task<Guid> CreateClosedRequisitionAsync(string title = "Closed Role")
    {
        var requisition = RequisitionEntity.Create(title, "Description");
        requisition.Publish();
        requisition.Close();
        _dbContext.Requisitions.Add(requisition);
        await _dbContext.SaveChangesAsync();
        return requisition.Id;
    }

    private async Task<Guid> AddStageDirectAsync(Guid requisitionId, string name, int sortOrder)
    {
        var stage = StageEntity.Create(requisitionId, name, sortOrder);
        _dbContext.Stages.Add(stage);
        await _dbContext.SaveChangesAsync();
        return stage.Id;
    }

    private async Task<Guid> CreateUserAsync(string email, string firstName, string lastName)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            FirstName = firstName,
            LastName = lastName,
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user.Id;
    }

    private Task<Guid> CreateCandidateAsync(string email) => CreateUserAsync(email, "Ada", "Lovelace");

    private Task<Guid> CreateRecruiterAsync(string email = "recruiter@example.com") =>
        CreateUserAsync(email, "Jane", "Recruiter");

    private async Task<Guid> CreateApplicationAsync(Guid requisitionId, Guid candidateId, Guid currentStageId)
    {
        var application = ApplicationEntity.Create(requisitionId, candidateId, currentStageId);
        _dbContext.Applications.Add(application);
        await _dbContext.SaveChangesAsync();
        return application.Id;
    }

    private async Task<(Guid RequisitionId, Guid ApplicationId, Guid AppliedStageId, Guid ScreeningStageId, Guid InterviewStageId)>
        SeedActiveApplicationAsync()
    {
        var requisitionId = await CreateDraftRequisitionAsync();
        var appliedId = await AddStageDirectAsync(requisitionId, "Applied", 0);
        var screeningId = await AddStageDirectAsync(requisitionId, "Screening", 1);
        var interviewId = await AddStageDirectAsync(requisitionId, "Interview", 2);
        var candidateId = await CreateCandidateAsync("candidate@example.com");
        var applicationId = await CreateApplicationAsync(requisitionId, candidateId, appliedId);
        return (requisitionId, applicationId, appliedId, screeningId, interviewId);
    }

    // ---- Stage configuration (T-29) ----

    [Fact]
    public async Task AddStageAsync_ValidName_ReturnsCreatedAtPosition()
    {
        var requisitionId = await CreateDraftRequisitionAsync();
        await AddStageDirectAsync(requisitionId, "Applied", 0);
        await AddStageDirectAsync(requisitionId, "Offer", 1);

        var result = await _service.AddStageAsync(requisitionId, new AddStageRequestDto("Interview", 1));

        Assert.True(result.IsSuccess);
        Assert.Equal("Interview", result.Value!.Name);
        Assert.Equal(1, result.Value.SortOrder);

        var stages = (await _service.GetStagesAsync(requisitionId)).Value!;
        Assert.Equal(new[] { "Applied", "Interview", "Offer" }, stages.Select(s => s.Name));
    }

    [Fact]
    public async Task AddStageAsync_NoPosition_AppendsAtEnd()
    {
        var requisitionId = await CreateDraftRequisitionAsync();
        await AddStageDirectAsync(requisitionId, "Applied", 0);

        var result = await _service.AddStageAsync(requisitionId, new AddStageRequestDto("Screening", null));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.SortOrder);
    }

    [Fact]
    public async Task AddStageAsync_DuplicateName_ReturnsConflict()
    {
        var requisitionId = await CreateDraftRequisitionAsync();
        await AddStageDirectAsync(requisitionId, "Screening", 0);

        var result = await _service.AddStageAsync(requisitionId, new AddStageRequestDto("screening", null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("stage.add.duplicate-name", result.ErrorCode);
    }

    [Fact]
    public async Task AddStageAsync_ClosedRequisition_ReturnsConflict()
    {
        var requisitionId = await CreateClosedRequisitionAsync();

        var result = await _service.AddStageAsync(requisitionId, new AddStageRequestDto("Applied", null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("stage.add.requisition-closed", result.ErrorCode);
    }

    [Fact]
    public async Task AddStageAsync_MissingRequisition_ReturnsNotFound()
    {
        var result = await _service.AddStageAsync(Guid.NewGuid(), new AddStageRequestDto("Applied", null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("stage.add.requisition-not-found", result.ErrorCode);
    }

    [Fact]
    public async Task AddStageAsync_EmptyName_ReturnsValidation()
    {
        var requisitionId = await CreateDraftRequisitionAsync();

        var result = await _service.AddStageAsync(requisitionId, new AddStageRequestDto("   ", null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Equal("stage.add.validation-failed", result.ErrorCode);
    }

    [Fact]
    public async Task RenameStageAsync_KeepsApplicationsAssigned()
    {
        var requisitionId = await CreateDraftRequisitionAsync();
        var stageId = await AddStageDirectAsync(requisitionId, "Screening", 0);
        var candidateId = await CreateCandidateAsync("candidate@example.com");
        var applicationId = await CreateApplicationAsync(requisitionId, candidateId, stageId);

        var result = await _service.RenameStageAsync(requisitionId, stageId, new RenameStageRequestDto("Phone Screen"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Phone Screen", result.Value!.Name);
        var application = await _dbContext.Applications.AsNoTracking().SingleAsync(a => a.Id == applicationId);
        Assert.Equal(stageId, application.CurrentStageId);
    }

    [Fact]
    public async Task RenameStageAsync_DuplicateName_ReturnsConflict()
    {
        var requisitionId = await CreateDraftRequisitionAsync();
        await AddStageDirectAsync(requisitionId, "Applied", 0);
        var screeningId = await AddStageDirectAsync(requisitionId, "Screening", 1);

        var result = await _service.RenameStageAsync(requisitionId, screeningId, new RenameStageRequestDto("applied"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("stage.rename.duplicate-name", result.ErrorCode);
    }

    [Fact]
    public async Task RenameStageAsync_MissingStage_ReturnsNotFound()
    {
        var requisitionId = await CreateDraftRequisitionAsync();

        var result = await _service.RenameStageAsync(requisitionId, Guid.NewGuid(), new RenameStageRequestDto("Applied"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("stage.rename.not-found", result.ErrorCode);
    }

    [Fact]
    public async Task RenameStageAsync_ClosedRequisition_ReturnsConflict()
    {
        var requisitionId = await CreateClosedRequisitionAsync();
        var stageId = await AddStageDirectAsync(requisitionId, "Applied", 0);

        var result = await _service.RenameStageAsync(requisitionId, stageId, new RenameStageRequestDto("Screening"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("stage.rename.requisition-closed", result.ErrorCode);
    }

    [Fact]
    public async Task ReorderStagesAsync_ValidSet_UpdatesSortOrder()
    {
        var requisitionId = await CreateDraftRequisitionAsync();
        var appliedId = await AddStageDirectAsync(requisitionId, "Applied", 0);
        var screeningId = await AddStageDirectAsync(requisitionId, "Screening", 1);

        var result = await _service.ReorderStagesAsync(
            requisitionId, new ReorderStagesRequestDto(new[] { screeningId, appliedId }));

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { screeningId, appliedId }, result.Value!.Select(s => s.Id));
        Assert.Equal(0, result.Value![0].SortOrder);
        Assert.Equal(1, result.Value[1].SortOrder);
    }

    [Fact]
    public async Task ReorderStagesAsync_MismatchedSet_ReturnsValidation()
    {
        var requisitionId = await CreateDraftRequisitionAsync();
        await AddStageDirectAsync(requisitionId, "Applied", 0);
        await AddStageDirectAsync(requisitionId, "Screening", 1);

        var result = await _service.ReorderStagesAsync(requisitionId, new ReorderStagesRequestDto(new[] { Guid.NewGuid() }));

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Equal("stage.reorder.invalid-set", result.ErrorCode);
    }

    [Fact]
    public async Task ReorderStagesAsync_ClosedRequisition_ReturnsConflict()
    {
        var requisitionId = await CreateClosedRequisitionAsync();
        var stageId = await AddStageDirectAsync(requisitionId, "Applied", 0);

        var result = await _service.ReorderStagesAsync(requisitionId, new ReorderStagesRequestDto(new[] { stageId }));

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("stage.reorder.requisition-closed", result.ErrorCode);
    }

    [Fact]
    public async Task RemoveStageAsync_Unoccupied_Removes()
    {
        var requisitionId = await CreateDraftRequisitionAsync();
        var appliedId = await AddStageDirectAsync(requisitionId, "Applied", 0);
        var screeningId = await AddStageDirectAsync(requisitionId, "Screening", 1);
        var offerId = await AddStageDirectAsync(requisitionId, "Offer", 2);

        var result = await _service.RemoveStageAsync(requisitionId, screeningId);

        Assert.True(result.IsSuccess);
        var stages = (await _service.GetStagesAsync(requisitionId)).Value!;
        Assert.Equal(new[] { appliedId, offerId }, stages.Select(s => s.Id));
        Assert.Equal(1, stages.Single(s => s.Id == offerId).SortOrder);
    }

    [Fact]
    public async Task RemoveStageAsync_Occupied_ReturnsConflict()
    {
        var requisitionId = await CreateDraftRequisitionAsync();
        var stageId = await AddStageDirectAsync(requisitionId, "Applied", 0);
        var candidateId = await CreateCandidateAsync("candidate@example.com");
        await CreateApplicationAsync(requisitionId, candidateId, stageId);

        var result = await _service.RemoveStageAsync(requisitionId, stageId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("stage.remove.occupied", result.ErrorCode);
        Assert.NotNull(await _dbContext.Stages.AsNoTracking().SingleOrDefaultAsync(s => s.Id == stageId));
    }

    [Fact]
    public async Task RemoveStageAsync_ClosedRequisition_ReturnsConflict()
    {
        var requisitionId = await CreateClosedRequisitionAsync();
        var stageId = await AddStageDirectAsync(requisitionId, "Applied", 0);

        var result = await _service.RemoveStageAsync(requisitionId, stageId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("stage.remove.requisition-closed", result.ErrorCode);
    }

    [Fact]
    public async Task RemoveStageAsync_MissingStage_ReturnsNotFound()
    {
        var requisitionId = await CreateDraftRequisitionAsync();

        var result = await _service.RemoveStageAsync(requisitionId, Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("stage.remove.not-found", result.ErrorCode);
    }

    // ---- Move / Reject (T-30) ----

    [Fact]
    public async Task MoveApplicationAsync_ValidMove_UpdatesStageAndWritesTransition()
    {
        var seed = await SeedActiveApplicationAsync();
        var recruiterId = await CreateRecruiterAsync();

        var result = await _service.MoveApplicationAsync(
            seed.ApplicationId,
            new MoveApplicationRequestDto(seed.ScreeningStageId, seed.AppliedStageId, "Strong candidate."),
            recruiterId);

        Assert.True(result.IsSuccess);
        Assert.Equal(seed.ScreeningStageId, result.Value!.CurrentStageId);
        Assert.Equal("Screening", result.Value.CurrentStageName);
        Assert.False(result.Value.IsRejected);
        Assert.Equal("move", result.Value.Transition.Kind);
        Assert.Equal("Applied", result.Value.Transition.FromStageName);
        Assert.Equal("Screening", result.Value.Transition.ToStageName);
        Assert.Equal("Jane Recruiter", result.Value.Transition.ActorDisplayLabel);
        Assert.Equal("Strong candidate.", result.Value.Transition.Note);

        var application = await _dbContext.Applications.AsNoTracking().SingleAsync(a => a.Id == seed.ApplicationId);
        Assert.Equal(seed.ScreeningStageId, application.CurrentStageId);
        Assert.Single(_dbContext.StageTransitions);
    }

    [Fact]
    public async Task MoveApplicationAsync_BackwardMove_Succeeds()
    {
        var seed = await SeedActiveApplicationAsync();
        var recruiterId = await CreateRecruiterAsync();
        var forward = await _service.MoveApplicationAsync(
            seed.ApplicationId, new MoveApplicationRequestDto(seed.InterviewStageId, seed.AppliedStageId, null), recruiterId);
        Assert.True(forward.IsSuccess);

        var backward = await _service.MoveApplicationAsync(
            seed.ApplicationId, new MoveApplicationRequestDto(seed.AppliedStageId, seed.InterviewStageId, null), recruiterId);

        Assert.True(backward.IsSuccess);
        Assert.Equal(seed.AppliedStageId, backward.Value!.CurrentStageId);
    }

    [Fact]
    public async Task MoveApplicationAsync_ForeignStage_ReturnsNotFound()
    {
        var seed = await SeedActiveApplicationAsync();
        var otherRequisitionId = await CreateDraftRequisitionAsync("Other Role");
        var foreignStageId = await AddStageDirectAsync(otherRequisitionId, "Applied", 0);
        var recruiterId = await CreateRecruiterAsync();

        var result = await _service.MoveApplicationAsync(
            seed.ApplicationId, new MoveApplicationRequestDto(foreignStageId, seed.AppliedStageId, null), recruiterId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("application.move.not-found", result.ErrorCode);
        var application = await _dbContext.Applications.AsNoTracking().SingleAsync(a => a.Id == seed.ApplicationId);
        Assert.Equal(seed.AppliedStageId, application.CurrentStageId);
    }

    [Fact]
    public async Task MoveApplicationAsync_MissingStage_ReturnsNotFound()
    {
        var seed = await SeedActiveApplicationAsync();
        var recruiterId = await CreateRecruiterAsync();

        var result = await _service.MoveApplicationAsync(
            seed.ApplicationId, new MoveApplicationRequestDto(Guid.NewGuid(), seed.AppliedStageId, null), recruiterId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("application.move.not-found", result.ErrorCode);
    }

    [Fact]
    public async Task MoveApplicationAsync_AlreadyRejected_ReturnsConflict()
    {
        var seed = await SeedActiveApplicationAsync();
        var recruiterId = await CreateRecruiterAsync();
        var reject = await _service.RejectApplicationAsync(seed.ApplicationId, new RejectApplicationRequestDto(null), recruiterId);
        Assert.True(reject.IsSuccess);

        var result = await _service.MoveApplicationAsync(
            seed.ApplicationId, new MoveApplicationRequestDto(seed.ScreeningStageId, seed.AppliedStageId, null), recruiterId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("application.move.already-rejected", result.ErrorCode);
    }

    [Fact]
    public async Task MoveApplicationAsync_StaleExpectedStage_ReturnsConflictWithActual()
    {
        var seed = await SeedActiveApplicationAsync();
        var recruiterId = await CreateRecruiterAsync();

        var result = await _service.MoveApplicationAsync(
            seed.ApplicationId, new MoveApplicationRequestDto(seed.InterviewStageId, seed.ScreeningStageId, null), recruiterId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("application.move.conflict", result.ErrorCode);
        Assert.NotNull(result.Extensions);
        Assert.Equal(seed.AppliedStageId, (Guid)result.Extensions!["actualCurrentStageId"]);
        Assert.Equal("Applied", (string)result.Extensions["actualCurrentStageName"]);
    }

    [Fact]
    public async Task MoveApplicationAsync_ConcurrentSaveChanges_ThrowsConcurrencyMappedToConflict()
    {
        var seed = await SeedActiveApplicationAsync();
        var recruiterId = await CreateRecruiterAsync();

        // Pre-load and track the Application on this test's own DbContext — its current and
        // original CurrentStageId both become "Applied", simulating a Recruiter whose in-memory
        // view of the board is about to go stale.
        await _dbContext.Applications.FirstAsync(a => a.Id == seed.ApplicationId);

        // A second, independent DbContext (same underlying connection) moves the Application
        // first, so the database row's CurrentStageId becomes "Screening" while _dbContext's
        // already-tracked copy still reads "Applied" (EF Core does not silently overwrite a
        // tracked entity's values from a later query).
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        await using (var otherContext = new AppDbContext(options))
        {
            var otherService = new PipelineService(otherContext);
            var otherResult = await otherService.MoveApplicationAsync(
                seed.ApplicationId, new MoveApplicationRequestDto(seed.ScreeningStageId, seed.AppliedStageId, null), recruiterId);
            Assert.True(otherResult.IsSuccess);
        }

        // _dbContext's own pre-check (step 6) sees its stale in-memory "Applied" value, which
        // still matches the caller's (also stale) belief — so it is SaveChangesAsync's
        // concurrency-token mismatch, not the pre-check, that actually catches this race (HLD §4.2).
        var result = await _service.MoveApplicationAsync(
            seed.ApplicationId, new MoveApplicationRequestDto(seed.InterviewStageId, seed.AppliedStageId, null), recruiterId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("application.move.conflict", result.ErrorCode);
        Assert.NotNull(result.Extensions);
        Assert.Equal(seed.ScreeningStageId, (Guid)result.Extensions!["actualCurrentStageId"]);
    }

    [Fact]
    public async Task MoveApplicationAsync_ClosedRequisition_ReturnsConflict()
    {
        var requisitionId = await CreateDraftRequisitionAsync();
        var appliedId = await AddStageDirectAsync(requisitionId, "Applied", 0);
        var screeningId = await AddStageDirectAsync(requisitionId, "Screening", 1);
        var candidateId = await CreateCandidateAsync("candidate@example.com");
        var applicationId = await CreateApplicationAsync(requisitionId, candidateId, appliedId);
        var recruiterId = await CreateRecruiterAsync();

        var requisition = await _dbContext.Requisitions.SingleAsync(r => r.Id == requisitionId);
        requisition.Publish();
        requisition.Close();
        await _dbContext.SaveChangesAsync();

        var result = await _service.MoveApplicationAsync(
            applicationId, new MoveApplicationRequestDto(screeningId, appliedId, null), recruiterId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("application.move.requisition-closed", result.ErrorCode);
    }

    [Fact]
    public async Task MoveApplicationAsync_MissingApplication_ReturnsNotFound()
    {
        var recruiterId = await CreateRecruiterAsync();

        var result = await _service.MoveApplicationAsync(
            Guid.NewGuid(), new MoveApplicationRequestDto(Guid.NewGuid(), Guid.NewGuid(), null), recruiterId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("application.move.not-found", result.ErrorCode);
    }

    [Fact]
    public async Task RejectApplicationAsync_ActiveApplication_SetsRejectedKeepsStage()
    {
        var seed = await SeedActiveApplicationAsync();
        var recruiterId = await CreateRecruiterAsync();

        var result = await _service.RejectApplicationAsync(
            seed.ApplicationId, new RejectApplicationRequestDto("Not enough depth."), recruiterId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsRejected);
        Assert.Equal(seed.AppliedStageId, result.Value.CurrentStageId);
        Assert.Equal("reject", result.Value.Transition.Kind);
        Assert.Null(result.Value.Transition.ToStageId);

        var application = await _dbContext.Applications.AsNoTracking().SingleAsync(a => a.Id == seed.ApplicationId);
        Assert.True(application.IsRejected);
        Assert.Equal(seed.AppliedStageId, application.CurrentStageId);
    }

    [Fact]
    public async Task RejectApplicationAsync_AlreadyRejected_ReturnsConflict()
    {
        var seed = await SeedActiveApplicationAsync();
        var recruiterId = await CreateRecruiterAsync();
        var first = await _service.RejectApplicationAsync(seed.ApplicationId, new RejectApplicationRequestDto(null), recruiterId);
        Assert.True(first.IsSuccess);

        var result = await _service.RejectApplicationAsync(seed.ApplicationId, new RejectApplicationRequestDto(null), recruiterId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("application.reject.already-rejected", result.ErrorCode);
        Assert.Single(_dbContext.StageTransitions);
    }

    [Fact]
    public async Task RejectApplicationAsync_WithNote_NoteVisibleInHistoryOnly()
    {
        var seed = await SeedActiveApplicationAsync();
        var recruiterId = await CreateRecruiterAsync();

        var reject = await _service.RejectApplicationAsync(
            seed.ApplicationId, new RejectApplicationRequestDto("Not enough backend depth."), recruiterId);
        Assert.True(reject.IsSuccess);

        var history = await _service.GetTransitionHistoryAsync(seed.ApplicationId);

        Assert.True(history.IsSuccess);
        Assert.Single(history.Value!);
        Assert.Equal("Not enough backend depth.", history.Value![0].Note);
    }

    [Fact]
    public async Task RejectApplicationAsync_ClosedRequisition_ReturnsConflict()
    {
        var requisitionId = await CreateDraftRequisitionAsync();
        var appliedId = await AddStageDirectAsync(requisitionId, "Applied", 0);
        var candidateId = await CreateCandidateAsync("candidate@example.com");
        var applicationId = await CreateApplicationAsync(requisitionId, candidateId, appliedId);
        var recruiterId = await CreateRecruiterAsync();

        var requisition = await _dbContext.Requisitions.SingleAsync(r => r.Id == requisitionId);
        requisition.Publish();
        requisition.Close();
        await _dbContext.SaveChangesAsync();

        var result = await _service.RejectApplicationAsync(applicationId, new RejectApplicationRequestDto(null), recruiterId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("application.reject.requisition-closed", result.ErrorCode);
    }

    [Fact]
    public async Task RejectApplicationAsync_MissingApplication_ReturnsNotFound()
    {
        var recruiterId = await CreateRecruiterAsync();

        var result = await _service.RejectApplicationAsync(Guid.NewGuid(), new RejectApplicationRequestDto(null), recruiterId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("application.reject.not-found", result.ErrorCode);
    }

    // ---- Board / History (T-31) ----

    [Fact]
    public async Task GetPipelineBoardAsync_GroupsByStageWithRejectedSeparate()
    {
        var requisitionId = await CreateDraftRequisitionAsync();
        var appliedId = await AddStageDirectAsync(requisitionId, "Applied", 0);
        var screeningId = await AddStageDirectAsync(requisitionId, "Screening", 1);
        var candidate1 = await CreateCandidateAsync("c1@example.com");
        var candidate2 = await CreateCandidateAsync("c2@example.com");
        var candidate3 = await CreateCandidateAsync("c3@example.com");
        var app1 = await CreateApplicationAsync(requisitionId, candidate1, appliedId);
        await CreateApplicationAsync(requisitionId, candidate2, screeningId);
        var app3 = await CreateApplicationAsync(requisitionId, candidate3, appliedId);
        var recruiterId = await CreateRecruiterAsync();
        var reject = await _service.RejectApplicationAsync(app3, new RejectApplicationRequestDto(null), recruiterId);
        Assert.True(reject.IsSuccess);

        var result = await _service.GetPipelineBoardAsync(requisitionId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Stages.Count);
        var appliedGroup = result.Value.Stages.Single(s => s.StageId == appliedId);
        Assert.Equal(1, appliedGroup.Count);
        Assert.Equal(app1, appliedGroup.Applications.Single().ApplicationId);
        var screeningGroup = result.Value.Stages.Single(s => s.StageId == screeningId);
        Assert.Equal(1, screeningGroup.Count);
        Assert.Equal(1, result.Value.Rejected.Count);
        Assert.Equal(app3, result.Value.Rejected.Applications.Single().ApplicationId);
    }

    [Fact]
    public async Task GetPipelineBoardAsync_ZeroApplications_EveryStageZeroCount()
    {
        var requisitionId = await CreateDraftRequisitionAsync();
        await AddStageDirectAsync(requisitionId, "Applied", 0);
        await AddStageDirectAsync(requisitionId, "Screening", 1);

        var result = await _service.GetPipelineBoardAsync(requisitionId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Stages.Count);
        Assert.All(result.Value.Stages, s => Assert.Equal(0, s.Count));
        Assert.Empty(result.Value.Rejected.Applications);
    }

    [Fact]
    public async Task GetPipelineBoardAsync_MissingRequisition_ReturnsNotFound()
    {
        var result = await _service.GetPipelineBoardAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("requisition.pipeline.not-found", result.ErrorCode);
    }

    [Fact]
    public async Task GetTransitionHistoryAsync_ReturnsChronological()
    {
        var seed = await SeedActiveApplicationAsync();
        var recruiterId = await CreateRecruiterAsync();
        await _service.MoveApplicationAsync(
            seed.ApplicationId, new MoveApplicationRequestDto(seed.ScreeningStageId, seed.AppliedStageId, null), recruiterId);
        await _service.MoveApplicationAsync(
            seed.ApplicationId, new MoveApplicationRequestDto(seed.InterviewStageId, seed.ScreeningStageId, null), recruiterId);

        var result = await _service.GetTransitionHistoryAsync(seed.ApplicationId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("Screening", result.Value[0].ToStageName);
        Assert.Equal("Interview", result.Value[1].ToStageName);
        Assert.True(result.Value[0].OccurredAtUtc <= result.Value[1].OccurredAtUtc);
    }

    [Fact]
    public async Task GetTransitionHistoryAsync_NoTransitions_ReturnsEmptyList()
    {
        var seed = await SeedActiveApplicationAsync();

        var result = await _service.GetTransitionHistoryAsync(seed.ApplicationId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetTransitionHistoryAsync_MissingApplication_ReturnsNotFound()
    {
        var result = await _service.GetTransitionHistoryAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("application.transitions.not-found", result.ErrorCode);
    }
}
