# Low-Level Design — 0004 Application Submission and CV Upload

**Spec:** `../spec.md` · **HLD:** `hld.md` · **Updated:** 2026-08-06

> This file is **living**: when implementation diverges from this design, `/implement`
> patches the affected section here and records the deviation in
> `../implementation/changelog.md`. Silent drift is a defect.

---

## 1. File Manifest

Backend (`backend/`), following the existing `Ats.Shared` → `Ats.Db` → `Ats.Service` →
`Ats.Api` layering (`tests/Ats.ArchitectureTests/LayeringRuleTests.cs`).

| Action | Path | Purpose |
|---|---|---|
| Create | `src/Shared/Storage/IFileStorage.cs` | Storage interface (D-1) |
| Create | `src/Shared/Storage/LocalDiskFileStorage.cs` | Local-disk implementation |
| Create | `src/Db/Applications/Application.cs` | Aggregate entity |
| Create | `src/Db/Applications/CvAttachment.cs` | Dependent entity (1:1 with `Application`) |
| Create | `src/Db/Configurations/ApplicationConfiguration.cs` | EF Core mapping |
| Create | `src/Db/Configurations/CvAttachmentConfiguration.cs` | EF Core mapping |
| Modify | `src/Db/AppDbContext.cs` | Add `DbSet<Application>`, `DbSet<CvAttachment>`; apply both configurations |
| Create | `src/Db/Migrations/<timestamp>_AddApplicationsAndCvAttachments.cs` (+ `.Designer.cs`) | Migration (generated via `dotnet ef migrations add`) |
| Modify | `src/Db/Migrations/AppDbContextModelSnapshot.cs` | Auto-updated by the same command |
| Create | `src/Service/Application/Dtos/ApplicationDto.cs` | Submission response |
| Create | `src/Service/Application/Dtos/CandidateApplicationListItemDto.cs` | Candidate "mine" list item |
| Create | `src/Service/Application/Dtos/StaffApplicationListItemDto.cs` | Staff list item |
| Create | `src/Service/Application/Dtos/CvDownloadResult.cs` | Stream + metadata for a CV download |
| Create | `src/Service/Application/IApplicationService.cs` | Service contract |
| Create | `src/Service/Application/ApplicationService.cs` | Implementation |
| Modify | `src/Service/ServiceCollectionExtensions.cs` | Register `IApplicationService`, `IFileStorage` |
| Create | `src/Api/ApplicationEndpoints.cs` | All four endpoints in `api.md` |
| Modify | `src/Api/Program.cs` | Map the new endpoint group |
| Modify | `src/Api/appsettings.json` | Add `Storage:CvBasePath`, `Applications:MaxCvSizeBytes` |
| Modify | `.gitignore` (repo root) | Ignore the backend's app-data directory — CV files are candidate PII, same reasoning as `0001` FR-1 for the SQLite file |
| Create | `tests/Ats.UnitTests/Application/ApplicationEntityTests.cs` | Entity invariants |
| Create | `tests/Ats.UnitTests/Application/ApplicationServiceTests.cs` | Service logic, all validation/authorization branches |
| Create | `tests/Ats.UnitTests/Storage/LocalDiskFileStorageTests.cs` | Save/open/delete round-trip, path-traversal rejection |
| Create | `tests/Ats.IntegrationTests/Application/ApplicationEndpointsTests.cs` | HTTP-level tests for all documented status codes |
| Modify | `tests/Ats.IntegrationTests/CustomWebApplicationFactory.cs` | Add a per-test temp `Storage:CvBasePath`, cleaned up in `Dispose` (mirrors the existing per-test SQLite file pattern) |

Frontend (`frontend/`):

| Action | Path | Purpose |
|---|---|---|
| Modify | `src/app/api/bff/proxy/[...path]/route.ts` | Binary-safe (`ArrayBuffer`) request/response body passthrough; forward `Content-Disposition` (HLD D-4) |
| Create | `src/lib/types/application.ts` | Shared TS types mirroring `api.md` §4 |
| Modify | `src/lib/auth-guards.ts` | Add `isCandidateRole` |
| Modify | `src/middleware.ts` | Gate `/applications/*` for the `Candidate` role, mirroring the existing `/staff/*` gate |
| Create | `src/components/portal/ApplicationForm.tsx` | Client Component: CV file input + submit |
| Create | `src/app/(portal)/jobs/[id]/apply/page.tsx` | Server Component: loads the Requisition, session-gates, renders `ApplicationForm` |
| Create | `src/app/(portal)/jobs/[id]/apply/loading.tsx` | Loading state |
| Modify | `src/app/(portal)/jobs/[id]/page.tsx` | Add a session-aware "Apply" call to action |
| Create | `src/components/portal/ApplicationList.tsx` | Presentational — candidate's own Applications |
| Create | `src/app/(portal)/applications/page.tsx` | "My Applications" (AC-12, AC-13) |
| Create | `src/app/(portal)/applications/loading.tsx` | Loading state |
| Create | `src/app/(portal)/applications/error.tsx` | Error state |
| Modify | `src/components/HeaderNav.tsx` | Add "My Applications" link for Candidate sessions |
| Create | `src/components/staff/ApplicationsTable.tsx` | Presentational — staff per-Requisition list |
| Create | `src/app/staff/requisitions/[id]/applications/page.tsx` | Staff Applications list (AC-16–AC-19) |
| Create | `src/app/staff/requisitions/[id]/applications/loading.tsx` | Loading state |
| Create | `src/app/staff/requisitions/[id]/applications/error.tsx` | Error state |
| Modify | `src/app/staff/requisitions/[id]/page.tsx` | Add a "View Applications" link |
| Create | `tests/portal/application-form.test.tsx` | AC-2, AC-3, AC-4 (client-side surfaced errors) |
| Create | `tests/portal/application-list.test.tsx` | AC-12, AC-13 |
| Create | `tests/staff/applications-table.test.tsx` | AC-16, AC-18 |
| Modify | `tests/lib/auth-guards.test.ts` | `isCandidateRole` cases |

