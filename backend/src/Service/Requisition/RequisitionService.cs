namespace Ats.Service.Requisition;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ats.Db;
using Ats.Service.Common;
using Ats.Service.Requisition.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RequisitionEntity = Ats.Db.Requisitions.Requisition;
using RequisitionStatus = Ats.Db.Requisitions.RequisitionStatus;

public class RequisitionService : IRequisitionService
{
    private const int DefaultMaxPageSize = 50;

    private readonly AppDbContext _dbContext;
    private readonly int _maxPageSize;

    public RequisitionService(AppDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        ArgumentNullException.ThrowIfNull(configuration);

        _maxPageSize = int.TryParse(configuration["Requisitions:MaxPageSize"], out var max) ? max : DefaultMaxPageSize;
    }

    public async Task<Result<RequisitionDto>> CreateAsync(CreateRequisitionRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var errors = ValidateContent(dto.Title, dto.Description);
        if (errors != null)
        {
            return Result<RequisitionDto>.Validation(errors, "requisition.create.validation-failed", "Validation failed.");
        }

        var requisition = RequisitionEntity.Create(dto.Title, dto.Description);
        _dbContext.Requisitions.Add(requisition);
        await _dbContext.SaveChangesAsync(ct);

        return Result<RequisitionDto>.Ok(ToDto(requisition));
    }

    public async Task<Result<RequisitionDto>> UpdateContentAsync(Guid id, UpdateRequisitionRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var requisition = await _dbContext.Requisitions.FindAsync(new object?[] { id }, ct);
        if (requisition == null)
        {
            return Result<RequisitionDto>.NotFound("requisition.not-found", "Requisition not found.");
        }

        if (requisition.Status == RequisitionStatus.Closed)
        {
            return Result<RequisitionDto>.Conflict("requisition.update.closed", "A closed requisition cannot be edited.");
        }

        var errors = ValidateContent(dto.Title, dto.Description);
        if (errors != null)
        {
            return Result<RequisitionDto>.Validation(errors, "requisition.update.validation-failed", "Validation failed.");
        }

        requisition.UpdateContent(dto.Title, dto.Description);
        await _dbContext.SaveChangesAsync(ct);

        return Result<RequisitionDto>.Ok(ToDto(requisition));
    }

    public async Task<Result<RequisitionDto>> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var requisition = await _dbContext.Requisitions.FindAsync(new object?[] { id }, ct);
        if (requisition == null)
        {
            return Result<RequisitionDto>.NotFound("requisition.not-found", "Requisition not found.");
        }

        if (requisition.Status != RequisitionStatus.Draft)
        {
            return Result<RequisitionDto>.Conflict("requisition.publish.invalid-transition", "Only a draft requisition can be published.");
        }

        requisition.Publish();
        await _dbContext.SaveChangesAsync(ct);

        return Result<RequisitionDto>.Ok(ToDto(requisition));
    }

    public async Task<Result<RequisitionDto>> UnpublishAsync(Guid id, CancellationToken ct = default)
    {
        var requisition = await _dbContext.Requisitions.FindAsync(new object?[] { id }, ct);
        if (requisition == null)
        {
            return Result<RequisitionDto>.NotFound("requisition.not-found", "Requisition not found.");
        }

        if (requisition.Status != RequisitionStatus.Published)
        {
            return Result<RequisitionDto>.Conflict("requisition.unpublish.invalid-transition", "Only a published requisition can be unpublished.");
        }

        requisition.Unpublish();
        await _dbContext.SaveChangesAsync(ct);

        return Result<RequisitionDto>.Ok(ToDto(requisition));
    }

    public async Task<Result<RequisitionDto>> CloseAsync(Guid id, CancellationToken ct = default)
    {
        var requisition = await _dbContext.Requisitions.FindAsync(new object?[] { id }, ct);
        if (requisition == null)
        {
            return Result<RequisitionDto>.NotFound("requisition.not-found", "Requisition not found.");
        }

        if (requisition.Status != RequisitionStatus.Published)
        {
            return Result<RequisitionDto>.Conflict("requisition.close.invalid-transition", "Only a published requisition can be closed.");
        }

        requisition.Close();
        await _dbContext.SaveChangesAsync(ct);

        return Result<RequisitionDto>.Ok(ToDto(requisition));
    }

    public async Task<Result<RequisitionDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var requisition = await _dbContext.Requisitions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (requisition == null)
        {
            return Result<RequisitionDto>.NotFound("requisition.not-found", "Requisition not found.");
        }

        return Result<RequisitionDto>.Ok(ToDto(requisition));
    }

    public async Task<Result<IReadOnlyList<RequisitionDto>>> ListAsync(CancellationToken ct = default)
    {
        var requisitions = await _dbContext.Requisitions
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        IReadOnlyList<RequisitionDto> dtos = requisitions.Select(ToDto).ToList();
        return Result<IReadOnlyList<RequisitionDto>>.Ok(dtos);
    }

    public async Task<Result<PublicRequisitionDto>> GetPublicByIdAsync(Guid id, CancellationToken ct = default)
    {
        // "Doesn't exist" and "exists but isn't published" share one branch and one response —
        // no distinguishing code path, per AC-22/E-10 (no existence leak).
        var requisition = await _dbContext.Requisitions
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id && r.Status == RequisitionStatus.Published, ct);

        if (requisition == null)
        {
            return Result<PublicRequisitionDto>.NotFound("requisition.not-found", "This job posting is no longer available.");
        }

        return Result<PublicRequisitionDto>.Ok(ToPublicDto(requisition));
    }

    public async Task<Result<PagedResult<PublicRequisitionDto>>> SearchPublicAsync(
        string? keyword, int page, int pageSize, CancellationToken ct = default)
    {
        // `page` is trusted to already be a validated positive integer — the endpoint rejects
        // an invalid one with 400 before this method runs (AC-24: "without executing the query").
        var resolvedPageSize = Math.Clamp(pageSize, 1, _maxPageSize);

        var query = _dbContext.Requisitions
            .AsNoTracking()
            .Where(r => r.Status == RequisitionStatus.Published);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // EF Core's Sqlite provider translates string.Contains to instr(), which is
            // case-sensitive — EF.Functions.Like forces a LIKE translation instead, which
            // SQLite evaluates case-insensitively for ASCII by default (LLD §3.2 intent;
            // see Deviation Log).
            var pattern = $"%{keyword}%";
            query = query.Where(r => EF.Functions.Like(r.Title, pattern) || EF.Functions.Like(r.Description, pattern));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .ToListAsync(ct);

        var dtoItems = items.Select(ToPublicDto).ToList();
        return Result<PagedResult<PublicRequisitionDto>>.Ok(
            new PagedResult<PublicRequisitionDto>(dtoItems, page, resolvedPageSize, total));
    }

    private static Dictionary<string, string[]>? ValidateContent(string title, string description)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(title))
        {
            errors["title"] = new[] { "Title is required." };
        }
        else if (title.Length > 200)
        {
            errors["title"] = new[] { "Title must be 200 characters or fewer." };
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            errors["description"] = new[] { "Description is required." };
        }

        return errors.Count > 0 ? errors : null;
    }

    private static RequisitionDto ToDto(RequisitionEntity requisition) => new(
        requisition.Id,
        requisition.Title,
        requisition.Description,
        requisition.Status.ToString().ToLowerInvariant(),
        requisition.CreatedAtUtc,
        requisition.UpdatedAtUtc);

    private static PublicRequisitionDto ToPublicDto(RequisitionEntity requisition) => new(
        requisition.Id,
        requisition.Title,
        requisition.Description,
        requisition.UpdatedAtUtc);
}
