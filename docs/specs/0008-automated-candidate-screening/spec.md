---
id: 0008
slug: automated-candidate-screening
title: Automated Candidate Screening
status: implementing
components: [api/application, service/application, service/pipeline, service/screening, db/application, db/pipeline, shared/storage, ui/staff]
entities: [Application, CvAttachment, Requisition, Stage, StageTransition, ScreeningReport]
depends_on: [0004, 0005]
created: 2026-08-14
updated: 2026-08-14
---

# Automated Candidate Screening

## Problem & Context

When candidates submit applications with their CVs (`0004`), every applicant currently lands in the first stage of the Requisition's pipeline (`0005`, default `Applied`). In high-volume hiring listings, Recruiters and Hiring Managers must manually open each PDF CV attachment, read through qualifications, match them against job requirements, and manually advance candidates one by one.

This manual triage creates significant latency in candidate evaluation and consumes substantial staff time on initial qualification checks. Recruiters need an automated screening agent that extracts text from the applicant's uploaded CV, evaluates candidate qualifications against the Requisition's title and description, produces a structured evaluation report (with an overall score, match summary, key strengths, potential gaps/concerns, and a recommendation), and automatically advances highly qualified candidates to the next pipeline stage while keeping borderline or unqualified applicants in place for human review.

This spec introduces automated candidate screening triggered upon application submission, an on-demand re-screening capability for staff, and staff-facing visibility into screening scores and reports on both the pipeline board and application detail views.

## Goals

- **G-1** Automatically screen every candidate upon application submission by evaluating their uploaded CV against the target Requisition's job title and description.
- **G-2** Generate a structured screening report capturing an overall score (0–100), qualification summary, identified strengths, identified concerns/gaps, and a progression recommendation (`Advance` or `Review`).
- **G-3** Automatically advance qualified candidates (`Advance` recommendation) from the initial pipeline stage to the next sequential pipeline stage, recording an audited system transition (`ActorKind = System`).
- **G-4** Keep borderline/unqualified candidates in their initial stage for human review without automated rejection.
- **G-5** Provide Recruiters and Hiring Managers with clear screening indicators on the pipeline board and a full screening report view on the application detail page, with an on-demand "Re-run Screening" action for Recruiters.

## Non-Goals

- **Automated rejection** — Candidates with low or borderline screening scores are never automatically rejected by the agent; rejection remains an explicit human recruiter action (`0005` FR-10).
- **Candidate visibility into AI screening reports** — Screening scores, detailed rationale, strengths, and concerns are strictly internal to Staff (`Recruiter` and `HiringManager`); candidates only see their stage name or general status (`0004` FR-8, `0005` FR-17).
- **Custom prompt templates per requisition** — Screening uses the Requisition's title and job description directly; arbitrary prompt scripting or custom scoring rubrics are deferred.
- **External ATS integrations or third-party background check automation** — The feature evaluates internal ATS data only.
- **Non-PDF CV parsing** — Only PDF attachments (`0004` FR-3) are supported for text extraction.

## Users & Personas

| Persona | Need this feature serves |
|---|---|
| Recruiter | Receives immediate, objective screening evaluations on incoming applicants, has top candidates auto-advanced to the next stage, and can review structured insights before interviewing. |
| HiringManager | Reviews applicant match summaries and strengths/concerns directly from the pipeline board and application views. |
| Candidate | Benefits from faster initial application processing without exposure to internal scoring metrics. |

## Functional Requirements

- **FR-1** — When a Candidate submits an Application (`0004`), the system automatically initiates automated screening against the target Requisition's title and job description using the text extracted from the uploaded PDF CV.
- **FR-2** — Automated screening generates a persistent `ScreeningReport` for the Application, containing:
  - An overall numerical score from `0` to `100`.
  - A recommendation outcome: `Advance` (score meets or exceeds qualification threshold, default ≥ 75) or `Review` (score below threshold).
  - An executive match summary (markdown/plain text).
  - A list of identified candidate strengths relevant to the job.
  - A list of identified concerns, missing qualifications, or skill gaps.
  - The UTC timestamp when the evaluation was completed.
  - An evaluation status (`Pending`, `Completed`, `Failed`).
