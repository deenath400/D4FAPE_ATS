# Tasks — 0004 Application Submission and CV Upload

**Spec:** `../spec.md` · **LLD:** `lld.md` · **Updated:** 2026-08-06

Execution order for `/implement`. Tasks are grouped into **checkpoints**; `/implement` runs
one checkpoint per invocation, then stops for review.

**Progress:** 41 / 46 tasks · checkpoints CP-1, CP-2, CP-3 of 4 done, CP-4 next

---

## How to read this file

- `- [ ]` pending · `- [x]` done · `- [~]` in progress (partial, see changelog)
- Each task names the files it touches and the `AC-n` it serves.
- A checkpoint ends at a state where the project **builds and its tests pass**. Never define
  a checkpoint that leaves the tree broken.
- `/implement` ticks boxes only after the code exists and the checkpoint's tests run green.

---

## CP-1 — Data layer (`shared/storage` + `db/application`)

*Exit condition: `dotnet build` succeeds, the migration applies cleanly against a fresh
database, and all CP-1 unit tests pass.*

- [x] **T-01** — `IFileStorage` interface
  - Files: `backend/src/Shared/Storage/IFileStorage.cs`
  - Covers: —
  - Depends on: —

- [x] **T-02** — `LocalDiskFileStorage` implementation
  - Files: `backend/src/Shared/Storage/LocalDiskFileStorage.cs`
  - Covers: FR-7, NFR-2
  - Depends on: T-01

- [x] **T-03** — `Application` entity
  - Files: `backend/src/Db/Applications/Application.cs`
  - Covers: FR-13
  - Depends on: —

- [x] **T-04** — `CvAttachment` entity
  - Files: `backend/src/Db/Applications/CvAttachment.cs`
  - Covers: FR-7
  - Depends on: —

- [x] **T-05** — EF Core configurations (`ApplicationConfiguration`, `CvAttachmentConfiguration`), including the unique `(CandidateId, RequisitionId)` and `ApplicationId` indexes
  - Files: `backend/src/Db/Configurations/ApplicationConfiguration.cs`, `backend/src/Db/Configurations/CvAttachmentConfiguration.cs`
  - Covers: FR-5, NFR-2
  - Depends on: T-03, T-04

- [x] **T-06** — Wire `AppDbContext`: `DbSet<Application>`, `DbSet<CvAttachment>`, apply both configurations
  - Files: `backend/src/Db/AppDbContext.cs`
  - Covers: —
  - Depends on: T-05

- [x] **T-07** — Migration `AddApplicationsAndCvAttachments`
  - Files: `backend/src/Db/Migrations/<timestamp>_AddApplicationsAndCvAttachments.cs`, `.Designer.cs`, `backend/src/Db/Migrations/AppDbContextModelSnapshot.cs`
  - Covers: FR-13
  - Depends on: T-06

- [x] **T-08** — Add `Storage:CvBasePath` and `Applications:MaxCvSizeBytes` to `appsettings.json`
  - Files: `backend/src/Api/appsettings.json`
  - Covers: FR-3, FR-7
  - Depends on: —

- [x] **T-09** — Ignore the backend's app-data directory (CV files are candidate PII)
  - Files: `.gitignore`
  - Covers: NFR-2
  - Depends on: —

- [x] **T-10** — Unit tests: `Application`/`CvAttachment` entity invariants
  - Files: `backend/tests/Ats.UnitTests/Application/ApplicationEntityTests.cs`
  - Covers: AC-1, AC-22, NFR-1
  - Depends on: T-03, T-04

- [x] **T-11** — Unit tests: `LocalDiskFileStorage` save/open/delete round-trip and path-traversal rejection
  - Files: `backend/tests/Ats.UnitTests/Storage/LocalDiskFileStorageTests.cs`
  - Covers: FR-7, NFR-2
  - Depends on: T-02

## CP-2 — Service & API (`service/application`, `api/application`)

*Exit condition: all four endpoints return the documented shapes and status codes; unit and
integration tests pass; `dotnet build` succeeds.*

- [x] **T-12** — `IApplicationService` contract
  - Files: `backend/src/Service/Application/IApplicationService.cs`
  - Covers: —
  - Depends on: T-06

