# Low-Level Design — 0005 Pipeline Progression

**Spec:** `../spec.md` · **HLD:** `hld.md` · **Updated:** 2026-08-06

> This file is **living**: when implementation diverges from this design, `/implement` patches
> the affected section here and records the deviation in `../implementation/changelog.md`.
> Silent drift is a defect.

---

## 1. File Manifest

Backend (`backend/`), following the existing `Ats.Shared` → `Ats.Db` → `Ats.Service` →
`Ats.Api` layering (`tests/Ats.ArchitectureTests/LayeringRuleTests.cs`).

| Action | Path | Purpose |
|---|---|---|
| Modify | `src/Db/Requisitions/Stage.cs` | Add `SortOrder`, `NormalizedName`; `Create` gains a `sortOrder` param; add `Rename`/`ChangeSortOrder` mutators; add `DefaultStageNames` constant |
| Modify | `src/Db/Configurations/StageConfiguration.cs` | Map `SortOrder`/`NormalizedName`; add unique `(RequisitionId, NormalizedName)` index |
| Modify | `src/Db/Applications/Application.cs` | `Create` gains a required `currentStageId` param; add `CurrentStageId`/`IsRejected` properties; add `MoveToStage`/`Reject` mutators |
| Modify | `src/Db/Configurations/ApplicationConfiguration.cs` | Map `CurrentStageId` (FK, `RESTRICT`, concurrency token) and `IsRejected` (concurrency token); add `(RequisitionId, CurrentStageId)` index |
| Create | `src/Db/Pipeline/StageTransitionKind.cs` | `Move` \| `Reject` enum |
| Create | `src/Db/Pipeline/StageTransitionActorKind.cs` | `User` \| `System` enum (FR-13) |
| Create | `src/Db/Pipeline/StageTransition.cs` | Append-only entity |
| Create | `src/Db/Configurations/StageTransitionConfiguration.cs` | EF Core mapping |
| Modify | `src/Db/AppDbContext.cs` | Add `DbSet<StageTransition> StageTransitions`; apply its configuration |
| Create | `src/Db/Migrations/<timestamp>_AddPipelineProgression.cs` (+ `.Designer.cs`) | Hand-adjusted migration — see §10 |
| Modify | `src/Db/Migrations/AppDbContextModelSnapshot.cs` | Auto-updated, then verified against §10's final shape |
| Create | `src/Service/Pipeline/Dtos/StageDto.cs`, `AddStageRequestDto.cs`, `RenameStageRequestDto.cs`, `ReorderStagesRequestDto.cs` | Stage config DTOs |
| Create | `src/Service/Pipeline/Dtos/MoveApplicationRequestDto.cs`, `RejectApplicationRequestDto.cs`, `ApplicationTransitionDto.cs`, `StageTransitionDto.cs` | Transition DTOs |
| Create | `src/Service/Pipeline/Dtos/PipelineBoardDto.cs`, `PipelineStageGroupDto.cs`, `PipelineRejectedGroupDto.cs`, `PipelineBoardApplicationDto.cs` | Board DTOs |
| Create | `src/Service/Pipeline/IPipelineService.cs` | Service contract |
| Create | `src/Service/Pipeline/PipelineService.cs` | Implementation |
| Modify | `src/Service/ServiceCollectionExtensions.cs` | Register `IPipelineService` |
| Modify | `src/Service/Requisition/RequisitionService.cs` | `CreateAsync` seeds the default 4-Stage set (FR-5) |
| Modify | `src/Service/Application/ApplicationService.cs` | `SubmitAsync` resolves and assigns the first Stage (FR-7); `ListMineAsync` projects `currentStageName`/`isRejected` |
| Modify | `src/Service/Application/Dtos/CandidateApplicationListItemDto.cs` | Add `CurrentStageName`, `IsRejected` |
| Modify | `src/Service/Common/Result.cs` | Add optional `Extensions` dictionary + `Conflict(code, message, extensions)` overloads on `Result`/`Result<T>` |
| Create | `src/Api/PipelineEndpoints.cs` | Endpoints 1–9 in `api.md` |
| Modify | `src/Api/ApplicationEndpoints.cs` | No route changes; `ToProblemResult()` continues to be reused (it lives in `AuthEndpoints.cs`, updated instead — see below) |
| Modify | `src/Api/AuthEndpoints.cs` | `ToProblemResult()` merges `Result.Extensions` into `ProblemDetails.Extensions` |
| Modify | `src/Api/Program.cs` | Map `app.MapPipelineEndpoints();` |
| Create | `tests/Ats.UnitTests/Pipeline/StageEntityTests.cs` | AC-1, AC-3, AC-31 |
| Create | `tests/Ats.UnitTests/Pipeline/ApplicationTransitionEntityTests.cs` | AC-11, AC-14 |
| Create | `tests/Ats.UnitTests/Pipeline/PipelineServiceTests.cs` | Stage config + transition + board + history logic |
| Modify | `tests/Ats.UnitTests/Requisition/RequisitionServiceTests.cs` | AC-7, AC-8, AC-33 |
| Modify | `tests/Ats.UnitTests/Application/ApplicationServiceTests.cs` | AC-10, R-1 guard |
| Create | `tests/Ats.IntegrationTests/Pipeline/StageEndpointsTests.cs` | Stage config HTTP tests |
| Create | `tests/Ats.IntegrationTests/Pipeline/TransitionEndpointsTests.cs` | Move/reject/board/history HTTP tests |
| Modify | `tests/Ats.IntegrationTests/Application/ApplicationEndpointsTests.cs` | AC-22, AC-23 (candidate status fields) |
| Create | `tests/Ats.IntegrationTests/Pipeline/PipelineMigrationBackfillTests.cs` | AC-32, R-2 (migration + backfill correctness) |

Frontend (`frontend/`):

| Action | Path | Purpose |
|---|---|---|
| Create | `src/lib/types/pipeline.ts` | Shared TS types mirroring `api.md` §4 |
| Modify | `src/lib/types/application.ts` | Add `currentStageName`, `isRejected` to `CandidateApplicationListItemDto` |
| Create | `src/components/staff/StageConfigPanel.tsx` | Add/rename/reorder/remove Stages (Recruiter-only writes) |
| Create | `src/components/staff/PipelineBoard.tsx` | Grouped-by-Stage board + rejected column |
| Create | `src/components/staff/MoveApplicationControl.tsx` | Target-Stage select + note + submit, per Application card |
| Create | `src/components/staff/RejectApplicationControl.tsx` | Note + confirm, per Application card |
| Create | `src/components/staff/TransitionHistoryList.tsx` | Chronological list for one Application |
| Create | `src/app/staff/requisitions/[id]/stages/page.tsx` | Stage-configuration screen |
| Create | `src/app/staff/requisitions/[id]/stages/loading.tsx` | Loading state |
| Create | `src/app/staff/requisitions/[id]/stages/error.tsx` | Error state |
| Create | `src/app/staff/requisitions/[id]/pipeline/page.tsx` | Pipeline board |
| Create | `src/app/staff/requisitions/[id]/pipeline/loading.tsx` | Loading state |
| Create | `src/app/staff/requisitions/[id]/pipeline/error.tsx` | Error state |
| Create | `src/app/staff/applications/[id]/page.tsx` | Application detail + transition history |
| Create | `src/app/staff/applications/[id]/loading.tsx` | Loading state |
| Create | `src/app/staff/applications/[id]/error.tsx` | Error state |
| Modify | `src/app/staff/requisitions/[id]/page.tsx` | Add "Configure Stages" / "View Pipeline" links, alongside the existing "View Applications" link |
| Modify | `src/components/portal/ApplicationList.tsx` | Render `currentStageName` or a "Rejected" badge (AC-22, AC-23) instead of nothing |
| Create | `tests/staff/stage-config-panel.test.tsx` | AC-1, AC-3, AC-4, AC-5, AC-6, AC-31 |
| Create | `tests/staff/pipeline-board.test.tsx` | AC-18, AC-19 |
| Create | `tests/staff/move-application-control.test.tsx` | AC-11, AC-29 |
| Create | `tests/staff/transition-history-list.test.tsx` | AC-20, AC-21, AC-30 |
| Modify | `tests/portal/application-list.test.tsx` | AC-22, AC-23 |

