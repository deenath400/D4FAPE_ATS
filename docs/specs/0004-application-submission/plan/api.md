# API Design — 0004 Application Submission and CV Upload

**Spec:** `../spec.md` · **Updated:** 2026-08-06

> **Convention inheritance.** `plan/api.md` of `0001`, `0002`, and `0003` were read in full.
> Every convention below is reused unchanged unless listed in §7.

---

## 1. Conventions In Force

| Concern | Convention | Established by |
|---|---|---|
| Base path | `/api` | 0001 |
| Casing | camelCase JSON bodies, kebab-case paths | 0001 |
| Auth | `Authorization: Bearer <jwt>` header | 0001 establishes header name; 0002 populates & validates; this spec adds no new auth mechanism, only consumes `CandidateOnly`/`StaffOnly` and a bare `RequireAuthorization()` (any authenticated role) |
| Errors | RFC 7807 ProblemDetails for every non-2xx response, `code` field for the machine-readable reason | 0001, reused via `AuthEndpoints.ToProblemResult()` unchanged |
| Dates | ISO-8601 UTC with `Z` | 0001/0002 |
| Pagination | `?page=1&pageSize=20`, envelope `{ items, page, pageSize, total }` — **not used here**; every list in this spec is unpaginated (a Candidate's own Applications and one Requisition's Applications are both small, bounded collections) | 0003 |
| Route nesting for a Requisition-scoped operation | `/api/requisitions/{id}/<sub-resource>` | 0003 |
| Request body encoding | `application/json` for every prior endpoint | 0001/0002/0003 — **this spec is the first to accept `multipart/form-data`** (the CV submission endpoint only; every other endpoint in this spec is a plain `GET`) |

## 2. Endpoint Summary

| # | Method | Path | Purpose | Auth | AC |
|---|---|---|---|---|---|
| 1 | POST | `/api/requisitions/{requisitionId}/applications` | Submit an Application with a CV | `CandidateOnly` | AC-1–AC-11 |
| 2 | GET | `/api/requisitions/{requisitionId}/applications` | Staff list of Applications for a Requisition | `StaffOnly` | AC-16–AC-19 |
| 3 | GET | `/api/applications/mine` | Candidate's own Applications | `CandidateOnly` | AC-12, AC-13 |
| 4 | GET | `/api/applications/{id}/cv` | Download a CV (owner Candidate or any Staff) | Authenticated (any role) | AC-14, AC-15, AC-20, AC-21 |

## 3. Endpoint Detail

### 3.1 `POST /api/requisitions/{requisitionId}/applications`

**Purpose.** A Candidate submits an Application, with exactly one CV file, to a `published`
Requisition.

**Path parameters**

| Name | Type | Notes |
|---|---|---|
| `requisitionId` | uuid | Target Requisition |

**Request body.** `multipart/form-data`, one part:

| Field | Type | Required | Rule |
|---|---|---|---|
| `cv` | file | Yes | PDF only, ≤ 5 MB, content verified by declared content-type, `.pdf` extension, and a magic-byte check |

**Responses**

| Status | Body | When |
|---|---|---|
| 201 | `ApplicationDto` | Created |
| 400 | ProblemDetails, `code: "application.submit.cv-required"` | No file attached, or an empty file (AC-2) |
| 400 | ProblemDetails, `code: "application.submit.invalid-file-type"` | Not a PDF by content-type, extension, or magic bytes (AC-3) |
| 400 | ProblemDetails, `code: "application.submit.file-too-large"` | File exceeds 5 MB (AC-4) |
| 401 | ProblemDetails | No/invalid bearer token (AC-10) |
| 403 | ProblemDetails | Token valid but not `Candidate` (AC-11) |
| 404 | ProblemDetails, `code: "application.submit.requisition-not-found"` | Requisition is `draft`, `closed`, or does not exist — identical body in all three cases (AC-5, AC-6, AC-7) |
| 409 | ProblemDetails, `code: "application.submit.duplicate"` | This Candidate already has an Application against this Requisition (AC-8) |
| 500 | ProblemDetails, `code: "application.submit.storage-failed"` | The CV could not be written to disk (E-4) |

