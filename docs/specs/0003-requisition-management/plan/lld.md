# Low-Level Design — 0003 Requisition Management

**Spec:** `../spec.md` · **HLD:** `hld.md` · **Updated:** 2026-08-05

The *how*. Precise enough that the implementation agent writes code without re-deciding
anything. Every file it will create or modify is named here, with signatures.

> This file is **living**: when implementation diverges from this design, `/implement`
> patches the affected section here and records the deviation in
> `../implementation/changelog.md`. Silent drift is a defect.

---

## 1. File Manifest

Backend (`backend/`), following the existing `Ats.Db` → `Ats.Service` → `Ats.Api` layering
(single project per layer — see `Ats.ArchitectureTests/LayeringRuleTests.cs`).

| Action | Path | Purpose |
|---|---|---|
| Create | `src/Db/Requisitions/RequisitionStatus.cs` | Lifecycle enum |
| Create | `src/Db/Requisitions/Requisition.cs` | Aggregate entity |
| Create | `src/Db/Requisitions/Stage.cs` | Owned-by-Requisition entity (ownership shape only, FR-14) |
| Create | `src/Db/Configurations/RequisitionConfiguration.cs` | EF Core mapping |
| Create | `src/Db/Configurations/StageConfiguration.cs` | EF Core mapping |
| Modify | `src/Db/AppDbContext.cs` | Add `DbSet<Requisition>`, `DbSet<Stage>`; apply the two configurations |
| Create | `src/Db/Migrations/<timestamp>_AddRequisitionsAndStages.cs` (+ `.Designer.cs`) | Migration (generated via `dotnet ef migrations add`) |
| Modify | `src/Db/Migrations/AppDbContextModelSnapshot.cs` | Auto-updated by the same command |
| Create | `src/Service/Common/PagedResult.cs` | Generic pagination envelope (first use in the project) |
| Create | `src/Service/Requisition/Dtos/RequisitionDto.cs` | Staff-facing DTO |
| Create | `src/Service/Requisition/Dtos/PublicRequisitionDto.cs` | Public-facing DTO |
| Create | `src/Service/Requisition/Dtos/CreateRequisitionRequestDto.cs` | Create request |
| Create | `src/Service/Requisition/Dtos/UpdateRequisitionRequestDto.cs` | Edit request |
| Create | `src/Service/Requisition/IRequisitionService.cs` | Service contract |
| Create | `src/Service/Requisition/RequisitionService.cs` | Implementation |
| Modify | `src/Service/ServiceCollectionExtensions.cs` | Register `IRequisitionService` |
| Create | `src/Api/RequisitionEndpoints.cs` | Staff endpoints (1–7 in `api.md`) |
| Create | `src/Api/PublicRequisitionEndpoints.cs` | Public endpoints (8–9 in `api.md`) |
| Modify | `src/Api/Program.cs` | Map both new endpoint groups |
| Create | `tests/Ats.UnitTests/Requisition/RequisitionEntityTests.cs` | Entity invariants, AC-23 |
| Create | `tests/Ats.UnitTests/Requisition/RequisitionServiceTests.cs` | Service logic |
| Create | `tests/Ats.IntegrationTests/Requisition/RequisitionEndpointsTests.cs` | Staff HTTP tests |
| Create | `tests/Ats.IntegrationTests/Requisition/PublicRequisitionEndpointsTests.cs` | Public HTTP tests, NFR-1 |

Frontend (`frontend/`):

