# Low-Level Design — 0008 Automated Candidate Screening

**Spec:** `../spec.md` · **HLD:** `hld.md` · **Updated:** 2026-08-14

> **Standard:** could a competent developer who has not read the spec build this from the LLD
> alone?

---

## 1. File Manifest

| # | Path | Action | Purpose |
|---|---|---|---|
| 1 | `backend/src/Db/Applications/ScreeningReport.cs` | Create | Domain entity for screening evaluation results |
| 2 | `backend/src/Db/Applications/ScreeningRecommendation.cs` | Create | Enum: `Advance`, `Review` |
| 3 | `backend/src/Db/Applications/ScreeningStatus.cs` | Create | Enum: `Pending`, `Completed`, `Failed` |
| 4 | `backend/src/Db/Configurations/ScreeningReportConfiguration.cs` | Create | EF Core `IEntityTypeConfiguration<ScreeningReport>` |
| 5 | `backend/src/Db/AppDbContext.cs` | Modify | Add `DbSet<ScreeningReport>`, apply configuration |
| 6 | `backend/src/Db/Migrations/YYYYMMDD_AddScreeningReport.cs` | Create | Migration: `ScreeningReports` table |
| 7 | `backend/src/Db/Applications/Application.cs` | Modify | Add optional navigation `ScreeningReport?` |
| 8 | `backend/src/Db/Pipeline/StageTransition.cs` | Modify | Add `CreateSystemMove` factory method |
| 9 | `backend/src/Service/Screening/IScreeningService.cs` | Create | Pluggable AI evaluation abstraction |
| 10 | `backend/src/Service/Screening/ScreeningResult.cs` | Create | Return type from `IScreeningService` |
| 11 | `backend/src/Service/Screening/MockScreeningService.cs` | Create | Deterministic test/dev implementation |
| 12 | `backend/src/Service/Screening/PdfTextExtractor.cs` | Create | Extracts plain text from a PDF stream |
| 13 | `backend/src/Service/Screening/IScreeningOrchestrator.cs` | Create | Orchestrator interface |
| 14 | `backend/src/Service/Screening/ScreeningOrchestrator.cs` | Create | Coordinates extraction, evaluation, report, auto-advance |
| 15 | `backend/src/Service/Screening/Dtos/ScreeningReportDto.cs` | Create | Staff-facing DTO |
| 16 | `backend/src/Service/Pipeline/IPipelineService.cs` | Modify | Add `SystemMoveToNextStageAsync` |
| 17 | `backend/src/Service/Pipeline/PipelineService.cs` | Modify | Implement `SystemMoveToNextStageAsync` |
| 18 | `backend/src/Service/Pipeline/Dtos/PipelineBoardApplicationDto` (or inline) | Modify | Add optional screening badge fields |
| 19 | `backend/src/Service/Application/ApplicationService.cs` | Modify | Fire screening after successful submission |
| 20 | `backend/src/Service/ServiceCollectionExtensions.cs` | Modify | Register screening services |
| 21 | `backend/src/Api/ApplicationEndpoints.cs` | Modify | Add two staff screening endpoints |
| 22 | `backend/src/Api/appsettings.json` | Modify | Add `Screening` config section |
| 23 | `frontend/src/app/(staff)/...` | Modify | Screening badge on pipeline board, report view, re-screen button |
| 24 | `backend/src/Service/Ats.Service.csproj` | Modify | Add PdfPig NuGet reference |
| 25 | `backend/tests/Ats.UnitTests/...` | Create | Unit tests for screening orchestrator, mock service, PDF extraction |
| 26 | `backend/tests/Ats.IntegrationTests/...` | Create | Integration tests for screening endpoints |
| 27 | `docs/specs/meta/architecture.md` | Modify | Add `service/screening` to component map, update ER diagram |

## 2. Domain Types

### 2.1 `ScreeningReport` (new entity)

