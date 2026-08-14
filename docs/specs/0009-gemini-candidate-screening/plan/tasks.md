# Tasks — 0009 Google Gemini 2.0 Flash Candidate Screening Integration

**Spec:** `../spec.md` · **LLD:** `lld.md` · **Updated:** 2026-08-14

Execution order for `/implement`. Tasks are grouped into **checkpoints**; `/implement` runs
one checkpoint per invocation, then stops for review.

**Progress:** 0 / 18 tasks · checkpoint CP-1 of 4

---

## How to read this file

- `- [ ]` pending · `- [x]` done · `- [~]` in progress (partial, see changelog)
- Each task names the files it touches and the `AC-n` it serves.
- A checkpoint ends at a state where the project **builds and its tests pass**. Never define
  a checkpoint that leaves the tree broken.
- `/implement` ticks boxes only after the code exists and the checkpoint's tests run green.

---

## CP-1 — Data layer and ScreeningResult expansion

*Exit condition: migration applies cleanly, `ScreeningReport.Complete()` accepts category scores, `ScreeningResult` record has new fields, `dotnet build` succeeds, existing unit tests updated and green.*

- [ ] **T-01** — Add `SkillsScore`, `ExperienceScore`, `EducationScore` to `ScreeningReport` entity
  - Files: `backend/src/Db/Applications/ScreeningReport.cs`
  - Covers: AC-1
  - Depends on: —

- [ ] **T-02** — Update `ScreeningReportConfiguration` to map three new nullable columns
  - Files: `backend/src/Db/Configurations/ScreeningReportConfiguration.cs`
  - Covers: AC-1
  - Depends on: T-01

- [ ] **T-03** — Add EF Core migration `AddScreeningCategoryScores`
  - Files: `backend/src/Db/Migrations/*_AddScreeningCategoryScores.cs`
  - Covers: AC-1
  - Depends on: T-02

- [ ] **T-04** — Expand `ScreeningResult` record with optional category score fields
  - Files: `backend/src/Service/Screening/ScreeningResult.cs`
  - Covers: AC-1, AC-9
  - Depends on: —

- [ ] **T-05** — Update `ScreeningReportDto` to include category score fields
  - Files: `backend/src/Service/Screening/Dtos/ScreeningReportDto.cs`
  - Covers: AC-7, AC-8
  - Depends on: —

- [ ] **T-06** — Update `ScreeningOrchestrator.Complete()` call and `ToDto()` mapping for category scores
  - Files: `backend/src/Service/Screening/ScreeningOrchestrator.cs`
  - Covers: AC-1, AC-7
  - Depends on: T-01, T-04, T-05

- [ ] **T-07** — Update existing `ScreeningOrchestratorTests` to construct `ScreeningResult` with new fields
  - Files: `backend/tests/Ats.UnitTests/Screening/ScreeningOrchestratorTests.cs`
  - Covers: AC-9
  - Depends on: T-04, T-06

## CP-2 — Gemini service implementation and provider selection

*Exit condition: `GeminiScreeningService` compiles, `MockScreeningService` updated, `ServiceCollectionExtensions` handles provider selection, unit tests for Gemini service pass with mocked HTTP handler, `dotnet build` and `dotnet test tests/Ats.UnitTests` succeed.*

- [ ] **T-08** — Create `GeminiOptions` configuration model
  - Files: `backend/src/Service/Screening/GeminiOptions.cs`
  - Covers: AC-2
  - Depends on: —

- [ ] **T-09** — Create `GeminiModels` request/response DTOs
  - Files: `backend/src/Service/Screening/GeminiModels.cs`
  - Covers: AC-1
  - Depends on: —

- [ ] **T-10** — Create `GeminiScreeningService` with structured JSON schema, retry, and error handling
  - Files: `backend/src/Service/Screening/GeminiScreeningService.cs`
  - Covers: AC-1, AC-5, AC-6
  - Depends on: T-04, T-08, T-09

