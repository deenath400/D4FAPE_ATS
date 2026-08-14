---
id: 0009
slug: gemini-candidate-screening
title: Google Gemini 2.0 Flash Candidate Screening Integration
status: implemented
components: [api/application, service/application, service/screening, db/application, ui/staff]
entities: [Application, CvAttachment, Requisition, Stage, StageTransition, ScreeningReport]
depends_on: [0008]
created: 2026-08-14
updated: 2026-08-14
---

# Google Gemini 2.0 Flash Candidate Screening Integration

## Problem & Context

Spec `0008` introduced automated candidate screening architecture, persistence (`ScreeningReport`), auto-advance stage progression (`0005`), and staff-facing screening visibility (`ui/staff`). However, `0008` utilized a deterministic keyword-matching heuristic (`MockScreeningService`) as the default implementation for offline verification.

Keyword matching cannot understand candidate context, qualitative depth of experience, transferable technical skills, or semantic fit against nuanced job requisition descriptions. Recruiters and Hiring Managers spend significant manual effort reviewing resumes when keyword scores fail to reflect true candidate qualifications.

Integrating Google's **Gemini 2.0 Flash** model via its native structured JSON output capability delivers high-speed, cost-effective, and deeply contextual candidate evaluations. In addition to an overall score and recommendation, Recruiters require granular category breakdown scores (Skills Fit, Experience Fit, Education Fit) to quickly pinpoint why a candidate was recommended for advancement or flagged for manual review.

## Goals

- **G-1** Provide a production-ready `GeminiScreeningService` using typed `HttpClient` communication against Google's Gemini 2.0 Flash REST API (`gemini-2.0-flash:generateContent`).
- **G-2** Enforce strict structured JSON schema responses from Gemini 2.0 Flash, capturing overall score (0–100), category breakdown scores (Skills Fit, Experience Fit, Education Fit, each 0–100), progression recommendation (`Advance` or `Review`), executive match summary, bulleted strengths, and bulleted potential gaps/concerns.
- **G-3** Support runtime provider configuration (`Screening:Provider` = `"Gemini"` | `"Mock"`) with graceful fallback to `MockScreeningService` in offline/development environments when an API key is not configured.
- **G-4** Persist category breakdown scores on `ScreeningReport` and expose them through staff API endpoints and `ui/staff` screening components.
- **G-5** Handle transient API failures, rate limits, and outages with retry logic and graceful failure recording without disrupting applicant submission or pipeline workflows.

## Non-Goals

- **External SDK dependencies** — Third-party or heavyweight SDK packages are excluded; integration uses typed .NET `HttpClient` with standard `System.Text.Json` structured schemas.
- **Multimodal image/video analysis** — Only text extracted from PDF CVs (`IPdfTextExtractor`) is passed to the Gemini model.
- **Altering stage progression mechanics** — Auto-advance and stage transition rules established in `0008` and `0005` remain unchanged.
- **Candidate-facing AI score exposure** — Screening reports and breakdown scores remain strictly internal to Staff (`StaffOnly`).
- **Custom per-requisition prompt editing UI** — System prompt uses a standardized ATS screening rubric based on job title and description.

## Users & Personas

| Persona | Need this feature serves |
|---|---|
| Recruiter | Receives deep semantic evaluation and granular category scores (Skills, Experience, Education) on incoming CVs, with top candidates auto-advanced; can trigger on-demand Gemini re-screening. |
| HiringManager | Reviews structured AI match summaries, category score breakdowns, strengths, and concerns on the pipeline board and application detail views. |
| Candidate | Benefits from rapid application triage without exposure to internal evaluation metrics. |

## Functional Requirements

- **FR-1** — The system provides `GeminiScreeningService` implementing `IScreeningService`, sending Requisition details (title, description) and candidate CV text to the Gemini 2.0 Flash API (`v1beta/models/gemini-2.0-flash:generateContent`).
- **FR-2** — The active screening provider is configurable via `Screening:Provider` (`Gemini` or `Mock`). If set to `Gemini` but `Gemini:ApiKey` is missing or empty, the system logs a warning and gracefully falls back to `MockScreeningService`.
- **FR-3** — Gemini 2.0 Flash requests use structured JSON schema enforcement (`response_mime_type: "application/json"` with schema definition) requiring the model to return:
  - `score` (integer, 0 to 100)
  - `skillsScore` (integer, 0 to 100)
  - `experienceScore` (integer, 0 to 100)
  - `educationScore` (integer, 0 to 100)
  - `recommendation` (string: `"Advance"` or `"Review"`)
  - `summary` (string)
  - `strengths` (array of strings)
  - `concerns` (array of strings)
