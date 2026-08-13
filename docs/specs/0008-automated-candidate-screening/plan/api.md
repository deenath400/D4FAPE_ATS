# API Design — 0008 Automated Candidate Screening

**Spec:** `../spec.md` · **Updated:** 2026-08-14

> **Convention inheritance.** `plan/api.md` of `0004`, `0005` read in full. Every convention
> below is reused unchanged unless listed in §7.

---

## 1. Conventions In Force

| Concern | Convention | Established by |
|---|---|---|
| Base path | `/api` | 0001 |
| Casing | camelCase JSON bodies, kebab-case paths | 0001 |
| Auth | `Authorization: Bearer <jwt>` header | 0001; 0002 populates & validates; this spec adds no new auth mechanism, only consumes `RecruiterOnly`/`StaffOnly` unchanged |
| Errors | RFC 7807 ProblemDetails for every non-2xx response, `code` field for the machine-readable reason | 0001, reused via `AuthEndpoints.ToProblemResult()` |
| Dates | ISO-8601 UTC with `Z` | 0001/0002 |
| Top-level addressing for an Application by id | `/api/applications/{id}/<sub-resource>` | 0004 — reused for `/screening-report` and `/screen` |
| Staff-scoped sub-path | `/api/staff/applications/{id}/<sub-resource>` | **Established by this spec** — see §7 |

## 2. Endpoint Summary

| # | Method | Path | Purpose | Auth | AC |
|---|---|---|---|---|---|
| 1 | GET | `/api/staff/applications/{applicationId}/screening-report` | Retrieve screening report | `StaffOnly` | AC-7, AC-8 |
| 2 | POST | `/api/staff/applications/{applicationId}/screen` | Trigger re-screening | `RecruiterOnly` | AC-5, AC-6, AC-9 |

## 3. Endpoint Detail

### 3.1 `GET /api/staff/applications/{applicationId}/screening-report`

**Purpose.** Staff (Recruiter or HiringManager) retrieves the full screening evaluation for
one Application (FR-10).

**Path parameters**

| Name | Type | Notes |
|---|---|---|
| `applicationId` | uuid | The Application to retrieve the screening report for |

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `ScreeningReportDto` | A `ScreeningReport` exists for this Application (any status: Pending, Completed, Failed) |
| 401 | ProblemDetails | No/invalid bearer token |
| 403 | ProblemDetails | Candidate token (AC-8) |
| 404 | ProblemDetails, `code: "screening.report.not-found"` | No Application with that id, or no ScreeningReport exists for the Application |

**Success example (200)**

```json
{
  "id": "f1a2b3c4-d5e6-f7a8-b9c0-d1e2f3a4b5c6",
  "applicationId": "9c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f",
  "score": 85,
  "recommendation": "Advance",
  "summary": "Strong match for the Senior Backend Engineer position. Candidate demonstrates extensive experience with .NET and distributed systems.",
  "strengths": [
    "8+ years of .NET experience",
    "Distributed systems expertise",
    "Strong architectural background"
  ],
  "concerns": [
    "Limited frontend experience mentioned",
    "No explicit cloud infrastructure certifications"
  ],
  "status": "Completed",
  "failureReason": null,
  "evaluatedAtUtc": "2026-08-14T10:15:30Z"
}
```

**Error example (404)**

```json
{
  "type": "https://d4fape.ats/errors/screening-report-not-found",
  "title": "NotFound",
  "status": 404,
  "code": "screening.report.not-found",
  "detail": "No screening report found for this application."
}
```

**Side effects.** None — read-only. **Idempotency.** `GET`, naturally idempotent.

---

### 3.2 `POST /api/staff/applications/{applicationId}/screen`

**Purpose.** A Recruiter triggers a re-run of automated screening on an Application (FR-7).

**Path parameters**

| Name | Type | Notes |
|---|---|---|
| `applicationId` | uuid | The Application to re-screen |

**Request body.** None — `Content-Type` header not required.

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `ScreeningReportDto` | Screening re-run initiated and completed; returns the new report |
| 401 | ProblemDetails | No/invalid bearer token |
| 403 | ProblemDetails | Token valid but not `Recruiter` (AC-6) — includes HiringManager |
| 404 | ProblemDetails, `code: "screening.run.not-found"` | No Application with that id |
| 409 | ProblemDetails, `code: "screening.run.already-rejected"` | Application is rejected |

