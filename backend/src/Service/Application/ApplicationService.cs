namespace Ats.Service.Application;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ats.Db;
using Ats.Db.Applications;
using Ats.Service.Application.Dtos;
using Ats.Service.Common;
using Ats.Shared.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ApplicationEntity = Ats.Db.Applications.Application;
using RequisitionStatus = Ats.Db.Requisitions.RequisitionStatus;

public class ApplicationService : IApplicationService
{
    private const int DefaultMaxCvSizeBytes = 5 * 1024 * 1024;

    // "%PDF-" — the first five bytes of every well-formed PDF file (Assumption A-8).
    private static readonly byte[] PdfMagicBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D };

    private readonly AppDbContext _dbContext;
    private readonly IFileStorage _fileStorage;
    private readonly int _maxCvSizeBytes;

    public ApplicationService(AppDbContext dbContext, IFileStorage fileStorage, IConfiguration configuration)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
        ArgumentNullException.ThrowIfNull(configuration);

        _maxCvSizeBytes = int.TryParse(configuration["Applications:MaxCvSizeBytes"], out var max)
            ? max
            : DefaultMaxCvSizeBytes;
    }

    public async Task<Result<ApplicationDto>> SubmitAsync(
        Guid requisitionId, Guid candidateId,
        Stream cvContent, string cvFileName, string cvContentType, long cvSizeBytes,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cvContent);
        cvFileName ??= string.Empty;
        cvContentType ??= string.Empty;

        // "Doesn't exist" and "exists but isn't published" share one branch and one response —
        // no distinguishing code path, reusing 0003's no-existence-leak pattern (FR-4, AC-5..7).
        var requisition = await _dbContext.Requisitions
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == requisitionId && r.Status == RequisitionStatus.Published, ct);

        if (requisition == null)
        {
            return Result<ApplicationDto>.NotFound(
                "application.submit.requisition-not-found", "This job posting is no longer available.");
        }

        if (cvSizeBytes <= 0)
        {
            return Result<ApplicationDto>.Validation(
                new Dictionary<string, string[]> { ["cv"] = new[] { "A CV file is required." } },
                "application.submit.cv-required", "Validation failed.");
        }

        if (!string.Equals(cvContentType, "application/pdf", StringComparison.OrdinalIgnoreCase) ||
            !cvFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Result<ApplicationDto>.Validation(
                new Dictionary<string, string[]> { ["cv"] = new[] { "Only PDF files are accepted." } },
                "application.submit.invalid-file-type", "Validation failed.");
        }

        if (cvSizeBytes > _maxCvSizeBytes)
        {
            return Result<ApplicationDto>.Validation(
                new Dictionary<string, string[]> { ["cv"] = new[] { "The CV file must be 5 MB or smaller." } },
                "application.submit.file-too-large", "Validation failed.");
        }

        if (!await HasPdfMagicBytesAsync(cvContent, ct))
        {
            return Result<ApplicationDto>.Validation(
                new Dictionary<string, string[]> { ["cv"] = new[] { "Only PDF files are accepted." } },
                "application.submit.invalid-file-type", "Validation failed.");
        }

        var alreadyApplied = await _dbContext.Applications
            .AnyAsync(a => a.CandidateId == candidateId && a.RequisitionId == requisitionId, ct);
        if (alreadyApplied)
        {
            return Result<ApplicationDto>.Conflict(
                "application.submit.duplicate", "You have already applied to this requisition.");
        }

        // FR-7 (0005): every Application is assigned its Requisition's first Stage (in current
        // pipeline order) at submission time. Pulled forward from CP-2's T-24 — see
        // implementation/changelog.md CP-1 Deviations — because T-03's change to
        // Application.Create (currentStageId now required) makes this resolution a compile-time
        // necessity, not a later enhancement; CP-2 completes the rest of T-24 (ListMineAsync's
        // currentStageName/isRejected projection) and its dedicated AC-10 test coverage.
        var firstStageId = await _dbContext.Stages
            .AsNoTracking()
            .Where(s => s.RequisitionId == requisitionId)
            .OrderBy(s => s.SortOrder)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(ct);

        if (firstStageId == Guid.Empty)
        {
            // R-1 (HLD §9): defensive guard for a Requisition a Recruiter has edited down to zero
            // Stages before any Application arrived. FR-5 guarantees every Requisition starts with
            // 4 Stages; FR-4 does not itself forbid removing all of them while unoccupied.
            return Result<ApplicationDto>.Conflict(
                "application.submit.no-stages-configured",
                "This job posting's pipeline has not been configured yet.");
        }

        // Server-generated — never derived from the client-supplied filename (NFR-2).
        var storageKey = $"{Guid.NewGuid():N}.pdf";

        try
        {
            await _fileStorage.SaveAsync(storageKey, cvContent, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // E-4: the write failed before any row exists — nothing to roll back.
            return Result<ApplicationDto>.Error(
                "application.submit.storage-failed", "Could not save the uploaded file. Please try again.");
        }

        var application = ApplicationEntity.Create(requisitionId, candidateId, firstStageId);
        var cv = CvAttachment.Create(application.Id, storageKey, cvFileName, cvContentType, cvSizeBytes);
        application.AttachCv(cv);
        _dbContext.Applications.Add(application);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // E-1: the structural fallback for the race the pre-check above cannot fully close —
            // the unique (CandidateId, RequisitionId) index is what actually enforces FR-5.
            await _fileStorage.DeleteAsync(storageKey, ct);
            return Result<ApplicationDto>.Conflict(
                "application.submit.duplicate", "You have already applied to this requisition.");
        }

        return Result<ApplicationDto>.Ok(ToDto(application, cv));
    }

    public async Task<Result<IReadOnlyList<CandidateApplicationListItemDto>>> ListMineAsync(
        Guid candidateId, CancellationToken ct = default)
    {
        // Join to a plain anonymous projection first, then order and construct the DTO in a
        // final Select — ordering by a property read off a constructed record does not
        // translate to SQL (EF Core), so the ORDER BY must apply to scalar columns.
        var items = await _dbContext.Applications
            .AsNoTracking()
            .Where(a => a.CandidateId == candidateId)
            .Join(
                _dbContext.Requisitions.AsNoTracking(),
                a => a.RequisitionId,
                r => r.Id,
                (a, r) => new { a.Id, a.RequisitionId, r.Title, a.SubmittedAtUtc })
            .OrderByDescending(x => x.SubmittedAtUtc)
            .Select(x => new CandidateApplicationListItemDto(
                x.Id, x.RequisitionId, x.Title, x.SubmittedAtUtc, $"/api/applications/{x.Id}/cv"))
            .ToListAsync(ct);

        return Result<IReadOnlyList<CandidateApplicationListItemDto>>.Ok(items);
    }

    public async Task<Result<IReadOnlyList<StaffApplicationListItemDto>>> ListForRequisitionAsync(
        Guid requisitionId, CancellationToken ct = default)
    {
        // Any status — staff already have unrestricted visibility into Requisitions of any
        // status via 0003's StaffOnly endpoints, so this list is not gated by Published.
        var requisitionExists = await _dbContext.Requisitions.AsNoTracking().AnyAsync(r => r.Id == requisitionId, ct);
        if (!requisitionExists)
        {
            return Result<IReadOnlyList<StaffApplicationListItemDto>>.NotFound(
                "application.list.requisition-not-found", "Requisition not found.");
        }

        var items = await _dbContext.Applications
            .AsNoTracking()
            .Where(a => a.RequisitionId == requisitionId)
            .Join(
                _dbContext.Users.AsNoTracking(),
                a => a.CandidateId,
                u => u.Id,
                (a, u) => new
                {
                    ApplicationId = a.Id,
                    CandidateId = u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    a.SubmittedAtUtc
                })
            .OrderByDescending(x => x.SubmittedAtUtc)
            .ToListAsync(ct);

        var dtos = items
            .Select(x => new StaffApplicationListItemDto(
                x.ApplicationId,
                new StaffApplicationCandidateDto(x.CandidateId, x.FirstName, x.LastName, x.Email ?? string.Empty),
                x.SubmittedAtUtc,
                $"/api/applications/{x.ApplicationId}/cv"))
            .ToList();

        return Result<IReadOnlyList<StaffApplicationListItemDto>>.Ok(dtos);
    }

    public async Task<Result<CvDownloadResult>> GetCvAsync(
        Guid applicationId, Guid requestingUserId, bool requesterIsStaff, CancellationToken ct = default)
    {
        var application = await _dbContext.Applications
            .AsNoTracking()
            .Include(a => a.CvAttachment)
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

        if (application?.CvAttachment == null)
        {
            return Result<CvDownloadResult>.NotFound("application.cv.not-found", "Application not found.");
        }

        if (!requesterIsStaff && application.CandidateId != requestingUserId)
        {
            return Result<CvDownloadResult>.Forbidden(
                "application.cv.forbidden", "You do not have access to this file.");
        }

        var stream = await _fileStorage.OpenReadAsync(application.CvAttachment.StorageKey, ct);
        return Result<CvDownloadResult>.Ok(new CvDownloadResult(
            stream, application.CvAttachment.OriginalFileName, application.CvAttachment.ContentType));
    }

    // Requires a seekable stream — the API layer buffers IFormFile.OpenReadStream() into a
    // MemoryStream before calling SubmitAsync (LLD §12); the raw IFormFile stream is forward-only.
    private static async Task<bool> HasPdfMagicBytesAsync(Stream cvContent, CancellationToken ct)
    {
        cvContent.Position = 0;
        var buffer = new byte[PdfMagicBytes.Length];
        var bytesRead = await cvContent.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
        cvContent.Position = 0;

        return bytesRead == PdfMagicBytes.Length && buffer.SequenceEqual(PdfMagicBytes);
    }

    private static ApplicationDto ToDto(ApplicationEntity application, CvAttachment cv) => new(
        application.Id,
        application.RequisitionId,
        application.CandidateId,
        application.SubmittedAtUtc,
        new ApplicationCvSummaryDto(cv.OriginalFileName, cv.ContentType, cv.SizeBytes));
}