- [ ] **T-11** — Update `MockScreeningService` to return category breakdown scores
  - Files: `backend/src/Service/Screening/MockScreeningService.cs`
  - Covers: AC-9
  - Depends on: T-04

- [ ] **T-12** — Update `ServiceCollectionExtensions` for provider selection with fallback
  - Files: `backend/src/Service/ServiceCollectionExtensions.cs`, `backend/src/Api/appsettings.json`
  - Covers: AC-2
  - Depends on: T-08, T-10, T-11

- [ ] **T-13** — Unit tests for `GeminiScreeningService` (valid response, auth failure, rate limit, malformed JSON, CV truncation)
  - Files: `backend/tests/Ats.UnitTests/Screening/GeminiScreeningServiceTests.cs`
  - Covers: AC-1, AC-5, AC-6, E-1, E-2, E-3, E-5
  - Depends on: T-10

## CP-3 — Frontend category score rendering

*Exit condition: `ScreeningReportCard` and `ScreeningReportModal` render category scores when present and gracefully hide them when null, `npm run build` succeeds, `npm test` passes.*

- [ ] **T-14** — Update TypeScript `ScreeningReportDto` type with category score fields
  - Files: `frontend/src/lib/types/screening.ts`
  - Covers: AC-8
  - Depends on: T-05

- [ ] **T-15** — Update `ScreeningReportCard` to display category score progress bars
  - Files: `frontend/src/components/staff/ScreeningReportCard.tsx`
  - Covers: AC-8
  - Depends on: T-14

- [ ] **T-16** — Update `ScreeningReportModal` to display category score progress bars
  - Files: `frontend/src/components/staff/ScreeningReportModal.tsx`
  - Covers: AC-8
  - Depends on: T-14

## CP-4 — Hardening and integration tests

*Exit condition: full backend test suite green, full frontend test suite green, provider registration integration tests pass, existing screening integration tests pass with updated assertions, `docs/specs/meta/architecture.md` updated.*

- [ ] **T-17** — Integration tests for provider registration and fallback logic
  - Files: `backend/tests/Ats.IntegrationTests/Screening/GeminiProviderRegistrationTests.cs`
  - Covers: AC-2, AC-9
  - Depends on: T-12

- [ ] **T-18** — Update `docs/specs/meta/architecture.md` and `docs/specs/meta/tech-stack.md`
  - Files: `docs/specs/meta/architecture.md`, `docs/specs/meta/tech-stack.md`
  - Covers: —
  - Depends on: all

---

## Coverage Check

Every acceptance criterion must appear in at least one task.

| AC | Covered by |
|---|---|
| AC-1 | T-01, T-02, T-03, T-04, T-06, T-09, T-10, T-13 |
| AC-2 | T-08, T-12, T-17 |
| AC-3 | T-06 (orchestrator auto-advance unchanged from 0008 — category scores are additive; no new test needed for AC-3 specifically, the existing integration test covers it) |
| AC-4 | T-06 (same as AC-3 — Review path unchanged) |
| AC-5 | T-10, T-13 |
| AC-6 | T-10, T-13 |
| AC-7 | T-05, T-06 |
| AC-8 | T-14, T-15, T-16 |
| AC-9 | T-04, T-07, T-11, T-17 |

Any AC with no task is a planning defect — fix it before `/implement` runs.

## Parallelisable

Tasks with no dependency edge between them, safe to do in any order within their checkpoint:

- CP-1: T-01 ‖ T-04 ‖ T-05 (no mutual dependencies)
- CP-2: T-08 ‖ T-09 (no mutual dependencies); T-10 after both
- CP-3: T-15 ‖ T-16 after T-14

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0008` (Automated Candidate Screening) | 1 | Parent spec — orchestrator, entity, and endpoint code this spec modifies. |
| `0004` (Application Submission and CV Upload) | 1 | `Application`, `CvAttachment` entities referenced. |
| `0005` (Pipeline Progression) | 1 | `StageTransition` written by auto-advance. |

Tier 0 read in full.
Considered and skipped: `0001`, `0002`, `0003`, `0006`, `0007`.
Cap reached: no.
