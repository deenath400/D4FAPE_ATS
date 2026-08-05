# Tasks — 0003 Requisition Management

**Spec:** `../spec.md` · **LLD:** `lld.md` · **Updated:** 2026-08-05

Execution order for `/implement`. Tasks are grouped into **checkpoints**; `/implement` runs
one checkpoint per invocation, then stops for review.

**Progress:** 6 / 42 tasks · checkpoint CP-1 of 4 complete, CP-2 next

---

## How to read this file

- `- [ ]` pending · `- [x]` done · `- [~]` in progress (partial, see changelog)
- Each task names the files it touches and the `AC-n` it serves.
- A checkpoint ends at a state where the project **builds and its tests pass**. Never define
  a checkpoint that leaves the tree broken.
- `/implement` ticks boxes only after the code exists and the checkpoint's tests run green.

---

## CP-1 — Data layer

*Exit condition: `dotnet build` succeeds, migration applies cleanly against a fresh SQLite
file, `dotnet test tests/Ats.UnitTests` (Requisition entity tests) passes.*

- [x] **T-01** — `RequisitionStatus` enum + `Requisition` entity
  - Files: `backend/src/Db/Requisitions/RequisitionStatus.cs`, `backend/src/Db/Requisitions/Requisition.cs`
  - Covers: — (foundation for AC-1, AC-3–AC-11)
  - Depends on: —

- [x] **T-02** — `Stage` entity
  - Files: `backend/src/Db/Requisitions/Stage.cs`
  - Covers: — (foundation for AC-23)
  - Depends on: T-01

- [x] **T-03** — EF Core configurations for `Requisition` and `Stage`
  - Files: `backend/src/Db/Configurations/RequisitionConfiguration.cs`, `backend/src/Db/Configurations/StageConfiguration.cs`
  - Covers: —
  - Depends on: T-01, T-02

- [x] **T-04** — Register `DbSet<Requisition>`/`DbSet<Stage>` and apply configurations
  - Files: `backend/src/Db/AppDbContext.cs`
  - Covers: —
  - Depends on: T-03

- [x] **T-05** — Migration `AddRequisitionsAndStages`
  - Files: `backend/src/Db/Migrations/*AddRequisitionsAndStages*.cs`, `backend/src/Db/Migrations/AppDbContextModelSnapshot.cs`
  - Covers: —
  - Depends on: T-04

- [x] **T-06** — Unit tests: entity invariants + Stage ownership shape
  - Files: `backend/tests/Ats.UnitTests/Requisition/RequisitionEntityTests.cs`
  - Covers: AC-23
  - Depends on: T-05

## CP-2 — Service / API layer

*Exit condition: `dotnet build` succeeds; `dotnet test tests/Ats.UnitTests` and
`dotnet test tests/Ats.IntegrationTests` both pass; every endpoint in `api.md` returns the
documented status/body.*

- [ ] **T-07** — `PagedResult<T>` generic type
  - Files: `backend/src/Service/Common/PagedResult.cs`
  - Covers: —
  - Depends on: —

- [ ] **T-08** — Requisition DTOs (`RequisitionDto`, `PublicRequisitionDto`, `CreateRequisitionRequestDto`, `UpdateRequisitionRequestDto`)
  - Files: `backend/src/Service/Requisition/Dtos/RequisitionDto.cs`, `backend/src/Service/Requisition/Dtos/PublicRequisitionDto.cs`, `backend/src/Service/Requisition/Dtos/CreateRequisitionRequestDto.cs`, `backend/src/Service/Requisition/Dtos/UpdateRequisitionRequestDto.cs`
  - Covers: —
  - Depends on: —

- [ ] **T-09** — `IRequisitionService` interface
  - Files: `backend/src/Service/Requisition/IRequisitionService.cs`
  - Covers: —
  - Depends on: T-07, T-08

