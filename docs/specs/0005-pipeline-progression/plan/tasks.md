# Tasks — 0005 Pipeline Progression

**Spec:** `../spec.md` · **LLD:** `lld.md` · **Updated:** 2026-08-06

**Progress:** 0 / 58 tasks · checkpoint CP-1 of 4

---

## How to read this file

- `- [ ]` pending · `- [x]` done · `- [~]` in progress (partial, see changelog)
- Each task names the files it touches and the `AC-n` it serves.
- A checkpoint ends at a state where the project **builds and its tests pass**.
- `/implement` ticks boxes only after the code exists and the checkpoint's tests run green.

---

## CP-1 — Data layer

*Exit condition: `AddPipelineProgression` migration applies cleanly (including backfill),
entity/migration unit and integration tests pass, `dotnet build` succeeds.*

- [ ] **T-01** — Modify `Stage` entity: `SortOrder`, `NormalizedName`, `DefaultStageNames`, `Rename`, `ChangeSortOrder`, `Create` gains `sortOrder`
  - Files: `src/Db/Requisitions/Stage.cs`
  - Covers: AC-1, AC-3, AC-4, AC-5, AC-6, AC-31
  - Depends on: —

- [ ] **T-02** — Modify `StageConfiguration`: map `SortOrder`/`NormalizedName`, add unique `(RequisitionId, NormalizedName)` index
  - Files: `src/Db/Configurations/StageConfiguration.cs`
  - Covers: AC-31
  - Depends on: T-01

- [ ] **T-03** — Modify `Application` entity: `CurrentStageId`, `IsRejected`, `Create` requires `currentStageId`, `MoveToStage`, `Reject`
  - Files: `src/Db/Applications/Application.cs`
  - Covers: AC-10, AC-11, AC-14, AC-15
  - Depends on: —

- [ ] **T-04** — Modify `ApplicationConfiguration`: `CurrentStageId`/`IsRejected` as concurrency tokens, FK `RESTRICT`, `(RequisitionId, CurrentStageId)` index
  - Files: `src/Db/Configurations/ApplicationConfiguration.cs`
  - Covers: AC-29
  - Depends on: T-03, T-01

- [ ] **T-05** — Create `StageTransitionKind`, `StageTransitionActorKind` enums
  - Files: `src/Db/Pipeline/StageTransitionKind.cs`, `src/Db/Pipeline/StageTransitionActorKind.cs`
  - Covers: AC-16 (FR-13)
  - Depends on: —

- [ ] **T-06** — Create `StageTransition` entity (`CreateMove`/`CreateRejection` factories)
  - Files: `src/Db/Pipeline/StageTransition.cs`
  - Covers: AC-11, AC-14, AC-16, AC-17, AC-30
  - Depends on: T-05

- [ ] **T-07** — Create `StageTransitionConfiguration`
  - Files: `src/Db/Configurations/StageTransitionConfiguration.cs`
  - Covers: AC-17
  - Depends on: T-06

- [ ] **T-08** — Modify `AppDbContext`: `DbSet<StageTransition>`, apply configuration
  - Files: `src/Db/AppDbContext.cs`
  - Covers: —
  - Depends on: T-07

- [ ] **T-09** — Generate and hand-adjust `AddPipelineProgression` migration (schema ops + raw-SQL backfill, per `erd.md` §5 / `lld.md` §10)
  - Files: `src/Db/Migrations/<timestamp>_AddPipelineProgression.cs`, `.Designer.cs`, `AppDbContextModelSnapshot.cs`
  - Covers: AC-32
  - Depends on: T-01, T-02, T-03, T-04, T-08

- [ ] **T-10** — Unit tests: `Stage` entity invariants
  - Files: `tests/Ats.UnitTests/Pipeline/StageEntityTests.cs`
  - Covers: AC-1, AC-3
  - Depends on: T-01

- [ ] **T-11** — Unit tests: `StageTransition` factory invariants
  - Files: `tests/Ats.UnitTests/Pipeline/ApplicationTransitionEntityTests.cs`
  - Covers: AC-11, AC-14, AC-16
  - Depends on: T-06

- [ ] **T-12** — Integration tests: migration backfill correctness and default-name consistency
  - Files: `tests/Ats.IntegrationTests/Pipeline/PipelineMigrationBackfillTests.cs`
  - Covers: AC-32
  - Depends on: T-09

## CP-2 — Service & API