```csharp
namespace Ats.Db.Applications;

public class ScreeningReport
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public int Score { get; private set; }
    public ScreeningRecommendation Recommendation { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public string Strengths { get; private set; } = string.Empty;   // JSON array stored as string
    public string Concerns { get; private set; } = string.Empty;    // JSON array stored as string
    public ScreeningStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime EvaluatedAtUtc { get; private set; }

    private ScreeningReport() { } // EF Core

    public static ScreeningReport CreatePending(Guid applicationId);
    public void Complete(int score, ScreeningRecommendation recommendation,
                         string summary, string strengths, string concerns);
    public void Fail(string reason);
}
```

**Invariants:**
- `Score` is clamped to `[0, 100]` in `Complete()`.
- `Recommendation` is derived from the score and threshold but passed explicitly (the
  `IScreeningService` determines it, not the entity).
- `Complete()` and `Fail()` may only be called when `Status == Pending`; calling either on a
  non-`Pending` report throws `InvalidOperationException`.
- For re-screening (FR-8), the orchestrator deletes the existing `ScreeningReport` and creates
  a new one, rather than mutating a completed/failed report. This keeps the entity's state
  machine simple: `Pending -> Completed | Failed`, no backward transitions.

### 2.2 `ScreeningRecommendation` (new enum)

```csharp
public enum ScreeningRecommendation { Advance, Review }
```

### 2.3 `ScreeningStatus` (new enum)

```csharp
public enum ScreeningStatus { Pending, Completed, Failed }
```

### 2.4 `StageTransition.CreateSystemMove` (new factory, added to existing entity)

```csharp
public static StageTransition CreateSystemMove(
    Guid applicationId,
    Guid fromStageId, string fromStageName,
    Guid toStageId, string toStageName,
    string actorDisplayLabel, string? note)
```

- Sets `ActorKind = System`, `ActorUserId = null`.
- Validates `applicationId`, `fromStageId`, `toStageId`, and `actorDisplayLabel` are non-empty.
- `actorDisplayLabel` will be `"AI Screening Agent"` for all screening-initiated moves.

### 2.5 `Application` (existing entity, modified)

Add navigation property:

```csharp
public ScreeningReport? ScreeningReport { get; private set; }
```

No new business methods — the orchestrator writes the `ScreeningReport` directly and calls
the existing `MoveToStage(Guid)` method through `PipelineService` for auto-advance.

## 3. Service Methods

### 3.1 `IScreeningService.EvaluateAsync`

```csharp
namespace Ats.Service.Screening;

public interface IScreeningService
{
    Task<ScreeningResult> EvaluateAsync(
        string requisitionTitle,
        string requisitionDescription,
        string cvText,
        CancellationToken ct = default);
}
```

**`ScreeningResult`** (record):

```csharp
public record ScreeningResult(
    int Score,
    ScreeningRecommendation Recommendation,
    string Summary,
    string Strengths,   // JSON array as string
    string Concerns);   // JSON array as string
```

### 3.2 `MockScreeningService.EvaluateAsync`

Deterministic, no network calls. Scoring logic:

1. Tokenise `cvText` and `requisitionTitle + " " + requisitionDescription` into lowercase words.
2. Count the number of job-description words that appear in the CV text.
3. Score = `min(100, matchCount * 10)`.
4. Recommendation = `Score >= threshold ? Advance : Review` (threshold from config, default 75).
5. Summary = `"Mock screening: {matchCount} keyword matches found."`.
6. Strengths = JSON array of up to 3 matched keywords.
7. Concerns = JSON array: `["Mock provider — no real AI evaluation performed"]`.

This is a test double — production callers swap in a real LLM implementation.

### 3.3 `PdfTextExtractor.ExtractTextAsync`

```csharp
public static class PdfTextExtractor
{
    public static string ExtractText(Stream pdfStream);
}
```

- Uses `PdfPig` (`UglyToad.PdfPig` NuGet) to open the stream and iterate pages.
- Concatenates each page's `page.Text` with a newline separator.
- Returns the trimmed result. If the result is empty after trimming, returns `string.Empty`
  (caller interprets as E-1).