| Action | Path | Purpose |
|---|---|---|
| Create | `src/middleware.ts` | `/staff/*` route gating (FR-9, AC-14, AC-15) |
| Create | `src/lib/auth-guards.ts` | Pure role-check helpers used by middleware and Server Components |
| Create | `src/lib/types/requisition.ts` | Shared TS types mirroring `api.md` §4 |
| Delete | `src/app/(staff)/.gitkeep` | Placeholder from `0001` — replaced by a real `/staff` segment (HLD D-4) |
| Create | `src/app/staff/layout.tsx` | Staff workspace shell (reuses `HeaderNav`) |
| Create | `src/app/staff/requisitions/page.tsx` | Staff list (AC-12) |
| Create | `src/app/staff/requisitions/loading.tsx` | Loading state |
| Create | `src/app/staff/requisitions/error.tsx` | Error state |
| Create | `src/app/staff/requisitions/new/page.tsx` | Create page (AC-1) |
| Create | `src/app/staff/requisitions/[id]/page.tsx` | Detail/edit/lifecycle page |
| Create | `src/app/staff/requisitions/[id]/loading.tsx` | Loading state |
| Create | `src/app/staff/requisitions/[id]/error.tsx` | Error state |
| Create | `src/components/staff/RequisitionForm.tsx` | Create/edit client form |
| Create | `src/components/staff/RequisitionLifecycleActions.tsx` | Publish/unpublish/close buttons |
| Create | `src/components/portal/JobSearchForm.tsx` | Keyword search (progressive-enhancement GET form) |
| Create | `src/components/portal/JobList.tsx` | List + pagination controls |
| Create | `src/app/(portal)/jobs/page.tsx` | Public list (AC-16–20, AC-24) |
| Create | `src/app/(portal)/jobs/loading.tsx` | Loading state |
| Create | `src/app/(portal)/jobs/[id]/page.tsx` | Public detail (AC-21, AC-22) |
| Create | `src/app/(portal)/jobs/[id]/loading.tsx` | Loading state |
| Modify | `src/components/HeaderNav.tsx` | Add "Staff Workspace" (staff sessions) and "Browse Jobs" links |
| Create | `tests/staff/requisition-form.test.tsx` | AC-1, AC-3, AC-5 |
| Create | `tests/staff/requisition-lifecycle-actions.test.tsx` | AC-6, AC-7, AC-9, AC-10, AC-11 |
| Create | `tests/portal/job-search-form.test.tsx` | AC-16, AC-17, AC-20 |
| Create | `tests/lib/auth-guards.test.ts` | AC-14, AC-15 (logic level) |

## 2. Domain / Data Layer

### 2.1 `RequisitionStatus` — `src/Db/Requisitions/RequisitionStatus.cs`

```csharp
namespace Ats.Db.Requisitions;

public enum RequisitionStatus
{
    Draft,
    Published,
    Closed
}
```

### 2.2 `Requisition` — `src/Db/Requisitions/Requisition.cs`

```csharp
namespace Ats.Db.Requisitions;

public class Requisition
{
    private readonly List<Stage> _stages = new();

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public RequisitionStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<Stage> Stages => _stages.AsReadOnly();

    private Requisition() { } // EF Core

    public static Requisition Create(string title, string description)
    {
        var now = DateTime.UtcNow;
        return new Requisition
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Status = RequisitionStatus.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void UpdateContent(string title, string description)
    {
        Title = title;
        Description = description;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Publish() => Transition(RequisitionStatus.Published);
    public void Unpublish() => Transition(RequisitionStatus.Draft);
    public void Close() => Transition(RequisitionStatus.Closed);

    private void Transition(RequisitionStatus target)
    {
        Status = target;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
```

**Invariants.**
- `Status` only changes via `Publish()`/`Unpublish()`/`Close()`. These mutators apply the
  transition **unconditionally** — legality (FR-6) is `RequisitionService`'s responsibility,
  checked *before* calling the mutator, per `coding-standards.md`'s "model expected failures
  as return values, don't throw." The mutators themselves never throw.
- `UpdatedAtUtc` is refreshed by every content edit and every lifecycle transition; `CreatedAtUtc`
  is set once and never changes.
- Every element of `Stages` has `RequisitionId == this.Id` by construction — `Stage.Create`
  requires a `requisitionId` at creation and exposes no way to change it afterward (FR-14).

**Persistence notes.** `_stages` is the EF Core backing field for the `Stages` navigation
(configured in §2.4). No `RowVersion`/concurrency token (spec Assumption A-4 — last write
wins).

### 2.3 `Stage` — `src/Db/Requisitions/Stage.cs`

```csharp
namespace Ats.Db.Requisitions;

public class Stage
{
    public Guid Id { get; private set; }
    public Guid RequisitionId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private Stage() { } // EF Core

    public static Stage Create(Guid requisitionId, string name)
    {
        if (requisitionId == Guid.Empty)
        {
            throw new ArgumentException("RequisitionId cannot be empty.", nameof(requisitionId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        return new Stage { Id = Guid.NewGuid(), RequisitionId = requisitionId, Name = name };
    }
}
```