*Exit condition: all `api.md` endpoints return their documented status codes and bodies;
`RequisitionService`/`ApplicationService` modifications are covered; integration tests pass.*

- [ ] **T-13** — Modify `Result`/`Result<T>`: add `Extensions` dictionary and `Conflict(code, message, extensions)` overloads
  - Files: `src/Service/Common/Result.cs`
  - Covers: AC-29
  - Depends on: T-01..T-09 (CP-1 exit)

- [ ] **T-14** — Modify `ToProblemResult()`: merge `Result.Extensions` into `ProblemDetails.Extensions`
  - Files: `src/Api/AuthEndpoints.cs`
  - Covers: AC-29
  - Depends on: T-13

- [ ] **T-15** — Create Stage-config DTOs
  - Files: `src/Service/Pipeline/Dtos/StageDto.cs`, `AddStageRequestDto.cs`, `RenameStageRequestDto.cs`, `ReorderStagesRequestDto.cs`
  - Covers: AC-1, AC-3, AC-4, AC-9
  - Depends on: —

- [ ] **T-16** — Create transition DTOs
  - Files: `src/Service/Pipeline/Dtos/MoveApplicationRequestDto.cs`, `RejectApplicationRequestDto.cs`, `ApplicationTransitionDto.cs`, `StageTransitionDto.cs`
  - Covers: AC-11, AC-14, AC-16, AC-20, AC-29, AC-30
  - Depends on: —

- [ ] **T-17** — Create pipeline-board DTOs
  - Files: `src/Service/Pipeline/Dtos/PipelineBoardDto.cs`, `PipelineStageGroupDto.cs`, `PipelineRejectedGroupDto.cs`, `PipelineBoardApplicationDto.cs`
  - Covers: AC-18, AC-19
  - Depends on: —

- [ ] **T-18** — Create `IPipelineService`
  - Files: `src/Service/Pipeline/IPipelineService.cs`
  - Covers: —
  - Depends on: T-15, T-16, T-17

- [ ] **T-19** — Implement `PipelineService` — Stage configuration methods (`AddStageAsync`, `GetStagesAsync`, `RenameStageAsync`, `ReorderStagesAsync`, `RemoveStageAsync`)
  - Files: `src/Service/Pipeline/PipelineService.cs`
  - Covers: AC-1, AC-3, AC-4, AC-5, AC-6, AC-9, AC-28, AC-31
  - Depends on: T-18

- [ ] **T-20** — Implement `PipelineService` — transition methods (`MoveApplicationAsync`, `RejectApplicationAsync`)
  - Files: `src/Service/Pipeline/PipelineService.cs`
  - Covers: AC-11, AC-12, AC-13, AC-14, AC-15, AC-16, AC-28, AC-29, AC-30, NFR-2
  - Depends on: T-19, T-14

- [ ] **T-21** — Implement `PipelineService` — board and history methods (`GetPipelineBoardAsync`, `GetTransitionHistoryAsync`)
  - Files: `src/Service/Pipeline/PipelineService.cs`
  - Covers: AC-17, AC-18, AC-19, AC-20, AC-21, AC-27, NFR-1
  - Depends on: T-20

- [ ] **T-22** — Modify `ServiceCollectionExtensions`: register `IPipelineService`
  - Files: `src/Service/ServiceCollectionExtensions.cs`
  - Covers: —
  - Depends on: T-21

- [ ] **T-23** — Modify `RequisitionService.CreateAsync`: seed default 4-Stage set
  - Files: `src/Service/Requisition/RequisitionService.cs`
  - Covers: AC-7, AC-8, AC-33
  - Depends on: T-01

- [ ] **T-24** — Modify `ApplicationService.SubmitAsync`: resolve and assign first Stage; no-stages-configured guard
  - Files: `src/Service/Application/ApplicationService.cs`
  - Covers: AC-10
  - Depends on: T-03, T-23

- [ ] **T-25** — Modify `ApplicationService.ListMineAsync` and `CandidateApplicationListItemDto`: add `currentStageName`/`isRejected`
  - Files: `src/Service/Application/ApplicationService.cs`, `src/Service/Application/Dtos/CandidateApplicationListItemDto.cs`
  - Covers: AC-22, AC-23
  - Depends on: T-24

- [ ] **T-26** — Create `PipelineEndpoints` — Stage-config routes (add, list, rename, reorder, remove)
  - Files: `src/Api/PipelineEndpoints.cs`
  - Covers: AC-1, AC-2, AC-5, AC-6, AC-9, AC-26, AC-28, AC-31
  - Depends on: T-19