- [ ] **T-10** — `RequisitionService` implementation (create/update/publish/unpublish/close/get/list/public search/public detail)
  - Files: `backend/src/Service/Requisition/RequisitionService.cs`
  - Covers: AC-1, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8, AC-9, AC-10, AC-11, AC-12, AC-16, AC-17, AC-18, AC-19, AC-20, AC-21, AC-22, AC-24, NFR-1, NFR-2
  - Depends on: T-05, T-09

- [ ] **T-11** — Unit tests for `RequisitionService`
  - Files: `backend/tests/Ats.UnitTests/Requisition/RequisitionServiceTests.cs`
  - Covers: AC-1, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8, AC-9, AC-10, AC-11, AC-16, AC-17, AC-18, AC-19, AC-20, AC-21, AC-22, AC-24, NFR-1
  - Depends on: T-10

- [ ] **T-12** — Register `IRequisitionService` in DI
  - Files: `backend/src/Service/ServiceCollectionExtensions.cs`
  - Covers: —
  - Depends on: T-10

- [ ] **T-13** — Staff `RequisitionEndpoints` (create/list/get/update/publish/unpublish/close)
  - Files: `backend/src/Api/RequisitionEndpoints.cs`
  - Covers: AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8, AC-9, AC-10, AC-11, AC-12, AC-13
  - Depends on: T-12

- [ ] **T-14** — Public `PublicRequisitionEndpoints` (list + detail)
  - Files: `backend/src/Api/PublicRequisitionEndpoints.cs`
  - Covers: AC-16, AC-17, AC-18, AC-19, AC-20, AC-21, AC-22, AC-24
  - Depends on: T-12

- [ ] **T-15** — Wire both endpoint groups into the host
  - Files: `backend/src/Api/Program.cs`
  - Covers: —
  - Depends on: T-13, T-14

- [ ] **T-16** — Integration tests: staff endpoints
  - Files: `backend/tests/Ats.IntegrationTests/Requisition/RequisitionEndpointsTests.cs`
  - Covers: AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8, AC-9, AC-10, AC-11, AC-12, AC-13
  - Depends on: T-15

- [ ] **T-17** — Integration tests: public endpoints
  - Files: `backend/tests/Ats.IntegrationTests/Requisition/PublicRequisitionEndpointsTests.cs`
  - Covers: AC-16, AC-17, AC-18, AC-19, AC-20, AC-21, AC-22, AC-24
  - Depends on: T-15

## CP-3 — Frontend

*Exit condition: `npm run build` succeeds, `npm test` (Vitest) passes, `npm run lint` is
clean.*

- [ ] **T-18** — Shared TS types for Requisition
  - Files: `frontend/src/lib/types/requisition.ts`
  - Covers: —
  - Depends on: T-14

- [ ] **T-19** — Role-check helpers
  - Files: `frontend/src/lib/auth-guards.ts`
  - Covers: — (foundation for AC-14, AC-15)
  - Depends on: —

- [ ] **T-20** — Unit tests for role-check helpers
  - Files: `frontend/tests/lib/auth-guards.test.ts`
  - Covers: AC-14, AC-15
  - Depends on: T-19

- [ ] **T-21** — `middleware.ts` — `/staff/*` route gating
  - Files: `frontend/src/middleware.ts`
  - Covers: AC-14, AC-15
  - Depends on: T-19

- [ ] **T-22** — Replace the `(staff)` placeholder with a real `/staff` layout
  - Files: Delete `frontend/src/app/(staff)/.gitkeep`; Create `frontend/src/app/staff/layout.tsx`
  - Covers: — (foundation for G-4)
  - Depends on: T-21

- [ ] **T-23** — `RequisitionForm` client component (create + edit)
  - Files: `frontend/src/components/staff/RequisitionForm.tsx`
  - Covers: AC-1, AC-3, AC-4, AC-5
  - Depends on: T-18

- [ ] **T-24** — `RequisitionLifecycleActions` client component
  - Files: `frontend/src/components/staff/RequisitionLifecycleActions.tsx`
  - Covers: AC-6, AC-7, AC-8, AC-9, AC-10, AC-11
  - Depends on: T-18

- [ ] **T-25** — Staff requisitions list page
  - Files: `frontend/src/app/staff/requisitions/page.tsx`
  - Covers: AC-12
  - Depends on: T-18, T-22

