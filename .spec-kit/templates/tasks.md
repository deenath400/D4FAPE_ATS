# Tasks — NNNN <Title>

**Spec:** `../spec.md` · **LLD:** `lld.md` · **Updated:** YYYY-MM-DD

Execution order for `/implement`. Tasks are grouped into **checkpoints**; `/implement` runs
one checkpoint per invocation, then stops for review.

**Progress:** 0 / N tasks · checkpoint CP-1 of M

---

## How to read this file

- `- [ ]` pending · `- [x]` done · `- [~]` in progress (partial, see changelog)
- Each task names the files it touches and the `AC-n` it serves.
- A checkpoint ends at a state where the project **builds and its tests pass**. Never define
  a checkpoint that leaves the tree broken.
- `/implement` ticks boxes only after the code exists and the checkpoint's tests run green.

---

## CP-1 — Data layer

*Exit condition: migrations apply cleanly, entity tests pass, `dotnet build` succeeds.*

- [ ] **T-01** — Add `StageHistory` entity and configuration
  - Files: `src/Domain/Pipeline/StageHistory.cs`, `src/Infrastructure/Config/StageHistoryConfig.cs`
  - Covers: AC-4
  - Depends on: —

- [ ] **T-02** — Migration: add `current_stage_id` + `row_version` to `application`
  - Files: `src/Infrastructure/Migrations/*`
  - Covers: AC-2
  - Depends on: T-01

- [ ] **T-03** — Unit tests for `Application.MoveToStage` invariants
  - Files: `tests/Unit/Pipeline/MoveToStageTests.cs`
  - Covers: AC-2, AC-3, E-1
  - Depends on: T-01

## CP-2 — API

*Exit condition: endpoints return the documented shapes; integration tests pass.*

- [ ] **T-04** — `PipelineService.MoveToStageAsync`
  - Files: `src/Application/Pipeline/PipelineService.cs`
  - Covers: AC-2, AC-3
  - Depends on: T-01, T-02

- [ ] **T-05** — `PATCH /api/candidates/{id}/stage` endpoint + authorization policy
  - Files: `src/Api/Controllers/CandidatesController.cs`, `src/Api/Auth/Policies.cs`
  - Covers: AC-2
  - Depends on: T-04

- [ ] **T-06** — Integration tests for all documented status codes
  - Files: `tests/Integration/Pipeline/MoveStageEndpointTests.cs`
  - Covers: AC-2, AC-3, E-1
  - Depends on: T-05

## CP-3 — Frontend

*Exit condition: board renders and moves persist; component tests pass.*

- [ ] **T-07** — `PipelineBoard` component with drag-and-drop
  - Files: `src/features/pipeline/PipelineBoard.tsx`
  - Covers: AC-1
  - Depends on: T-05

- [ ] **T-08** — `useMoveStage` mutation with optimistic update and rollback
  - Files: `src/features/pipeline/api.ts`
  - Covers: AC-2, E-1
  - Depends on: T-05

- [ ] **T-09** — Loading / empty / error states
  - Files: `src/features/pipeline/PipelineBoard.tsx`
  - Covers: AC-1
  - Depends on: T-07

## CP-4 — Hardening

*Exit condition: full suite green, lint clean, NFRs demonstrated.*

- [ ] **T-10** — Index + query-plan check against NFR-1
  - Files: `src/Infrastructure/Migrations/*`
  - Covers: NFR-1
  - Depends on: T-02

- [ ] **T-11** — Update `docs/specs/meta/architecture.md` component map and ER diagram
  - Files: `docs/specs/meta/architecture.md`
  - Covers: —
  - Depends on: all

---

## Coverage Check

Every acceptance criterion must appear in at least one task.

| AC | Covered by |
|---|---|
| AC-1 | T-07, T-09 |
| AC-2 | T-02, T-04, T-05, T-06, T-08 |
| AC-3 | T-03, T-04, T-06 |
| AC-4 | T-01 |
| NFR-1 | T-10 |

Any AC with no task is a planning defect — fix it before `/implement` runs.

## Parallelisable

Tasks with no dependency edge between them, safe to do in any order within their checkpoint:
T-07 ‖ T-08 after T-05.

## Related Specs

<Per spec-kit/context-loading.md §4.>