## 2. Domain / Data Layer

### 2.1 `Application` — `src/Db/Applications/Application.cs`

```csharp
namespace Ats.Db.Applications;

public class Application
{
    public Guid Id { get; private set; }
    public Guid RequisitionId { get; private set; }
    public Guid CandidateId { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }
    public CvAttachment? CvAttachment { get; private set; }

    private Application() { } // EF Core

    public static Application Create(Guid requisitionId, Guid candidateId)
    {
        if (requisitionId == Guid.Empty)
        {
            throw new ArgumentException("RequisitionId cannot be empty.", nameof(requisitionId));
        }

        if (candidateId == Guid.Empty)
        {
            throw new ArgumentException("CandidateId cannot be empty.", nameof(candidateId));
        }

        return new Application
        {
            Id = Guid.NewGuid(),
            RequisitionId = requisitionId,
            CandidateId = candidateId,
            SubmittedAtUtc = DateTime.UtcNow
        };
    }

    // Invariant: called exactly once, before the entity is ever added to the DbContext.
    // ApplicationService never calls DbContext.Applications.Add() without having called this
    // first (LLD §3.2 step 9) — the DB schema does not enforce "CvAttachment required" as a
    // constraint (same precedent as 0003's Status field: encoded in code shape, not a CHECK).
    public void AttachCv(CvAttachment cv)
    {
        ArgumentNullException.ThrowIfNull(cv);
        if (CvAttachment != null)
        {
            throw new InvalidOperationException("A CvAttachment is already attached.");
        }

        CvAttachment = cv;
    }
}
```

**Invariants.**
- `RequisitionId`/`CandidateId` are set once at construction and never reassigned — an
  `Application` cannot be moved to a different Requisition or Candidate after creation.
- `SubmittedAtUtc` is set once, at construction, and never updated (FR-13).
- `CvAttachment` is attached exactly once via `AttachCv`; a second call throws.

**Persistence notes.** `CvAttachment` is a reference navigation to a separate table (not an EF
Core owned type), configured 1:1 in §2.4. EF Core's default change-tracking graph traversal
picks up `CvAttachment` when `Application` is added, so `DbContext.Applications.Add(application)`
alone is sufficient to insert both rows — no separate `DbContext.CvAttachments.Add(cv)` call is
needed provided `AttachCv` was called first.

### 2.2 `CvAttachment` — `src/Db/Applications/CvAttachment.cs`

```csharp
namespace Ats.Db.Applications;

public class CvAttachment
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }

    private CvAttachment() { } // EF Core

    public static CvAttachment Create(
        Guid applicationId, string storageKey, string originalFileName, string contentType, long sizeBytes)
    {
        if (applicationId == Guid.Empty)
        {
            throw new ArgumentException("ApplicationId cannot be empty.", nameof(applicationId));
        }

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("StorageKey cannot be empty.", nameof(storageKey));
        }

        if (sizeBytes <= 0)
        {
            throw new ArgumentException("SizeBytes must be positive.", nameof(sizeBytes));
        }

        return new CvAttachment
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            StorageKey = storageKey,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            UploadedAtUtc = DateTime.UtcNow
        };
    }
}
```