- **FR-4** — The `ScreeningReport` entity, database schema, and staff API DTOs are updated to persist and expose category breakdown scores (`SkillsScore`, `ExperienceScore`, `EducationScore`).
- **FR-5** — When Gemini evaluation produces an `Advance` recommendation (overall score ≥ qualification threshold, default 75), the orchestrator automatically advances the Application to the next sequential pipeline stage per `0008` FR-3.
- **FR-6** — The Gemini client implements configurable HTTP timeout (default 30 seconds) and transient failure retry with exponential backoff for rate limits (HTTP 429) and transient server errors (HTTP 503).
- **FR-7** — On terminal API failure (e.g. invalid API key, quota exhaustion, malformed JSON), `ScreeningReport` status is set to `Failed` with a descriptive `FailureReason`, and the Application remains in its current Stage without disrupting submission.
- **FR-8** — When a Recruiter triggers manual re-screening (`POST /api/staff/applications/{id}/screen`), the request routes through the configured Gemini screening provider.
- **FR-9** — The Staff UI (`ui/staff`) displays category breakdown scores (Skills, Experience, Education) alongside the overall score in the `ScreeningReportCard` and `ScreeningReportModal`.
- **FR-10** — The test suite and local development environments remain fully functional offline using `MockScreeningService` or mocked HTTP handlers without requiring live Gemini API access.

## Non-Functional Requirements

- **NFR-1** — Gemini 2.0 Flash request formulation and structured response deserialization execute with minimal overhead, completing evaluation within normal LLM response latency (p95 < 5s).
- **NFR-2** — API keys are loaded via .NET configuration (`IOptions<GeminiOptions>` / user secrets / environment variables) and are never logged or returned in client responses (`meta/coding-standards.md`).
- **NFR-3** — Candidate CV text and PII are never logged in plaintext application telemetry (`meta/coding-standards.md`).
- **NFR-4** — Unit and integration tests remain 100% executable offline with zero external network dependencies.

## Acceptance Criteria

Each criterion is independently testable and traces to one or more FRs.

- **AC-1** *(FR-1, FR-3, FR-4)*
  - **Given** `Screening:Provider` is configured as `"Gemini"` with a valid API key, and an Application is submitted with a readable PDF CV
  - **When** automated screening executes
  - **Then** Gemini 2.0 Flash is invoked via typed `HttpClient`, and a `ScreeningReport` is persisted with status `Completed`, overall `Score` (0–100), `SkillsScore` (0–100), `ExperienceScore` (0–100), `EducationScore` (0–100), `Recommendation` (`Advance` or `Review`), summary, strengths, and concerns.

- **AC-2** *(FR-2, FR-10)*
  - **Given** `Screening:Provider` is configured as `"Mock"` or `Screening:Provider` is `"Gemini"` but the API key is not configured
  - **When** candidate screening executes
  - **Then** the system uses `MockScreeningService` without throwing configuration exceptions, logging an informational warning if fallback occurred.

- **AC-3** *(FR-3, FR-5)*
  - **Given** an Application in the initial Stage evaluated by Gemini 2.0 Flash
  - **When** Gemini returns an overall score ≥ 75 resulting in an `Advance` recommendation
  - **Then** the Application is auto-advanced to the next sequential Stage in the pipeline, and an append-only `StageTransition` is recorded with `ActorKind = System` and label `"AI Screening Agent"`.

- **AC-4** *(FR-3, FR-5)*
  - **Given** an Application in the initial Stage evaluated by Gemini 2.0 Flash
  - **When** Gemini returns an overall score < 75 resulting in a `Review` recommendation
  - **Then** the Application remains in the initial Stage, and no `StageTransition` is recorded.

- **AC-5** *(FR-6, FR-7)*
  - **Given** Gemini 2.0 Flash returns a rate limit (HTTP 429) or transient server error (HTTP 503)
  - **When** the screening service executes
  - **Then** it retries with backoff; if the failure persists, `ScreeningReport` status is set to `Failed` with `FailureReason` populated, and the Application remains intact in its initial stage.