**Success example (201)**

```json
{
  "id": "9c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f",
  "requisitionId": "1b2c3d4e-5f6a-7b8c-9d0e-1f2a3b4c5d6e",
  "candidateId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "submittedAtUtc": "2026-08-06T10:15:00Z",
  "cv": {
    "fileName": "jane-doe-resume.pdf",
    "contentType": "application/pdf",
    "sizeBytes": 184320
  }
}
```

**Error example (409)**

```json
{
  "type": "https://d4fape.ats/errors/application-submit-duplicate",
  "title": "Conflict",
  "status": 409,
  "code": "application.submit.duplicate",
  "detail": "You have already applied to this requisition."
}
```

**Error example (404 — identical for draft, closed, or missing)**

```json
{
  "type": "https://d4fape.ats/errors/application-submit-requisition-not-found",
  "title": "NotFound",
  "status": 404,
  "code": "application.submit.requisition-not-found",
  "detail": "This job posting is no longer available."
}
```

**Side effects.** Writes one CV file to local disk (`shared/storage`) and inserts one
`Applications` row + one `CvAttachments` row.

**Idempotency.** Non-idempotent — a second identical call by the same Candidate against the
same Requisition returns `409`, not a second row (structurally enforced, see `erd.md` §3.1).

---

### 3.2 `GET /api/requisitions/{requisitionId}/applications`

**Purpose.** Staff (Recruiter or HiringManager) lists Applications submitted against a
Requisition, with candidate identity, submission date, and a CV download link — no stage
grouping (Clarification C-2).

**Path parameters**

| Name | Type | Notes |
|---|---|---|
| `requisitionId` | uuid | Any status — staff visibility is not restricted to `published` |

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `StaffApplicationListItemDto[]` | Always for a valid Requisition id; `[]` if none exist (AC-18) |
| 401 | ProblemDetails | No/invalid bearer token |
| 403 | ProblemDetails, | Candidate token (AC-19) |
| 404 | ProblemDetails, `code: "application.list.requisition-not-found"` | No Requisition with that id (E-5) |

**Success example (200)**

```json
[
  {
    "id": "9c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f",
    "candidate": {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "firstName": "Jane",
      "lastName": "Doe",
      "email": "jane.doe@example.com"
    },
    "submittedAtUtc": "2026-08-06T10:15:00Z",
    "cvDownloadUrl": "/api/applications/9c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f/cv"
  }
]
```

**Side effects.** None — read-only. **Idempotency.** `GET`, naturally idempotent.

---

### 3.3 `GET /api/applications/mine`

**Purpose.** A Candidate lists their own submitted Applications: Requisition title and
submission date (Clarification C-3).

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `CandidateApplicationListItemDto[]` | Always for a valid Candidate token; `[]` if none exist (AC-13) |
| 401 | ProblemDetails | No/invalid bearer token |
| 403 | ProblemDetails | Staff token |

**Success example (200)**

```json
[
  {
    "id": "9c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f",
    "requisitionId": "1b2c3d4e-5f6a-7b8c-9d0e-1f2a3b4c5d6e",
    "requisitionTitle": "Senior Backend Engineer",
    "submittedAtUtc": "2026-08-06T10:15:00Z",
    "cvDownloadUrl": "/api/applications/9c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f/cv"
  }
]
```

**Side effects.** None. **Idempotency.** `GET`, naturally idempotent.

---

### 3.4 `GET /api/applications/{id}/cv`

**Purpose.** Downloads the CV file bytes for one Application — the owning Candidate, or any
Staff caller (FR-9, FR-11).

**Path parameters**

| Name | Type | Notes |
|---|---|---|
| `id` | uuid | Application id, not the CV's storage key — the storage key is never exposed to a client (NFR-2) |

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `application/pdf` bytes, `Content-Disposition: attachment; filename="<originalFileName>"` | Caller is the owning Candidate, or any Staff role (AC-14, AC-20) |
| 401 | ProblemDetails | No/invalid bearer token |
| 403 | ProblemDetails, `code: "application.cv.forbidden"` | Candidate token, but not the owner (AC-15, AC-21) |
| 404 | ProblemDetails, `code: "application.cv.not-found"` | No Application with that id (E-6) |