**Success example (200)**

```json
{
  "id": "a1b2c3d4-e5f6-a7b8-c9d0-e1f2a3b4c5d6",
  "applicationId": "9c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f",
  "score": 78,
  "recommendation": "Advance",
  "summary": "Re-screening confirms strong match...",
  "strengths": ["Relevant backend experience", "Clean code practices"],
  "concerns": ["Limited DevOps exposure"],
  "status": "Completed",
  "failureReason": null,
  "evaluatedAtUtc": "2026-08-14T11:00:00Z"
}
```

**Error example (409)**

```json
{
  "type": "https://d4fape.ats/errors/screening-run-already-rejected",
  "title": "Conflict",
  "status": 409,
  "code": "screening.run.already-rejected",
  "detail": "Cannot screen a rejected application."
}
```

**Side effects.** Deletes the existing `ScreeningReport` (if any), creates a new one, and
may auto-advance the Application per FR-3/FR-8 rules. Writes one `StageTransition` row if
auto-advance occurs.

**Idempotency.** Not idempotent — each call produces a new evaluation (the AI may return
different results). However, the spec requires serialization of concurrent re-screens for the
same Application (E-5); concurrent calls are rejected or serialized.

## 4. Shared Schemas

```ts
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
  evaluatedAtUtc: string; // ISO-8601 UTC
};
```

**Modified schemas (additive fields only):**

```ts
// PipelineBoardApplicationDto (0005, modified — additive fields)
type PipelineBoardApplicationDto = {
  // ... existing fields unchanged
  screeningScore: number | null;
  screeningRecommendation: "Advance" | "Review" | null;
  screeningStatus: "Pending" | "Completed" | "Failed" | null;
};

// StaffApplicationListItemDto (0004, modified — additive fields)
type StaffApplicationListItemDto = {
  // ... existing fields unchanged
  screeningScore: number | null;
  screeningRecommendation: "Advance" | "Review" | null;
  screeningStatus: "Pending" | "Completed" | "Failed" | null;
};
```

## 5. Authorization Matrix

| Endpoint | Anonymous | Candidate | Recruiter | HiringManager |
|---|---|---|---|---|
| `GET /api/staff/applications/{id}/screening-report` | 401 | 403 | Allowed | Allowed |
| `POST /api/staff/applications/{id}/screen` | 401 | 403 | Allowed | 403 |

## 6. Events Published

None. The auto-advance on screening completion is a synchronous call within the background
task, not an event. No `worker/*` component exists (per `architecture.md`).

## 7. Deviations From Inherited Conventions

| Convention | Deviation | Reason |
|---|---|---|
| Application endpoints use `/api/applications/{id}/...` or `/api/requisitions/{id}/applications` | This spec introduces `/api/staff/applications/{id}/...` for staff-only screening operations | The spec explicitly names `POST /api/staff/applications/{id}/screen` (AC-5) and `GET /api/staff/applications/{id}/screening-report` (AC-7). The `/staff/` segment makes the authorization intent self-documenting and avoids collision with the existing `GET /api/applications/{id}/cv` endpoint (0004) which serves both roles. Future staff-only Application operations can reuse this prefix. |

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0004` (Application Submission and CV Upload) | 1 | Reused unchanged: `/api/applications/{id}/...` addressing (extended with `/staff/` prefix here), ProblemDetails envelope, `StaffOnly`/`CandidateOnly`/`RecruiterOnly` policy names. Modified: `StaffApplicationListItemDto` gains additive screening badge fields. |
| `0005` (Pipeline Progression) | 1 | Reused unchanged: ProblemDetails envelope, `StaffOnly` policy on pipeline board. Modified: `PipelineBoardApplicationDto` gains additive screening badge fields. |
| `0002` (User Authentication and Refresh Token Flow) | 1 | Reused unchanged: `Authorization: Bearer` header, `RecruiterOnly`/`StaffOnly` policy names, 401-vs-403 behaviour. |