- [x] **T-13** — DTOs: `ApplicationDto`, `CandidateApplicationListItemDto`, `StaffApplicationListItemDto`, `CvDownloadResult`
  - Files: `backend/src/Service/Application/Dtos/ApplicationDto.cs`, `CandidateApplicationListItemDto.cs`, `StaffApplicationListItemDto.cs`, `CvDownloadResult.cs`
  - Covers: —
  - Depends on: T-12

- [x] **T-14** — `ApplicationService.SubmitAsync` — Requisition-published check, CV validation (type/size/magic-byte), duplicate pre-check, storage write, insert, race-fallback cleanup
  - Files: `backend/src/Service/Application/ApplicationService.cs`
  - Covers: AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8, AC-9, NFR-1, NFR-3, E-1, E-4, E-7
  - Depends on: T-02, T-07, T-13

- [x] **T-15** — `ApplicationService.ListMineAsync`
  - Files: `backend/src/Service/Application/ApplicationService.cs`
  - Covers: AC-12, AC-13
  - Depends on: T-14

- [x] **T-16** — `ApplicationService.ListForRequisitionAsync`
  - Files: `backend/src/Service/Application/ApplicationService.cs`
  - Covers: AC-16, AC-18, E-5
  - Depends on: T-14

- [x] **T-17** — `ApplicationService.GetCvAsync`
  - Files: `backend/src/Service/Application/ApplicationService.cs`
  - Covers: AC-14, AC-15, AC-20, AC-21, NFR-2, E-6
  - Depends on: T-14

- [x] **T-18** — Register `IApplicationService` and `IFileStorage`/`LocalDiskFileStorage` in DI
  - Files: `backend/src/Service/ServiceCollectionExtensions.cs`
  - Covers: —
  - Depends on: T-02, T-14, T-15, T-16, T-17

- [x] **T-19** — `ApplicationEndpoints`: `POST /api/requisitions/{id}/applications`, `GET /api/requisitions/{id}/applications`
  - Files: `backend/src/Api/ApplicationEndpoints.cs`
  - Covers: AC-1–AC-11, AC-16–AC-19
  - Depends on: T-18

- [x] **T-20** — `ApplicationEndpoints`: `GET /api/applications/mine`, `GET /api/applications/{id}/cv`
  - Files: `backend/src/Api/ApplicationEndpoints.cs`
  - Covers: AC-12, AC-13, AC-14, AC-15, AC-20, AC-21
  - Depends on: T-19

- [x] **T-21** — Map `ApplicationEndpoints` in `Program.cs`
  - Files: `backend/src/Api/Program.cs`
  - Covers: —
  - Depends on: T-20

- [x] **T-22** — Add a per-test temp `Storage:CvBasePath` to `CustomWebApplicationFactory`, cleaned up in `Dispose`
  - Files: `backend/tests/Ats.IntegrationTests/CustomWebApplicationFactory.cs`
  - Covers: —
  - Depends on: T-21

- [x] **T-23** — Unit tests: `ApplicationService` — every validation, authorization, and duplicate branch
  - Files: `backend/tests/Ats.UnitTests/Application/ApplicationServiceTests.cs`
  - Covers: AC-1–AC-9, AC-12–AC-21, NFR-1, NFR-2, E-1, E-4, E-5, E-6, E-7
  - Depends on: T-17

- [x] **T-24** — Integration tests: all four endpoints, every documented status code
  - Files: `backend/tests/Ats.IntegrationTests/Application/ApplicationEndpointsTests.cs`
  - Covers: AC-1–AC-21
  - Depends on: T-22

## CP-3 — Frontend (`ui/portal`, `ui/staff`, `ui/bff`)

*Exit condition: `npm run build` succeeds, and the **full** Vitest suite (existing + new) passes
— not just this spec's new tests, per `hld.md` R-1.*

- [x] **T-25** — Generalise the `ui/bff` proxy route to binary-safe (`ArrayBuffer`) request/response passthrough and forward `Content-Disposition`
  - Files: `frontend/src/app/api/bff/proxy/[...path]/route.ts`
  - Covers: enables AC-1, AC-14, AC-20 (frontend leg)
  - Depends on: T-21

- [x] **T-26** — Shared TS types mirroring `api.md` §4
  - Files: `frontend/src/lib/types/application.ts`
  - Covers: —
  - Depends on: —

- [x] **T-27** — `isCandidateRole` helper
  - Files: `frontend/src/lib/auth-guards.ts`
  - Covers: —
  - Depends on: —