- [ ] **T-27** — Extend `PipelineEndpoints` — move, reject, board, history routes
  - Files: `src/Api/PipelineEndpoints.cs`
  - Covers: AC-11, AC-14, AC-24, AC-25, AC-26, AC-27, AC-29, AC-30
  - Depends on: T-26, T-20, T-21

- [ ] **T-28** — Modify `Program.cs`: `app.MapPipelineEndpoints();`
  - Files: `src/Api/Program.cs`
  - Covers: —
  - Depends on: T-27

- [ ] **T-29** — Unit tests: `PipelineService` Stage-configuration logic
  - Files: `tests/Ats.UnitTests/Pipeline/PipelineServiceTests.cs`
  - Covers: AC-1, AC-3, AC-4, AC-5, AC-6, AC-28, AC-31
  - Depends on: T-19

- [ ] **T-30** — Unit tests: `PipelineService` move/reject logic
  - Files: `tests/Ats.UnitTests/Pipeline/PipelineServiceTests.cs`
  - Covers: AC-11, AC-12, AC-13, AC-14, AC-15, AC-16, AC-28, AC-29, AC-30
  - Depends on: T-20

- [ ] **T-31** — Unit tests: `PipelineService` board/history logic
  - Files: `tests/Ats.UnitTests/Pipeline/PipelineServiceTests.cs`
  - Covers: AC-17, AC-18, AC-19, AC-20, AC-21, AC-30
  - Depends on: T-21

- [ ] **T-32** — Unit tests: `RequisitionService.CreateAsync` default-Stage seeding
  - Files: `tests/Ats.UnitTests/Requisition/RequisitionServiceTests.cs`
  - Covers: AC-7, AC-8, AC-33
  - Depends on: T-23

- [ ] **T-33** — Unit tests: `ApplicationService.SubmitAsync`/`ListMineAsync` modifications
  - Files: `tests/Ats.UnitTests/Application/ApplicationServiceTests.cs`
  - Covers: AC-10, AC-22, AC-23
  - Depends on: T-24, T-25

- [ ] **T-34** — Integration tests: Stage-config endpoints, all documented status codes
  - Files: `tests/Ats.IntegrationTests/Pipeline/StageEndpointsTests.cs`
  - Covers: AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-9, AC-26, AC-28, AC-31
  - Depends on: T-28

- [ ] **T-35** — Integration tests: move/reject/board/history endpoints, all documented status codes
  - Files: `tests/Ats.IntegrationTests/Pipeline/TransitionEndpointsTests.cs`
  - Covers: AC-11, AC-12, AC-13, AC-14, AC-15, AC-16, AC-17, AC-18, AC-19, AC-20, AC-21, AC-24, AC-25, AC-26, AC-27, AC-28, AC-29, AC-30
  - Depends on: T-28

- [ ] **T-36** — Integration tests: `GET /api/applications/mine` status fields, note exclusion
  - Files: `tests/Ats.IntegrationTests/Application/ApplicationEndpointsTests.cs`
  - Covers: AC-22, AC-23, AC-30
  - Depends on: T-25

## CP-3 — Frontend

*Exit condition: Stage configuration, pipeline board, move/reject, transition history, and the
updated Candidate status view all render; component tests pass.*

- [ ] **T-37** — Create shared pipeline TS types
  - Files: `src/lib/types/pipeline.ts`
  - Covers: —
  - Depends on: CP-2 exit

- [ ] **T-38** — Modify `application.ts` types: `currentStageName`, `isRejected`
  - Files: `src/lib/types/application.ts`
  - Covers: AC-22, AC-23
  - Depends on: —

- [ ] **T-39** — Create `StageConfigPanel`
  - Files: `src/components/staff/StageConfigPanel.tsx`
  - Covers: AC-1, AC-3, AC-4, AC-5, AC-6, AC-31
  - Depends on: T-37

- [ ] **T-40** — Create Stage-configuration page
  - Files: `src/app/staff/requisitions/[id]/stages/page.tsx`, `loading.tsx`, `error.tsx`
  - Covers: AC-9
  - Depends on: T-39

- [ ] **T-41** — Modify Requisition detail page: add "Configure Stages"/"View Pipeline" links
  - Files: `src/app/staff/requisitions/[id]/page.tsx`
  - Covers: —
  - Depends on: T-40