- [ ] **T-26** — Staff list loading/error states
  - Files: `frontend/src/app/staff/requisitions/loading.tsx`, `frontend/src/app/staff/requisitions/error.tsx`
  - Covers: AC-12 (UI states)
  - Depends on: T-25

- [ ] **T-27** — Staff new-requisition page
  - Files: `frontend/src/app/staff/requisitions/new/page.tsx`
  - Covers: AC-1
  - Depends on: T-23

- [ ] **T-28** — Staff requisition detail/edit/lifecycle page
  - Files: `frontend/src/app/staff/requisitions/[id]/page.tsx`
  - Covers: AC-3, AC-4, AC-5, AC-6, AC-7, AC-8, AC-9, AC-10, AC-11
  - Depends on: T-23, T-24

- [ ] **T-29** — Staff detail loading/error states
  - Files: `frontend/src/app/staff/requisitions/[id]/loading.tsx`, `frontend/src/app/staff/requisitions/[id]/error.tsx`
  - Covers: — (UI states)
  - Depends on: T-28

- [ ] **T-30** — Component tests: `RequisitionForm`
  - Files: `frontend/tests/staff/requisition-form.test.tsx`
  - Covers: AC-1, AC-3, AC-5
  - Depends on: T-23

- [ ] **T-31** — Component tests: `RequisitionLifecycleActions`
  - Files: `frontend/tests/staff/requisition-lifecycle-actions.test.tsx`
  - Covers: AC-6, AC-7, AC-9, AC-10, AC-11
  - Depends on: T-24

- [ ] **T-32** — `JobSearchForm` component (portal keyword search)
  - Files: `frontend/src/components/portal/JobSearchForm.tsx`
  - Covers: AC-16, AC-17
  - Depends on: T-18

- [ ] **T-33** — `JobList` component (list + pagination)
  - Files: `frontend/src/components/portal/JobList.tsx`
  - Covers: AC-18, AC-19, AC-20
  - Depends on: T-18

- [ ] **T-34** — Portal jobs list page
  - Files: `frontend/src/app/(portal)/jobs/page.tsx`
  - Covers: AC-16, AC-17, AC-18, AC-19, AC-20, AC-24
  - Depends on: T-32, T-33

- [ ] **T-35** — Portal jobs list loading state
  - Files: `frontend/src/app/(portal)/jobs/loading.tsx`
  - Covers: — (UI states)
  - Depends on: T-34

- [ ] **T-36** — Portal job detail page
  - Files: `frontend/src/app/(portal)/jobs/[id]/page.tsx`
  - Covers: AC-21, AC-22
  - Depends on: T-18

- [ ] **T-37** — Portal job detail loading state
  - Files: `frontend/src/app/(portal)/jobs/[id]/loading.tsx`
  - Covers: — (UI states)
  - Depends on: T-36

- [ ] **T-38** — `HeaderNav` — add Staff Workspace / Browse Jobs links
  - Files: `frontend/src/components/HeaderNav.tsx`
  - Covers: — (G-2, G-3 discoverability)
  - Depends on: T-25, T-34

- [ ] **T-39** — Component tests: `JobSearchForm` / `JobList`
  - Files: `frontend/tests/portal/job-search-form.test.tsx`
  - Covers: AC-16, AC-17, AC-20
  - Depends on: T-32, T-33

## CP-4 — Hardening

*Exit condition: full backend + frontend suites green, `dotnet format --verify-no-changes`
and `npm run lint`/`format` clean, NFR-1/NFR-2 demonstrated by a passing test,
`meta/architecture.md` reflects the shipped components.*

- [ ] **T-40** — NFR-1 verification: `pageSize` above 50 is clamped, not rejected
  - Files: `backend/tests/Ats.IntegrationTests/Requisition/PublicRequisitionEndpointsTests.cs`
  - Covers: NFR-1
  - Depends on: T-17

