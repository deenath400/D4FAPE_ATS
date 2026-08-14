# API Design — 0009 Google Gemini 2.0 Flash Candidate Screening Integration

**Spec:** `../spec.md` · **Updated:** 2026-08-14

> **Convention inheritance.** `plan/api.md` of `0008`, `0004`, `0005` read in full. Every
> convention below is reused unchanged unless listed in §7.

---

## 1. Conventions In Force

| Concern | Convention | Established by |
|---|---|---|
| Base path | `/api` | 0001 |
| Casing | camelCase JSON bodies, kebab-case paths | 0001 |
| Auth | `Authorization: Bearer <jwt>` header | 0001; 0002 populates & validates |
| Errors | RFC 7807 ProblemDetails for every non-2xx response | 0001 |
| Dates | ISO-8601 UTC with `Z` | 0001/0002 |
| Staff-scoped sub-path | `/api/staff/applications/{id}/<sub-resource>` | 0008 |

## 2. Endpoint Summary

No new endpoints. Two existing endpoints return an expanded DTO:

| # | Method | Path | Purpose | Auth | Change in 0009 |
|---|---|---|---|---|---|
| 1 | GET | `/api/staff/applications/{applicationId}/screening-report` | Retrieve screening report | `StaffOnly` | Response DTO gains `skillsScore`, `experienceScore`, `educationScore` |
| 2 | POST | `/api/staff/applications/{applicationId}/screen` | Trigger re-screening | `RecruiterOnly` | Response DTO gains `skillsScore`, `experienceScore`, `educationScore` |

## 3. Endpoint Detail

### 3.1 `GET /api/staff/applications/{applicationId}/screening-report`

Unchanged from 0008 except the response body adds three nullable fields.

**Success example (200)**

```json
{
  "id": "f1a2b3c4-d5e6-f7a8-b9c0-d1e2f3a4b5c6",
  "applicationId": "9c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f",
  "score": 85,
  "recommendation": "Advance",
  "summary": "Strong match for the Senior Backend Engineer position. Candidate demonstrates extensive .NET expertise with distributed systems architecture background.",
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
  "evaluatedAtUtc": "2026-08-14T10:15:30Z",
  "skillsScore": 90,
  "experienceScore": 82,
  "educationScore": 75
}
```

When evaluated by MockScreeningService (no category breakdown available) or for pre-0009 reports:

```json
{
  "...": "...",
  "skillsScore": null,
  "experienceScore": null,
  "educationScore": null
}
```

### 3.2 `POST /api/staff/applications/{applicationId}/screen`

Unchanged from 0008 except the response body adds the same three nullable fields as 3.1.

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
  evaluatedAtUtc: string;           // ISO-8601 UTC
  skillsScore: number | null;       // NEW (0009)
  experienceScore: number | null;   // NEW (0009)
  educationScore: number | null;    // NEW (0009)
};
```

**Modified schemas (additive fields only):**

The `PipelineBoardApplicationDto` and `StaffApplicationListItemDto` schemas from 0008 are **unchanged** — they carry only `screeningScore`, `screeningRecommendation`, and `screeningStatus` (the overall score and recommendation). Category breakdown scores are available only via the full `ScreeningReportDto` from the dedicated endpoint or the Application detail page. This avoids bloating the board/list responses.

## 5. Authorization Matrix

Unchanged from 0008:

| Endpoint | Anonymous | Candidate | Recruiter | HiringManager |
|---|---|---|---|---|
| `GET /api/staff/applications/{id}/screening-report` | 401 | 403 | Allowed | Allowed |
| `POST /api/staff/applications/{id}/screen` | 401 | 403 | Allowed | 403 |

## 6. Events Published

None. Unchanged from 0008.

## 7. Deviations From Inherited Conventions

None — this spec widens an existing DTO with additive nullable fields, following the same pattern 0008 used when adding screening fields to `PipelineBoardApplicationDto` and `StaffApplicationListItemDto`.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0008` (Automated Candidate Screening) | 1 | Established the screening endpoint shapes, `ScreeningReportDto`, `StaffOnly`/`RecruiterOnly` policies, and the `/api/staff/applications/{id}/...` convention this spec extends. |
| `0004` (Application Submission and CV Upload) | 1 | Established the `StaffApplicationListItemDto` additive-field pattern and ProblemDetails envelope. |
| `0005` (Pipeline Progression) | 1 | Established the `PipelineBoardApplicationDto` additive-field pattern. |

Tier 0 read in full.
Considered and skipped: `0001`, `0002`, `0003`, `0006`, `0007`.
Cap reached: no.