No `middleware.ts` change — every new staff page lives under `/staff/*`, already gated (`0003`
T-…, unchanged matcher).

## 2. Domain / Data Layer

### 2.1 `Stage` — `src/Db/Requisitions/Stage.cs` *(modified)*

```csharp
namespace Ats.Db.Requisitions;

public class Stage
{
    public static readonly string[] DefaultStageNames = { "Applied", "Screening", "Interview", "Offer" };

    public Guid Id { get; private set; }
    public Guid RequisitionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    private Stage() { } // EF Core

    public static Stage Create(Guid requisitionId, string name, int sortOrder)
    {
        if (requisitionId == Guid.Empty)
        {
            throw new ArgumentException("RequisitionId cannot be empty.", nameof(requisitionId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        return new Stage
        {
            Id = Guid.NewGuid(),
            RequisitionId = requisitionId,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            SortOrder = sortOrder
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        Name = name;
        NormalizedName = name.ToUpperInvariant();
    }

    public void ChangeSortOrder(int sortOrder) => SortOrder = sortOrder;
}
```

**Invariants.** `RequisitionId` immutable after construction (unchanged from `0003`).
`NormalizedName` is always `Name.ToUpperInvariant()` — the two can never drift because `Rename`
is the only way to change either. `SortOrder` legality (contiguity, uniqueness within a
Requisition) is `PipelineService`'s responsibility, not enforced by the entity itself — mirrors
`Requisition.Transition`'s "mutator applies unconditionally, service checks legality first"
precedent (`0003`).

**Persistence notes.** `DefaultStageNames` is the single C# source of truth for FR-5's seed
list; the migration's raw SQL (erd.md §5, step 9) cannot reference it and must be kept in sync
by hand — verified by `PipelineMigrationBackfillTests` (R-2).

### 2.2 `StageConfiguration` — `src/Db/Configurations/StageConfiguration.cs` *(modified)*

```csharp
namespace Ats.Db.Configurations;

public class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.ToTable("Stages");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.NormalizedName).IsRequired().HasMaxLength(200);
        builder.Property(s => s.SortOrder).IsRequired().HasDefaultValue(0);

        builder.HasIndex(s => s.RequisitionId);
        builder.HasIndex(s => new { s.RequisitionId, s.NormalizedName }).IsUnique();
    }
}
```

### 2.3 `Application` — `src/Db/Applications/Application.cs` *(modified)*

```csharp
namespace Ats.Db.Applications;

public class Application
{
    public Guid Id { get; private set; }
    public Guid RequisitionId { get; private set; }
    public Guid CandidateId { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }
    public Guid CurrentStageId { get; private set; }
    public bool IsRejected { get; private set; }
    public CvAttachment? CvAttachment { get; private set; }

    private Application() { } // EF Core

    // currentStageId is now a required constructor argument, not assigned after the fact —
    // G-1 requires the current-Stage reference to exist "from the moment it is submitted",
    // structurally, not as a follow-up call an operator could omit (unlike AttachCv, which
    // stays a post-hoc call since a CvAttachment is a distinct dependent row, not a scalar FK).
    public static Application Create(Guid requisitionId, Guid candidateId, Guid currentStageId)
    {
        if (requisitionId == Guid.Empty)
        {
            throw new ArgumentException("RequisitionId cannot be empty.", nameof(requisitionId));
        }

        if (candidateId == Guid.Empty)
        {
            throw new ArgumentException("CandidateId cannot be empty.", nameof(candidateId));
        }

        if (currentStageId == Guid.Empty)
        {
            throw new ArgumentException("CurrentStageId cannot be empty.", nameof(currentStageId));
        }

        return new Application
        {
            Id = Guid.NewGuid(),
            RequisitionId = requisitionId,
            CandidateId = candidateId,
            SubmittedAtUtc = DateTime.UtcNow,
            CurrentStageId = currentStageId,
            IsRejected = false
        };
    }

    public void AttachCv(CvAttachment cv) { /* unchanged from 0004 */ }

    // Unconditional, like Requisition.Publish()/Close() — PipelineService checks legality
    // (not rejected, Requisition not closed, target Stage in the same Requisition, FR-22's
    // expected-stage match) before calling this.
    public void MoveToStage(Guid stageId) => CurrentStageId = stageId;

    // Unconditional, same reasoning. CurrentStageId is deliberately left unchanged (FR-10:
    // "retains the Stage it was rejected from").
    public void Reject() => IsRejected = true;
}
```

**Invariants.** `CurrentStageId` is never null/empty from the moment an `Application` exists
(G-1) — enforced at construction, then only ever changed by `MoveToStage`. `IsRejected` only
ever transitions `false → true`, never back (FR-11); `PipelineService`, not the entity, is
responsible for refusing a second `Reject()` call (mirrors the Requisition/Stage precedent of
service-owned legality).

**Persistence notes.** `CurrentStageId`/`IsRejected` are both EF Core concurrency tokens
(§2.4) — every `SaveChangesAsync` that touches this entity implicitly asserts neither has
changed since it was loaded, throwing `DbUpdateConcurrencyException` otherwise (HLD D-3).

### 2.4 `ApplicationConfiguration` — `src/Db/Configurations/ApplicationConfiguration.cs` *(modified)*

```csharp
namespace Ats.Db.Configurations;

public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.ToTable("Applications");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.RequisitionId).IsRequired();
        builder.Property(a => a.CandidateId).IsRequired();
        builder.Property(a => a.SubmittedAtUtc).IsRequired();
        builder.Property(a => a.CurrentStageId).IsRequired().IsConcurrencyToken();
        builder.Property(a => a.IsRejected).IsRequired().HasDefaultValue(false).IsConcurrencyToken();

        builder.HasIndex(a => new { a.CandidateId, a.RequisitionId }).IsUnique();
        builder.HasIndex(a => a.RequisitionId);
        builder.HasIndex(a => new { a.RequisitionId, a.CurrentStageId });

        builder.HasOne<Ats.Db.Requisitions.Requisition>()
            .WithMany().HasForeignKey(a => a.RequisitionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Ats.Shared.Auth.ApplicationUser>()
            .WithMany().HasForeignKey(a => a.CandidateId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Ats.Db.Requisitions.Stage>()
            .WithMany().HasForeignKey(a => a.CurrentStageId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CvAttachment)
            .WithOne().HasForeignKey<CvAttachment>(c => c.ApplicationId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

### 2.5 `StageTransition` family — `src/Db/Pipeline/` *(new)*

```csharp
// src/Db/Pipeline/StageTransitionKind.cs
namespace Ats.Db.Pipeline;

public enum StageTransitionKind { Move, Reject }

// src/Db/Pipeline/StageTransitionActorKind.cs
namespace Ats.Db.Pipeline;

// FR-13/C-2: a forward-compatible column shape only. No code path in this spec produces
// ActorKind.System — every transition this spec writes is ActorKind.User.
public enum StageTransitionActorKind { User, System }

// src/Db/Pipeline/StageTransition.cs
namespace Ats.Db.Pipeline;