- Does not log the extracted text (NFR-3).

### 3.4 `IScreeningOrchestrator.RunScreeningAsync`

```csharp
public interface IScreeningOrchestrator
{
    Task RunScreeningAsync(Guid applicationId, CancellationToken ct = default);
}
```

**`ScreeningOrchestrator` behaviour steps:**

1. Open a new `IServiceScope` (the orchestrator is invoked from a fire-and-forget task, so it
   creates its own scope with a fresh `AppDbContext`).
2. Load the `Application` with `Include(a => a.CvAttachment).Include(a => a.ScreeningReport)`
   and `AsTracking()`.
3. Guard: if Application not found, log a warning and return.
4. Guard: if `Application.IsRejected`, log info and return (E-4).
5. If a `ScreeningReport` already exists (re-screen path), delete it:
   `dbContext.Remove(application.ScreeningReport)` + `SaveChangesAsync`.
6. Create a new `ScreeningReport.CreatePending(application.Id)` and add it to the context.
   `SaveChangesAsync` to persist the `Pending` state.
7. Open the CV file via `IFileStorage.OpenReadAsync(cvAttachment.StorageKey)`.
8. Extract text via `PdfTextExtractor.ExtractText(stream)`.
9. If extracted text is empty: `report.Fail("No extractable text found in CV attachment")`,
   `SaveChangesAsync`, return (E-1).
10. Load the Requisition (title, description only — `AsNoTracking`).
11. Guard: if Requisition not found or closed, `report.Fail(...)`, `SaveChangesAsync`, return (E-4).
12. Call `IScreeningService.EvaluateAsync(title, description, cvText, ct)`.
    - On failure (exception): retry once after a short delay.
    - If still failing: `report.Fail("AI service unavailable after retry: {message}")`,
      `SaveChangesAsync`, return (E-2).
13. `report.Complete(result.Score, result.Recommendation, result.Summary, result.Strengths, result.Concerns)`.
14. `SaveChangesAsync`.
15. **Auto-advance check:**
    - Load the Requisition's Stages ordered by `SortOrder`.
    - Find the Application's current Stage index.
    - If `result.Recommendation == Advance` AND the Application is in the initial Stage (index 0)
      AND there is a next Stage (index 1 exists): call
      `PipelineService.SystemMoveToNextStageAsync(applicationId, ct)`.
    - If only 1 Stage exists: log info, no move (E-3).
    - If Application is not in the initial Stage: log info, no move.

**Error handling:** The entire method body is wrapped in try/catch. Unhandled exceptions log
an error and do NOT crash the host.

### 3.5 `IPipelineService.SystemMoveToNextStageAsync` (new method on existing service)

```csharp
Task<Result> SystemMoveToNextStageAsync(Guid applicationId, CancellationToken ct = default);
```

**Behaviour steps:**

1. Load Application with tracking.
2. Guard: not found → `NotFound`. Rejected → `Conflict`. Requisition closed → `Conflict`.
3. Load the Requisition's Stages ordered by `SortOrder`.
4. Find the current Stage's index. If it is the last Stage → return `Ok()` with no move.
5. Determine `nextStage = stages[currentIndex + 1]`.
6. `application.MoveToStage(nextStage.Id)`.
7. Create `StageTransition.CreateSystemMove(applicationId, currentStage.Id, currentStage.Name,
   nextStage.Id, nextStage.Name, "AI Screening Agent",
   $"Automated screening: score {score}, recommendation Advance")`.
8. Add the transition, `SaveChangesAsync`.
9. Return `Ok()`.

The note includes the screening score so the transition history shows the AI's reasoning.

### 3.6 `ApplicationService.SubmitAsync` (existing, modified)

After the existing `SaveChangesAsync` that persists the Application + CvAttachment, and before
returning `Result<ApplicationDto>.Ok(...)`:

```csharp
// Fire-and-forget background screening (NFR-1) — the submission response is not blocked.
_ = Task.Run(async () =>
{
    using var scope = _serviceScopeFactory.CreateScope();
    var orchestrator = scope.ServiceProvider.GetRequiredService<IScreeningOrchestrator>();
    await orchestrator.RunScreeningAsync(application.Id, CancellationToken.None);
}, CancellationToken.None);
```

`ApplicationService` gains an `IServiceScopeFactory` constructor parameter.

## 4. Frontend Changes

### 4.1 Pipeline Board — Screening Badge

Each `PipelineBoardApplicationDto` gains optional fields:

```typescript
type PipelineBoardApplicationDto = {
  // ... existing fields
  screeningScore: number | null;
  screeningRecommendation: "Advance" | "Review" | null;
  screeningStatus: "Pending" | "Completed" | "Failed" | null;
};
```

The pipeline board card shows a small badge:
- `Completed` + `Advance`: green badge with score (e.g. "85 ✓")
- `Completed` + `Review`: amber badge with score (e.g. "62 ⚠")
- `Pending`: grey spinner badge
- `Failed`: red "!" badge
- `null` (no report): no badge

### 4.2 Staff Applications List — Screening Badge

Same badge pattern on the staff per-Requisition applications list.

### 4.3 Application Detail — Full Report View

A `ScreeningReportPanel` component on the Application detail page:
- Fetches `GET /api/staff/applications/{id}/screening-report`.
- Displays: score (large number), recommendation badge, summary text, strengths list,
  concerns list, evaluated timestamp.
- Shows a "Re-run Screening" button for Recruiters (conditionally rendered based on role).
- Four async states: loading, error, empty (no report yet), success.

### 4.4 Re-run Screening

The button sends `POST /api/staff/applications/{id}/screen`.
On success, the panel refreshes to show the updated report.

## 5. DTOs

### 5.1 `ScreeningReportDto` (backend → frontend)

```csharp
public record ScreeningReportDto(
    Guid Id,
    Guid ApplicationId,
    int Score,
    string Recommendation,     // "Advance" | "Review"
    string Summary,
    string[] Strengths,
    string[] Concerns,
    string Status,             // "Pending" | "Completed" | "Failed"
    string? FailureReason,
    DateTime EvaluatedAtUtc);
```

```typescript
type ScreeningReportDto = {
  id: string;
  applicationId: string;
  score: number;
  recommendation: "Advance" | "Review";
  summary: string;
  strengths: string[];
  concerns: string[];
  status: "Pending" | "Completed" | "Failed";
  failureReason: string | null;
  evaluatedAtUtc: string;
};
```

## 6. Validation Rules

| # | Rule | Location | AC |
|---|---|---|---|
| V-1 | `applicationId` is a valid Guid and references an existing Application | `ScreeningOrchestrator`, `ApplicationEndpoints` | AC-5, AC-7 |
| V-2 | Caller must be `Recruiter` for re-screen | `ApplicationEndpoints` (`RecruiterOnly` policy) | AC-6 |
| V-3 | Caller must be Staff for report view | `ApplicationEndpoints` (`StaffOnly` policy) | AC-7, AC-8 |
| V-4 | Score is clamped to `[0, 100]` in `ScreeningReport.Complete()` | `ScreeningReport` entity | AC-1 |
| V-5 | Re-screen on a rejected Application returns 409 | `ScreeningOrchestrator` | AC-5 |

## 7. Error Handling

| Outcome | Result | HTTP | AC |
|---|---|---|---|
| Screening report found | `Ok(ScreeningReportDto)` | 200 | AC-7 |
| Application not found | `NotFound("screening.report.not-found", ...)` | 404 | AC-7 |
| Caller is Candidate | Policy rejects | 403 | AC-8 |
| Re-screen triggered | `Ok(ScreeningReportDto)` | 200 | AC-5 |
| Re-screen: Application not found | `NotFound("screening.run.not-found", ...)` | 404 | AC-5 |
| Re-screen: Caller is HiringManager or Candidate | Policy rejects | 403 | AC-6 |
| Re-screen: Application is rejected | `Conflict("screening.run.already-rejected", ...)` | 409 | AC-5 |