- **FR-3** — If screening produces an `Advance` recommendation and the Application is currently in the Requisition's initial stage, the system automatically advances the Application to the next sequential Stage in the Requisition's pipeline.
- **FR-4** — When an automated stage advance occurs, the system writes an append-only `StageTransition` record (`0005` FR-12) with `ActorKind = System`, null `UserId`, and display name `"AI Screening Agent"`, including a transition note indicating the screening score and recommendation.
- **FR-5** — If screening produces a `Review` recommendation, the Application remains in its current Stage and no automated stage move occurs.
- **FR-6** — If text extraction fails or the AI evaluation service fails, the `ScreeningReport` status is set to `Failed` with an error message, the Application remains in its current Stage, and staff are notified via the UI.
- **FR-7** — Staff holding the `Recruiter` role can manually trigger a re-run of automated screening on any non-rejected Application from the staff workspace.
- **FR-8** — Re-running screening updates or replaces the Application's `ScreeningReport` with a new evaluation; if the Application is still in the initial Stage and achieves an `Advance` recommendation, it is auto-advanced per FR-3. If it has already moved beyond the initial stage or been rejected, its stage is not changed.
- **FR-9** — Staff (Recruiters and Hiring Managers) can view the screening summary badge (score and recommendation) on the Staff Pipeline Board (`0005` FR-15) and Applications table (`0004` FR-10).
- **FR-10** — Staff (Recruiters and Hiring Managers) can view the complete `ScreeningReport` (score, summary, strengths, concerns, and evaluation timestamp) on the Application detail view in `/staff`.
- **FR-11** — Candidates cannot retrieve or view the `ScreeningReport` or screening scores for any Application, including their own (`StaffOnly` policy enforcement).
- **FR-12** — The AI screening service is implemented behind a pluggable abstraction (`IScreeningService`) with support for external LLM evaluation as well as a deterministic test/mock provider for offline development and CI environments.

## Non-Functional Requirements

- **NFR-1** — Application submission response time is not blocked by synchronous LLM processing: the initial submission returns HTTP 201 Created promptly (`0004` AC-1), while screening executes via a background task or immediate asynchronous queue without holding the candidate's HTTP connection open.
- **NFR-2** — In the event of an unrecoverable AI service outage or timeout, the Application submission itself remains successful and persistent (`0004` NFR-1 invariant preserved); the `ScreeningReport` records `Failed` state gracefully.
- **NFR-3** — Candidate PII in CV text is only sent to the configured AI screening provider and is never logged in plaintext application telemetry (`meta/coding-standards.md`).

## Acceptance Criteria

Each criterion is independently testable and traces to one or more FRs.

- **AC-1** *(FR-1, FR-2)*
  - **Given** a published Requisition and a submitted Application with a readable PDF CV
  - **When** automated screening completes
  - **Then** a `ScreeningReport` is persisted for the Application with status `Completed`, a score between 0 and 100, recommendation (`Advance` or `Review`), summary, strengths list, and concerns list.

- **AC-2** *(FR-3, FR-4)*
  - **Given** an Application in the initial Stage of a Requisition with at least two Stages
  - **When** automated screening completes with an `Advance` recommendation (score ≥ 75)
  - **Then** the Application's current Stage is updated to the second Stage in the pipeline, and an append-only `StageTransition` is recorded with `ActorKind = System` and label `"AI Screening Agent"`.

- **AC-3** *(FR-5)*
  - **Given** an Application in the initial Stage of a Requisition
  - **When** automated screening completes with a `Review` recommendation (score < 75)
  - **Then** the Application remains in the initial Stage, and no `StageTransition` is recorded.

- **AC-4** *(FR-6, NFR-2)*
  - **Given** an Application submitted with an unreadable or encrypted PDF CV, or an AI provider outage
  - **When** automated screening executes
  - **Then** the Application submission remains intact, the `ScreeningReport` status is set to `Failed` with a failure reason, and the Application remains in the initial Stage.

- **AC-5** *(FR-7, FR-8)*
  - **Given** an authenticated Recruiter viewing an Application in the staff workspace
  - **When** they trigger `POST /api/staff/applications/{id}/screen`
  - **Then** the system re-runs the screening evaluation and returns HTTP 200 with the updated `ScreeningReport`.

- **AC-6** *(FR-7)*
  - **Given** an authenticated HiringManager or Candidate
  - **When** they attempt to call `POST /api/staff/applications/{id}/screen`
  - **Then** the API returns HTTP 403 Forbidden (`RecruiterOnly` policy enforced).

- **AC-7** *(FR-9, FR-10)*
  - **Given** an authenticated Staff member (Recruiter or HiringManager)
  - **When** they request `GET /api/staff/applications/{id}/screening-report` or retrieve the pipeline board
  - **Then** the API returns HTTP 200 with the complete screening evaluation details and score badges.