public class StageTransition
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid? FromStageId { get; private set; }
    public string FromStageName { get; private set; } = string.Empty;
    public Guid? ToStageId { get; private set; }
    public string? ToStageName { get; private set; }
    public StageTransitionKind Kind { get; private set; }
    public StageTransitionActorKind ActorKind { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string ActorDisplayLabel { get; private set; } = string.Empty;
    public string? Note { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    private StageTransition() { } // EF Core

    public static StageTransition CreateMove(
        Guid applicationId, Guid fromStageId, string fromStageName,
        Guid toStageId, string toStageName,
        Guid actorUserId, string actorDisplayLabel, string? note)
    {
        return new StageTransition
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            FromStageId = fromStageId,
            FromStageName = fromStageName,
            ToStageId = toStageId,
            ToStageName = toStageName,
            Kind = StageTransitionKind.Move,
            ActorKind = StageTransitionActorKind.User,
            ActorUserId = actorUserId,
            ActorDisplayLabel = actorDisplayLabel,
            Note = note,
            OccurredAtUtc = DateTime.UtcNow
        };
    }

    public static StageTransition CreateRejection(
        Guid applicationId, Guid fromStageId, string fromStageName,
        Guid actorUserId, string actorDisplayLabel, string? note)
    {
        return new StageTransition
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            FromStageId = fromStageId,
            FromStageName = fromStageName,
            ToStageId = null,
            ToStageName = null,
            Kind = StageTransitionKind.Reject,
            ActorKind = StageTransitionActorKind.User,
            ActorUserId = actorUserId,
            ActorDisplayLabel = actorDisplayLabel,
            Note = note,
            OccurredAtUtc = DateTime.UtcNow
        };
    }
}
```

**Invariants.** No mutator exists beyond the two factories — append-only by construction (FR-14).
`Kind` is the sole authoritative discriminator for "move vs. reject" (erd.md §3.3) — a reader
must never infer it from `ToStageId`'s nullability alone (a move's `ToStageId` can independently
become `null` via `ON DELETE SET NULL` if its target Stage is later deleted).

**Persistence notes.** Both factories exist to make "which fields are required for which kind"
compiler-checked rather than a runtime convention — there is no single `Create` overload that
would let a caller accidentally build a `Reject` with a `ToStageId` set.

### 2.6 `StageTransitionConfiguration` — `src/Db/Configurations/StageTransitionConfiguration.cs` *(new)*

```csharp
namespace Ats.Db.Configurations;