**Error example (403)**

```json
{
  "type": "https://d4fape.ats/errors/application-cv-forbidden",
  "title": "Forbidden",
  "status": 403,
  "code": "application.cv.forbidden",
  "detail": "You do not have access to this file."
}
```

**Side effects.** None — read-only. **Idempotency.** `GET`, naturally idempotent.

## 4. Shared Schemas

```ts
type ApplicationDto = {
  id: string;
  requisitionId: string;
  candidateId: string;
  submittedAtUtc: string; // ISO-8601 UTC
  cv: { fileName: string; contentType: string; sizeBytes: number };
};

type CandidateApplicationListItemDto = {
  id: string;
  requisitionId: string;
  requisitionTitle: string;
  submittedAtUtc: string; // ISO-8601 UTC
  cvDownloadUrl: string; // backend-relative path; frontend prefixes /api/bff/proxy
};

type StaffApplicationListItemDto = {
  id: string;
  candidate: { id: string; firstName: string; lastName: string; email: string };
  submittedAtUtc: string; // ISO-8601 UTC
  cvDownloadUrl: string;
};

type ProblemDetails = {
  type: string;
  title: string;
  status: number;
  code: string;
  detail?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
};
```

## 5. Authorization Matrix

| Endpoint | Anonymous | Candidate | Recruiter | HiringManager |
|---|---|---|---|---|
| `POST /api/requisitions/{id}/applications` | 401 | Allowed | 403 | 403 |
| `GET /api/requisitions/{id}/applications` | 401 | 403 | Allowed | Allowed |
| `GET /api/applications/mine` | 401 | Allowed (own only) | 403 | 403 |
| `GET /api/applications/{id}/cv` | 401 | Allowed if owner, else 403 | Allowed (any) | Allowed (any) |

## 6. Events Published

None. No `worker/*` component exists (per `architecture.md`), and no side effect in this spec
needs to be announced beyond the rows it writes.

## 7. Deviations From Inherited Conventions

| Convention | Deviation | Reason |
|---|---|---|
| Request body encoding — every prior endpoint is `application/json` | `POST /api/requisitions/{id}/applications` accepts `multipart/form-data` | The endpoint's whole purpose is a file upload; there is no JSON-representable way to carry binary CV bytes. No existing JSON endpoint is affected — `Content-Type` continues to be forwarded verbatim by `ui/bff`'s proxy (see `plan/hld.md` D-4) regardless of value |
| `ui/bff`'s proxy route reads/writes bodies as text | Generalised to `ArrayBuffer` passthrough (request and response), with `Content-Disposition` now forwarded | Required for this spec's upload and CV-download traffic to survive the proxy without corruption; documented as HLD Design Decision D-4, not repeated here. Strictly backward-compatible — existing JSON traffic is unaffected |

Since this table is non-empty, note for future specs: `ui/bff`'s proxy is no longer
text-body-only. Any future binary endpoint (e.g. a future attachment type) can reuse it as-is.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0003` (Requisition Management) | 1 | Reused unchanged: `/api` base path, camelCase JSON, ProblemDetails envelope, `MapGroup` route-nesting style for a Requisition-scoped sub-resource. Its `GET /api/public/requisitions/{id}` 404-for-draft-or-closed-or-missing pattern is reused verbatim for FR-4. |
| `0002` (User Authentication and Refresh Token Flow) | 1 | Reused unchanged: `Authorization: Bearer` header, `CandidateOnly`/`StaffOnly` policy names, 401-vs-403 behaviour for unauthenticated vs wrong-role. |
| `0001` (Project Scaffolding and Walking Skeleton) | 1 | Reused unchanged: `/api` base path, ProblemDetails shape, `traceId` convention. Its `ui/bff` proxy is the component this spec's Deviation (above) modifies. |
