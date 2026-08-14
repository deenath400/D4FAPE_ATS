namespace Ats.Service.Screening;

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ats.Db;
using Ats.Db.Applications;
using Ats.Db.Requisitions;
using Ats.Service.Common;
using Ats.Service.Pipeline;
using Ats.Service.Screening.Dtos;
using Ats.Shared.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class ScreeningOrchestrator : IScreeningOrchestrator
{
    private readonly AppDbContext _dbContext;
    private readonly IFileStorage _fileStorage;
    private readonly IScreeningService _screeningService;
    private readonly IPipelineService _pipelineService;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly ILogger<ScreeningOrchestrator> _logger;

    public ScreeningOrchestrator(
        AppDbContext dbContext,
        IFileStorage fileStorage,
        IScreeningService screeningService,
        IPipelineService pipelineService,
        ILogger<ScreeningOrchestrator> logger,
        IPdfTextExtractor? pdfTextExtractor = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
        _screeningService = screeningService ?? throw new ArgumentNullException(nameof(screeningService));
        _pipelineService = pipelineService ?? throw new ArgumentNullException(nameof(pipelineService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pdfTextExtractor = pdfTextExtractor ?? new PdfTextExtractor();
    }

    public async Task<Result<ScreeningReportDto>> RunScreeningAsync(Guid applicationId, CancellationToken ct = default)
    {
        var application = await _dbContext.Applications
            .Include(a => a.CvAttachment)
            .Include(a => a.ScreeningReport)
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

        if (application == null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Screening requested for non-existent application {ApplicationId}", applicationId);
            }
            return Result<ScreeningReportDto>.NotFound("screening.run.not-found", "Application not found.");
        }

        var appId = application.Id;
        var reqId = application.RequisitionId;

        if (application.IsRejected)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Screening skipped for already rejected application {ApplicationId}", appId);
            }
            return Result<ScreeningReportDto>.Conflict(
                "screening.run.already-rejected", "Cannot screen a rejected application.");
        }

        var requisition = await _dbContext.Requisitions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reqId, ct);

        if (requisition == null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Screening failed: Requisition {RequisitionId} not found for application {ApplicationId}",
                    reqId, appId);
            }
            return Result<ScreeningReportDto>.NotFound("screening.run.requisition-not-found", "Requisition not found.");
        }

        // If a previous report exists (re-screen flow), remove it first to start clean.
        if (application.ScreeningReport != null)
        {
            _dbContext.ScreeningReports.Remove(application.ScreeningReport);
            await _dbContext.SaveChangesAsync(ct);
        }

        var report = ScreeningReport.CreatePending(appId);
        _dbContext.ScreeningReports.Add(report);
        await _dbContext.SaveChangesAsync(ct);

        if (application.CvAttachment == null || string.IsNullOrWhiteSpace(application.CvAttachment.StorageKey))
        {
            report.Fail("No CV attachment found for this application");
            await _dbContext.SaveChangesAsync(ct);
            return Result<ScreeningReportDto>.Ok(ToDto(report));
        }

        string cvText;
        try
        {
            await using var stream = await _fileStorage.OpenReadAsync(application.CvAttachment.StorageKey, ct);
            cvText = _pdfTextExtractor.ExtractText(stream);
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(ex, "Failed to read or extract CV text for application {ApplicationId}", appId);
            }
            cvText = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(cvText))
        {
            report.Fail("No extractable text found in CV attachment");
            await _dbContext.SaveChangesAsync(ct);
            return Result<ScreeningReportDto>.Ok(ToDto(report));
        }

        if (requisition.Status == RequisitionStatus.Closed)
        {
            report.Fail("Requisition is closed");
            await _dbContext.SaveChangesAsync(ct);
            return Result<ScreeningReportDto>.Ok(ToDto(report));
        }

        ScreeningResult? screeningResult = null;
        try
        {
            screeningResult = await _screeningService.EvaluateAsync(
                requisition.Title, requisition.Description, cvText, ct);
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(ex, "Screening evaluation attempt 1 failed for application {ApplicationId}. Retrying...",
                    appId);
            }
            try
            {
                await Task.Delay(100, ct);
                screeningResult = await _screeningService.EvaluateAsync(
                    requisition.Title, requisition.Description, cvText, ct);
            }
            catch (Exception retryEx)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(retryEx, "Screening evaluation attempt 2 failed for application {ApplicationId}",
                        appId);
                }
                report.Fail($"AI service unavailable after retry: {retryEx.Message}");
                await _dbContext.SaveChangesAsync(ct);
                return Result<ScreeningReportDto>.Ok(ToDto(report));
            }
        }

        report.Complete(
            screeningResult.Score,
            screeningResult.Recommendation,
            screeningResult.Summary,
            screeningResult.Strengths,
            screeningResult.Concerns);

        await _dbContext.SaveChangesAsync(ct);

        // Auto-advance check (FR-3, FR-4, FR-8):
        // Only if recommendation is Advance AND application is in initial stage AND not rejected
        if (screeningResult.Recommendation == ScreeningRecommendation.Advance)
        {
            var stages = await _dbContext.Stages
                .AsNoTracking()
                .Where(s => s.RequisitionId == reqId)
                .OrderBy(s => s.SortOrder)
                .ToListAsync(ct);

            if (stages.Count >= 2 && application.CurrentStageId == stages[0].Id && !application.IsRejected)
            {
                var advanceNote = $"Automated screening: score {screeningResult.Score}, recommendation Advance";
                var moveResult = await _pipelineService.SystemMoveToNextStageAsync(appId, advanceNote, ct);
                if (!moveResult.IsSuccess)
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        var errCode = moveResult.ErrorCode;
                        _logger.LogWarning("System auto-advance could not move application {ApplicationId}: {ErrorCode}",
                            appId, errCode);
                    }
                }
            }
        }

        return Result<ScreeningReportDto>.Ok(ToDto(report));
    }

    public async Task<Result<ScreeningReportDto>> GetScreeningReportAsync(Guid applicationId, CancellationToken ct = default)
    {
        var report = await _dbContext.ScreeningReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ApplicationId == applicationId, ct);

        if (report == null)
        {
            return Result<ScreeningReportDto>.NotFound(
                "screening.report.not-found", "No screening report found for this application.");
        }

        return Result<ScreeningReportDto>.Ok(ToDto(report));
    }

    private static ScreeningReportDto ToDto(ScreeningReport report)
    {
        string[] strengths;
        try
        {
            strengths = JsonSerializer.Deserialize<string[]>(report.Strengths) ?? Array.Empty<string>();
        }
        catch
        {
            strengths = Array.Empty<string>();
        }

        string[] concerns;
        try
        {
            concerns = JsonSerializer.Deserialize<string[]>(report.Concerns) ?? Array.Empty<string>();
        }
        catch
        {
            concerns = Array.Empty<string>();
        }

        return new ScreeningReportDto(
            report.Id,
            report.ApplicationId,
            report.Score,
            report.Recommendation.ToString(),
            report.Summary,
            strengths,
            concerns,
            report.Status.ToString(),
            report.FailureReason,
            report.EvaluatedAtUtc);
    }
}