public class StageTransitionConfiguration : IEntityTypeConfiguration<StageTransition>
{
    public void Configure(EntityTypeBuilder<StageTransition> builder)
    {
        builder.ToTable("StageTransitions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.FromStageName).IsRequired().HasMaxLength(200);
        builder.Property(t => t.ToStageName).HasMaxLength(200);
        builder.Property(t => t.Kind).IsRequired().HasConversion<string>();
        builder.Property(t => t.ActorKind).IsRequired().HasConversion<string>();
        builder.Property(t => t.ActorDisplayLabel).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Note).HasMaxLength(2000);
        builder.Property(t => t.OccurredAtUtc).IsRequired();

        builder.HasIndex(t => new { t.ApplicationId, t.OccurredAtUtc });

        builder.HasOne<Ats.Db.Applications.Application>()
            .WithMany().HasForeignKey(t => t.ApplicationId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Ats.Db.Requisitions.Stage>()
            .WithMany().HasForeignKey(t => t.FromStageId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Ats.Db.Requisitions.Stage>()
            .WithMany().HasForeignKey(t => t.ToStageId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Ats.Shared.Auth.ApplicationUser>()
            .WithMany().HasForeignKey(t => t.ActorUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
```

`AppDbContext.OnModelCreating` gains `builder.ApplyConfiguration(new StageTransitionConfiguration())`
plus `DbSet<StageTransition> StageTransitions`.

## 3. Service / Application Layer

### 3.1 `IPipelineService` — `src/Service/Pipeline/IPipelineService.cs`

```csharp
namespace Ats.Service.Pipeline;

public interface IPipelineService
{
    Task<Result<StageDto>> AddStageAsync(Guid requisitionId, AddStageRequestDto dto, CancellationToken ct = default);
    Task<Result<IReadOnlyList<StageDto>>> GetStagesAsync(Guid requisitionId, CancellationToken ct = default);
    Task<Result<StageDto>> RenameStageAsync(Guid requisitionId, Guid stageId, RenameStageRequestDto dto, CancellationToken ct = default);
    Task<Result<IReadOnlyList<StageDto>>> ReorderStagesAsync(Guid requisitionId, ReorderStagesRequestDto dto, CancellationToken ct = default);
    Task<Result> RemoveStageAsync(Guid requisitionId, Guid stageId, CancellationToken ct = default);

    Task<Result<ApplicationTransitionDto>> MoveApplicationAsync(
        Guid applicationId, MoveApplicationRequestDto dto, Guid actingUserId, CancellationToken ct = default);
    Task<Result<ApplicationTransitionDto>> RejectApplicationAsync(
        Guid applicationId, RejectApplicationRequestDto dto, Guid actingUserId, CancellationToken ct = default);

    Task<Result<PipelineBoardDto>> GetPipelineBoardAsync(Guid requisitionId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<StageTransitionDto>>> GetTransitionHistoryAsync(Guid applicationId, CancellationToken ct = default);
}
```

`actingUserId` is a plain `Guid`, resolved from `ClaimsPrincipal` at the API layer exactly as
`ApplicationEndpoints.GetUserId` already does — `service/pipeline` never sees a
`ClaimsPrincipal` (layering rule #3/#4). The service resolves the actor's display label itself
by querying `_dbContext.Users`, the same technique `ApplicationService.ListForRequisitionAsync`
already uses for candidate identity.

### 3.2 `PipelineService` — `src/Service/Pipeline/PipelineService.cs`

**Constructor.** `(AppDbContext dbContext)` — no configuration dependency; every limit in this
spec (200-char name, 2000-char note) is a compile-time constant, not configurable, matching
`0003`'s `Stage`/`Requisition` field-length precedent (also compile-time constants).

**Shared helper — `BuildActorLabelAsync(Guid userId, ct)`**

```
1. u = await _dbContext.Users.AsNoTracking().SingleAsync(x => x.Id == userId, ct);
2. return $"{u.FirstName} {u.LastName}".Trim();
```

**Behaviour — `AddStageAsync(requisitionId, dto)`**

1. Load Requisition by id (`FindAsync`); `NotFound("stage.add.requisition-not-found", ...)` if
   absent.
2. If `Status == Closed` → `Conflict("stage.add.requisition-closed", ...)`.
3. Validate `dto.Name` (non-empty, ≤200 chars) → else `Validation(..., "stage.add.validation-failed", ...)`.
4. Load the Requisition's current Stages ordered by `SortOrder` (tracked — they will be shifted).
5. `position = Math.Clamp(dto.Position ?? stages.Count, 0, stages.Count)`.
6. Duplicate-name pre-check: if any existing Stage's `NormalizedName == dto.Name.ToUpperInvariant()`
   → `Conflict("stage.add.duplicate-name", ...)`.
7. For every Stage with `SortOrder >= position`, `ChangeSortOrder(SortOrder + 1)`.
8. `var stage = Stage.Create(requisitionId, dto.Name, position); _dbContext.Stages.Add(stage);`
9. `try { await _dbContext.SaveChangesAsync(ct); } catch (DbUpdateException) { return Conflict("stage.add.duplicate-name", ...); }`
   — the unique-index fallback for the race the step-6 pre-check cannot fully close (mirrors
   `0004`'s `SubmitAsync` duplicate-Application fallback).
10. Return `Ok(ToDto(stage))`.

**Behaviour — `GetStagesAsync(requisitionId)`**

1. If the Requisition does not exist → `NotFound("stage.list.requisition-not-found", ...)`.
2. `_dbContext.Stages.AsNoTracking().Where(s => s.RequisitionId == requisitionId).OrderBy(s => s.SortOrder).ToListAsync(ct)` → `Ok(list)`.

**Behaviour — `RenameStageAsync(requisitionId, stageId, dto)`**

1. Load Requisition; `NotFound("stage.rename.not-found", ...)` if absent.
2. If `Status == Closed` → `Conflict("stage.rename.requisition-closed", ...)`.
3. Load the target Stage (tracked); `NotFound("stage.rename.not-found", ...)` if absent or its
   `RequisitionId != requisitionId` (one unified 404, no existence leak).
4. Validate `dto.Name` → else `Validation(..., "stage.rename.validation-failed", ...)`.
5. Duplicate-name pre-check against every **other** Stage in the Requisition (`Id != stageId`) →
   `Conflict("stage.rename.duplicate-name", ...)` if collision.
6. `stage.Rename(dto.Name)`.
7. `try { SaveChangesAsync } catch (DbUpdateException) { return Conflict("stage.rename.duplicate-name", ...); }`.
8. Return `Ok(ToDto(stage))`.

**Behaviour — `ReorderStagesAsync(requisitionId, dto)`**

1. Load Requisition; `NotFound("stage.reorder.requisition-not-found", ...)` if absent.
2. If `Status == Closed` → `Conflict("stage.reorder.requisition-closed", ...)`.
3. Load all Stages for the Requisition (tracked).
4. Validate `dto.StageIds`: same length as the loaded set, no duplicates, and
   `dto.StageIds.ToHashSet().SetEquals(stages.Select(s => s.Id))` → else
   `Validation(..., "stage.reorder.invalid-set", ...)` (400, not 409 — this is a malformed
   request, not a state conflict).
5. For each `(stageId, index)` in `dto.StageIds`, find the matching tracked Stage and
   `ChangeSortOrder(index)`.
6. `SaveChangesAsync`.
7. Return `Ok(stages ordered by the new SortOrder)`.

**Behaviour — `RemoveStageAsync(requisitionId, stageId)`**

1. Load Requisition; `NotFound("stage.remove.not-found", ...)` if absent.
2. If `Status == Closed` → `Conflict("stage.remove.requisition-closed", ...)`.
3. Load the target Stage (tracked); `NotFound("stage.remove.not-found", ...)` if absent/foreign.
4. `occupied = await _dbContext.Applications.AnyAsync(a => a.CurrentStageId == stageId, ct)` →
   if `true`, `Conflict("stage.remove.occupied", ...)` (AC-6, E-1).
5. Load every other Stage in the Requisition with `SortOrder > stage.SortOrder` (tracked);
   `ChangeSortOrder(SortOrder - 1)` on each, to keep the sequence contiguous.
6. `_dbContext.Stages.Remove(stage); await _dbContext.SaveChangesAsync(ct);` — the DB-level
   `ON DELETE RESTRICT` on `Applications.CurrentStageId` is the structural fallback if step 4's
   pre-check loses a race to a concurrent move landing on this Stage; a `DbUpdateException` in
   that case is mapped to the same `Conflict("stage.remove.occupied", ...)`.
7. Return `Result.Ok()`.

**Behaviour — `MoveApplicationAsync(applicationId, dto, actingUserId)`**

1. Load the Application (tracked, **not** `AsNoTracking` — concurrency tokens require tracking);
   `NotFound("application.move.not-found", ...)` if absent.
2. Validate `dto.Note` length (≤2000) → else `Validation(..., "application.move.validation-failed", ...)`.
3. Load the owning Requisition's `Status`; if `Closed` → `Conflict("application.move.requisition-closed", ...)`.
4. If `application.IsRejected` → `Conflict("application.move.already-rejected", ...)`.
5. Load the target Stage; `NotFound("application.move.not-found", ...)` if it does not exist or
   its `RequisitionId != application.RequisitionId` (FR-9, AC-13, E-12 — one unified 404).
6. Pre-check: if `application.CurrentStageId != dto.ExpectedCurrentStageId`, reload the actual
   current Stage's name and return
   `Conflict("application.move.conflict", ..., extensions: { actualCurrentStageId, actualCurrentStageName })`.
7. `actorLabel = await BuildActorLabelAsync(actingUserId, ct)`.
8. `var fromStageName = (await _dbContext.Stages.AsNoTracking().Where(s => s.Id == application.CurrentStageId).Select(s => s.Name).SingleAsync(ct));`
9. `application.MoveToStage(dto.TargetStageId);` `var transition = StageTransition.CreateMove(application.Id, dto.ExpectedCurrentStageId, fromStageName, dto.TargetStageId, targetStage.Name, actingUserId, actorLabel, dto.Note); _dbContext.StageTransitions.Add(transition);`
10. `try { await _dbContext.SaveChangesAsync(ct); }`
    `catch (DbUpdateConcurrencyException) { var actual = await ReloadActualStageAsync(applicationId, ct); return Conflict("application.move.conflict", ..., extensions: actual); }`
11. Return `Ok(ToTransitionDto(application, targetStage, transition))`.

**Behaviour — `RejectApplicationAsync(applicationId, dto, actingUserId)`**

1. Load the Application (tracked); `NotFound("application.reject.not-found", ...)` if absent.
2. Validate `dto.Note` length → else `Validation(..., "application.reject.validation-failed", ...)`.
3. Load the owning Requisition's `Status`; if `Closed` → `Conflict("application.reject.requisition-closed", ...)`.
4. If `application.IsRejected` → `Conflict("application.reject.already-rejected", ...)` (FR-11,
   AC-15, E-4).
5. `actorLabel = await BuildActorLabelAsync(actingUserId, ct)`.
6. `fromStageName = ...` (same lookup as move step 8).
7. `application.Reject();` `var transition = StageTransition.CreateRejection(application.Id, application.CurrentStageId, fromStageName, actingUserId, actorLabel, dto.Note); _dbContext.StageTransitions.Add(transition);`
8. `try { SaveChangesAsync } catch (DbUpdateConcurrencyException) { return Conflict("application.reject.already-rejected", ...); }`
   — a concurrent double-reject is caught here even though no `ExpectedCurrentStageId` was
   supplied, because `IsRejected` is itself a concurrency token (HLD D-3).
9. Return `Ok(ToTransitionDto(...))`.

**Behaviour — `GetPipelineBoardAsync(requisitionId)`**

1. If the Requisition does not exist → `NotFound("requisition.pipeline.not-found", ...)`.
2. `stages = await _dbContext.Stages.AsNoTracking().Where(s => s.RequisitionId == requisitionId).OrderBy(s => s.SortOrder).ToListAsync(ct)`.
3. `apps = await _dbContext.Applications.AsNoTracking().Where(a => a.RequisitionId == requisitionId).Join(_dbContext.Users.AsNoTracking(), a => a.CandidateId, u => u.Id, (a, u) => new { a.Id, a.CurrentStageId, a.IsRejected, u.FirstName, u.LastName, Email = u.Email ?? "", a.SubmittedAtUtc }).ToListAsync(ct)`
   — one query, then grouped **in memory** (NFR-1: ≤500 rows, trivial cost).
4. For each Stage (in order): `group = apps.Where(a => !a.IsRejected && a.CurrentStageId == stage.Id)` → `PipelineStageGroupDto(stage.Id, stage.Name, stage.SortOrder, group.Count, group.Select(ToBoardAppDto).ToList())`.
5. `rejected = apps.Where(a => a.IsRejected)` → `PipelineRejectedGroupDto(rejected.Count, rejected.Select(ToBoardAppDto).ToList())`.
6. Return `Ok(new PipelineBoardDto(requisitionId, stageGroups, rejectedGroup))`. Empty
   Requisition (0 Applications) still returns every Stage at zero count (AC-19), since step 4
   iterates the Stage list, not the (possibly empty) Application list.

**Behaviour — `GetTransitionHistoryAsync(applicationId)`**

1. If the Application does not exist → `NotFound("application.transitions.not-found", ...)`.
2. `_dbContext.StageTransitions.AsNoTracking().Where(t => t.ApplicationId == applicationId).OrderBy(t => t.OccurredAtUtc).ToListAsync(ct)` → `Ok(list.Select(ToTransitionSummaryDto).ToList())`. Empty list, not an error (AC-21).

**Returns — outcome→result→HTTP**

| Outcome | Result | Maps to HTTP |
|---|---|---|
| Success (mutation) | `Result.Ok(dto)` | 200 (201 for Add Stage, 204 for Remove Stage) |
| Success (read) | `Result.Ok(dto/list)` | 200 |
| Missing Requisition/Stage/Application | `Result.NotFound(code, ...)` | 404 |
| Invalid `name`/`note`/reorder set | `Result.Validation(errors, code, message)` | 400 |
| Requisition closed | `Result.Conflict(code, ...)` | 409 |
| Duplicate Stage name | `Result.Conflict(code, ...)` | 409 |
| Stage occupied on remove | `Result.Conflict(code, ...)` | 409 |
| Application already rejected | `Result.Conflict(code, ...)` | 409 |
| Stale `expectedCurrentStageId` | `Result.Conflict(code, ..., extensions)` | 409 |

### 3.3 `RequisitionService.CreateAsync` — `src/Service/Requisition/RequisitionService.cs` *(modified)*

**Diff against `0003`'s shipped behaviour.** After step 2 (`RequisitionEntity.Create(...)`) and
before step 3 (`SaveChangesAsync`), insert:

```
2a. for (var i = 0; i < Stage.DefaultStageNames.Length; i++)
    {
        _dbContext.Stages.Add(Stage.Create(requisition.Id, Stage.DefaultStageNames[i], i));
    }
```

Everything else in `CreateAsync` — validation, the single `SaveChangesAsync` call, the returned
`RequisitionDto` shape, the `draft` starting status (AC-33) — is unchanged. `UpdateContentAsync`,
`PublishAsync`, `UnpublishAsync`, `CloseAsync`, `GetByIdAsync`, `ListAsync`,
`GetPublicByIdAsync`, `SearchPublicAsync` are **not touched** by this spec.

### 3.4 `ApplicationService.SubmitAsync` — `src/Service/Application/ApplicationService.cs` *(modified)*

**Diff against `0004`'s shipped behaviour.** Insert a new step between the existing duplicate
pre-check (step 6) and the storage write (step 7):

```
6a. var firstStageId = await _dbContext.Stages.AsNoTracking()
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
```

Step 9 (`ApplicationEntity.Create(requisitionId, candidateId)`) becomes
`ApplicationEntity.Create(requisitionId, candidateId, firstStageId)`. No other step changes —
the CV validation order (steps 2–5), the duplicate pre-check (step 6), the storage write
(steps 7–8), and the `DbUpdateException` duplicate-fallback (step 10) are all unchanged from
`0004`.

**`ListMineAsync` diff.** The `.Join(...)` to `Requisitions` gains a second `.Join(...)` to
`Stages` on `a.CurrentStageId == s.Id`, projecting `s.Name` and `a.IsRejected` alongside the
existing fields; `CandidateApplicationListItemDto` gains `CurrentStageName`/`IsRejected`
parameters, populated from that projection. `ListForRequisitionAsync`, `GetCvAsync` are **not
touched**.

### 3.5 `Result`/`Result<T>` — `src/Service/Common/Result.cs` *(modified)*

```csharp
public class Result
{
    // ... existing members unchanged ...
    public IDictionary<string, object>? Extensions { get; protected set; }

    public static Result Conflict(string code, string message, IDictionary<string, object>? extensions) =>
        new() { Status = ResultStatus.Conflict, ErrorCode = code, ErrorMessage = message, Extensions = extensions };
}

public class Result<T> : Result
{
    // ... existing members unchanged ...
    public static new Result<T> Conflict(string code, string message, IDictionary<string, object>? extensions) =>
        new() { Status = ResultStatus.Conflict, ErrorCode = code, ErrorMessage = message, Extensions = extensions };
}
```

The existing two-argument `Conflict(code, message)` overloads are untouched — every prior
caller (`RequisitionService`, `ApplicationService`) compiles and behaves identically. Only
`PipelineService.MoveApplicationAsync` calls the new three-argument overload.

`AuthEndpoints.ToProblemResult()` gains, immediately before the final `return`:

```csharp
if (result.Extensions != null)
{
    foreach (var (key, value) in result.Extensions)
    {
        problem.Extensions[key] = value;
    }
}
```

## 4. API Layer

Endpoint shapes are specified in `api.md`. `PipelineEndpoints.cs` follows the same
`MapGroup`/`ToProblemResult()` pattern as `RequisitionEndpoints.cs`/`ApplicationEndpoints.cs`.

| Route | Handler | Auth policy | Maps service result via |
|---|---|---|---|
| `POST /api/requisitions/{id}/stages` | `PipelineEndpoints` | `RecruiterOnly` | `AddStageAsync` → `Results.Created($"/api/requisitions/{id}/stages/{dto.Id}", dto)` / `ToProblemResult()` |
| `GET /api/requisitions/{id}/stages` | `PipelineEndpoints` | `StaffOnly` | `GetStagesAsync` → `Results.Ok` / `ToProblemResult()` |
| `PUT /api/requisitions/{id}/stages/{stageId}` | `PipelineEndpoints` | `RecruiterOnly` | `RenameStageAsync` → `Results.Ok` / `ToProblemResult()` |
| `PUT /api/requisitions/{id}/stages/reorder` | `PipelineEndpoints` | `RecruiterOnly` | `ReorderStagesAsync` → `Results.Ok` / `ToProblemResult()` |
| `DELETE /api/requisitions/{id}/stages/{stageId}` | `PipelineEndpoints` | `RecruiterOnly` | `RemoveStageAsync` → `Results.NoContent()` / `ToProblemResult()` |
| `POST /api/applications/{id}/move` | `PipelineEndpoints` | `RecruiterOnly` | `MoveApplicationAsync(id, dto, actingUserId)` → `Results.Ok` / `ToProblemResult()` |
| `POST /api/applications/{id}/reject` | `PipelineEndpoints` | `RecruiterOnly` | `RejectApplicationAsync(id, dto, actingUserId)` → `Results.Ok` / `ToProblemResult()` |
| `GET /api/requisitions/{id}/pipeline` | `PipelineEndpoints` | `StaffOnly` | `GetPipelineBoardAsync` → `Results.Ok` / `ToProblemResult()` |
| `GET /api/applications/{id}/transitions` | `PipelineEndpoints` | `StaffOnly` | `GetTransitionHistoryAsync` → `Results.Ok` / `ToProblemResult()` |

`actingUserId` for the move/reject handlers is resolved via the same
`ClaimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)` pattern already private to
`ApplicationEndpoints.GetUserId` — duplicated as a local `private static Guid? GetUserId(...)`
in `PipelineEndpoints.cs` (both are `internal`/`private` to their own file; no shared helper
exists yet, consistent with how `0004` did not extract one either).

`Program.cs` gains `app.MapPipelineEndpoints();` after the existing `MapApplicationEndpoints()`
call.

## 5. Frontend

### 5.1 Components

| Component | Path | Props | State | Notes |
|---|---|---|---|---|
| `StageConfigPanel` | `src/components/staff/StageConfigPanel.tsx` | `requisitionId`, `stages: StageDto[]`, `canWrite: boolean` | `stages` (local, optimistic reorder), `loading`, `error` | Add form + per-Stage rename/remove + up/down reorder buttons (no drag-and-drop library in `tech-stack.md`); `canWrite=false` hides all write affordances, mirrors `RequisitionLifecycleActions` |
| `PipelineBoard` | `src/components/staff/PipelineBoard.tsx` | `requisitionId`, `board: PipelineBoardDto`, `canWrite: boolean` | none (server-fetched, mutations trigger `router.refresh()`) | Column per Stage + a "Rejected" column; each card renders `MoveApplicationControl`/`RejectApplicationControl` only when `canWrite` |
| `MoveApplicationControl` | `src/components/staff/MoveApplicationControl.tsx` | `applicationId`, `currentStageId`, `stages: StageDto[]` | `targetStageId`, `note`, `loading`, `error` (client) | `<select>` of target Stage + optional note; POSTs to `/api/bff/proxy/applications/{id}/move`; on 409 conflict, shows the `actualCurrentStageName` from the ProblemDetails body and `router.refresh()`s so the board reflects reality |
| `RejectApplicationControl` | `src/components/staff/RejectApplicationControl.tsx` | `applicationId` | `note`, `loading`, `error`, `confirming` (client) | Confirm-before-submit (destructive, terminal action); POSTs to `/reject` |
| `TransitionHistoryList` | `src/components/staff/TransitionHistoryList.tsx` | `items: StageTransitionDto[]` | none (presentational) | Chronological list: kind, from→to (or "Rejected"), actor, note, timestamp |

### 5.2 Data Access

| Call site | Endpoint (via proxy unless noted) | Method | Re-render trigger |
|---|---|---|---|
| `staff/requisitions/[id]/stages/page.tsx` (Server Component) | `invokeBackend` → `/api/requisitions/{id}/stages` | GET | Normal SSR on navigation |
| `StageConfigPanel` add/rename/remove/reorder | `/api/bff/proxy/requisitions/{id}/stages[...]` | POST/PUT/DELETE | `router.refresh()` on success |
| `staff/requisitions/[id]/pipeline/page.tsx` (Server Component) | `invokeBackend` → `/api/requisitions/{id}/pipeline` | GET | Normal SSR on navigation |
| `MoveApplicationControl` / `RejectApplicationControl` | `/api/bff/proxy/applications/{id}/move` \| `/reject` | POST | `router.refresh()` on success (re-fetches the board) |
| `staff/applications/[id]/page.tsx` (Server Component) | `invokeBackend` → `/api/applications/{id}/transitions` | GET | Normal SSR on navigation |
| `(portal)/applications/page.tsx` (Server Component, unchanged path, `0004`) | `invokeBackend` → `/api/applications/mine` | GET | Normal SSR on navigation — response now includes `currentStageName`/`isRejected`, consumed by the modified `ApplicationList` |

### 5.3 UI States

| Surface | Loading | Empty | Error | Success |
|---|---|---|---|---|
| Stage config (`stages/page.tsx`) | `loading.tsx` skeleton | Never empty — FR-5 guarantees ≥0 Stages exist by construction at creation, though a Recruiter can still empty it (R-1); render "No stages configured — add one to accept applications" when `stages.length === 0` | `error.tsx` with retry | `StageConfigPanel` list |
| Pipeline board (`pipeline/page.tsx`) | `loading.tsx` skeleton columns | Every Stage column renders at zero count (AC-19) — no separate empty state needed | `error.tsx` with retry; `notFound()` for a missing Requisition | Columns of Application cards |
| `MoveApplicationControl` submit | Submit button spinner | N/A | Inline `role="alert"` banner; a `409 application.move.conflict` shows "This application already moved to {actualCurrentStageName}." from the ProblemDetails extension, then refreshes | Card moves column via `router.refresh()` |
| Application detail (`staff/applications/[id]`) | `loading.tsx` skeleton | "No transitions yet" inside `TransitionHistoryList` (AC-21) | `error.tsx`; `notFound()` for a missing id | Chronological list |
| Candidate "My Applications" (`0004`, modified) | Unchanged (`loading.tsx`) | Unchanged ("You haven't applied...") | Unchanged (`error.tsx`) | Each row now shows the Stage name, or a "Rejected" badge when `isRejected` (AC-22, AC-23) |

## 6. DTOs & Contracts

```ts
// src/lib/types/pipeline.ts — mirrors api.md §4 exactly
export type StageDto = { id: string; requisitionId: string; name: string; sortOrder: number };

export type MoveApplicationRequestDto = {
  targetStageId: string;
  expectedCurrentStageId: string;
  note?: string;
};

export type StageTransitionDto = {
  id: string;
  applicationId: string;
  fromStageId: string | null;
  fromStageName: string;
  toStageId: string | null;
  toStageName: string | null;
  kind: "move" | "reject";
  actorDisplayLabel: string;
  note: string | null;
  occurredAtUtc: string;
};

export type ApplicationTransitionDto = {
  applicationId: string;
  requisitionId: string;
  currentStageId: string;
  currentStageName: string;
  isRejected: boolean;
  transition: StageTransitionDto;
};

export type PipelineBoardApplicationDto = {
  applicationId: string;
  candidateId: string;
  candidateFirstName: string;
  candidateLastName: string;
  candidateEmail: string;
  submittedAtUtc: string;
};

export type PipelineStageGroupDto = {
  stageId: string;
  stageName: string;
  sortOrder: number;
  count: number;
  applications: PipelineBoardApplicationDto[];
};

export type PipelineBoardDto = {
  requisitionId: string;
  stages: PipelineStageGroupDto[];
  rejected: { count: number; applications: PipelineBoardApplicationDto[] };
};
```

`src/lib/types/application.ts`'s `CandidateApplicationListItemDto` gains
`currentStageName: string; isRejected: boolean;`.

## 7. Validation Rules

| Field | Rule | Message | AC |
|---|---|---|---|
| `name` (add/rename Stage) | Required, non-whitespace, ≤200 chars | "Stage name is required." / "Stage name must be 200 characters or fewer." | AC-1, AC-3 |
| `name` (add/rename Stage) | Unique within the Requisition, case-insensitive | "A stage with this name already exists." | AC-31 |
| `position` (add Stage) | Optional int, clamped to `[0, count]` | N/A (never an error) | AC-1 |
| `stageIds` (reorder) | Exact set match against the Requisition's current Stages | "The stage list does not match this requisition's current stages." | AC-4 |
| `note` (move/reject) | Optional, ≤2000 chars | "Note must be 2000 characters or fewer." | FR-23 |
| `expectedCurrentStageId` (move) | Required; must equal the Application's actual current Stage | (409, not a 400 — see `api.md` §3.6) | AC-29 |
| `targetStageId` (move) | Required; must be a Stage of the same Requisition as the Application | (404 — no existence leak) | AC-13 |

## 8. Error Handling

| Condition | Code | Logged at | User-facing message |
|---|---|---|---|
| Empty/oversized Stage name | `stage.add.validation-failed` / `stage.rename.validation-failed` | Warning | "Please check the stage name." |
| Duplicate Stage name | `stage.add.duplicate-name` / `stage.rename.duplicate-name` | Information | "A stage with this name already exists." |
| Stage config on a closed Requisition | `stage.*.requisition-closed` | Information | "This requisition is closed and its pipeline can no longer be edited." |
| Remove an occupied Stage | `stage.remove.occupied` | Information | "This stage still has applications assigned to it." |
| Invalid reorder set | `stage.reorder.invalid-set` | Warning | "The stage list does not match this requisition's current stages." |
| Move/reject on a closed Requisition | `application.move.requisition-closed` / `application.reject.requisition-closed` | Information | "This requisition is closed." |
| Move/reject an already-rejected Application | `application.move.already-rejected` / `application.reject.already-rejected` | Information | "This application has already been rejected." |
| Stale `expectedCurrentStageId` | `application.move.conflict` | Information | "This application already moved. Refreshing." |
| Move to a foreign/missing Stage, or missing Application | `application.move.not-found` | Information | "Application or stage not found." |
| No Stage configured at submission time | `application.submit.no-stages-configured` | Warning | "This job posting's pipeline has not been configured yet." |

## 9. Configuration

None new. No configuration key is introduced by this spec — every limit (200/2000 chars, 4
default Stages) is a compile-time constant, consistent with `0003`'s `Requisition`/`Stage` field
limits.

## 10. Database Migration

Full ordered detail — including the exact `migrationBuilder` calls and raw SQL — lives in
`erd.md` §5; this section states only the sequencing `/implement` must follow when hand-adjusting
the `dotnet ef migrations add`-generated file.

> **As shipped (CP-1), diverges from the table originally below — see Deviation Log.** The
> actual `Up()` order is: (1) `Stages` gains `SortOrder`/`NormalizedName` + its unique index;
> (2) `CreateTable("StageTransitions")` + its indexes; (3) `Applications` gains `CurrentStageId`
> **nullable, with no FK yet** + `IsRejected` + the `(RequisitionId, CurrentStageId)` and
> `CurrentStageId` indexes; (4) raw SQL — seed default Stages, then backfill
> `Applications.CurrentStageId`/`IsRejected`; (5) `AlterColumn CurrentStageId` to **NOT NULL**,
> immediately followed by `AddForeignKey` — both SQLite table-rebuild operations, placed
> back-to-back with nothing else in between or after.

| Step | Change | Reversible |
|---|---|---|
| 1 | `Stages`: add `SortOrder` (int, NOT NULL, default 0), `NormalizedName` (text, NOT NULL, default `''`) | Yes |
| 2 | `Stages`: create unique index `(RequisitionId, NormalizedName)` | Yes |
| 3 | `CreateTable("StageTransitions")` + its index | Yes |
| 4 | `Applications`: add `CurrentStageId` (Guid, **nullable, no FK yet**) | Yes |
| 5 | `Applications`: add `IsRejected` (bool, NOT NULL, default `false`) | Yes |
| 6 | `Applications`: create index `(RequisitionId, CurrentStageId)` | Yes |
| 7 | Raw SQL: seed default 4 Stages for every Requisition with zero Stages | No (data op) |
| 8 | Raw SQL: backfill `Applications.CurrentStageId`/`IsRejected` for every row where `CurrentStageId IS NULL` | No (data op) |
| 9 | `Applications`: `AlterColumn` `CurrentStageId` to **NOT NULL**, then `AddForeignKey` → `Stages.Id` `RESTRICT` (back-to-back, no raw SQL between them) | Yes (back to nullable / drop FK) |

**Ordering is load-bearing**: step 7 must run before step 8 (the backfill's subquery needs Stage
rows to exist), and step 9 must run after step 8 (tightening to `NOT NULL` before every row has
a value would fail outright). It is *also* load-bearing that no `migrationBuilder.Sql(...)` call
is interleaved between an operation that triggers a SQLite table rebuild (`AddForeignKey`,
`AlterColumn`) and the point that rebuild is flushed — see Deviation Log. `dotnet ef migrations
add AddPipelineProgression` will not, by itself, produce this exact sequence — `/implement`
generates the migration once `StageConfiguration`/`ApplicationConfiguration`/
`StageTransitionConfiguration` are all at their **final** shape (i.e. `CurrentStageId` modelled
as required in the entity/config), then manually splits whatever single
`AddColumn(nullable:false)+AddForeignKey` operation EF emits for `CurrentStageId` into steps
4 + 7 + 8 + 9 above, inserting the two raw-SQL `migrationBuilder.Sql(...)` calls at the right
point. `Down()` reverses steps 1–6, 9 in reverse order (see erd.md §5 rollback plan); no `Down()`
SQL is needed for steps 7/8 since dropping the columns/table already discards the seeded data.

## 11. Test Plan

| Test | Type | Covers | Path |
|---|---|---|---|
| `Stage_Create_SetsNormalizedNameAndSortOrder` | Unit | AC-1 | `tests/Ats.UnitTests/Pipeline/StageEntityTests.cs` |
| `Stage_Rename_RecomputesNormalizedName` | Unit | AC-3 | same |
| `Application_Create_RequiresCurrentStageId` | Unit | AC-10 | `tests/Ats.UnitTests/Application/ApplicationServiceTests.cs` (modified) |
| `StageTransition_CreateMove_PopulatesBothStages` | Unit | AC-11, AC-16 | `tests/Ats.UnitTests/Pipeline/ApplicationTransitionEntityTests.cs` |
| `StageTransition_CreateRejection_LeavesToStageNull` | Unit | AC-14 | same |
| `AddStageAsync_ValidName_ReturnsCreatedAtPosition` | Unit | AC-1 | `tests/Ats.UnitTests/Pipeline/PipelineServiceTests.cs` |
| `AddStageAsync_HiringManagerOrCandidate_NeverReached` (authz proven at integration level) | — | AC-2 | integration only |
| `AddStageAsync_DuplicateName_ReturnsConflict` | Unit | AC-31 | same |
| `AddStageAsync_ClosedRequisition_ReturnsConflict` | Unit | AC-28 | same |
| `RenameStageAsync_KeepsApplicationsAssigned` | Unit | AC-3 | same |
| `RenameStageAsync_DuplicateName_ReturnsConflict` | Unit | AC-31 | same |
| `ReorderStagesAsync_ValidSet_UpdatesSortOrder` | Unit | AC-4 | same |
| `ReorderStagesAsync_MismatchedSet_ReturnsValidation` | Unit | E-12-adjacent (invalid set) | same |
| `RemoveStageAsync_Unoccupied_Removes` | Unit | AC-5 | same |
| `RemoveStageAsync_Occupied_ReturnsConflict` | Unit | AC-6, E-1 | same |
| `RemoveStageAsync_ClosedRequisition_ReturnsConflict` | Unit | AC-28 | same |
| `MoveApplicationAsync_ValidMove_UpdatesStageAndWritesTransition` | Unit | AC-11, AC-16 | same |
| `MoveApplicationAsync_BackwardMove_Succeeds` | Unit | AC-12 | same |
| `MoveApplicationAsync_ForeignStage_ReturnsNotFound` | Unit | AC-13, E-3 | same |
| `MoveApplicationAsync_MissingStage_ReturnsNotFound` | Unit | E-12 | same |
| `MoveApplicationAsync_AlreadyRejected_ReturnsConflict` | Unit | AC-15 | same |
| `MoveApplicationAsync_StaleExpectedStage_ReturnsConflictWithActual` | Unit | AC-29, E-2 | same |
| `MoveApplicationAsync_ConcurrentSaveChanges_ThrowsConcurrencyMappedToConflict` | Unit | E-2 | same |
| `MoveApplicationAsync_ClosedRequisition_ReturnsConflict` | Unit | AC-28 | same |
| `RejectApplicationAsync_ActiveApplication_SetsRejectedKeepsStage` | Unit | AC-14, AC-16 | same |
| `RejectApplicationAsync_AlreadyRejected_ReturnsConflict` | Unit | AC-15, E-4 | same |
| `RejectApplicationAsync_WithNote_NoteVisibleInHistoryOnly` | Unit | AC-30 | same |
| `RejectApplicationAsync_ClosedRequisition_ReturnsConflict` | Unit | AC-28 | same |
| `GetPipelineBoardAsync_GroupsByStageWithRejectedSeparate` | Unit | AC-18 | same |
| `GetPipelineBoardAsync_ZeroApplications_EveryStageZeroCount` | Unit | AC-19, E-9 | same |
| `GetTransitionHistoryAsync_ReturnsChronological` | Unit | AC-20, AC-17 | same |
| `GetTransitionHistoryAsync_NoTransitions_ReturnsEmptyList` | Unit | AC-21 | same |
| `CreateAsync_SeedsFourDefaultStagesInOrder` | Unit | AC-7 | `tests/Ats.UnitTests/Requisition/RequisitionServiceTests.cs` (modified) |
| `CreateAsync_StatusStillDraft` | Unit | AC-33 | same |
| `SubmitAsync_AssignsRequisitionsFirstStage` | Unit | AC-10 | `tests/Ats.UnitTests/Application/ApplicationServiceTests.cs` (modified) |
| `SubmitAsync_NoStagesConfigured_ReturnsConflict` | Unit | R-1 (HLD) | same |
| `ListMineAsync_IncludesCurrentStageNameAndIsRejected` | Unit | AC-22, AC-23 | same |
| `POST_stages_AsRecruiter_Returns201` | Integration | AC-1 | `tests/Ats.IntegrationTests/Pipeline/StageEndpointsTests.cs` |
| `POST_stages_AsHiringManagerOrCandidate_Returns403` | Integration | AC-2, AC-26 | same |
| `PUT_stages_id_DuplicateName_Returns409` | Integration | AC-31 | same |
| `PUT_stages_reorder_ReturnsNewOrder` | Integration | AC-4 | same |
| `DELETE_stages_id_Unoccupied_Returns204` | Integration | AC-5 | same |
| `DELETE_stages_id_Occupied_Returns409` | Integration | AC-6 | same |
| `ANY_stagesEndpoint_OnClosedRequisition_Returns409` | Integration | AC-28 | same |
| `POST_applications_id_move_AsRecruiter_Returns200` | Integration | AC-11, AC-12, AC-16 | `tests/Ats.IntegrationTests/Pipeline/TransitionEndpointsTests.cs` |
| `POST_applications_id_move_AsHiringManagerOrCandidate_Returns403` | Integration | AC-26 | same |
| `POST_applications_id_move_ForeignStage_Returns404` | Integration | AC-13 | same |
| `POST_applications_id_move_StaleExpected_Returns409WithActualStage` | Integration | AC-29 | same |
| `POST_applications_id_reject_Returns200` | Integration | AC-14 | same |
| `POST_applications_id_reject_Twice_SecondReturns409` | Integration | AC-15, E-4 | same |
| `GET_requisitions_id_pipeline_GroupsAndCounts` | Integration | AC-18 | same |
| `GET_requisitions_id_pipeline_AsCandidate_Returns403` | Integration | AC-25 | same |
| `GET_requisitions_id_pipeline_AsHiringManager_Returns200` | Integration | AC-27 | same |
| `GET_applications_id_transitions_ChronologicalOrder` | Integration | AC-20 | same |
| `GET_applications_id_transitions_AsCandidate_Returns403` | Integration | AC-24, AC-25 | same |
| `GET_applications_mine_IncludesStatus_ExcludesNote` | Integration | AC-22, AC-23, AC-30 | `tests/Ats.IntegrationTests/Application/ApplicationEndpointsTests.cs` (modified) |
| `Migration_Backfill_SeedsDefaultStagesForExistingRequisitions` | Integration | AC-32 | `tests/Ats.IntegrationTests/Pipeline/PipelineMigrationBackfillTests.cs` |
| `Migration_Backfill_AssignsExistingApplicationsFirstStageNoTransitionRow` | Integration | AC-32 | same |
| `Migration_Backfill_SeededNamesMatchDefaultStageNamesConstant` | Integration | R-2 (HLD) | same |
| `StageConfigPanel` shows validation error for empty name | Component | AC-1 | `tests/staff/stage-config-panel.test.tsx` |
| `StageConfigPanel` shows 409 banner on duplicate name | Component | AC-31 | same |
| `StageConfigPanel` disables remove for an occupied stage's error banner on 409 | Component | AC-6 | same |
| `PipelineBoard` renders every stage column at zero count | Component | AC-19 | `tests/staff/pipeline-board.test.tsx` |
| `PipelineBoard` renders a separate Rejected column | Component | AC-18 | same |
| `MoveApplicationControl` shows actual-stage banner on 409 | Component | AC-29 | `tests/staff/move-application-control.test.tsx` |
| `TransitionHistoryList` renders empty state | Component | AC-21 | `tests/staff/transition-history-list.test.tsx` |
| `TransitionHistoryList` renders note for a rejection | Component | AC-30 | same |
| `ApplicationList` shows stage name for an active application | Component | AC-22 | `tests/portal/application-list.test.tsx` (modified) |
| `ApplicationList` shows Rejected badge, not a stage name | Component | AC-23 | same |

Every `AC-n` (AC-1 through AC-33) appears at least once above.

## 12. Implementation Notes

- Build order: `Stage`/`Application`/`StageTransition` entity + configuration changes must all
  compile before `dotnet ef migrations add AddPipelineProgression` is run (it reads the compiled
  model) — same constraint `0003`/`0004` already noted.
- `SqlitePragmaConnectionInterceptor` (backend `src/Db/SqlitePragmaConnectionInterceptor.cs`)
  does **not** set `PRAGMA foreign_keys=ON` today — confirmed by reading the file. This is why
  HLD D-4 rejected the sentinel-default single-rebuild approach on principle (correctness
  should not depend on a pragma the project doesn't currently set) rather than because it would
  visibly fail today.
- The migration's raw SQL (erd.md §5, steps 7–8) must be written with `migrationBuilder.Sql(...)`
  calls placed **between** the schema-operation calls in `Up()`, not appended after — ordering
  within `Up()` is executed top-to-bottom exactly as written.
- `MoveApplicationAsync`/`RejectApplicationAsync` must load the `Application` **tracked**
  (no `AsNoTracking()`) — the concurrency-token mechanism (HLD D-3) only functions against a
  tracked entity's `SaveChangesAsync`.
- `PipelineService` and `ApplicationService` both now depend on `Ats.Db.Requisitions.Stage` —
  no new project reference is needed (`Ats.Service.Application`/`Ats.Service.Pipeline` already
  reference `Ats.Db`, which contains `Stage`).
- Verify after `dotnet ef migrations add` that the generated file matches the §10 step order
  exactly before hand-editing — if EF's diff produces a different but equally-safe shape,
  update this LLD section (Deviation Log) rather than silently diverging.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0003` (Requisition Management) | 1 | `Result`/`ResultStatus`/`ToProblemResult()` pattern, `MapGroup` endpoint style, `Stage`/`Requisition` entity shapes this spec extends, `AppDbContextModelSnapshot` migration workflow. `RequisitionService.CreateAsync` is the method this spec modifies (§3.3). |
| `0004` (Application Submission and CV Upload) | 1 | `Application`/`CvAttachment` entity shapes and `ApplicationService.SubmitAsync`/`ListMineAsync`, both modified by this spec (§3.4); the duplicate-pre-check + DB-constraint-fallback pattern reused for the reject/move concurrency guard. |
| `0002` (User Authentication and Refresh Token Flow) | 1 | `AspNetUsers`/`ClaimsPrincipal` claim-extraction pattern (`GetUserId`), `RecruiterOnly`/`StaffOnly`/`CandidateOnly` policy names, `NormalizedName` pattern reused for Stage names. |

## Deviation Log

Appended by `/implement` when reality diverged from this design.

| Date | Task | Section | Designed | Actual | Reason |
|---|---|---|---|---|---|
| 2026-08-06 | T-09 | §10, erd.md §5 step 6 | FK `Applications.CurrentStageId → Stages.Id` declared on the column at the same time it is added (nullable) | FK added via a separate `AddForeignKey` call placed immediately after the `AlterColumn` to `NOT NULL`, at the very end of `Up()`, with no raw SQL between the two | Interleaving a `migrationBuilder.Sql(...)` call between an `AddForeignKey`/`AlterColumn` operation and the point EF's Sqlite generator flushes the table rebuild it requires fails outright — SQLite refuses to toggle `PRAGMA foreign_keys` mid-transaction, and the two raw-SQL backfill steps sit exactly in that window under the original design. Verified empirically against a scratch SQLite file before finalizing (see `implementation/changelog.md` CP-1 Decisions). |
| 2026-08-06 | T-09 | erd.md §5 step 9 SQL | `CROSS JOIN (VALUES ('Applied', 0), ... ) AS v(Name, SortOrder)` | `CROSS JOIN (SELECT 'Applied' AS Name, 0 AS SortOrder UNION ALL SELECT ...) AS v` | The column-aliased `VALUES` table-value-constructor form (`AS v(col1, col2)`) is not accepted by the SQLite version this project's `Microsoft.Data.Sqlite` package ships (`near "(": syntax error`); the `UNION ALL SELECT` derived-table form is equivalent and portable. |
| 2026-08-06 | T-24 (CP-2), pulled forward | §3.4 | `ApplicationService.SubmitAsync`'s first-Stage resolution and no-stages-configured guard are CP-2 work | The write-path portion (first-Stage lookup + `application.submit.no-stages-configured` guard, exactly as designed here) was implemented in CP-1, alongside the equivalent minimal slice of T-23 (`RequisitionService.CreateAsync` seeding the default Stage set) | `Application.Create`'s signature change (T-03: `currentStageId` now a required constructor argument) is a compile-time break at this call site — `dotnet build` cannot succeed at the end of CP-1 without it, and leaving `SubmitAsync` able to construct an `Application` against a Requisition with zero Stages would silently violate G-1 for every Requisition created before CP-2 ships. CP-2 still owns T-23/T-24's full scope: `ListMineAsync`'s `currentStageName`/`isRejected` projection (part of T-24), and the dedicated AC-7/AC-8/AC-10/AC-33 test coverage named in T-32/T-33. See `implementation/changelog.md` CP-1 Deviations for the full reasoning. |