**Invariants.** `RequisitionId` is set once at construction via the required factory
parameter and has no setter — a `Stage` cannot be reassigned to a different `Requisition`
after creation (AC-23). No `SortOrder`/status column — deferred to the pipeline spec (this
spec's Non-Goals).

**Persistence notes.** No public API in this spec calls `Stage.Create` (no Stage CRUD
endpoint ships here, per Non-Goals) — it exists so `RequisitionEntityTests` can prove the
ownership shape (AC-23) and so the pipeline spec has a stable factory to build on.

### 2.4 EF Core configurations

```csharp
// src/Db/Configurations/RequisitionConfiguration.cs
namespace Ats.Db.Configurations;

public class RequisitionConfiguration : IEntityTypeConfiguration<Requisition>
{
    public void Configure(EntityTypeBuilder<Requisition> builder)
    {
        builder.ToTable("Requisitions");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Description).IsRequired();
        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasDefaultValue(RequisitionStatus.Draft);

        builder.HasIndex(r => r.Status);

        builder.HasMany(r => r.Stages)
            .WithOne()
            .HasForeignKey(s => s.RequisitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Requisition.Stages))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

// src/Db/Configurations/StageConfiguration.cs
namespace Ats.Db.Configurations;

public class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.ToTable("Stages");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(s => s.RequisitionId);
    }
}
```

`AppDbContext.OnModelCreating` gains `builder.ApplyConfiguration(new RequisitionConfiguration())`
and `builder.ApplyConfiguration(new StageConfiguration())`, plus `DbSet<Requisition> Requisitions`
and `DbSet<Stage> Stages` properties, mirroring the existing `DbSet<RefreshToken> RefreshTokens`.

## 3. Service / Application Layer

### 3.1 `IRequisitionService` — `src/Service/Requisition/IRequisitionService.cs`

```csharp
namespace Ats.Service.Requisition;

public interface IRequisitionService
{
    Task<Result<RequisitionDto>> CreateAsync(CreateRequisitionRequestDto dto, CancellationToken ct = default);
    Task<Result<RequisitionDto>> UpdateContentAsync(Guid id, UpdateRequisitionRequestDto dto, CancellationToken ct = default);
    Task<Result<RequisitionDto>> PublishAsync(Guid id, CancellationToken ct = default);
    Task<Result<RequisitionDto>> UnpublishAsync(Guid id, CancellationToken ct = default);
    Task<Result<RequisitionDto>> CloseAsync(Guid id, CancellationToken ct = default);
    Task<Result<RequisitionDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RequisitionDto>>> ListAsync(CancellationToken ct = default);
    Task<Result<PublicRequisitionDto>> GetPublicByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<PagedResult<PublicRequisitionDto>>> SearchPublicAsync(
        string? keyword, int page, int pageSize, CancellationToken ct = default);
}
```

### 3.2 `RequisitionService` — `src/Service/Requisition/RequisitionService.cs`

**Behaviour — `CreateAsync`**

1. Validate `dto.Title` (non-empty, ≤200 chars) and `dto.Description` (non-empty); on failure
   return `Result<RequisitionDto>.Validation(errors)`.
2. `Requisition.Create(title, description)`.
3. Add to `_dbContext.Requisitions`, `SaveChangesAsync`.
4. Map to `RequisitionDto`, return `Result.Ok`.

**Behaviour — `UpdateContentAsync(id, dto)`**

1. Load requisition by id (`FindAsync`); if absent return `Result.NotFound("requisition.not-found", ...)`.
2. If `Status == Closed`, return `Result.Conflict("requisition.update.closed", ...)` (AC-5) —
   **before** validating the new content, so a closed requisition is always rejected the same
   way regardless of payload.
3. Validate `dto.Title`/`dto.Description` as in `CreateAsync`; on failure return `Validation`.
4. `requisition.UpdateContent(title, description)`, `SaveChangesAsync`.
5. Return `Result.Ok(dto)`.

**Behaviour — `PublishAsync(id)` / `UnpublishAsync(id)` / `CloseAsync(id)`**

Identical shape, only the guard and target differ:

1. Load requisition by id; `NotFound` if absent.
2. Guard the *current* status:
   - `PublishAsync` requires `Status == Draft`, else `Result.Conflict("requisition.publish.invalid-transition", ...)`.
   - `UnpublishAsync` requires `Status == Published`, else `Result.Conflict("requisition.unpublish.invalid-transition", ...)`.
   - `CloseAsync` requires `Status == Published`, else `Result.Conflict("requisition.close.invalid-transition", ...)`.
3. Call the matching entity mutator (`Publish()`/`Unpublish()`/`Close()`), `SaveChangesAsync`.
4. Return `Result.Ok(dto)`.

**Behaviour — `GetByIdAsync(id)` / `ListAsync()`** (staff, any status)

Plain `FindAsync` / `ToListAsync()` over `_dbContext.Requisitions`, ordered by
`CreatedAtUtc` descending for `ListAsync`.
> **Assumption:** newest-first ordering — not specified by the spec, low-cost default.
`NotFound` for a missing `GetByIdAsync` id.

**Behaviour — `GetPublicByIdAsync(id)`**

1. `_dbContext.Requisitions.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id && r.Status == RequisitionStatus.Published)`.
2. If null (doesn't exist **or** exists but isn't published — same branch, same result, no
   distinguishing code path per AC-22/E-10) → `Result.NotFound("requisition.not-found", ...)`.
3. Else map to `PublicRequisitionDto`, `Result.Ok`.

**Behaviour — `SearchPublicAsync(keyword, page, pageSize)`**

1. Resolve `resolvedPageSize = Math.Clamp(pageSize, 1, MaxPageSize)` (config, default 50).
2. Build `IQueryable<Requisition>` filtered `Status == Published`, `AsNoTracking()`.
3. If `keyword` is non-empty, `.Where(r => r.Title.Contains(keyword) || r.Description.Contains(keyword))`
   — SQLite's default `LIKE` translation is ASCII case-insensitive, satisfying "matches the
   keyword" without an explicit collation (Assumption A-8, HLD R-1).
4. `total = await query.CountAsync(ct)`.
5. `items = await query.OrderByDescending(r => r.CreatedAtUtc).Skip((page - 1) * resolvedPageSize).Take(resolvedPageSize).ToListAsync(ct)`.
6. Return `Result.Ok(new PagedResult<PublicRequisitionDto>(items, page, resolvedPageSize, total))`.

`page` is trusted to already be a validated positive integer — the endpoint (§4) rejects an
invalid one with 400 *before* calling this method (AC-24: "without executing the query").

**Returns — outcome→result→HTTP**

| Outcome | Result | Maps to HTTP |
|---|---|---|
| Success | `Result.Ok(dto)` | 200 (201 for Create, mapped at the endpoint) |
| Missing requisition | `Result.NotFound("requisition.not-found", ...)` | 404 |
| Invalid title/description | `Result.Validation(errors)` | 400 |
| Edit while closed | `Result.Conflict("requisition.update.closed", ...)` | 409 |
| Invalid lifecycle transition | `Result.Conflict("requisition.<action>.invalid-transition", ...)` | 409 |

## 4. API Layer

Endpoint shapes are specified in `api.md`. Here, only the wiring — both files use the same
minimal-API `MapGroup` pattern as `AuthEndpoints.cs`/`SystemStatusEndpoints.cs`, and reuse
`AuthEndpoints.ToProblemResult()` (same `Ats.Api` namespace, no extra `using` needed) for
every `Result` → `IResult` mapping.

| Route | Handler | Auth policy | Maps service result via |
|---|---|---|---|
| `POST /api/requisitions` | `RequisitionEndpoints` `/` POST | `RecruiterOnly` | `CreateAsync` → `Results.Created` / `ToProblemResult()` |
| `GET /api/requisitions` | `RequisitionEndpoints` `/` GET | `StaffOnly` | `ListAsync` → `Results.Ok` |
| `GET /api/requisitions/{id}` | `RequisitionEndpoints` `/{id}` GET | `StaffOnly` | `GetByIdAsync` → `Results.Ok` / `ToProblemResult()` |
| `PUT /api/requisitions/{id}` | `RequisitionEndpoints` `/{id}` PUT | `RecruiterOnly` | `UpdateContentAsync` → `Results.Ok` / `ToProblemResult()` |
| `POST /api/requisitions/{id}/publish` | `RequisitionEndpoints` `/{id}/publish` | `RecruiterOnly` | `PublishAsync` → `Results.Ok` / `ToProblemResult()` |
| `POST /api/requisitions/{id}/unpublish` | `RequisitionEndpoints` `/{id}/unpublish` | `RecruiterOnly` | `UnpublishAsync` → `Results.Ok` / `ToProblemResult()` |
| `POST /api/requisitions/{id}/close` | `RequisitionEndpoints` `/{id}/close` | `RecruiterOnly` | `CloseAsync` → `Results.Ok` / `ToProblemResult()` |
| `GET /api/public/requisitions` | `PublicRequisitionEndpoints` `/` GET | `AllowAnonymous` | manual `page`/`pageSize` parse (400 if `page` invalid) → `SearchPublicAsync` → `Results.Ok` |
| `GET /api/public/requisitions/{id}` | `PublicRequisitionEndpoints` `/{id}` GET | `AllowAnonymous` | `GetPublicByIdAsync` → `Results.Ok` / `ToProblemResult()` |

`Program.cs` gains `app.MapRequisitionEndpoints();` and `app.MapPublicRequisitionEndpoints();`
alongside the existing `MapSystemStatus()`/`MapAuthEndpoints()` calls.

## 5. Frontend

### 5.1 Components

| Component | Path | Props | State | Notes |
|---|---|---|---|---|
| `RequisitionForm` | `src/components/staff/RequisitionForm.tsx` | `mode: "create" \| "edit"`, `requisitionId?`, `initialTitle?`, `initialDescription?` | `title`, `description`, `loading`, `error` (client) | POSTs to `/api/bff/proxy/requisitions` (create) or PUTs `/api/bff/proxy/requisitions/{id}` (edit); on success `router.push`/`router.refresh()`. Mirrors `RegisterForm.tsx`'s structure. |
| `RequisitionLifecycleActions` | `src/components/staff/RequisitionLifecycleActions.tsx` | `requisitionId`, `status: RequisitionStatus`, `canWrite: boolean` | `loading`, `error` (client) | Renders Publish/Unpublish/Close buttons appropriate to `status`; `canWrite=false` (HiringManager) renders nothing. POSTs the matching lifecycle proxy path, then `router.refresh()`. |
| `JobSearchForm` | `src/components/portal/JobSearchForm.tsx` | `defaultKeyword?: string` | none (server-navigable `<form method="get">`) | No client JS required — submitting navigates to `/jobs?keyword=...&page=1`, read by the Server Component page via `searchParams`. |
| `JobList` | `src/components/portal/JobList.tsx` | `items: PublicRequisitionDto[]`, `page`, `pageSize`, `total`, `keyword?` | none (presentational Server Component) | Renders cards + Prev/Next links built from `searchParams`, disabled at the first/last page. |

### 5.2 Data Access

No client-side cache library exists in this project yet (HLD D-5) — reads happen in Server
Components via `invokeBackend`, mutations happen in Client Components via `fetch` to the
existing `/api/bff/proxy/*` route handler, followed by `router.refresh()` to re-fetch the
Server Component tree. Cache-key columns don't apply; this table lists call sites instead.

| Call site | Endpoint (via proxy unless noted) | Method | Re-render trigger |
|---|---|---|---|
| `staff/requisitions/page.tsx` (Server Component) | `invokeBackend` → `/api/requisitions` | GET | Normal SSR on navigation |
| `staff/requisitions/[id]/page.tsx` (Server Component) | `invokeBackend` → `/api/requisitions/{id}` | GET | Normal SSR on navigation |
| `RequisitionForm` (create) | `/api/bff/proxy/requisitions` | POST | `router.push("/staff/requisitions/{id}")` on success |
| `RequisitionForm` (edit) | `/api/bff/proxy/requisitions/{id}` | PUT | `router.refresh()` on success |
| `RequisitionLifecycleActions` | `/api/bff/proxy/requisitions/{id}/publish\|unpublish\|close` | POST | `router.refresh()` on success |
| `(portal)/jobs/page.tsx` (Server Component) | `invokeBackend` → `/api/public/requisitions?...` | GET | Normal SSR on navigation (search/pagination are plain links/`<form method="get">`) |
| `(portal)/jobs/[id]/page.tsx` (Server Component) | `invokeBackend` → `/api/public/requisitions/{id}`; `notFound()` on `BackendInvokeError` with `status === 404` | GET | Normal SSR on navigation |

### 5.3 UI States

Every async surface implements all four states, using Next.js App Router's `loading.tsx`/`error.tsx`
convention for the two Server-Component list/detail surfaces, and local component state for
the two Client-Component forms.

| Surface | Loading | Empty | Error | Success |
|---|---|---|---|---|
| Staff requisition list | `staff/requisitions/loading.tsx` skeleton rows | "No requisitions yet — create one" + CTA, inside `page.tsx` | `staff/requisitions/error.tsx` with retry | Table of requisitions with status badges |
| Staff requisition detail | `staff/requisitions/[id]/loading.tsx` skeleton | N/A (a specific id always has content or 404s) | `staff/requisitions/[id]/error.tsx`; `notFound()` for a missing id | Form pre-filled + lifecycle buttons |
| `RequisitionForm` submit | Submit button shows a spinner, disabled inputs | N/A | Inline `role="alert"` banner, same pattern as `RegisterForm.tsx` | Redirect/refresh |
| `RequisitionLifecycleActions` | Clicked button shows a spinner, all buttons disabled | N/A | Inline `role="alert"` banner | Buttons re-render for the new status |
| Portal job list | `(portal)/jobs/loading.tsx` skeleton | "No roles match your search" inside `JobList` | Next.js default error boundary (no custom `error.tsx` — public page, generic messaging acceptable) | `JobList` renders cards + pagination |
| Portal job detail | `(portal)/jobs/[id]/loading.tsx` skeleton | N/A | `notFound()` renders Next.js's default `not-found.tsx` (none customised — no AC requires bespoke copy) | Full job detail |

## 6. DTOs & Contracts

```ts
// src/lib/types/requisition.ts — mirrors api.md §4 exactly
export type RequisitionStatus = "draft" | "published" | "closed";

export type RequisitionDto = {
  id: string;
  title: string;
  description: string;
  status: RequisitionStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type PublicRequisitionDto = {
  id: string;
  title: string;
  description: string;
  updatedAtUtc: string;
};

export type Paged<T> = { items: T[]; page: number; pageSize: number; total: number };
```

## 7. Validation Rules

| Field | Rule | Message | AC |
|---|---|---|---|
| `title` (create/edit) | Required, non-whitespace, ≤200 chars | "Title is required." / "Title must be 200 characters or fewer." | AC-1, AC-3, E-11 |
| `description` (create/edit) | Required, non-whitespace | "Description is required." | AC-1, AC-3, E-11 |
| `page` (public list) | If present: parses as integer, `>= 1` | "page must be a positive integer." | AC-24 |
| `pageSize` (public list) | Not validated — clamped to `[1, 50]`, default 20 if absent/non-numeric | N/A (never an error) | FR-11, NFR-1 |
| `keyword` (public list) | Optional; blank/absent means "no filter" | N/A | AC-16, AC-17, AC-20 |

## 8. Error Handling

Follows the project envelope (`meta/coding-standards.md` §Error Handling — RFC 7807
ProblemDetails, `<entity>.<operation>.<condition>` codes).

| Condition | Code | Logged at | User-facing message |
|---|---|---|---|
| Invalid title/description on create | `requisition.create.validation-failed` | Warning | "Please check the highlighted fields." |
| Invalid title/description on edit | `requisition.update.validation-failed` | Warning | "Please check the highlighted fields." |
| Edit attempted on a `closed` requisition | `requisition.update.closed` | Information | "This requisition is closed and can no longer be edited." |
| Publish from a non-`draft` status | `requisition.publish.invalid-transition` | Information | "Only a draft requisition can be published." |
| Unpublish from a non-`published` status | `requisition.unpublish.invalid-transition` | Information | "Only a published requisition can be unpublished." |
| Close from a non-`published` status | `requisition.close.invalid-transition` | Information | "Only a published requisition can be closed." |
| Any staff endpoint, missing id | `requisition.not-found` | Information | "Requisition not found." |
| Public detail, missing or non-published id | `requisition.not-found` | Debug (expected traffic, not worth an audit trail) | "This job posting is no longer available." |
| Public list, invalid `page` | `requisition.list.invalid-page` | Warning | "Invalid page number." |

## 9. Configuration

| Key | Type | Default | Required | Where consumed |
|---|---|---|---|---|
| `Requisitions:DefaultPageSize` | int | `20` | No | `RequisitionService.SearchPublicAsync` |
| `Requisitions:MaxPageSize` | int | `50` | No | `RequisitionService.SearchPublicAsync` (NFR-1 ceiling) |

Both follow the existing `int.TryParse(_configuration["Jwt:..."], ...)`-with-fallback pattern
already used by `AuthService`/`JwtTokenGenerator` — no new configuration-binding mechanism.

## 10. Database Migration

| Step | Change | Reversible |
|---|---|---|
| 1 | `CreateTable("Requisitions")` — `Id`, `Title`, `Description`, `Status`, `CreatedAtUtc`, `UpdatedAtUtc` | Yes |
| 2 | `CreateTable("Stages")` — `Id`, `RequisitionId` (FK, cascade), `Name` | Yes |
| 3 | `CreateIndex(IX_Requisitions_Status)` | Yes |
| 4 | `CreateIndex(IX_Stages_RequisitionId)` | Yes |

No backfill — both tables start empty. Full detail and rollback plan in `erd.md` §5.

## 11. Test Plan

| Test | Type | Covers | Path |
|---|---|---|---|
| `Requisition_Create_StartsInDraft` | Unit | AC-1 | `tests/Ats.UnitTests/Requisition/RequisitionEntityTests.cs` |
| `Stage_Create_IsScopedToItsOwnRequisition` | Unit | AC-23 | `tests/Ats.UnitTests/Requisition/RequisitionEntityTests.cs` |
| `CreateAsync_WithValidPayload_ReturnsDraft` | Unit | AC-1 | `tests/Ats.UnitTests/Requisition/RequisitionServiceTests.cs` |
| `UpdateContentAsync_WhileDraft_UpdatesFields` | Unit | AC-3 | same |
| `UpdateContentAsync_WhilePublished_UpdatesFieldsKeepsStatus` | Unit | AC-4 | same |
| `UpdateContentAsync_WhileClosed_ReturnsConflict` | Unit | AC-5 | same |
| `PublishAsync_FromDraft_ReturnsPublished` | Unit | AC-6 | same |
| `UnpublishAsync_FromPublished_ReturnsDraft` | Unit | AC-7 | same |
| `PublishAsync_AfterUnpublishAndEdit_ReflectsEditedContent` | Unit | AC-8 | same |
| `CloseAsync_FromPublished_ReturnsClosed` | Unit | AC-9 | same |
| `CloseAsync_FromDraft_ReturnsConflict` | Unit | AC-10, E-3 | same |
| `PublishAsync_FromClosed_ReturnsConflict` / `UnpublishAsync_FromClosed_ReturnsConflict` | Unit | AC-11, E-4 | same |
| `SearchPublicAsync_WithMatchingKeyword_ReturnsOnlyPublished` | Unit | AC-16 | same |
| `SearchPublicAsync_WithNoMatch_ReturnsEmptyNotError` | Unit | AC-17, E-6 | same |
| `ListAsync_DefaultPage_ReturnsFirst20WithMetadata` | Unit | AC-18 | same |
| `SearchPublicAsync_PageBeyondLast_ReturnsEmptyWithTrueTotal` | Unit | AC-19, E-7 | same |
| `SearchPublicAsync_KeywordAndPage_FiltersThenPaginates` | Unit | AC-20 | same |
| `GetPublicByIdAsync_Published_ReturnsContent` | Unit | AC-21 | same |
| `GetPublicByIdAsync_DraftClosedOrMissing_ReturnsNotFound` | Unit | AC-22, E-10 | same |
| `SearchPublicAsync_PageSizeAboveMax_IsClampedTo50` | Unit | NFR-1 | same |
| `POST_requisitions_AsRecruiter_Returns201` | Integration | AC-1 | `tests/Ats.IntegrationTests/Requisition/RequisitionEndpointsTests.cs` |
| `POST_requisitions_AsHiringManagerOrCandidate_Returns403` | Integration | AC-2 | same |
| `PUT_requisitions_AsRecruiterOnDraft_Returns200` | Integration | AC-3 | same |
| `PUT_requisitions_AsRecruiterOnPublished_Returns200AndPortalReflectsIt` | Integration | AC-4 | same |
| `PUT_requisitions_OnClosed_Returns409` | Integration | AC-5, E-5 | same |
| `POST_publish_FromDraft_Returns200` | Integration | AC-6 | same |
| `POST_unpublish_FromPublished_Returns200AndPortalDetail404s` | Integration | AC-7, E-1 | same |
| `POST_publish_AfterUnpublishAndEdit_Returns200WithEditedContent` | Integration | AC-8, E-2 | same |
| `POST_close_FromPublished_Returns200AndPortalRemovesIt` | Integration | AC-9 | same |
| `POST_close_FromDraft_Returns409` | Integration | AC-10, E-3 | same |
| `POST_publishOrUnpublish_FromClosed_Returns409` | Integration | AC-11, E-4 | same |
| `GET_requisitions_AsHiringManager_Returns200AllStatuses` | Integration | AC-12 | same |
| `ANY_requisitionsEndpoint_AsCandidate_Returns403` | Integration | AC-13 | same |
| `GET_public_requisitions_KeywordEngineer_ReturnsOnlyMatchingPublished` | Integration | AC-16 | `tests/Ats.IntegrationTests/Requisition/PublicRequisitionEndpointsTests.cs` |
| `GET_public_requisitions_NoMatch_Returns200EmptyList` | Integration | AC-17, E-6 | same |
| `GET_public_requisitions_NoPageParam_ReturnsFirst20WithMetadata` | Integration | AC-18 | same |
| `GET_public_requisitions_PageBeyondLast_Returns200EmptyWithTrueTotal` | Integration | AC-19, E-7 | same |
| `GET_public_requisitions_KeywordAndPage_FiltersThenPaginates` | Integration | AC-20 | same |
| `GET_public_requisitions_id_Published_Returns200` | Integration | AC-21 | same |
| `GET_public_requisitions_id_DraftOrClosed_Returns404` | Integration | AC-22, E-10 | same |
| `GET_public_requisitions_InvalidPageParam_Returns400WithoutQuerying` | Integration | AC-24, E-8 | same |
| `GET_public_requisitions_PageSize1000_Returns50Items` | Integration | NFR-1 | same |
| `GetPublicByIdAsync_NeverOpensTransaction` | Integration | NFR-2 | same file, or a dedicated `RequisitionServiceTests` case asserting `dbContext.Database.CurrentTransaction == null` |
| `RequisitionForm` shows validation error on empty title | Component | AC-1, AC-3 | `tests/staff/requisition-form.test.tsx` |
| `RequisitionForm` shows 409 error banner when editing a closed requisition | Component | AC-5 | same |
| `RequisitionLifecycleActions` shows 409 error banner on invalid transition | Component | AC-10, AC-11 | `tests/staff/requisition-lifecycle-actions.test.tsx` |
| `RequisitionLifecycleActions` renders Publish for draft, Unpublish+Close for published | Component | AC-6, AC-7, AC-9 | same |
| `JobSearchForm` builds correct `?keyword=` query string | Component | AC-16 | `tests/portal/job-search-form.test.tsx` |
| `JobList` renders empty state for zero results | Component | AC-17 | same |
| `isStaffRole` returns true only for Recruiter/HiringManager | Unit | AC-14, AC-15 | `tests/lib/auth-guards.test.ts` |

Every `AC-n` (AC-1 through AC-24) appears at least once above; the spec's Non-Goals exclude
FR-14's Stage *behaviour* from testing here — only its ownership shape (AC-23) is tested.

## 12. Implementation Notes

- Build order matters: `RequisitionConfiguration`/`StageConfiguration` must exist before the
  migration is generated (`dotnet ef migrations add` reads the compiled model).
- `RequisitionService`'s public methods (`GetPublicByIdAsync`, `SearchPublicAsync`) must never
  be called from a code path that also calls `SaveChangesAsync` in the same request — keeping
  them structurally separate from the staff mutation methods is what makes NFR-2 easy to keep
  true, not just true today.
- `ToProblemResult()` (defined in `AuthEndpoints.cs`, `Ats.Api` namespace) is reused as-is for
  every `Result` → `IResult` mapping in both new endpoint files — do not duplicate it.
- The `(staff)` route group deletion (File Manifest) is a one-line removal of a `.gitkeep`;
  confirm no other file references the old empty group before deleting.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0002` (User Authentication and Refresh Token Flow) | 1 | `Result`/`ResultStatus` pattern, `AuthEndpoints.ToProblemResult()`, `RecruiterOnly`/`StaffOnly` policy names, `AppDbContext`/`IEntityTypeConfiguration<T>` patterns, NextAuth session shape (`session.user.roles`), in-memory-SQLite unit test pattern (`AuthServiceTests`). |
| `0001` (Project Scaffolding and Walking Skeleton) | 1 | `ui/bff` proxy route + `invokeBackend`, Server-Component-for-reads/Client-Component-for-mutations split, `CustomWebApplicationFactory` integration test harness, `(portal)` route-group precedent. |

## Deviation Log

Appended by `/implement` when reality diverged from this design.

| Date | Task | Section | Designed | Actual | Reason |
|---|---|---|---|---|---|
