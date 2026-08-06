namespace Ats.UnitTests.Application;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ats.Db;
using Ats.Service.Application;
using Ats.Service.Common;
using Ats.Shared.Auth;
using Ats.Shared.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using RequisitionEntity = Ats.Db.Requisitions.Requisition;

public class ApplicationServiceTests : IDisposable
{
    private const string PdfContent = "%PDF-1.4 fake pdf content for testing";

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly string _storageBasePath;
    private readonly IConfiguration _configuration;
    private readonly ApplicationService _service;

    public ApplicationServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        _storageBasePath = Path.Combine(Path.GetTempPath(), "ats-application-service-tests-" + Guid.NewGuid().ToString("N"));

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:CvBasePath"] = _storageBasePath,
                ["Applications:MaxCvSizeBytes"] = "5242880"
            })
            .Build();

        _service = new ApplicationService(_dbContext, new LocalDiskFileStorage(_configuration), _configuration);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
        if (Directory.Exists(_storageBasePath))
        {
            Directory.Delete(_storageBasePath, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private async Task<Guid> CreatePublishedRequisitionAsync(string title = "Senior Engineer")
    {
        var requisition = RequisitionEntity.Create(title, "Description");
        requisition.Publish();
        _dbContext.Requisitions.Add(requisition);

        // This helper builds the Requisition entity directly rather than going through
        // RequisitionService.CreateAsync, so it must seed a Stage itself (0005 FR-7: SubmitAsync
        // now requires at least one Stage to assign as the Application's current Stage).
        _dbContext.Stages.Add(Ats.Db.Requisitions.Stage.Create(requisition.Id, "Applied", 0));

        await _dbContext.SaveChangesAsync();
        return requisition.Id;
    }

    private async Task<Guid> CreatePublishedRequisitionWithNoStagesAsync(string title = "Stageless Role")
    {
        // R-1 (HLD §9) test fixture: a published Requisition a Recruiter has edited down to
        // zero Stages before any Application arrived — deliberately skips the Stage seeding
        // CreatePublishedRequisitionAsync performs.
        var requisition = RequisitionEntity.Create(title, "Description");
        requisition.Publish();
        _dbContext.Requisitions.Add(requisition);
        await _dbContext.SaveChangesAsync();
        return requisition.Id;
    }

    private async Task<Guid> CreateDraftRequisitionAsync(string title = "Draft Role")
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

    private async Task<Guid> CreateCandidateAsync(string email = "candidate@example.com")
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            FirstName = "Ada",
            LastName = "Lovelace",
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user.Id;
    }

    private static MemoryStream ValidPdfStream() => new(Encoding.UTF8.GetBytes(PdfContent));

    [Fact]
    public async Task SubmitAsync_ValidPdf_ReturnsCreatedWithPersistedCv()
    {
        var requisitionId = await CreatePublishedRequisitionAsync();
        var candidateId = await CreateCandidateAsync();
        using var cvContent = ValidPdfStream();

        var result = await _service.SubmitAsync(
            requisitionId, candidateId, cvContent, "resume.pdf", "application/pdf", cvContent.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal(requisitionId, result.Value!.RequisitionId);
        Assert.Equal(candidateId, result.Value.CandidateId);
        Assert.Equal("resume.pdf", result.Value.Cv.FileName);
        Assert.Equal("application/pdf", result.Value.Cv.ContentType);
        Assert.Equal(cvContent.Length, result.Value.Cv.SizeBytes);
        Assert.Single(_dbContext.Applications);
        Assert.Single(_dbContext.CvAttachments);
    }

    [Fact]
    public async Task SubmitAsync_NoFile_ReturnsValidationNoRowWritten()
    {
        var requisitionId = await CreatePublishedRequisitionAsync();
        var candidateId = await CreateCandidateAsync();
        using var emptyContent = new MemoryStream();

        var result = await _service.SubmitAsync(
            requisitionId, candidateId, emptyContent, "resume.pdf", "application/pdf", 0);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Equal("application.submit.cv-required", result.ErrorCode);
        Assert.Empty(_dbContext.Applications);
    }

    [Fact]
    public async Task SubmitAsync_NonPdfContentType_ReturnsValidation()
    {
        var requisitionId = await CreatePublishedRequisitionAsync();
        var candidateId = await CreateCandidateAsync();
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("fake docx content"));

        var result = await _service.SubmitAsync(
            requisitionId, candidateId, content, "resume.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", content.Length);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Equal("application.submit.invalid-file-type", result.ErrorCode);
        Assert.Empty(_dbContext.Applications);
    }

    [Fact]
    public async Task SubmitAsync_PdfExtensionWrongMagicBytes_ReturnsValidation()
    {
        var requisitionId = await CreatePublishedRequisitionAsync();
        var candidateId = await CreateCandidateAsync();
        using var badContent = new MemoryStream(Encoding.UTF8.GetBytes("NOT A REAL PDF FILE, JUST TEXT"));

        var result = await _service.SubmitAsync(
            requisitionId, candidateId, badContent, "resume.pdf", "application/pdf", badContent.Length);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Equal("application.submit.invalid-file-type", result.ErrorCode);
        Assert.Empty(_dbContext.Applications);
    }

    [Fact]
    public async Task SubmitAsync_OversizedFile_ReturnsValidation()
    {
        var requisitionId = await CreatePublishedRequisitionAsync();
        var candidateId = await CreateCandidateAsync();

        var tinyLimitConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:CvBasePath"] = _storageBasePath,
                ["Applications:MaxCvSizeBytes"] = "10"
            })
            .Build();
        var service = new ApplicationService(_dbContext, new LocalDiskFileStorage(tinyLimitConfig), tinyLimitConfig);

        using var cvContent = ValidPdfStream();
        var result = await service.SubmitAsync(
            requisitionId, candidateId, cvContent, "resume.pdf", "application/pdf", cvContent.Length);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Equal("application.submit.file-too-large", result.ErrorCode);
        Assert.Empty(_dbContext.Applications);
    }

    [Fact]
    public async Task SubmitAsync_DraftRequisition_ReturnsNotFound()
    {
        var requisitionId = await CreateDraftRequisitionAsync();
        var candidateId = await CreateCandidateAsync();
        using var cvContent = ValidPdfStream();

        var result = await _service.SubmitAsync(
            requisitionId, candidateId, cvContent, "resume.pdf", "application/pdf", cvContent.Length);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("application.submit.requisition-not-found", result.ErrorCode);
        Assert.Empty(_dbContext.Applications);
    }

    [Fact]
    public async Task SubmitAsync_ClosedRequisition_ReturnsNotFound()
    {
        var requisitionId = await CreateClosedRequisitionAsync();
        var candidateId = await CreateCandidateAsync();
        using var cvContent = ValidPdfStream();

        var result = await _service.SubmitAsync(
            requisitionId, candidateId, cvContent, "resume.pdf", "application/pdf", cvContent.Length);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("application.submit.requisition-not-found", result.ErrorCode);
        Assert.Empty(_dbContext.Applications);
    }

    [Fact]
    public async Task SubmitAsync_MissingRequisition_ReturnsNotFoundIdenticalToClosed()
    {
        var candidateId = await CreateCandidateAsync();
        using var cvContent = ValidPdfStream();

        var result = await _service.SubmitAsync(
            Guid.NewGuid(), candidateId, cvContent, "resume.pdf", "application/pdf", cvContent.Length);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("application.submit.requisition-not-found", result.ErrorCode);
    }

    [Fact]
    public async Task SubmitAsync_SecondSubmissionSameCandidateSameRequisition_ReturnsConflict()
    {
        var requisitionId = await CreatePublishedRequisitionAsync();
        var candidateId = await CreateCandidateAsync();

        using (var cv1 = ValidPdfStream())
        {
            var first = await _service.SubmitAsync(
                requisitionId, candidateId, cv1, "resume.pdf", "application/pdf", cv1.Length);
            Assert.True(first.IsSuccess);
        }

        using var cv2 = ValidPdfStream();
        var result = await _service.SubmitAsync(
            requisitionId, candidateId, cv2, "resume.pdf", "application/pdf", cv2.Length);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("application.submit.duplicate", result.ErrorCode);
        Assert.Single(_dbContext.Applications);
    }

    [Fact]
    public async Task SubmitAsync_AssignsRequisitionsFirstStage()
    {
        // AC-10 (FR-7): the new Application's current Stage is the Requisition's first Stage in
        // current pipeline order — proven against a Requisition with more than one Stage so a
        // wrong (e.g. last-Stage) assignment would be caught.
        var requisition = RequisitionEntity.Create("Senior Engineer", "Description");
        requisition.Publish();
        _dbContext.Requisitions.Add(requisition);
        var screeningStage = Ats.Db.Requisitions.Stage.Create(requisition.Id, "Screening", 1);
        var appliedStage = Ats.Db.Requisitions.Stage.Create(requisition.Id, "Applied", 0);
        _dbContext.Stages.AddRange(screeningStage, appliedStage);
        await _dbContext.SaveChangesAsync();

        var candidateId = await CreateCandidateAsync();
        using var cvContent = ValidPdfStream();

        var result = await _service.SubmitAsync(
            requisition.Id, candidateId, cvContent, "resume.pdf", "application/pdf", cvContent.Length);

        Assert.True(result.IsSuccess);
        var application = await _dbContext.Applications.AsNoTracking().SingleAsync(a => a.Id == result.Value!.Id);
        Assert.Equal(appliedStage.Id, application.CurrentStageId);
        Assert.False(application.IsRejected);
    }

    [Fact]
    public async Task SubmitAsync_NoStagesConfigured_ReturnsConflict()
    {
        // R-1 (HLD §9): a Requisition edited down to zero Stages before any Application arrived
        // must not silently create a stage-less Application (G-1).
        var requisitionId = await CreatePublishedRequisitionWithNoStagesAsync();
        var candidateId = await CreateCandidateAsync();
        using var cvContent = ValidPdfStream();

        var result = await _service.SubmitAsync(
            requisitionId, candidateId, cvContent, "resume.pdf", "application/pdf", cvContent.Length);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("application.submit.no-stages-configured", result.ErrorCode);
        Assert.Empty(_dbContext.Applications);
    }

    [Fact]
    public async Task SubmitAsync_TwoDistinctCandidatesSameRequisition_BothSucceed()
    {
        var requisitionId = await CreatePublishedRequisitionAsync();
        var candidate1 = await CreateCandidateAsync("c1@example.com");
        var candidate2 = await CreateCandidateAsync("c2@example.com");

        using var cv1 = ValidPdfStream();
        var result1 = await _service.SubmitAsync(
            requisitionId, candidate1, cv1, "resume.pdf", "application/pdf", cv1.Length);
        using var cv2 = ValidPdfStream();
        var result2 = await _service.SubmitAsync(
            requisitionId, candidate2, cv2, "resume.pdf", "application/pdf", cv2.Length);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Equal(2, _dbContext.Applications.Count());
    }

    [Fact]
    public async Task SubmitAsync_SameCandidateDifferentRequisitions_BothSucceed()
    {
        var candidateId = await CreateCandidateAsync();
        var requisition1 = await CreatePublishedRequisitionAsync("Role One");
        var requisition2 = await CreatePublishedRequisitionAsync("Role Two");

        using var cv1 = ValidPdfStream();
        var result1 = await _service.SubmitAsync(
            requisition1, candidateId, cv1, "resume.pdf", "application/pdf", cv1.Length);
        using var cv2 = ValidPdfStream();
        var result2 = await _service.SubmitAsync(
            requisition2, candidateId, cv2, "resume.pdf", "application/pdf", cv2.Length);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Equal(2, _dbContext.Applications.Count());
    }

    [Fact]
    public async Task SubmitAsync_StorageThrows_ReturnsErrorNoRowWritten()
    {
        // NFR-1 dedicated verification (T-42): submission is atomic with respect to a valid,
        // persisted CV — a storage-write failure (E-4) must never leave an orphaned Application
        // (or CvAttachment) row behind. The write is attempted before either row is added to the
        // DbContext (ApplicationService.SubmitAsync step 8 precedes step 9), so a failure here
        // has nothing to roll back — but this test proves that structurally, not by inspection.
        var requisitionId = await CreatePublishedRequisitionAsync();
        var candidateId = await CreateCandidateAsync();

        var fileStorageMock = new Mock<IFileStorage>();
        fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk full."));

        var service = new ApplicationService(_dbContext, fileStorageMock.Object, _configuration);

        using var cvContent = ValidPdfStream();
        var result = await service.SubmitAsync(
            requisitionId, candidateId, cvContent, "resume.pdf", "application/pdf", cvContent.Length);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Error, result.Status);
        Assert.Equal("application.submit.storage-failed", result.ErrorCode);
        Assert.Empty(_dbContext.Applications);
        Assert.Empty(_dbContext.CvAttachments);
    }

    [Fact]
    public async Task SubmitAsync_SaveChangesThrowsDbUpdateException_DeletesFileReturnsConflict()
    {
        // E-1's structural fallback: force the SaveChangesAsync branch directly (rather than
        // racing two real threads) to isolate and verify the catch/cleanup behaviour. The
        // genuine concurrent race is covered end-to-end by T-44's integration test.
        var requisitionId = await CreatePublishedRequisitionAsync();
        var candidateId = await CreateCandidateAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        await using var throwingContext = new ThrowingSaveChangesDbContext(options);
        var fileStorage = new LocalDiskFileStorage(_configuration);
        var service = new ApplicationService(throwingContext, fileStorage, _configuration);

        using var cvContent = ValidPdfStream();
        var result = await service.SubmitAsync(
            requisitionId, candidateId, cvContent, "resume.pdf", "application/pdf", cvContent.Length);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("application.submit.duplicate", result.ErrorCode);
        Assert.Empty(_dbContext.Applications);
        Assert.False(Directory.Exists(_storageBasePath) && Directory.GetFiles(_storageBasePath).Length > 0);
    }

    [Fact]
    public async Task ListMineAsync_TwoApplications_ReturnsBothWithRequisitionTitle()
    {
        var candidateId = await CreateCandidateAsync();
        var req1 = await CreatePublishedRequisitionAsync("Backend Engineer");
        var req2 = await CreatePublishedRequisitionAsync("Frontend Engineer");

        using (var cv1 = ValidPdfStream())
        {
            await _service.SubmitAsync(req1, candidateId, cv1, "resume.pdf", "application/pdf", cv1.Length);
        }

        using (var cv2 = ValidPdfStream())
        {
            await _service.SubmitAsync(req2, candidateId, cv2, "resume.pdf", "application/pdf", cv2.Length);
        }

        var result = await _service.ListMineAsync(candidateId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Contains(result.Value, i => i.RequisitionTitle == "Backend Engineer");
        Assert.Contains(result.Value, i => i.RequisitionTitle == "Frontend Engineer");
    }

    [Fact]
    public async Task ListMineAsync_IncludesCurrentStageNameAndIsRejected()
    {
        // AC-22/AC-23 (FR-17): the Candidate's own-list rows carry the real current status —
        // a Stage name for an active Application, and isRejected=true (with its retained Stage
        // name still present, FR-10) for a rejected one.
        var candidateId = await CreateCandidateAsync();
        var activeRequisitionId = await CreatePublishedRequisitionAsync("Backend Engineer");
        var rejectedRequisitionId = await CreatePublishedRequisitionAsync("Frontend Engineer");

        using (var cv1 = ValidPdfStream())
        {
            var submitted = await _service.SubmitAsync(
                activeRequisitionId, candidateId, cv1, "resume.pdf", "application/pdf", cv1.Length);
            Assert.True(submitted.IsSuccess);
        }

        Guid rejectedApplicationId;
        using (var cv2 = ValidPdfStream())
        {
            var submitted = await _service.SubmitAsync(
                rejectedRequisitionId, candidateId, cv2, "resume.pdf", "application/pdf", cv2.Length);
            Assert.True(submitted.IsSuccess);
            rejectedApplicationId = submitted.Value!.Id;
        }

        var rejectedApplication = await _dbContext.Applications.SingleAsync(a => a.Id == rejectedApplicationId);
        rejectedApplication.Reject();
        await _dbContext.SaveChangesAsync();

        var result = await _service.ListMineAsync(candidateId);

        Assert.True(result.IsSuccess);
        var active = result.Value!.Single(i => i.RequisitionId == activeRequisitionId);
        Assert.Equal("Applied", active.CurrentStageName);
        Assert.False(active.IsRejected);

        var rejected = result.Value!.Single(i => i.RequisitionId == rejectedRequisitionId);
        Assert.Equal("Applied", rejected.CurrentStageName);
        Assert.True(rejected.IsRejected);
    }

    [Fact]
    public async Task ListMineAsync_NoApplications_ReturnsEmptyList()
    {
        var candidateId = await CreateCandidateAsync();

        var result = await _service.ListMineAsync(candidateId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task ListForRequisitionAsync_TwoApplications_ReturnsBothWithCandidateIdentity()
    {
        var requisitionId = await CreatePublishedRequisitionAsync();
        var candidate1 = await CreateCandidateAsync("c1@example.com");
        var candidate2 = await CreateCandidateAsync("c2@example.com");

        using (var cv1 = ValidPdfStream())
        {
            await _service.SubmitAsync(requisitionId, candidate1, cv1, "resume.pdf", "application/pdf", cv1.Length);
        }

        using (var cv2 = ValidPdfStream())
        {
            await _service.SubmitAsync(requisitionId, candidate2, cv2, "resume.pdf", "application/pdf", cv2.Length);
        }

        var result = await _service.ListForRequisitionAsync(requisitionId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Contains(result.Value, i => i.Candidate.Email == "c1@example.com");
        Assert.Contains(result.Value, i => i.Candidate.Email == "c2@example.com");
    }

    [Fact]
    public async Task ListForRequisitionAsync_MissingRequisition_ReturnsNotFound()
    {
        var result = await _service.ListForRequisitionAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("application.list.requisition-not-found", result.ErrorCode);
    }

    [Fact]
    public async Task ListForRequisitionAsync_NoApplications_ReturnsEmptyList()
    {
        var requisitionId = await CreatePublishedRequisitionAsync();

        var result = await _service.ListForRequisitionAsync(requisitionId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetCvAsync_Owner_ReturnsStream()
    {
        var requisitionId = await CreatePublishedRequisitionAsync();
        var candidateId = await CreateCandidateAsync();
        using var cvContent = ValidPdfStream();
        var submitResult = await _service.SubmitAsync(
            requisitionId, candidateId, cvContent, "resume.pdf", "application/pdf", cvContent.Length);
        Assert.True(submitResult.IsSuccess);

        var result = await _service.GetCvAsync(submitResult.Value!.Id, candidateId, requesterIsStaff: false);

        Assert.True(result.IsSuccess);
        await using var stream = result.Value!.Content;
        using var reader = new StreamReader(stream);
        Assert.Equal(PdfContent, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task GetCvAsync_NonOwnerCandidate_ReturnsForbidden()
    {
        var requisitionId = await CreatePublishedRequisitionAsync();
        var ownerCandidateId = await CreateCandidateAsync("owner@example.com");
        var otherCandidateId = await CreateCandidateAsync("other@example.com");
        using var cvContent = ValidPdfStream();
        var submitResult = await _service.SubmitAsync(
            requisitionId, ownerCandidateId, cvContent, "resume.pdf", "application/pdf", cvContent.Length);
        Assert.True(submitResult.IsSuccess);

        var result = await _service.GetCvAsync(submitResult.Value!.Id, otherCandidateId, requesterIsStaff: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Equal("application.cv.forbidden", result.ErrorCode);
    }

    [Fact]
    public async Task GetCvAsync_Staff_ReturnsStreamRegardlessOfOwnership()
    {
        var requisitionId = await CreatePublishedRequisitionAsync();
        var candidateId = await CreateCandidateAsync();
        using var cvContent = ValidPdfStream();
        var submitResult = await _service.SubmitAsync(
            requisitionId, candidateId, cvContent, "resume.pdf", "application/pdf", cvContent.Length);
        Assert.True(submitResult.IsSuccess);

        var result = await _service.GetCvAsync(submitResult.Value!.Id, Guid.NewGuid(), requesterIsStaff: true);

        Assert.True(result.IsSuccess);
        await result.Value!.Content.DisposeAsync();
    }

    [Fact]
    public async Task GetCvAsync_MissingApplication_ReturnsNotFound()
    {
        var result = await _service.GetCvAsync(Guid.NewGuid(), Guid.NewGuid(), requesterIsStaff: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("application.cv.not-found", result.ErrorCode);
    }

    /// <summary>
    /// Forces SaveChangesAsync to throw DbUpdateException on demand, used by
    /// <see cref="SubmitAsync_SaveChangesThrowsDbUpdateException_DeletesFileReturnsConflict"/> to
    /// exercise the E-1 structural-fallback branch without relying on genuine thread concurrency.
    /// </summary>
    private sealed class ThrowingSaveChangesDbContext : AppDbContext
    {
        public ThrowingSaveChangesDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new DbUpdateException(
                "Simulated unique constraint violation.", new InvalidOperationException("UNIQUE constraint failed"));
    }
}