- **AC-6** *(FR-7)*
  - **Given** an invalid API key, unparseable response, or non-transient HTTP 400/401/403 error from the Gemini API
  - **When** screening executes
  - **Then** the failure is caught, `ScreeningReport.Status` is set to `Failed` with error diagnostic in `FailureReason`, and no unhandled exception crashes the background processor.

- **AC-7** *(FR-8)*
  - **Given** an authenticated Recruiter calling `POST /api/staff/applications/{id}/screen`
  - **When** Gemini screening is active
  - **Then** a fresh evaluation is performed via Gemini 2.0 Flash, updating the `ScreeningReport` with fresh scores (including category breakdown) and returning HTTP 200 with the updated DTO.

- **AC-8** *(FR-4, FR-9)*
  - **Given** a staff member viewing an Application with a completed Gemini `ScreeningReport` in `ui/staff`
  - **When** they view the application detail page or open the screening report modal
  - **Then** the UI displays the overall Score, Skills Score, Experience Score, Education Score, Recommendation badge, Executive Summary, Strengths list, and Concerns list.

- **AC-9** *(FR-10, NFR-4)*
  - **Given** the test suite running with mock HTTP message handler and mock screening service
  - **When** unit and integration tests execute
  - **Then** all tests pass deterministically without live network calls to Google APIs.

## Edge Cases & Error States

| # | Situation | Expected behaviour |
|---|---|---|
| E-1 | Gemini API key invalid or unauthorized (HTTP 401/403) | Marks `ScreeningReport` as `Failed` with reason `"Gemini authentication failed: check API key"`; logs error without leaking secret key. |
| E-2 | Abnormally large CV text extracted from PDF | Payload is capped at reasonable character limit (e.g. 50,000 characters) before sending to Gemini to prevent memory exhaustion; Gemini 2.0 Flash easily accommodates large context windows. |
| E-3 | Gemini returns malformed or incomplete JSON | Deserialization failure is caught; `ScreeningReport` marked `Failed` with `"Invalid structured JSON response from AI provider"`. |
| E-4 | Multilingual CV text submitted | Gemini 2.0 Flash processes multilingual text natively and provides evaluation in English/requisition language. |
| E-5 | Gemini API quota exhausted (persistent HTTP 429) | Retries with backoff up to 2 times, then fails gracefully setting `Status = Failed` with quota exhaustion note. |

## Data Touched

| Entity | New / Existing | Notes |
|---|---|---|
| `ScreeningReport` | Existing | Adds `SkillsScore` (int?), `ExperienceScore` (int?), and `EducationScore` (int?) columns to store category breakdowns. |
| `Application` | Existing | Unchanged schema; navigates to `ScreeningReport`. |
| `StageTransition` | Existing | Unchanged schema; records auto-advance transitions. |

## Impacted Components

| Component | Change |
|---|---|
| `service/screening` | Add `GeminiScreeningService`, `GeminiOptions`, request/response models with JSON Schema; update `ScreeningResult` and `MockScreeningService` to include category scores. |
| `api/application` | Update `ScreeningReportDto` to return `SkillsScore`, `ExperienceScore`, and `EducationScore`. |
| `db/application` | Add category score properties to `ScreeningReport` entity and EF Core mapping; add migration `AddScreeningCategoryScores`. |
| `ui/staff` | Update `ScreeningReportCard`, `ScreeningReportModal`, and TypeScript types to render category score breakdowns (Skills, Experience, Education). |

## Out of Scope

- Audio/video interview screening.
- Candidate visibility into AI screening scores.
- Automated applicant rejection.
- Custom scoring rubric editor UI per requisition.

## Open Questions

None — all clarifications resolved, see `clarifications.md`.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0008` (Automated Candidate Screening) | 1 | Establishes `ScreeningReport`, `IScreeningService`, `ScreeningOrchestrator`, and baseline screening workflow. |
| `0004` (Application Submission and CV Upload) | 1 | Defines CV attachment storage and submission triggers. |
| `0005` (Pipeline Progression) | 1 | Defines pipeline stages, `StageTransition`, and auto-advancement semantics. |

Tier 0 was read in full: `meta/architecture.md`, `meta/tech-stack.md`, `meta/coding-standards.md`, `docs/specs/index.md`.
Considered and skipped: `0001`, `0002`, `0003`, `0006`, `0007`.
Cap reached: no (3 prior specs scored above threshold).
