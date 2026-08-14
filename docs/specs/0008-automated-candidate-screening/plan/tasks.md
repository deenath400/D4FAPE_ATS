# Tasks — 0008 Automated Candidate Screening

**Spec:** `../spec.md` · **LLD:** `lld.md` · **Updated:** 2026-08-14

Execution order for `/implement`. Tasks are grouped into **checkpoints**; `/implement` runs
one checkpoint per invocation, then stops for review.

**Progress:** 9 / 24 tasks · checkpoint CP-1 of 4 (completed)

---

## How to read this file

- `- [ ]` pending · `- [x]` done · `- [~]` in progress (partial, see changelog)
- Each task names the files it touches and the `AC-n` it serves.
- A checkpoint ends at a state where the project **builds and its tests pass**. Never define
  a checkpoint that leaves the tree broken.
- `/implement` ticks boxes only after the code exists and the checkpoint's tests run green.

---

## CP-1 — Data Layer and Domain

*Exit condition: migration applies cleanly, entity unit tests pass, `dotnet build` succeeds,
`dotnet test tests/Ats.UnitTests` and `dotnet test tests/Ats.IntegrationTests` pass.*

- [x] **T-01** — Add `ScreeningStatus` enum
  - Files: `backend/src/Db/Applications/ScreeningStatus.cs`
  - Covers: AC-1
  - Depends on: —

- [x] **T-02** — Add `ScreeningRecommendation` enum
  - Files: `backend/src/Db/Applications/ScreeningRecommendation.cs`
  - Covers: AC-1
  - Depends on: —

- [x] **T-03** — Add `ScreeningReport` entity
  - Files: `backend/src/Db/Applications/ScreeningReport.cs`
  - Covers: AC-1, AC-4
  - Depends on: T-01, T-02

- [x] **T-04** — Add `ScreeningReportConfiguration` (EF Core)
  - Files: `backend/src/Db/Configurations/ScreeningReportConfiguration.cs`
  - Covers: AC-1
  - Depends on: T-03

- [x] **T-05** — Add optional `ScreeningReport?` navigation to `Application` entity
  - Files: `backend/src/Db/Applications/Application.cs`
  - Covers: AC-1
  - Depends on: T-03

- [x] **T-06** — Register `DbSet<ScreeningReport>` and configuration in `AppDbContext`
  - Files: `backend/src/Db/AppDbContext.cs`
  - Covers: AC-1
  - Depends on: T-04, T-05

- [x] **T-07** — Create `AddScreeningReport` migration
  - Files: `backend/src/Db/Migrations/*_AddScreeningReport.cs`
  - Covers: AC-1
  - Depends on: T-06

- [x] **T-08** — Add `StageTransition.CreateSystemMove` factory method
  - Files: `backend/src/Db/Pipeline/StageTransition.cs`
  - Covers: AC-2
  - Depends on: —

- [x] **T-09** — Unit tests for `ScreeningReport` state machine and `StageTransition.CreateSystemMove`
  - Files: `backend/tests/Ats.UnitTests/Screening/ScreeningReportTests.cs`, `backend/tests/Ats.UnitTests/Pipeline/StageTransitionSystemMoveTests.cs`
  - Covers: AC-1, AC-2, AC-4
  - Depends on: T-03, T-08

## CP-2 — Service Layer (Screening + Pipeline Extension)

*Exit condition: `dotnet build` succeeds, all unit tests pass including new screening
orchestrator and mock service tests, all existing integration tests pass.*

- [ ] **T-10** — Add PdfPig NuGet reference to `Ats.Service.csproj`
  - Files: `backend/src/Service/Ats.Service.csproj`
  - Covers: AC-1
  - Depends on: —

- [ ] **T-11** — Create `PdfTextExtractor` static utility
  - Files: `backend/src/Service/Screening/PdfTextExtractor.cs`
  - Covers: AC-1, AC-4
  - Depends on: T-10

- [ ] **T-12** — Create `IScreeningService` interface and `ScreeningResult` record
  - Files: `backend/src/Service/Screening/IScreeningService.cs`, `backend/src/Service/Screening/ScreeningResult.cs`
  - Covers: AC-1, AC-10
  - Depends on: —

- [ ] **T-13** — Create `MockScreeningService` implementation
  - Files: `backend/src/Service/Screening/MockScreeningService.cs`
  - Covers: AC-10
  - Depends on: T-12

- [ ] **T-14** — Create `IScreeningOrchestrator` interface and `ScreeningOrchestrator` implementation
  - Files: `backend/src/Service/Screening/IScreeningOrchestrator.cs`, `backend/src/Service/Screening/ScreeningOrchestrator.cs`
  - Covers: AC-1, AC-2, AC-3, AC-4, AC-9
  - Depends on: T-03, T-08, T-11, T-13