**Invariants.** `StorageKey` is always server-generated (`ApplicationService`, never the
client's filename) — see §3.2 step 7. `ApplicationId` is set once at construction, matching the
`Stage.RequisitionId` precedent from `0003`.

**Persistence notes.** `ApplicationId` doubles as the FK and, via a unique index (§2.4), enforces
the 1:1 relationship at the database level.

### 2.3 `IFileStorage` / `LocalDiskFileStorage` — `src/Shared/Storage/`

```csharp
namespace Ats.Shared.Storage;

public interface IFileStorage
{
    Task SaveAsync(string storageKey, Stream content, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
```

```csharp
namespace Ats.Shared.Storage;

using Microsoft.Extensions.Configuration;

public class LocalDiskFileStorage : IFileStorage
{
    private const string DefaultBasePath = "./app-data/cv-attachments";
    private readonly string _basePath;

    public LocalDiskFileStorage(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _basePath = configuration["Storage:CvBasePath"] ?? DefaultBasePath;
        Directory.CreateDirectory(_basePath);
    }

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);
        await using var fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, ct);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    // Never trust a storage key as a path fragment (coding-standards.md: "Uploaded files are
    // never served from a path the client controls"). In practice the client never supplies a
    // storage key — ApplicationService always generates it — this is defence in depth.
    private string ResolvePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) ||
            storageKey.Contains("..", StringComparison.Ordinal) ||
            storageKey.Contains('/') || storageKey.Contains('\\'))
        {
            throw new ArgumentException("Invalid storage key.", nameof(storageKey));
        }

        return Path.Combine(_basePath, storageKey);
    }
}
```

**Persistence notes.** Registered as a Singleton (§4) — stateless beyond the resolved
`_basePath`, and `Directory.CreateDirectory` only needs to run once at startup.

### 2.4 EF Core configurations

```csharp
// src/Db/Configurations/ApplicationConfiguration.cs
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

        builder.HasIndex(a => new { a.CandidateId, a.RequisitionId }).IsUnique();
        builder.HasIndex(a => a.RequisitionId);

        builder.HasOne<Ats.Db.Requisitions.Requisition>()
            .WithMany()
            .HasForeignKey(a => a.RequisitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Ats.Shared.Auth.ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.CvAttachment)
            .WithOne()
            .HasForeignKey<CvAttachment>(c => c.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

// src/Db/Configurations/CvAttachmentConfiguration.cs
namespace Ats.Db.Configurations;

public class CvAttachmentConfiguration : IEntityTypeConfiguration<CvAttachment>
{
    public void Configure(EntityTypeBuilder<CvAttachment> builder)
    {
        builder.ToTable("CvAttachments");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.StorageKey).IsRequired().HasMaxLength(300);
        builder.Property(c => c.OriginalFileName).IsRequired().HasMaxLength(260);
        builder.Property(c => c.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(c => c.SizeBytes).IsRequired();
        builder.Property(c => c.UploadedAtUtc).IsRequired();

        builder.HasIndex(c => c.ApplicationId).IsUnique();
    }
}
```

`AppDbContext.OnModelCreating` gains both `ApplyConfiguration(...)` calls, plus
`DbSet<Application> Applications` and `DbSet<CvAttachment> CvAttachments`, mirroring the
existing `Requisitions`/`Stages` pair.

## 3. Service / Application Layer

### 3.1 `IApplicationService` — `src/Service/Application/IApplicationService.cs`

```csharp
namespace Ats.Service.Application;

public interface IApplicationService
{
    Task<Result<ApplicationDto>> SubmitAsync(
        Guid requisitionId, Guid candidateId,
        Stream cvContent, string cvFileName, string cvContentType, long cvSizeBytes,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<CandidateApplicationListItemDto>>> ListMineAsync(
        Guid candidateId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<StaffApplicationListItemDto>>> ListForRequisitionAsync(
        Guid requisitionId, CancellationToken ct = default);

    Task<Result<CvDownloadResult>> GetCvAsync(
        Guid applicationId, Guid requestingUserId, bool requesterIsStaff, CancellationToken ct = default);
}
```

`Stream` is `System.IO.Stream`, not an ASP.NET Core HTTP type — permitted across the `service/*`
boundary per `LayeringRuleTests.Service_DoesNotReferenceAspNetCoreHttp` (which forbids
`Microsoft.AspNetCore.Http`, not `System.IO`). This is also *why* `IFormFile` never appears in
this interface — the API layer extracts a plain `Stream` + primitives from it first (§4).

### 3.2 `ApplicationService` — `src/Service/Application/ApplicationService.cs`

**Constructor.** `(AppDbContext dbContext, IFileStorage fileStorage, IConfiguration configuration)`
— `_maxCvSizeBytes = int.TryParse(configuration["Applications:MaxCvSizeBytes"], out var max) ? max : 5 * 1024 * 1024`,
following the exact `IConfiguration`-with-fallback pattern `RequisitionService` established in
`0003` (no `IOptions<T>` — matching actual project precedent over the aspirational
`architecture.md` note, per `coding-standards.md`'s "prefer the project's existing patterns").

**Behaviour — `SubmitAsync`**

1. Load the Requisition: `_dbContext.Requisitions.AsNoTracking().SingleOrDefaultAsync(r => r.Id == requisitionId && r.Status == Published, ct)`. If `null` → `Result.NotFound("application.submit.requisition-not-found", "This job posting is no longer available.")` — byte-identical message to `RequisitionService.GetPublicByIdAsync` (FR-4 reuses `0003`'s no-existence-leak pattern; draft, closed, and non-existent all take this one branch).
2. If `cvSizeBytes <= 0` → `Result.Validation({"cv": ["A CV file is required."]}, "application.submit.cv-required", "Validation failed.")` (defence in depth — the API layer already rejects a null/zero-length `IFormFile` before calling this method, §4).
3. If `!string.Equals(cvContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)` or `!cvFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)` → `Result.Validation({"cv": ["Only PDF files are accepted."]}, "application.submit.invalid-file-type", "Validation failed.")`.
4. If `cvSizeBytes > _maxCvSizeBytes` → `Result.Validation({"cv": ["The CV file must be 5 MB or smaller."]}, "application.submit.file-too-large", "Validation failed.")`.
5. Magic-byte check (Assumption A-8): read the first 5 bytes of `cvContent` (must be seekable — the API layer passes a buffered `MemoryStream`, §4); if they are not `%PDF-` (`0x25 0x50 0x44 0x46 0x2D`), reset `cvContent.Position = 0` and return the same `application.submit.invalid-file-type` Validation as step 3. On a match, reset `cvContent.Position = 0` before continuing.
6. Duplicate pre-check: `await _dbContext.Applications.AnyAsync(a => a.CandidateId == candidateId && a.RequisitionId == requisitionId, ct)` → if `true`, `Result.Conflict("application.submit.duplicate", "You have already applied to this requisition.")`.
7. Generate `var storageKey = $"{Guid.NewGuid():N}.pdf";`.
8. `try { await _fileStorage.SaveAsync(storageKey, cvContent, ct); } catch (IOException) or (UnauthorizedAccessException) { return Result.Error("application.submit.storage-failed", "Could not save the uploaded file. Please try again."); }` (E-4; maps to `500` via the existing `Error` → `500` mapping in `ToProblemResult()`).
9. `var application = Application.Create(requisitionId, candidateId);` `var cv = CvAttachment.Create(application.Id, storageKey, cvFileName, cvContentType, cvSizeBytes);` `application.AttachCv(cv);` `_dbContext.Applications.Add(application);`
10. `try { await _dbContext.SaveChangesAsync(ct); } catch (DbUpdateException) { await _fileStorage.DeleteAsync(storageKey, ct); return Result.Conflict("application.submit.duplicate", "You have already applied to this requisition."); }` — the structural fallback for the E-1 race the step-6 pre-check cannot fully close.
11. Return `Result.Ok(ToDto(application, cv))`.

**Behaviour — `ListMineAsync(candidateId)`**

1. Query `Applications` where `CandidateId == candidateId`, joined to `Requisitions` for `Title`, ordered by `SubmittedAtUtc` descending.
2. Map each to `CandidateApplicationListItemDto(Id, RequisitionId, RequisitionTitle, SubmittedAtUtc, CvDownloadUrl: $"/api/applications/{Id}/cv")`. Empty list, not an error, when the Candidate has none (AC-13).

**Behaviour — `ListForRequisitionAsync(requisitionId)`**

1. `await _dbContext.Requisitions.AsNoTracking().AnyAsync(r => r.Id == requisitionId, ct)` — if `false`, `Result.NotFound("application.list.requisition-not-found", "Requisition not found.")` (E-5). Any status is accepted — staff already have unrestricted visibility into Requisitions of any status via `0003`'s `StaffOnly` endpoints, so this list is not gated by `Published`.
2. Query `Applications` where `RequisitionId == requisitionId`, joined to `AspNetUsers` (`_dbContext.Users`, inherited from `IdentityDbContext`) for candidate identity, ordered by `SubmittedAtUtc` descending.
3. Map each to `StaffApplicationListItemDto(Id, Candidate: { Id, FirstName, LastName, Email }, SubmittedAtUtc, CvDownloadUrl: $"/api/applications/{Id}/cv")`. Empty list, not an error, when there are none (AC-18).

**Behaviour — `GetCvAsync(applicationId, requestingUserId, requesterIsStaff)`**

1. `await _dbContext.Applications.AsNoTracking().Include(a => a.CvAttachment).FirstOrDefaultAsync(a => a.Id == applicationId, ct)`. If `null` or `CvAttachment == null` → `Result.NotFound("application.cv.not-found", "Application not found.")` (E-6).
2. If `!requesterIsStaff && application.CandidateId != requestingUserId` → `Result.Forbidden("application.cv.forbidden", "You do not have access to this file.")` (AC-15, AC-21).
3. `var stream = await _fileStorage.OpenReadAsync(application.CvAttachment.StorageKey, ct);` → `Result.Ok(new CvDownloadResult(stream, application.CvAttachment.OriginalFileName, application.CvAttachment.ContentType))`.

**Returns — outcome→result→HTTP**

| Outcome | Result | Maps to HTTP |
|---|---|---|
| Submission succeeded | `Result.Ok(dto)` | 201 |
| List succeeded (candidate or staff) | `Result.Ok(list)` | 200 (`[]` when empty) |
| CV download succeeded | `Result.Ok(stream+metadata)` | 200 |
| Requisition not published/missing | `Result.NotFound("application.submit.requisition-not-found", ...)` | 404 |
| Requisition missing (staff list) | `Result.NotFound("application.list.requisition-not-found", ...)` | 404 |
| Application/CV missing | `Result.NotFound("application.cv.not-found", ...)` | 404 |
| No file / wrong type / oversized | `Result.Validation(errors, code, message)` | 400 |
| Duplicate submission | `Result.Conflict("application.submit.duplicate", ...)` | 409 |
| Storage write failed | `Result.Error("application.submit.storage-failed", ...)` | 500 |
| Candidate requests another Candidate's CV | `Result.Forbidden("application.cv.forbidden", ...)` | 403 |

## 4. API Layer

Endpoint shapes are specified in `api.md`. `ApplicationEndpoints.cs` follows the same
`MapGroup`/`ToProblemResult()` pattern as `RequisitionEndpoints.cs` (reused unchanged, same
`Ats.Api` namespace).

| Route | Handler | Auth policy | Maps service result via |
|---|---|---|---|
| `POST /api/requisitions/{requisitionId}/applications` | `ApplicationEndpoints` | `CandidateOnly` | `IFormFile? cv` bound via `[FromForm]`/minimal-API form inference; null/zero-length short-circuits to `Result.Validation(...).ToProblemResult()` before calling the service; otherwise buffered into a seekable `MemoryStream`, then `SubmitAsync(...)` → `Results.Created($"/api/applications/{dto.Id}", dto)` / `ToProblemResult()`. Endpoint calls `.DisableAntiforgery()` — this API is stateless JWT-bearer, not cookie/form-session based, so ASP.NET Core's default form-binding antiforgery requirement does not apply |
| `GET /api/requisitions/{requisitionId}/applications` | `ApplicationEndpoints` | `StaffOnly` | `ListForRequisitionAsync` → `Results.Ok` / `ToProblemResult()` |
| `GET /api/applications/mine` | `ApplicationEndpoints` | `CandidateOnly` | `ListMineAsync` → `Results.Ok` |
| `GET /api/applications/{id}/cv` | `ApplicationEndpoints` | `RequireAuthorization()` (any authenticated role — the service, not the policy, distinguishes Candidate-owns-it from Staff) | `GetCvAsync(id, userId, isStaff)` → `Results.File(stream, contentType, fileName)` / `ToProblemResult()` |

`userId` and `isStaff` are both resolved from `ClaimsPrincipal` at the API layer (mirrors
`AuthEndpoints.MapGet("/me", ...)`'s `ClaimTypes.NameIdentifier`/`"sub"` extraction) — a
`ClaimsPrincipal` never crosses into `service/application`.

`Program.cs` gains `app.MapApplicationEndpoints();` after the existing
`MapPublicRequisitionEndpoints()` call.

## 5. Frontend

### 5.1 Components

| Component | Path | Props | State | Notes |
|---|---|---|---|---|
| `ApplicationForm` | `src/components/portal/ApplicationForm.tsx` | `requisitionId`, `requisitionTitle` | `file`, `loading`, `error`, `submitted` (client) | `FormData` with a single `cv` field, `POST` to `/api/bff/proxy/requisitions/{requisitionId}/applications`. Never sets `Content-Type` manually — the browser sets the multipart boundary. Mirrors `RegisterForm.tsx`'s error/loading structure |
| `ApplicationList` | `src/components/portal/ApplicationList.tsx` | `items: CandidateApplicationListItemDto[]` | none (presentational Server Component) | Renders Requisition title, submitted date, and a CV download link per row; empty-state copy when `items.length === 0` |
| `ApplicationsTable` | `src/components/staff/ApplicationsTable.tsx` | `items: StaffApplicationListItemDto[]` | none (presentational Server Component) | Candidate name/email, submitted date, CV download link per row; empty-state copy when `items.length === 0` |

### 5.2 Data Access

No client-side cache library exists in this project (per `0003`'s HLD D-5, unchanged) — reads
happen in Server Components via `invokeBackend`; the one mutation (`ApplicationForm`'s submit)
happens in a Client Component via `fetch` to `/api/bff/proxy/*`, followed by an in-place success
state (no `router.refresh()` needed — the form does not re-read a list it is not showing).

| Call site | Endpoint (via proxy unless noted) | Method | Re-render trigger |
|---|---|---|---|
| `(portal)/jobs/[id]/apply/page.tsx` (Server Component) | `invokeBackend` → `/api/public/requisitions/{id}`; `auth()` for session | GET | Normal SSR on navigation; `notFound()` on a `BackendInvokeError` with `status === 404` |
| `ApplicationForm` | `/api/bff/proxy/requisitions/{requisitionId}/applications` | POST (multipart) | Local `submitted` state → success panel + link to `/applications` |
| `(portal)/applications/page.tsx` (Server Component) | `invokeBackend` → `/api/applications/mine` | GET | Normal SSR on navigation |
| `staff/requisitions/[id]/applications/page.tsx` (Server Component) | `invokeBackend` → `/api/requisitions/{id}/applications` | GET | Normal SSR on navigation |
| CV download (both surfaces) | Plain `<a href="/api/bff/proxy/applications/{id}/cv">` | GET (browser navigation, not `fetch`) | N/A — triggers a native file download; session cookie travels automatically on same-origin navigation |

### 5.3 UI States

| Surface | Loading | Empty | Error | Success |
|---|---|---|---|---|
| Apply page (`jobs/[id]/apply`) | `loading.tsx` skeleton | N/A | `notFound()` for a non-published Requisition; unauthenticated → redirect to `/login?callbackUrl=...`; authenticated non-Candidate → inline "Only candidates can apply." message, no form rendered | `ApplicationForm` renders |
| `ApplicationForm` submit | Submit button spinner, file input disabled | N/A | Inline `role="alert"` banner mapping `application.submit.*` codes to copy (duplicate → "You've already applied to this role."; invalid-file-type/file-too-large → the field message from the ProblemDetails `errors` map) | Success panel + "View my applications" link, form replaced |
| My Applications (`(portal)/applications`) | `loading.tsx` skeleton rows | "You haven't applied to any roles yet." + link to `/jobs`, inside `ApplicationList` | `error.tsx` with retry | Table of Requisition title / submitted date / CV link |
| Staff Applications (`staff/requisitions/[id]/applications`) | `loading.tsx` skeleton rows | "No applications yet for this requisition." inside `ApplicationsTable` | `error.tsx` with retry; `notFound()` for a missing Requisition id | Table of candidate identity / submitted date / CV link |

## 6. DTOs & Contracts

```ts
// src/lib/types/application.ts — mirrors api.md §4 exactly
export type ApplicationDto = {
  id: string;
  requisitionId: string;
  candidateId: string;
  submittedAtUtc: string;
  cv: { fileName: string; contentType: string; sizeBytes: number };
};

export type CandidateApplicationListItemDto = {
  id: string;
  requisitionId: string;
  requisitionTitle: string;
  submittedAtUtc: string;
  cvDownloadUrl: string;
};

export type StaffApplicationListItemDto = {
  id: string;
  candidate: { id: string; firstName: string; lastName: string; email: string };
  submittedAtUtc: string;
  cvDownloadUrl: string;
};
```

## 7. Validation Rules

| Field | Rule | Message | AC |
|---|---|---|---|
| `cv` (submit) | Required, non-empty | "A CV file is required." | AC-2 |
| `cv` (submit) | Declared content-type `application/pdf` AND filename ends `.pdf` AND first 5 bytes are `%PDF-` | "Only PDF files are accepted." | AC-3 |
| `cv` (submit) | ≤ `Applications:MaxCvSizeBytes` (default 5 MB) | "The CV file must be 5 MB or smaller." | AC-4 |
| `requisitionId` (submit, path) | Must resolve to a `published` Requisition | (404, no body-level message — see `api.md`) | AC-5, AC-6, AC-7 |
| `(candidateId, requisitionId)` (submit) | No existing `Application` row | "You have already applied to this requisition." | AC-8 |

## 8. Error Handling

| Condition | Code | Logged at | User-facing message |
|---|---|---|---|
| No CV file attached | `application.submit.cv-required` | Warning | "A CV file is required." |
| Wrong file type / failed magic-byte check | `application.submit.invalid-file-type` | Warning | "Only PDF files are accepted." |
| File exceeds 5 MB | `application.submit.file-too-large` | Warning | "The CV file must be 5 MB or smaller." |
| Requisition not published or missing (submit) | `application.submit.requisition-not-found` | Debug (expected traffic, same reasoning as `0003`'s public-detail 404) | "This job posting is no longer available." |
| Duplicate submission | `application.submit.duplicate` | Information | "You have already applied to this requisition." |
| Disk write failed | `application.submit.storage-failed` | Error | "Could not save the uploaded file. Please try again." |
| Requisition missing (staff list) | `application.list.requisition-not-found` | Information | "Requisition not found." |
| Application/CV missing (download) | `application.cv.not-found` | Information | "Application not found." |
| Candidate requests another Candidate's CV | `application.cv.forbidden` | Warning | "You do not have access to this file." |

## 9. Configuration

| Key | Type | Default | Required | Where consumed |
|---|---|---|---|---|
| `Storage:CvBasePath` | string | `./app-data/cv-attachments` | No | `LocalDiskFileStorage` |
| `Applications:MaxCvSizeBytes` | int | `5242880` (5 MB) | No | `ApplicationService.SubmitAsync` |

Both follow the existing `configuration["Key"] ?? default` / `int.TryParse(...)`-with-fallback
pattern already used by `RequisitionService`/`JwtTokenGenerator` — no new configuration-binding
mechanism introduced.

## 10. Database Migration

| Step | Change | Reversible |
|---|---|---|
| 1 | `CreateTable("Applications")` — `Id`, `RequisitionId` (FK, cascade), `CandidateId` (FK, cascade), `SubmittedAtUtc` | Yes |
| 2 | `CreateTable("CvAttachments")` — `Id`, `ApplicationId` (FK, cascade), `StorageKey`, `OriginalFileName`, `ContentType`, `SizeBytes`, `UploadedAtUtc` | Yes |
| 3 | `CreateIndex(IX_Applications_CandidateId_RequisitionId)` UNIQUE | Yes |
| 4 | `CreateIndex(IX_Applications_RequisitionId)` | Yes |
| 5 | `CreateIndex(IX_CvAttachments_ApplicationId)` UNIQUE | Yes |

No backfill — both tables start empty. Full detail and rollback plan in `erd.md` §5.

## 11. Test Plan

| Test | Type | Covers | Path |
|---|---|---|---|
| `Application_Create_SetsFieldsAndStartsWithNoCv` | Unit | AC-1, AC-22 | `tests/Ats.UnitTests/Application/ApplicationEntityTests.cs` |
| `Application_AttachCv_CalledTwice_Throws` | Unit | NFR-1 | same |
| `CvAttachment_Create_WithNonPositiveSize_Throws` | Unit | FR-3 | same |
| `SubmitAsync_ValidPdf_ReturnsCreatedWithPersistedCv` | Unit | AC-1, FR-7, FR-13 | `tests/Ats.UnitTests/Application/ApplicationServiceTests.cs` |
| `SubmitAsync_NoFile_ReturnsValidationNoRowWritten` | Unit | AC-2 | same |
| `SubmitAsync_NonPdfContentType_ReturnsValidation` | Unit | AC-3 | same |
| `SubmitAsync_PdfExtensionWrongMagicBytes_ReturnsValidation` | Unit | AC-3, A-8 | same |
| `SubmitAsync_OversizedFile_ReturnsValidation` | Unit | AC-4 | same |
| `SubmitAsync_DraftRequisition_ReturnsNotFound` | Unit | AC-5 | same |
| `SubmitAsync_ClosedRequisition_ReturnsNotFound` | Unit | AC-6 | same |
| `SubmitAsync_MissingRequisition_ReturnsNotFoundIdenticalToClosed` | Unit | AC-7 | same |
| `SubmitAsync_SecondSubmissionSameCandidateSameRequisition_ReturnsConflict` | Unit | AC-8 | same |
| `SubmitAsync_TwoDistinctCandidatesSameRequisition_BothSucceed` | Unit | AC-9 | same |
| `SubmitAsync_SameCandidateDifferentRequisitions_BothSucceed` | Unit | E-7 | same |
| `SubmitAsync_StorageThrows_ReturnsErrorNoRowWritten` | Unit | E-4 | same |
| `SubmitAsync_SaveChangesThrowsDbUpdateException_DeletesFileReturnsConflict` | Unit | E-1 | same |
| `ListMineAsync_TwoApplications_ReturnsBothWithRequisitionTitle` | Unit | AC-12 | same |
| `ListMineAsync_NoApplications_ReturnsEmptyList` | Unit | AC-13 | same |
| `ListForRequisitionAsync_TwoApplications_ReturnsBothWithCandidateIdentity` | Unit | AC-16 | same |
| `ListForRequisitionAsync_MissingRequisition_ReturnsNotFound` | Unit | E-5 | same |
| `ListForRequisitionAsync_NoApplications_ReturnsEmptyList` | Unit | AC-18 | same |
| `GetCvAsync_Owner_ReturnsStream` | Unit | AC-14 | same |
| `GetCvAsync_NonOwnerCandidate_ReturnsForbidden` | Unit | AC-15, AC-21 | same |
| `GetCvAsync_Staff_ReturnsStreamRegardlessOfOwnership` | Unit | AC-20 | same |
| `GetCvAsync_MissingApplication_ReturnsNotFound` | Unit | E-6 | same |
| `SaveAsync_ThenOpenReadAsync_RoundTripsBytes` | Unit | FR-7 | `tests/Ats.UnitTests/Storage/LocalDiskFileStorageTests.cs` |
| `ResolvePath_RejectsPathTraversalKeys` | Unit | NFR-2 | same |
| `POST_applications_AsCandidateValidPdf_Returns201` | Integration | AC-1 | `tests/Ats.IntegrationTests/Application/ApplicationEndpointsTests.cs` |
| `POST_applications_NoFile_Returns400` | Integration | AC-2 | same |
| `POST_applications_NonPdf_Returns400` | Integration | AC-3 | same |
| `POST_applications_OversizedPdf_Returns400` | Integration | AC-4 | same |
| `POST_applications_DraftOrClosedOrMissingRequisition_Returns404` | Integration | AC-5, AC-6, AC-7 | same |
| `POST_applications_SecondSubmission_Returns409` | Integration | AC-8 | same |
| `POST_applications_Anonymous_Returns401` | Integration | AC-10 | same |
| `POST_applications_AsRecruiterOrHiringManager_Returns403` | Integration | AC-11 | same |
| `GET_applications_mine_ReturnsOwnApplicationsOnly` | Integration | AC-12, AC-13 | same |
| `GET_applications_id_cv_AsOwner_Returns200WithPdfBytes` | Integration | AC-14 | same |
| `GET_applications_id_cv_AsNonOwnerCandidate_Returns403` | Integration | AC-15, AC-21 | same |
| `GET_requisitions_id_applications_AsRecruiter_Returns200` | Integration | AC-16 | same |
| `GET_requisitions_id_applications_AsHiringManager_Returns200` | Integration | AC-17 | same |
| `GET_requisitions_id_applications_NoApplications_Returns200EmptyList` | Integration | AC-18 | same |
| `GET_requisitions_id_applications_AsCandidate_Returns403` | Integration | AC-19 | same |
| `GET_applications_id_cv_AsRecruiter_Returns200` | Integration | AC-20 | same |
| `GET_applications_id_cv_MissingId_Returns404` | Integration | E-6 | same |
| `GET_requisitions_id_applications_MissingRequisition_Returns404` | Integration | E-5 | same |
| `ApplicationForm` shows validation error banner for a non-PDF file | Component | AC-3 | `tests/portal/application-form.test.tsx` |
| `ApplicationForm` shows duplicate-submission error banner on 409 | Component | AC-8 | same |
| `ApplicationForm` shows success panel after 201 | Component | AC-1 | same |
| `ApplicationList` renders empty state for zero applications | Component | AC-13 | `tests/portal/application-list.test.tsx` |
| `ApplicationList` renders Requisition title and submitted date per row | Component | AC-12 | same |
| `ApplicationsTable` renders candidate identity, date, and CV link per row | Component | AC-16 | `tests/staff/applications-table.test.tsx` |
| `ApplicationsTable` renders empty state for zero applications | Component | AC-18 | same |
| `isCandidateRole` returns true only for `Candidate` | Unit | FR-6 (UI convenience gate) | `tests/lib/auth-guards.test.ts` |

Every `AC-n` (AC-1 through AC-22) appears at least once above.

## 12. Implementation Notes

- Build order: `ApplicationConfiguration`/`CvAttachmentConfiguration` must exist and compile
  before `dotnet ef migrations add` is run (it reads the compiled model), same constraint noted
  in `0003`'s LLD.
- The magic-byte check in `SubmitAsync` step 5 requires a **seekable** stream. The API layer
  must buffer `IFormFile.OpenReadStream()` into a `MemoryStream` before calling the service —
  do not pass the raw `IFormFile` stream through, it is forward-only.
- `ui/bff`'s proxy generalisation (HLD D-4) is a prerequisite for *any* frontend task in this
  spec that touches file bytes (`ApplicationForm` submit, both CV download links) — sequence it
  first within CP-3.
- `Results.File(stream, contentType, fileName)` sets `Content-Disposition: attachment;
  filename="..."` automatically; do not hand-construct that header.
- The `.gitignore` update (File Manifest) must land before the first local run that creates
  `backend/app-data/` — same ordering concern `0001`'s FR-1 called out for the SQLite file.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0003` (Requisition Management) | 1 | `Result`/`ResultStatus`/`ToProblemResult()` pattern, `IConfiguration`-with-fallback config pattern, `MapGroup` endpoint style, `AppDbContextModelSnapshot` migration workflow, `CustomWebApplicationFactory` integration harness. |
| `0002` (User Authentication and Refresh Token Flow) | 1 | `CandidateOnly`/`StaffOnly`/`RecruiterOnly` policy names, `ClaimsPrincipal` claim extraction pattern (`AuthEndpoints.MapGet("/me", ...)`), NextAuth session shape (`session.user.roles`). |
| `0001` (Project Scaffolding and Walking Skeleton) | 1 | `ui/bff` proxy route and shared invoke function this spec modifies (D-4) rather than bypasses; Server-Component-for-reads/Client-Component-for-mutations split; `(portal)`/`staff` route-group precedent. |

## Deviation Log

Appended by `/implement` when reality diverged from this design.

| Date | Task | Section | Designed | Actual | Reason |
|---|---|---|---|---|---|