- [x] **T-28** — Gate `/applications/*` for the `Candidate` role in `middleware.ts`
  - Files: `frontend/src/middleware.ts`
  - Covers: —
  - Depends on: T-27

- [x] **T-29** — `ApplicationForm` client component (file input, submit, four UI states)
  - Files: `frontend/src/components/portal/ApplicationForm.tsx`
  - Covers: AC-1, AC-2, AC-3, AC-4, AC-8
  - Depends on: T-25, T-26

- [x] **T-30** — Apply page (session/status guards, loads Requisition, renders `ApplicationForm`) + loading state
  - Files: `frontend/src/app/(portal)/jobs/[id]/apply/page.tsx`, `frontend/src/app/(portal)/jobs/[id]/apply/loading.tsx`
  - Covers: AC-1, AC-5, AC-6, AC-7
  - Depends on: T-29

- [x] **T-31** — Add a session-aware "Apply" call to action to the job detail page
  - Files: `frontend/src/app/(portal)/jobs/[id]/page.tsx`
  - Covers: —
  - Depends on: T-30, T-27

- [x] **T-32** — `ApplicationList` presentational component
  - Files: `frontend/src/components/portal/ApplicationList.tsx`
  - Covers: AC-12, AC-13
  - Depends on: T-26

- [x] **T-33** — "My Applications" page + loading + error states
  - Files: `frontend/src/app/(portal)/applications/page.tsx`, `frontend/src/app/(portal)/applications/loading.tsx`, `frontend/src/app/(portal)/applications/error.tsx`
  - Covers: AC-12, AC-13
  - Depends on: T-32, T-28

- [x] **T-34** — "My Applications" link in `HeaderNav` for Candidate sessions
  - Files: `frontend/src/components/HeaderNav.tsx`
  - Covers: —
  - Depends on: T-27

- [x] **T-35** — `ApplicationsTable` presentational component
  - Files: `frontend/src/components/staff/ApplicationsTable.tsx`
  - Covers: AC-16, AC-17, AC-18
  - Depends on: T-26

- [x] **T-36** — Staff Applications page + loading + error states
  - Files: `frontend/src/app/staff/requisitions/[id]/applications/page.tsx`, `frontend/src/app/staff/requisitions/[id]/applications/loading.tsx`, `frontend/src/app/staff/requisitions/[id]/applications/error.tsx`
  - Covers: AC-16, AC-17, AC-18, AC-19
  - Depends on: T-35

- [x] **T-37** — Add a "View Applications" link to the staff requisition detail page
  - Files: `frontend/src/app/staff/requisitions/[id]/page.tsx`
  - Covers: —
  - Depends on: T-36

- [x] **T-38** — Component tests: `ApplicationForm`
  - Files: `frontend/tests/portal/application-form.test.tsx`
  - Covers: AC-1, AC-3, AC-8
  - Depends on: T-29

- [x] **T-39** — Component tests: `ApplicationList`
  - Files: `frontend/tests/portal/application-list.test.tsx`
  - Covers: AC-12, AC-13
  - Depends on: T-32

- [x] **T-40** — Component tests: `ApplicationsTable`
  - Files: `frontend/tests/staff/applications-table.test.tsx`
  - Covers: AC-16, AC-18
  - Depends on: T-35

- [x] **T-41** — Extend `auth-guards.test.ts` with `isCandidateRole` cases
  - Files: `frontend/tests/lib/auth-guards.test.ts`
  - Covers: —
  - Depends on: T-27

## CP-4 — Hardening

*Exit condition: full backend and frontend suites green, NFRs demonstrated by a dedicated test
each, `meta/architecture.md` and `meta/tech-stack.md` reflect what shipped.*

- [ ] **T-42** — NFR-1 verification test: a storage-write failure leaves no `Application` row
  - Files: `backend/tests/Ats.UnitTests/Application/ApplicationServiceTests.cs`
  - Covers: NFR-1
  - Depends on: T-23

- [ ] **T-43** — NFR-3 verification test: the SQLite write transaction spans only the row insert, not the file write
  - Files: `backend/tests/Ats.IntegrationTests/Application/ApplicationEndpointsTests.cs`
  - Covers: NFR-3
  - Depends on: T-24

