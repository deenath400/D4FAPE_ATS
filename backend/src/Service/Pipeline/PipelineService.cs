namespace Ats.Service.Pipeline;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ats.Db;
using Ats.Db.Pipeline;
using Ats.Service.Common;
using Ats.Service.Pipeline.Dtos;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = Ats.Db.Applications.Application;
using RequisitionStatus = Ats.Db.Requisitions.RequisitionStatus;
using StageEntity = Ats.Db.Requisitions.Stage;

public class PipelineService : IPipelineService
{
    private const int MaxNameLength = 200;
    private const int MaxNoteLength = 2000;

    private readonly AppDbContext _dbContext;

    public PipelineService(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Result<StageDto>> AddStageAsync(Guid requisitionId, AddStageRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var requisition = await _dbContext.Requisitions.FindAsync(new object?[] { requisitionId }, ct);
        if (requisition == null)
        {
            return Result<StageDto>.NotFound("stage.add.requisition-not-found", "Requisition not found.");
        }

        if (requisition.Status == RequisitionStatus.Closed)
        {
            return Result<StageDto>.Conflict(
                "stage.add.requisition-closed", "This requisition is closed and its pipeline can no longer be edited.");
        }

        var nameError = ValidateName(dto.Name);
        if (nameError != null)
        {
            return Result<StageDto>.Validation(nameError, "stage.add.validation-failed", "Validation failed.");
        }

        var stages = await _dbContext.Stages
            .Where(s => s.RequisitionId == requisitionId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

        var position = Math.Clamp(dto.Position ?? stages.Count, 0, stages.Count);

        var normalizedName = dto.Name.ToUpperInvariant();
        if (stages.Any(s => s.NormalizedName == normalizedName))
        {
            return Result<StageDto>.Conflict(
                "stage.add.duplicate-name", "A stage with this name already exists.");
        }

        foreach (var stage in stages.Where(s => s.SortOrder >= position))
        {
            stage.ChangeSortOrder(stage.SortOrder + 1);
        }

        var newStage = StageEntity.Create(requisitionId, dto.Name, position);
        _dbContext.Stages.Add(newStage);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Structural fallback for the race the pre-check above cannot fully close — the
            // unique (RequisitionId, NormalizedName) index is what actually enforces AC-31.
            return Result<StageDto>.Conflict(
                "stage.add.duplicate-name", "A stage with this name already exists.");
        }

        return Result<StageDto>.Ok(ToDto(newStage));
    }

    public async Task<Result<IReadOnlyList<StageDto>>> GetStagesAsync(Guid requisitionId, CancellationToken ct = default)
    {
        var requisitionExists = await _dbContext.Requisitions.AsNoTracking().AnyAsync(r => r.Id == requisitionId, ct);
        if (!requisitionExists)
        {
            return Result<IReadOnlyList<StageDto>>.NotFound(
                "stage.list.requisition-not-found", "Requisition not found.");
        }

        var stages = await _dbContext.Stages
            .AsNoTracking()
            .Where(s => s.RequisitionId == requisitionId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

        IReadOnlyList<StageDto> dtos = stages.Select(ToDto).ToList();
        return Result<IReadOnlyList<StageDto>>.Ok(dtos);
    }

    public async Task<Result<StageDto>> RenameStageAsync(
        Guid requisitionId, Guid stageId, RenameStageRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var requisition = await _dbContext.Requisitions.FindAsync(new object?[] { requisitionId }, ct);
        if (requisition == null)
        {
            return Result<StageDto>.NotFound("stage.rename.not-found", "Requisition or stage not found.");
        }

        if (requisition.Status == RequisitionStatus.Closed)
        {
            return Result<StageDto>.Conflict(
                "stage.rename.requisition-closed", "This requisition is closed and its pipeline can no longer be edited.");
        }

        var stage = await _dbContext.Stages.FirstOrDefaultAsync(s => s.Id == stageId, ct);
        if (stage == null || stage.RequisitionId != requisitionId)
        {
            // One unified 404 — no existence leak, whether the Stage does not exist at all or
            // belongs to a different Requisition.
            return Result<StageDto>.NotFound("stage.rename.not-found", "Requisition or stage not found.");
        }

        var nameError = ValidateName(dto.Name);
        if (nameError != null)
        {
            return Result<StageDto>.Validation(nameError, "stage.rename.validation-failed", "Validation failed.");
        }

        var normalizedName = dto.Name.ToUpperInvariant();
        var duplicateExists = await _dbContext.Stages
            .AsNoTracking()
            .AnyAsync(s => s.RequisitionId == requisitionId && s.Id != stageId && s.NormalizedName == normalizedName, ct);
        if (duplicateExists)
        {
            return Result<StageDto>.Conflict(
                "stage.rename.duplicate-name", "A stage with this name already exists.");
        }

        stage.Rename(dto.Name);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Result<StageDto>.Conflict(
                "stage.rename.duplicate-name", "A stage with this name already exists.");
        }

        return Result<StageDto>.Ok(ToDto(stage));
    }

    public async Task<Result<IReadOnlyList<StageDto>>> ReorderStagesAsync(
        Guid requisitionId, ReorderStagesRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var requisition = await _dbContext.Requisitions.FindAsync(new object?[] { requisitionId }, ct);
        if (requisition == null)
        {
            return Result<IReadOnlyList<StageDto>>.NotFound(
                "stage.reorder.requisition-not-found", "Requisition not found.");
        }

        if (requisition.Status == RequisitionStatus.Closed)
        {
            return Result<IReadOnlyList<StageDto>>.Conflict(
                "stage.reorder.requisition-closed", "This requisition is closed and its pipeline can no longer be edited.");
        }

        var stages = await _dbContext.Stages
            .Where(s => s.RequisitionId == requisitionId)
            .ToListAsync(ct);

        var stageIds = dto.StageIds ?? Array.Empty<Guid>();
        var isValidSet = stageIds.Count == stages.Count
            && stageIds.Distinct().Count() == stageIds.Count
            && stageIds.ToHashSet().SetEquals(stages.Select(s => s.Id));

        if (!isValidSet)
        {
            var errors = new Dictionary<string, string[]>
            {
                ["stageIds"] = new[] { "The stage list does not match this requisition's current stages." }
            };
            return Result<IReadOnlyList<StageDto>>.Validation(
                errors, "stage.reorder.invalid-set", "Validation failed.");
        }

        var stagesById = stages.ToDictionary(s => s.Id);
        for (var index = 0; index < stageIds.Count; index++)
        {
            stagesById[stageIds[index]].ChangeSortOrder(index);
        }

        await _dbContext.SaveChangesAsync(ct);

        IReadOnlyList<StageDto> dtos = stages.OrderBy(s => s.SortOrder).Select(ToDto).ToList();
        return Result<IReadOnlyList<StageDto>>.Ok(dtos);
    }

    public async Task<Result> RemoveStageAsync(Guid requisitionId, Guid stageId, CancellationToken ct = default)
    {
        var requisition = await _dbContext.Requisitions.FindAsync(new object?[] { requisitionId }, ct);
        if (requisition == null)
        {
            return Result.NotFound("stage.remove.not-found", "Requisition or stage not found.");
        }

        if (requisition.Status == RequisitionStatus.Closed)
        {
            return Result.Conflict(
                "stage.remove.requisition-closed", "This requisition is closed and its pipeline can no longer be edited.");
        }

        var stage = await _dbContext.Stages.FirstOrDefaultAsync(s => s.Id == stageId, ct);
        if (stage == null || stage.RequisitionId != requisitionId)
        {
            return Result.NotFound("stage.remove.not-found", "Requisition or stage not found.");
        }

        var occupied = await _dbContext.Applications.AnyAsync(a => a.CurrentStageId == stageId, ct);
        if (occupied)
        {
            return Result.Conflict("stage.remove.occupied", "This stage still has applications assigned to it.");
        }

        var subsequentStages = await _dbContext.Stages
            .Where(s => s.RequisitionId == requisitionId && s.SortOrder > stage.SortOrder)
            .ToListAsync(ct);
        foreach (var subsequent in subsequentStages)
        {
            subsequent.ChangeSortOrder(subsequent.SortOrder - 1);
        }

        _dbContext.Stages.Remove(stage);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Structural fallback: the DB-level ON DELETE RESTRICT on Applications.CurrentStageId
            // is what actually enforces AC-6 if a concurrent move lands on this Stage between the
            // pre-check above and this SaveChangesAsync.
            return Result.Conflict("stage.remove.occupied", "This stage still has applications assigned to it.");
        }

        return Result.Ok();
    }