- [ ] **T-42** — Create `PipelineBoard`
  - Files: `src/components/staff/PipelineBoard.tsx`
  - Covers: AC-11, AC-18, AC-19
  - Depends on: T-37

- [ ] **T-43** — Create `MoveApplicationControl`
  - Files: `src/components/staff/MoveApplicationControl.tsx`
  - Covers: AC-11, AC-29
  - Depends on: T-37

- [ ] **T-44** — Create `RejectApplicationControl`
  - Files: `src/components/staff/RejectApplicationControl.tsx`
  - Covers: AC-14
  - Depends on: T-37

- [ ] **T-45** — Create pipeline-board page
  - Files: `src/app/staff/requisitions/[id]/pipeline/page.tsx`, `loading.tsx`, `error.tsx`
  - Covers: AC-18, AC-19
  - Depends on: T-42, T-43, T-44

- [ ] **T-46** — Create `TransitionHistoryList`
  - Files: `src/components/staff/TransitionHistoryList.tsx`
  - Covers: AC-20, AC-21, AC-30
  - Depends on: T-37

- [ ] **T-47** — Create Application detail (transition history) page
  - Files: `src/app/staff/applications/[id]/page.tsx`, `loading.tsx`, `error.tsx`
  - Covers: AC-20, AC-21
  - Depends on: T-46

- [ ] **T-48** — Modify `ApplicationList`: render Stage name or Rejected badge
  - Files: `src/components/portal/ApplicationList.tsx`
  - Covers: AC-22, AC-23
  - Depends on: T-38

- [ ] **T-49** — Component tests: `StageConfigPanel`
  - Files: `tests/staff/stage-config-panel.test.tsx`
  - Covers: AC-1, AC-6, AC-31
  - Depends on: T-39

- [ ] **T-50** — Component tests: `PipelineBoard`
  - Files: `tests/staff/pipeline-board.test.tsx`
  - Covers: AC-18, AC-19
  - Depends on: T-42

- [ ] **T-51** — Component tests: `MoveApplicationControl`
  - Files: `tests/staff/move-application-control.test.tsx`
  - Covers: AC-29
  - Depends on: T-43

- [ ] **T-52** — Component tests: `TransitionHistoryList`
  - Files: `tests/staff/transition-history-list.test.tsx`
  - Covers: AC-21, AC-30
  - Depends on: T-46

- [ ] **T-53** — Modify component tests: `ApplicationList`
  - Files: `tests/portal/application-list.test.tsx`
  - Covers: AC-22, AC-23
  - Depends on: T-48

## CP-4 — Hardening

*Exit condition: full backend + frontend suites green, lint clean, NFRs demonstrated,
`meta/architecture.md` and `meta/coding-standards.md` reflect this spec's shipped shape.*

- [ ] **T-54** — NFR-1 verification: pipeline board issues exactly two queries and groups in memory (no per-stage round trip) at up to 500 Applications
  - Files: `tests/Ats.IntegrationTests/Pipeline/TransitionEndpointsTests.cs`
  - Covers: NFR-1
  - Depends on: T-35

- [ ] **T-55** — NFR-2 verification: move/reject's open transaction spans only the Application/StageTransition writes
  - Files: `tests/Ats.IntegrationTests/Pipeline/TransitionEndpointsTests.cs`
  - Covers: NFR-2
  - Depends on: T-35

- [ ] **T-56** — E-2 regression: two concurrent moves against the same Application, exactly one succeeds
  - Files: `tests/Ats.IntegrationTests/Pipeline/TransitionEndpointsTests.cs`
  - Covers: AC-29, E-2
  - Depends on: T-35

- [ ] **T-57** — Update `docs/specs/meta/architecture.md`: Component Map (`api/pipeline`, `service/pipeline`, `db/pipeline`, modified rows), Data Model erDiagram (`Stage`/`Application`/`StageTransition` deltas), Change Log entries
  - Files: `docs/specs/meta/architecture.md`
  - Covers: —
  - Depends on: T-54, T-55, T-56

- [ ] **T-58** — Update `docs/specs/meta/coding-standards.md`: note the `Result.Extensions` convention (est. 0005) under Project-Specific Rules
  - Files: `docs/specs/meta/coding-standards.md`
  - Covers: —
  - Depends on: T-13

---

## Coverage Check