- **AC-8** *(FR-11)*
  - **Given** an authenticated Candidate
  - **When** they attempt to request `GET /api/staff/applications/{id}/screening-report` or `GET /api/applications/mine`
  - **Then** access to the screening report is blocked with HTTP 403 Forbidden, and the candidate's own application list does not expose the `ScreeningReport`, internal score, or AI notes.

- **AC-9** *(FR-8)*
  - **Given** an Application that has already been moved to a later Stage (e.g. Interview) by a recruiter
  - **When** a Recruiter triggers a re-screen resulting in an `Advance` recommendation
  - **Then** the `ScreeningReport` is updated, but the Application's current Stage is NOT altered or regressed.

- **AC-10** *(FR-12)*
  - **Given** the application running in a test or offline environment with the Mock screening provider active
  - **When** screening executes for a test application
  - **Then** deterministic scores and evaluations are produced without external network requests or API keys.

## Edge Cases & Error States

| # | Situation | Expected behaviour |
|---|---|---|
| E-1 | Uploaded PDF contains scanned images without OCR / no extractable text | Screening records status `Failed` with reason `"No extractable text found in CV attachment"`; Application remains in current stage for manual recruiter review. |
| E-2 | AI screening service times out or returns HTTP 5xx | System retries once; if still failing, marks `ScreeningReport` as `Failed`; does not crash submission or block recruiter operations. |
| E-3 | Requisition has only 1 Stage configured in its pipeline | Auto-advance cannot move to a next stage; `ScreeningReport` completes with `Advance` recommendation, but Application stays in the single stage and an informational note is logged. |
| E-4 | Requisition is closed or Application is rejected before async screening finishes | Screening report is saved for historical record, but no stage transition occurs. |
| E-5 | Concurrent re-screen requests triggered for the same Application | System serializes or prevents duplicate concurrent execution; returns existing in-progress or completed evaluation. |

## Data Touched

| Entity | New / Existing | Notes |
|---|---|---|
| `ScreeningReport` | New | Id, ApplicationId (1-to-1 FK), Score (int), Recommendation (enum: Advance, Review), Summary (string), Strengths (JSON array / string), Concerns (JSON array / string), Status (enum: Pending, Completed, Failed), FailureReason (nullable string), EvaluatedAtUtc (DateTime). |
| `Application` | Existing | Navigation property to optional `ScreeningReport`. |
| `StageTransition` | Existing | Existing `ActorKind` enum (`System`) and display label used for automated auto-advance transitions. |

## Impacted Components

| Component | Change |
|---|---|
| `api/application` | Add Staff endpoints: `GET /api/staff/applications/{id}/screening-report` (`StaffOnly`) and `POST /api/staff/applications/{id}/screen` (`RecruiterOnly`). |
| `service/application` | Text extraction from stored PDF CV, `IScreeningService` orchestration, `ScreeningReport` lifecycle, background screening trigger. |
| `service/pipeline` | Helper for auto-advancing Application to next Stage on `Advance` recommendation. |
| `db/application` | `ScreeningReport` entity, EF Core configuration, 1-to-1 relationship with `Application`, migration. |
| `shared/storage` | Reused to read persisted PDF CV stream for text parsing. |
| `ui/staff` | Pipeline Board candidate card score badge, Applications table screening badge, Application detail screening report drawer/tab, "Re-run Screening" action button. |

## Out of Scope

- Automated email notifications to candidates about screening results.
- Auto-rejection of applicants.
- Video or audio interview screening.
- Custom scoring rubrics or prompt engineering UI per job listing.
- OCR for image-only scanned PDFs (plain PDF text extraction is supported).

## Open Questions

None — all clarifications resolved, see `clarifications.md`.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0004` (Application Submission and CV Upload) | 1 | Defines `Application`, `CvAttachment`, `shared/storage`, and submission workflow that triggers screening. |
| `0005` (Pipeline Progression) | 1 | Defines `Stage`, `StageTransition`, `ActorKind.System`, pipeline board, and stage advance semantics. |
| `0003` (Requisition Management) | 1 | Defines `Requisition` job details (title and description) used as the baseline evaluation criteria. |

Tier 0 was read in full: `meta/architecture.md`, `meta/tech-stack.md`, `meta/coding-standards.md`, `docs/specs/index.md`.
Considered and skipped: `0001`, `0002`, `0006`, `0007` (auth/infra/seed specs).
Cap reached: no (3 prior specs scored above threshold).