- [ ] **T-44** — E-1 regression test: two near-simultaneous submissions by the same Candidate against the same Requisition — exactly one survives
  - Files: `backend/tests/Ats.IntegrationTests/Application/ApplicationEndpointsTests.cs`
  - Covers: E-1, AC-8
  - Depends on: T-24

- [ ] **T-45** — Update `docs/specs/meta/architecture.md`: Component Map (`api/application`, `service/application`, `db/application`, `shared/storage` — resolve "backing store TBD"), Data Model diagram, Change Log
  - Files: `docs/specs/meta/architecture.md`
  - Covers: —
  - Depends on: all

- [ ] **T-46** — Update `docs/specs/meta/tech-stack.md`: resolve "Object storage: TBD" row, add `Storage:CvBasePath`/`Applications:MaxCvSizeBytes` to Required Configuration
  - Files: `docs/specs/meta/tech-stack.md`
  - Covers: —
  - Depends on: all

---

## Coverage Check

Every acceptance criterion must appear in at least one task.

| AC | Covered by |
|---|---|
| AC-1 | T-10, T-14, T-19, T-23, T-24, T-29, T-30, T-38 |
| AC-2 | T-14, T-19, T-23, T-24, T-29, T-38 |
| AC-3 | T-14, T-19, T-23, T-24, T-29, T-38 |
| AC-4 | T-14, T-19, T-23, T-24, T-29 |
| AC-5 | T-14, T-19, T-23, T-24, T-30 |
| AC-6 | T-14, T-19, T-23, T-24, T-30 |
| AC-7 | T-14, T-19, T-23, T-24, T-30 |
| AC-8 | T-05, T-14, T-19, T-23, T-24, T-29, T-38, T-44 |
| AC-9 | T-14, T-23, T-24 |
| AC-10 | T-19, T-24 |
| AC-11 | T-19, T-24 |
| AC-12 | T-15, T-20, T-23, T-24, T-32, T-33, T-39 |
| AC-13 | T-15, T-20, T-23, T-24, T-32, T-33, T-39 |
| AC-14 | T-17, T-20, T-23, T-24, T-25 |
| AC-15 | T-17, T-20, T-23, T-24 |
| AC-16 | T-16, T-19, T-23, T-24, T-35, T-36, T-40 |
| AC-17 | T-16, T-19, T-23, T-24, T-35, T-36 |
| AC-18 | T-16, T-19, T-23, T-24, T-35, T-36, T-40 |
| AC-19 | T-16, T-19, T-23, T-24, T-36 |
| AC-20 | T-17, T-20, T-23, T-24, T-25 |
| AC-21 | T-17, T-20, T-23 |
| AC-22 | T-10 |
| NFR-1 | T-10, T-14, T-23, T-42 |
| NFR-2 | T-02, T-05, T-09, T-11, T-17, T-23 |
| NFR-3 | T-14, T-43 |

Any AC with no task is a planning defect — fix it before `/implement` runs.

## Parallelisable

- Within CP-1: T-01/T-02 (storage) ‖ T-03/T-04/T-05/T-06/T-07 (entities/migration) ‖ T-08/T-09 (config/gitignore) — no dependency edge between the three groups until T-14 in CP-2 needs both.
- Within CP-2: T-15 ‖ T-16 ‖ T-17 after T-14 (all three read-only methods depend only on `SubmitAsync` existing in the same file, not on each other).
- Within CP-3: T-26/T-27 (types/guards) ‖ T-25 (proxy fix) — independent, both prerequisites for T-29/T-32/T-35. T-32+T-33 (candidate list) ‖ T-35+T-36 (staff list) once T-25–T-28 are done.
- Within CP-4: T-42 ‖ T-43 ‖ T-44 — independent test additions.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0003` (Requisition Management) | 1 | Checkpoint shape (data → service/API → frontend → hardening) and its 4-checkpoint precedent reused directly; its `RequisitionEndpointsTests.cs`/`RequisitionServiceTests.cs` structure is the template for T-23/T-24. |
| `0002` (User Authentication and Refresh Token Flow) | 1 | `CandidateOnly`/`StaffOnly` policies this spec's endpoints (T-19, T-20) consume without redefining. |
| `0001` (Project Scaffolding and Walking Skeleton) | 1 | `ui/bff` proxy (T-25) and the CI/build command set (`dotnet build`, `npm run build`, `npm test`) every checkpoint's exit condition is measured against. |