## 8. Configuration Keys

| Key | Type | Default | Purpose |
|---|---|---|---|
| `Screening:QualificationThreshold` | int | `75` | Score at or above which the recommendation is `Advance` |
| `Screening:Provider` | string | `"Mock"` | Active `IScreeningService` implementation name |

## 9. Migration Steps

Single migration: `AddScreeningReport`.

| # | Operation | Reversible | Backfill | Downtime |
|---|---|---|---|---|
| 1 | `CreateTable("ScreeningReports")` with all columns from `erd.md` §3.1 | Yes — `DropTable` | None | None — new, empty table |
| 2 | `CreateIndex("IX_ScreeningReports_ApplicationId", unique: true)` | Yes — `DropIndex` | None | None |

**Rollback plan.** `dotnet ef database update AddSeedSampleAccounts --project src/Db` drops
the new table. No data loss to other tables since no existing table references `ScreeningReports`.

## 10. Test Plan

| AC | Test | Layer | Method |
|---|---|---|---|
| AC-1 | Screening orchestrator produces a Completed report with valid fields when mock service returns a result | Unit | `RunScreeningAsync_WithReadableCv_ProducesCompletedReport` |
| AC-2 | Auto-advance moves Application from initial Stage to second Stage with System transition | Unit | `RunScreeningAsync_AdvanceScore_MovesToNextStage` |
| AC-3 | Review recommendation leaves Application in initial Stage, no transition | Unit | `RunScreeningAsync_ReviewScore_StaysInInitialStage` |
| AC-4 | AI service failure sets report to Failed, Application unchanged | Unit | `RunScreeningAsync_AiFailure_SetsReportFailed` |
| AC-4 | Unreadable PDF sets report to Failed | Unit | `RunScreeningAsync_EmptyCvText_SetsReportFailed` |
| AC-5 | `POST .../screen` re-runs screening and returns updated report | Integration | `ReScreen_AsRecruiter_Returns200WithUpdatedReport` |
| AC-6 | `POST .../screen` as HiringManager returns 403 | Integration | `ReScreen_AsHiringManager_Returns403` |
| AC-6 | `POST .../screen` as Candidate returns 403 | Integration | `ReScreen_AsCandidate_Returns403` |
| AC-7 | `GET .../screening-report` returns full report for Staff | Integration | `GetReport_AsStaff_Returns200WithReport` |
| AC-8 | `GET .../screening-report` as Candidate returns 403 | Integration | `GetReport_AsCandidate_Returns403` |
| AC-8 | `GET /api/applications/mine` does not include screening data | Integration | `ListMine_NoScreeningFieldsExposed` |
| AC-9 | Re-screen on already-advanced Application updates report but does not regress Stage | Unit | `RunScreeningAsync_AlreadyAdvanced_UpdatesReportNoStageChange` |
| AC-10 | Mock service produces deterministic results without network calls | Unit | `MockScreeningService_ReturnsDeterministicResults` |
| NFR-1 | Submission returns 201 before screening completes | Integration | `Submit_Returns201BeforeScreeningCompletes` |

## 11. Deviation Log

*Empty — Stage 3 appends here when the implementation diverges from this LLD.*

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0004` (Application Submission and CV Upload) | 1 | `SubmitAsync` is the hook point; `CvAttachment.StorageKey` is the input to PDF extraction; `IFileStorage` is reused unchanged. |
| `0005` (Pipeline Progression) | 1 | `PipelineService`, `StageTransition.CreateMove`, `ActorKind.System`, pipeline board DTO, and optimistic concurrency are all directly extended or reused. |
| `0003` (Requisition Management) | 1 | `Requisition.Title`/`Description` are the evaluation criteria fed to `IScreeningService`. |

Tier 0 read in full: `meta/architecture.md`, `meta/tech-stack.md`, `meta/coding-standards.md`, `docs/specs/index.md`.
