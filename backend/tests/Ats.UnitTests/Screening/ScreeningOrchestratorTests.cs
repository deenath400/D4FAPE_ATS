namespace Ats.UnitTests.Screening;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ats.Db;
using Ats.Db.Applications;
using Ats.Db.Pipeline;
using Ats.Db.Requisitions;
using Ats.Service.Pipeline;
using Ats.Service.Screening;
using Ats.Shared.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

public class ScreeningOrchestratorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly Mock<IFileStorage> _fileStorageMock;
    private readonly Mock<IScreeningService> _screeningServiceMock;
    private readonly Mock<IPdfTextExtractor> _pdfTextExtractorMock;
    private readonly PipelineService _pipelineService;
    private readonly ScreeningOrchestrator _orchestrator;

    public ScreeningOrchestratorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        _fileStorageMock = new Mock<IFileStorage>();
        _screeningServiceMock = new Mock<IScreeningService>();
        _pdfTextExtractorMock = new Mock<IPdfTextExtractor>();
        _pdfTextExtractorMock
            .Setup(p => p.ExtractText(It.IsAny<Stream>()))
            .Returns("Senior C# .NET developer with microservices experience");

        _pipelineService = new PipelineService(_dbContext);
        _orchestrator = new ScreeningOrchestrator(
            _dbContext,
            _fileStorageMock.Object,
            _screeningServiceMock.Object,
            _pipelineService,
            NullLogger<ScreeningOrchestrator>.Instance,
            _pdfTextExtractorMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<(Requisition Requisition, Stage Stage1, Stage Stage2, Application Application)> SeedApplicationAsync()
    {
        var user = new Ats.Shared.Auth.ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = $"candidate-{Guid.NewGuid():N}@example.com",
            UserName = $"candidate-{Guid.NewGuid():N}@example.com",
            FirstName = "Test",
            LastName = "Candidate",
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var req = Requisition.Create("Senior Backend Engineer", "Build scalable services with C# .NET");
        req.Publish();
        _dbContext.Requisitions.Add(req);
        await _dbContext.SaveChangesAsync();

        var stage1 = Stage.Create(req.Id, "Applied", 0);
        var stage2 = Stage.Create(req.Id, "Screening", 1);
        _dbContext.Stages.AddRange(stage1, stage2);
        await _dbContext.SaveChangesAsync();

        var app = Application.Create(req.Id, user.Id, stage1.Id);
        var cv = CvAttachment.Create(app.Id, "key123.pdf", "resume.pdf", "application/pdf", 1024);
        app.AttachCv(cv);
        _dbContext.Applications.Add(app);
        await _dbContext.SaveChangesAsync();

        return (req, stage1, stage2, app);
    }

    [Fact]
    public async Task RunScreeningAsync_WithReadableCv_ProducesCompletedReport()
    {
        // Arrange
        var (_, _, _, app) = await SeedApplicationAsync();
        _fileStorageMock
            .Setup(f => f.OpenReadAsync("key123.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("Readable text")));

        _screeningServiceMock
            .Setup(s => s.EvaluateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScreeningResult(
                80, ScreeningRecommendation.Advance, "Good fit", "[\"C#\"]", "[\"None\"]"));

        // Act
        var result = await _orchestrator.RunScreeningAsync(app.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(80, result.Value!.Score);
        Assert.Equal("Advance", result.Value.Recommendation);
        Assert.Equal("Completed", result.Value.Status);
        Assert.Null(result.Value.FailureReason);

        var report = await _dbContext.ScreeningReports.SingleOrDefaultAsync(r => r.ApplicationId == app.Id);
        Assert.NotNull(report);
        Assert.Equal(80, report.Score);
        Assert.Equal(ScreeningStatus.Completed, report.Status);
    }

    [Fact]
    public async Task RunScreeningAsync_AdvanceScore_MovesToNextStage()
    {
        // Arrange
        var (_, stage1, stage2, app) = await SeedApplicationAsync();
        _fileStorageMock
            .Setup(f => f.OpenReadAsync("key123.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("Readable text")));

        _screeningServiceMock
            .Setup(s => s.EvaluateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScreeningResult(
                85, ScreeningRecommendation.Advance, "Top applicant", "[\".NET\"]", "[]"));

        // Act
        var result = await _orchestrator.RunScreeningAsync(app.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var updatedApp = await _dbContext.Applications.FindAsync(app.Id);
        Assert.Equal(stage2.Id, updatedApp!.CurrentStageId);

        var transition = await _dbContext.StageTransitions.SingleOrDefaultAsync(t => t.ApplicationId == app.Id);
        Assert.NotNull(transition);
        Assert.Equal(StageTransitionActorKind.System, transition.ActorKind);
        Assert.Equal("AI Screening Agent", transition.ActorDisplayLabel);
        Assert.Equal(stage1.Id, transition.FromStageId);
        Assert.Equal(stage2.Id, transition.ToStageId);
    }

    [Fact]
    public async Task RunScreeningAsync_ReviewScore_StaysInInitialStage()
    {
        // Arrange
        var (_, stage1, _, app) = await SeedApplicationAsync();
        _fileStorageMock
            .Setup(f => f.OpenReadAsync("key123.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("Readable text")));

        _screeningServiceMock
            .Setup(s => s.EvaluateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScreeningResult(
                45, ScreeningRecommendation.Review, "Needs review", "[]", "[\"Lacks .NET experience\"]"));

        // Act
        var result = await _orchestrator.RunScreeningAsync(app.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var updatedApp = await _dbContext.Applications.FindAsync(app.Id);
        Assert.Equal(stage1.Id, updatedApp!.CurrentStageId);

        var transitionCount = await _dbContext.StageTransitions.CountAsync(t => t.ApplicationId == app.Id);
        Assert.Equal(0, transitionCount);
    }

    [Fact]
    public async Task RunScreeningAsync_AiFailure_SetsReportFailed()
    {
        // Arrange
        var (_, _, _, app) = await SeedApplicationAsync();
        _fileStorageMock
            .Setup(f => f.OpenReadAsync("key123.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("Readable text")));

        _screeningServiceMock
            .Setup(s => s.EvaluateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("503 Service Unavailable"));

        // Act
        var result = await _orchestrator.RunScreeningAsync(app.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Failed", result.Value!.Status);
        Assert.Contains("AI service unavailable", result.Value.FailureReason);

        var report = await _dbContext.ScreeningReports.SingleOrDefaultAsync(r => r.ApplicationId == app.Id);
        Assert.NotNull(report);
        Assert.Equal(ScreeningStatus.Failed, report.Status);
    }

    [Fact]
    public async Task RunScreeningAsync_EmptyCvText_SetsReportFailed()
    {
        // Arrange
        var (_, _, _, app) = await SeedApplicationAsync();
        _fileStorageMock
            .Setup(f => f.OpenReadAsync("key123.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("")));
        _pdfTextExtractorMock
            .Setup(p => p.ExtractText(It.IsAny<Stream>()))
            .Returns("");

        // Act
        var result = await _orchestrator.RunScreeningAsync(app.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Failed", result.Value!.Status);
        Assert.Equal("No extractable text found in CV attachment", result.Value.FailureReason);
    }

    [Fact]
    public async Task RunScreeningAsync_AlreadyAdvanced_UpdatesReportNoStageChange()
    {
        // Arrange
        var (_, _, stage2, app) = await SeedApplicationAsync();
        app.MoveToStage(stage2.Id);
        await _dbContext.SaveChangesAsync();

        _fileStorageMock
            .Setup(f => f.OpenReadAsync("key123.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("Readable text")));

        _screeningServiceMock
            .Setup(s => s.EvaluateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScreeningResult(
                90, ScreeningRecommendation.Advance, "Excellent", "[]", "[]"));

        // Act
        var result = await _orchestrator.RunScreeningAsync(app.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var updatedApp = await _dbContext.Applications.FindAsync(app.Id);
        Assert.Equal(stage2.Id, updatedApp!.CurrentStageId); // Not moved further or changed
    }

    [Fact]
    public async Task RunScreeningAsync_WhenRejected_ReturnsConflict()
    {
        // Arrange
        var (_, _, _, app) = await SeedApplicationAsync();
        app.Reject();
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _orchestrator.RunScreeningAsync(app.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("screening.run.already-rejected", result.ErrorCode);
    }

    [Fact]
    public async Task GetScreeningReportAsync_WhenNotFound_ReturnsNotFound()
    {
        // Act
        var result = await _orchestrator.GetScreeningReportAsync(Guid.NewGuid());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("screening.report.not-found", result.ErrorCode);
    }

    [Fact]
    public async Task GetScreeningReportAsync_WhenFound_ReturnsReportDto()
    {
        // Arrange
        var (_, _, _, app) = await SeedApplicationAsync();
        var report = ScreeningReport.CreatePending(app.Id);
        report.Complete(92, ScreeningRecommendation.Advance, "Great candidate", "[\"C#\"]", "[\"None\"]");
        _dbContext.ScreeningReports.Add(report);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _orchestrator.GetScreeningReportAsync(app.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(92, result.Value!.Score);
        Assert.Equal("Advance", result.Value.Recommendation);
        Assert.Equal(new[] { "C#" }, result.Value.Strengths);
    }
}