- [ ] **T-15** — Add `SystemMoveToNextStageAsync` to `IPipelineService` and `PipelineService`
  - Files: `backend/src/Service/Pipeline/IPipelineService.cs`, `backend/src/Service/Pipeline/PipelineService.cs`
  - Covers: AC-2
  - Depends on: T-08

- [ ] **T-16** — Create `ScreeningReportDto`
  - Files: `backend/src/Service/Screening/Dtos/ScreeningReportDto.cs`
  - Covers: AC-7
  - Depends on: —

- [ ] **T-17** — Modify `ApplicationService.SubmitAsync` to fire background screening
  - Files: `backend/src/Service/Application/ApplicationService.cs`
  - Covers: AC-1
  - Depends on: T-14

- [ ] **T-18** — Register screening services in DI
  - Files: `backend/src/Service/ServiceCollectionExtensions.cs`
  - Covers: AC-1, AC-10
  - Depends on: T-13, T-14

- [ ] **T-19** — Unit tests: `MockScreeningService`, `PdfTextExtractor`, `ScreeningOrchestrator`
  - Files: `backend/tests/Ats.UnitTests/Screening/MockScreeningServiceTests.cs`, `backend/tests/Ats.UnitTests/Screening/PdfTextExtractorTests.cs`, `backend/tests/Ats.UnitTests/Screening/ScreeningOrchestratorTests.cs`
  - Covers: AC-1, AC-2, AC-3, AC-4, AC-9, AC-10
  - Depends on: T-14, T-13, T-11

## CP-3 — API Layer and Integration Tests

*Exit condition: `dotnet build` succeeds, all integration tests pass (including new screening
endpoint tests), `dotnet test` full suite green.*

- [ ] **T-20** — Add screening endpoints to `ApplicationEndpoints.cs`
  - Files: `backend/src/Api/ApplicationEndpoints.cs`
  - Covers: AC-5, AC-6, AC-7, AC-8
  - Depends on: T-14, T-16

- [ ] **T-21** — Add screening badge fields to pipeline board and staff application list DTOs
  - Files: `backend/src/Service/Pipeline/Dtos/PipelineBoardApplicationDto.cs` (or equivalent), `backend/src/Service/Application/Dtos/StaffApplicationListItemDto.cs`
  - Covers: AC-7
  - Depends on: T-06

- [ ] **T-22** — Add `Screening` configuration section to `appsettings.json`
  - Files: `backend/src/Api/appsettings.json`
  - Covers: AC-1, AC-10
  - Depends on: —

- [ ] **T-23** — Integration tests for screening endpoints
  - Files: `backend/tests/Ats.IntegrationTests/Screening/ScreeningEndpointTests.cs`
  - Covers: AC-5, AC-6, AC-7, AC-8
  - Depends on: T-20

## CP-4 — Frontend and Hardening

*Exit condition: full suite green (`dotnet test`, `npm test`, `npm run build`), lint clean,
architecture snapshot updated.*

- [ ] **T-24** — Update `docs/specs/meta/architecture.md`
  - Files: `docs/specs/meta/architecture.md`
  - Covers: —
  - Depends on: all

---

## Coverage Check

Every acceptance criterion must appear in at least one task.

| AC | Covered by |
|---|---|
| AC-1 | T-01, T-02, T-03, T-04, T-05, T-06, T-07, T-09, T-10, T-11, T-12, T-14, T-17, T-18, T-19, T-22 |
| AC-2 | T-08, T-09, T-14, T-15, T-19 |
| AC-3 | T-14, T-19 |
| AC-4 | T-03, T-09, T-11, T-14, T-19 |
| AC-5 | T-20, T-23 |
| AC-6 | T-20, T-23 |
| AC-7 | T-16, T-20, T-21, T-23 |
| AC-8 | T-20, T-23 |
| AC-9 | T-14, T-19 |
| AC-10 | T-12, T-13, T-18, T-19, T-22 |

Any AC with no task is a planning defect — fix it before `/implement` runs.

## Parallelisable

Tasks with no dependency edge between them, safe to do in any order within their checkpoint:

- CP-1: T-01 ‖ T-02 ‖ T-08 (all independent)
- CP-2: T-10 ‖ T-12 ‖ T-16 (all independent); T-11 ‖ T-13 (both depend on different T-1x)
- CP-3: T-21 ‖ T-22 (both independent of T-20)

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0004` (Application Submission and CV Upload) | 1 | `SubmitAsync` is modified (T-17); `Application`/`CvAttachment` entities are extended (T-05) or read (T-14). |
| `0005` (Pipeline Progression) | 1 | `PipelineService` is extended (T-15); `StageTransition` gains a factory (T-08); pipeline board DTO is extended (T-21). |
| `0003` (Requisition Management) | 1 | `Requisition.Title`/`Description` are read by the orchestrator (T-14). |

Tier 0 read in full: `meta/architecture.md`, `meta/tech-stack.md`, `meta/coding-standards.md`, `docs/specs/index.md`.