| AC | Covered by |
|---|---|
| AC-1 | T-01, T-19, T-26, T-29, T-34, T-39, T-49 |
| AC-2 | T-26, T-34 |
| AC-3 | T-01, T-10, T-19, T-29, T-34 |
| AC-4 | T-01, T-19, T-26, T-29, T-34, T-42, T-50 |
| AC-5 | T-01, T-19, T-26, T-29, T-34 |
| AC-6 | T-01, T-19, T-26, T-29, T-34, T-39, T-49 |
| AC-7 | T-23, T-32 |
| AC-8 | T-23, T-32 |
| AC-9 | T-19, T-26, T-34, T-40 |
| AC-10 | T-03, T-24, T-33 |
| AC-11 | T-03, T-06, T-11, T-16, T-20, T-27, T-30, T-35, T-42, T-43, T-50, T-51 |
| AC-12 | T-20, T-30, T-35 |
| AC-13 | T-20, T-30, T-35 |
| AC-14 | T-03, T-06, T-11, T-16, T-20, T-27, T-30, T-35, T-44 |
| AC-15 | T-03, T-04, T-20, T-30, T-35 |
| AC-16 | T-05, T-06, T-11, T-20, T-30, T-35 |
| AC-17 | T-06, T-07, T-21, T-31, T-35 |
| AC-18 | T-17, T-21, T-31, T-35, T-42, T-45, T-50 |
| AC-19 | T-17, T-21, T-31, T-35, T-42, T-45, T-50 |
| AC-20 | T-16, T-21, T-31, T-35, T-46, T-47, T-52 |
| AC-21 | T-16, T-21, T-31, T-35, T-46, T-47, T-52 |
| AC-22 | T-25, T-33, T-36, T-38, T-48, T-53 |
| AC-23 | T-25, T-33, T-36, T-38, T-48, T-53 |
| AC-24 | T-27, T-35 |
| AC-25 | T-27, T-35 |
| AC-26 | T-26, T-27, T-34, T-35 |
| AC-27 | T-21, T-27, T-35 |
| AC-28 | T-19, T-20, T-26, T-29, T-30, T-34, T-35 |
| AC-29 | T-04, T-13, T-14, T-16, T-20, T-27, T-30, T-35, T-43, T-51, T-56 |
| AC-30 | T-06, T-16, T-20, T-27, T-31, T-35, T-36, T-46, T-52 |
| AC-31 | T-01, T-02, T-19, T-26, T-29, T-34, T-39, T-49 |
| AC-32 | T-09, T-12 |
| AC-33 | T-23, T-32 |
| NFR-1 | T-21, T-54 |
| NFR-2 | T-20, T-55 |

Any AC with no task is a planning defect — fix it before `/implement` runs. All 33 ACs and both
NFRs are covered above.

## Parallelisable

- Within CP-1: T-01/T-02 (Stage) ‖ T-03/T-04 (Application) ‖ T-05/T-06/T-07 (StageTransition) —
  no dependency edge between the three entity families until T-08/T-09 merge them.
- Within CP-2: T-15/T-16/T-17 (DTO files) are independent of each other; T-23 (Requisition
  seeding) ‖ T-19/T-20/T-21 (PipelineService) — different services, only converge at T-24.
- Within CP-3: T-39 (StageConfigPanel) ‖ T-42/T-43/T-44 (PipelineBoard family) ‖ T-46 (History) —
  three independent component trees until T-41/T-45/T-47 wire them into pages.
- Within CP-4: T-54 ‖ T-55 ‖ T-56 — independent test files/scenarios.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0003` (Requisition Management) | 1 | `RequisitionService.CreateAsync` (T-23), `Stage`/`Requisition` entities (T-01–T-02), `RequisitionEndpoints.cs` pattern reused by `PipelineEndpoints.cs` (T-26–T-27). |
| `0004` (Application Submission and CV Upload) | 1 | `ApplicationService.SubmitAsync`/`ListMineAsync` (T-24–T-25), `Application`/`CvAttachment` entities (T-03–T-04), `ApplicationEndpoints.cs` pattern and its integration test harness reused (T-34–T-36). |
| `0002` (User Authentication and Refresh Token Flow) | 1 | `RecruiterOnly`/`StaffOnly`/`CandidateOnly` policies consumed unchanged (T-26–T-27, T-34–T-35); `AspNetUsers`/`ClaimsPrincipal` extraction pattern reused for `actingUserId` (T-27). |