    public async Task<Result<ApplicationTransitionDto>> MoveApplicationAsync(
        Guid applicationId, MoveApplicationRequestDto dto, Guid actingUserId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var application = await _dbContext.Applications.FirstOrDefaultAsync(a => a.Id == applicationId, ct);
        if (application == null)
        {
            return Result<ApplicationTransitionDto>.NotFound("application.move.not-found", "Application not found.");
        }

        var noteError = ValidateNote(dto.Note);
        if (noteError != null)
        {
            return Result<ApplicationTransitionDto>.Validation(
                noteError, "application.move.validation-failed", "Validation failed.");
        }

        var requisition = await _dbContext.Requisitions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == application.RequisitionId, ct);
        if (requisition != null && requisition.Status == RequisitionStatus.Closed)
        {
            return Result<ApplicationTransitionDto>.Conflict(
                "application.move.requisition-closed", "This requisition is closed.");
        }

        if (application.IsRejected)
        {
            return Result<ApplicationTransitionDto>.Conflict(
                "application.move.already-rejected", "This application has already been rejected.");
        }

        var targetStage = await _dbContext.Stages
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == dto.TargetStageId, ct);
        if (targetStage == null || targetStage.RequisitionId != application.RequisitionId)
        {
            // One unified 404 — no existence leak (FR-9, AC-13, E-12).
            return Result<ApplicationTransitionDto>.NotFound("application.move.not-found", "Application or stage not found.");
        }

        if (application.CurrentStageId != dto.ExpectedCurrentStageId)
        {
            var actual = await ReloadActualStageAsync(applicationId, ct);
            return Result<ApplicationTransitionDto>.Conflict(
                "application.move.conflict",
                "This application has already moved. Refresh and try again.",
                actual);
        }

        var actorLabel = await BuildActorLabelAsync(actingUserId, ct);
        var fromStageName = await _dbContext.Stages
            .AsNoTracking()
            .Where(s => s.Id == application.CurrentStageId)
            .Select(s => s.Name)
            .SingleAsync(ct);

        application.MoveToStage(dto.TargetStageId);
        var transition = StageTransition.CreateMove(
            application.Id, dto.ExpectedCurrentStageId, fromStageName,
            dto.TargetStageId, targetStage.Name, actingUserId, actorLabel, dto.Note);
        _dbContext.StageTransitions.Add(transition);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            var actual = await ReloadActualStageAsync(applicationId, ct);
            return Result<ApplicationTransitionDto>.Conflict(
                "application.move.conflict",
                "This application has already moved. Refresh and try again.",
                actual);
        }

        return Result<ApplicationTransitionDto>.Ok(ToTransitionDto(application, targetStage.Name, transition));
    }

    public async Task<Result<ApplicationTransitionDto>> RejectApplicationAsync(
        Guid applicationId, RejectApplicationRequestDto dto, Guid actingUserId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var application = await _dbContext.Applications.FirstOrDefaultAsync(a => a.Id == applicationId, ct);
        if (application == null)
        {
            return Result<ApplicationTransitionDto>.NotFound("application.reject.not-found", "Application not found.");
        }

        var noteError = ValidateNote(dto.Note);
        if (noteError != null)
        {
            return Result<ApplicationTransitionDto>.Validation(
                noteError, "application.reject.validation-failed", "Validation failed.");
        }

        var requisition = await _dbContext.Requisitions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == application.RequisitionId, ct);
        if (requisition != null && requisition.Status == RequisitionStatus.Closed)
        {
            return Result<ApplicationTransitionDto>.Conflict(
                "application.reject.requisition-closed", "This requisition is closed.");
        }

        if (application.IsRejected)
        {
            return Result<ApplicationTransitionDto>.Conflict(
                "application.reject.already-rejected", "This application has already been rejected.");
        }

        var actorLabel = await BuildActorLabelAsync(actingUserId, ct);
        var fromStageName = await _dbContext.Stages
            .AsNoTracking()
            .Where(s => s.Id == application.CurrentStageId)
            .Select(s => s.Name)
            .SingleAsync(ct);

        application.Reject();
        var transition = StageTransition.CreateRejection(
            application.Id, application.CurrentStageId, fromStageName, actingUserId, actorLabel, dto.Note);
        _dbContext.StageTransitions.Add(transition);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent double-reject is caught here even though no expected-state token was
            // supplied on the request — IsRejected is itself a concurrency token (HLD D-3).
            return Result<ApplicationTransitionDto>.Conflict(
                "application.reject.already-rejected", "This application has already been rejected.");
        }

        return Result<ApplicationTransitionDto>.Ok(ToTransitionDto(application, fromStageName, transition));
    }

    public async Task<Result<PipelineBoardDto>> GetPipelineBoardAsync(Guid requisitionId, CancellationToken ct = default)
    {
        // NFR-1: exactly two round trips for the whole board, regardless of Stage/Application
        // count — this Requisition+Stages query doubles as the existence check (a missing
        // Requisition materializes as null, a Requisition with zero Stages per R-1 still
        // materializes with an empty Stages collection), so no separate AnyAsync is needed.
        var requisition = await _dbContext.Requisitions
            .AsNoTracking()
            .Include(r => r.Stages.OrderBy(s => s.SortOrder))
            .FirstOrDefaultAsync(r => r.Id == requisitionId, ct);
        if (requisition == null)
        {
            return Result<PipelineBoardDto>.NotFound("requisition.pipeline.not-found", "Requisition not found.");
        }

        var stages = requisition.Stages.ToList();

        var apps = await _dbContext.Applications
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
                    a.CurrentStageId,
                    a.IsRejected,
                    u.FirstName,
                    u.LastName,
                    Email = u.Email ?? "",
                    a.SubmittedAtUtc
                })
            .ToListAsync(ct);

        var stageGroups = stages
            .Select(stage =>
            {
                var group = apps.Where(a => !a.IsRejected && a.CurrentStageId == stage.Id).ToList();
                return new PipelineStageGroupDto(
                    stage.Id,
                    stage.Name,
                    stage.SortOrder,
                    group.Count,
                    group.Select(a => new PipelineBoardApplicationDto(
                        a.ApplicationId, a.CandidateId, a.FirstName, a.LastName, a.Email, a.SubmittedAtUtc)).ToList());
            })
            .ToList();

        var rejected = apps.Where(a => a.IsRejected).ToList();
        var rejectedGroup = new PipelineRejectedGroupDto(
            rejected.Count,
            rejected.Select(a => new PipelineBoardApplicationDto(
                a.ApplicationId, a.CandidateId, a.FirstName, a.LastName, a.Email, a.SubmittedAtUtc)).ToList());

        return Result<PipelineBoardDto>.Ok(new PipelineBoardDto(requisitionId, stageGroups, rejectedGroup));
    }

    public async Task<Result<IReadOnlyList<StageTransitionDto>>> GetTransitionHistoryAsync(
        Guid applicationId, CancellationToken ct = default)
    {
        var applicationExists = await _dbContext.Applications.AsNoTracking().AnyAsync(a => a.Id == applicationId, ct);
        if (!applicationExists)
        {
            return Result<IReadOnlyList<StageTransitionDto>>.NotFound(
                "application.transitions.not-found", "Application not found.");
        }

        var transitions = await _dbContext.StageTransitions
            .AsNoTracking()
            .Where(t => t.ApplicationId == applicationId)
            .OrderBy(t => t.OccurredAtUtc)
            .ToListAsync(ct);

        IReadOnlyList<StageTransitionDto> dtos = transitions.Select(ToTransitionSummaryDto).ToList();
        return Result<IReadOnlyList<StageTransitionDto>>.Ok(dtos);
    }

    private async Task<string> BuildActorLabelAsync(Guid userId, CancellationToken ct)
    {
        var user = await _dbContext.Users.AsNoTracking().SingleAsync(u => u.Id == userId, ct);
        return $"{user.FirstName} {user.LastName}".Trim();
    }

    private async Task<Dictionary<string, object>> ReloadActualStageAsync(Guid applicationId, CancellationToken ct)
    {
        var actual = await _dbContext.Applications
            .AsNoTracking()
            .Where(a => a.Id == applicationId)
            .Join(
                _dbContext.Stages.AsNoTracking(),
                a => a.CurrentStageId,
                s => s.Id,
                (a, s) => new { s.Id, s.Name })
            .SingleAsync(ct);

        return new Dictionary<string, object>
        {
            ["actualCurrentStageId"] = actual.Id,
            ["actualCurrentStageName"] = actual.Name
        };
    }

    public async Task<Result> SystemMoveToNextStageAsync(
        Guid applicationId, string? note = null, CancellationToken ct = default)
    {
        var application = await _dbContext.Applications.FirstOrDefaultAsync(a => a.Id == applicationId, ct);
        if (application == null)
        {
            return Result.NotFound("application.move.not-found", "Application not found.");
        }

        if (application.IsRejected)
        {
            return Result.Conflict(
                "application.move.already-rejected", "This application has already been rejected.");
        }

        var requisition = await _dbContext.Requisitions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == application.RequisitionId, ct);
        if (requisition != null && requisition.Status == RequisitionStatus.Closed)
        {
            return Result.Conflict(
                "application.move.requisition-closed", "This requisition is closed.");
        }

        var stages = await _dbContext.Stages
            .AsNoTracking()
            .Where(s => s.RequisitionId == application.RequisitionId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

        var currentIndex = stages.FindIndex(s => s.Id == application.CurrentStageId);
        if (currentIndex < 0 || currentIndex >= stages.Count - 1)
        {
            // No subsequent stage exists (e.g. single-stage requisition or already in final stage)
            return Result.Ok();
        }

        var currentStage = stages[currentIndex];
        var nextStage = stages[currentIndex + 1];

        application.MoveToStage(nextStage.Id);
        var transition = StageTransition.CreateSystemMove(
            application.Id,
            currentStage.Id,
            currentStage.Name,
            nextStage.Id,
            nextStage.Name,
            "AI Screening Agent",
            note ?? "Automated screening: recommendation Advance");

        _dbContext.StageTransitions.Add(transition);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("application.move.conflict", "This application has already moved.");
        }

        return Result.Ok();
    }

    private static Dictionary<string, string[]>? ValidateName(string name)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = new[] { "Stage name is required." };
        }
        else if (name.Length > MaxNameLength)
        {
            errors["name"] = new[] { "Stage name must be 200 characters or fewer." };
        }

        return errors.Count > 0 ? errors : null;
    }

    private static Dictionary<string, string[]>? ValidateNote(string? note)
    {
        if (note != null && note.Length > MaxNoteLength)
        {
            return new Dictionary<string, string[]>
            {
                ["note"] = new[] { "Note must be 2000 characters or fewer." }
            };
        }

        return null;
    }

    private static StageDto ToDto(StageEntity stage) => new(stage.Id, stage.RequisitionId, stage.Name, stage.SortOrder);

    private static StageTransitionDto ToTransitionSummaryDto(StageTransition transition) => new(
        transition.Id,
        transition.ApplicationId,
        transition.FromStageId,
        transition.FromStageName,
        transition.ToStageId,
        transition.ToStageName,
        transition.Kind == StageTransitionKind.Move ? "move" : "reject",
        transition.ActorDisplayLabel,
        transition.Note,
        transition.OccurredAtUtc);

    private static ApplicationTransitionDto ToTransitionDto(
        ApplicationEntity application, string currentStageName, StageTransition transition) => new(
        application.Id,
        application.RequisitionId,
        application.CurrentStageId,
        currentStageName,
        application.IsRejected,
        ToTransitionSummaryDto(transition));
}