- [ ] **T-41** — NFR-2 verification: public reads never open a write transaction
  - Files: `backend/tests/Ats.UnitTests/Requisition/RequisitionServiceTests.cs`
  - Covers: NFR-2
  - Depends on: T-11

- [ ] **T-42** — Update `docs/specs/meta/architecture.md`
  - Files: `docs/specs/meta/architecture.md`
  - Covers: — (Data Model diagram: add `REQUISITION ||--o{ STAGE`; Component Map: owning spec `0003` for `api/requisition`, `service/requisition`, `db/requisition`, `ui/staff`, modified note for `ui/portal`; Change Log entry)
  - Depends on: all

---

## Coverage Check

Every acceptance criterion must appear in at least one task.

| AC | Covered by |
|---|---|
| AC-1 | T-10, T-11, T-13, T-16, T-23, T-27, T-30 |
| AC-2 | T-13, T-16 |
| AC-3 | T-10, T-11, T-13, T-16, T-23, T-28, T-30 |
| AC-4 | T-10, T-11, T-13, T-16, T-23, T-28 |
| AC-5 | T-10, T-11, T-13, T-16, T-23, T-28, T-30 |
| AC-6 | T-10, T-11, T-13, T-16, T-24, T-28, T-31 |
| AC-7 | T-10, T-11, T-13, T-16, T-24, T-28, T-31 |
| AC-8 | T-10, T-11, T-13, T-16, T-24, T-28 |
| AC-9 | T-10, T-11, T-13, T-16, T-24, T-28, T-31 |
| AC-10 | T-10, T-11, T-13, T-16, T-24, T-31 |
| AC-11 | T-10, T-11, T-13, T-16, T-24, T-31 |
| AC-12 | T-10, T-13, T-16, T-25, T-26 |
| AC-13 | T-13, T-16 |
| AC-14 | T-19, T-20, T-21, T-22 |
| AC-15 | T-19, T-20, T-21, T-22, T-25 |
| AC-16 | T-10, T-11, T-14, T-17, T-32, T-34, T-39 |
| AC-17 | T-10, T-11, T-14, T-17, T-32, T-34, T-39 |
| AC-18 | T-10, T-11, T-14, T-17, T-33, T-34 |
| AC-19 | T-10, T-11, T-14, T-17, T-33, T-34 |
| AC-20 | T-10, T-11, T-14, T-17, T-33, T-34, T-39 |
| AC-21 | T-10, T-11, T-14, T-17, T-36 |
| AC-22 | T-10, T-11, T-14, T-17, T-36 |
| AC-23 | T-02, T-06 |
| AC-24 | T-10, T-11, T-14, T-17, T-34 |
| NFR-1 | T-10, T-17, T-40 |
| NFR-2 | T-10, T-41 |

Any AC with no task is a planning defect — fix it before `/implement` runs. All 24 ACs and
both NFRs are covered.

## Parallelisable

- Within CP-1: T-01 ‖ (none before it exists) — mostly sequential (entity → config → DbContext
  → migration → tests) since each step depends on the compiled model from the previous one.
- Within CP-2: T-07 ‖ T-08 (independent files); T-13 ‖ T-14 once T-12 is done (disjoint
  endpoint files).
- Within CP-3: T-19 ‖ T-18 (independent); T-23 ‖ T-24 ‖ T-32 ‖ T-33 once T-18 is done
  (disjoint components); T-27 ‖ T-32/T-33/T-34/T-36 (staff vs. portal surfaces touch disjoint
  files); T-30 ‖ T-31 ‖ T-39 (disjoint test files).
- Within CP-4: T-40 ‖ T-41 (disjoint test files).

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0002` (User Authentication and Refresh Token Flow) | 1 | Task shape (entity → config → migration → service → endpoint → tests) mirrors how `0002`'s CP-1/CP-2 sequenced `RefreshToken`/`AuthService`/`AuthEndpoints`. |
| `0001` (Project Scaffolding and Walking Skeleton) | 1 | Frontend checkpoint shape (component → page → loading/error states → test) mirrors `0001`'s CP-3/CP-4 sequencing for `ServerStatusSection`/`ClientStatusPanel`. |
