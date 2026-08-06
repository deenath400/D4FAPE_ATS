# API Design — 0005 Pipeline Progression

**Spec:** `../spec.md` · **Updated:** 2026-08-06

> **Convention inheritance.** `plan/api.md` of `0002`, `0003`, `0004` read in full. Every
> convention below is reused unchanged unless listed in §7.

---

## 1. Conventions In Force

| Concern | Convention | Established by |
|---|---|---|
| Base path | `/api` | 0001 |
| Casing | camelCase JSON bodies, kebab-case paths | 0001 |
| Auth | `Authorization: Bearer <jwt>` header | 0001 establishes header name; 0002 populates & validates; this spec adds no new auth mechanism, only consumes `RecruiterOnly`/`StaffOnly`/`CandidateOnly` unchanged |
| Errors | RFC 7807 ProblemDetails for every non-2xx response, `code` field for the machine-readable reason | 0001, reused via `AuthEndpoints.ToProblemResult()` |
| Dates | ISO-8601 UTC with `Z` | 0001/0002 |
| Pagination | `?page=1&pageSize=20`, envelope `{ items, page, pageSize, total }` — **not used here**; the pipeline board and Stage lists are small, bounded collections (NFR-1: ≤500 Applications, no pagination in this spec) | 0003 |
| Route nesting for a Requisition-scoped operation | `/api/requisitions/{id}/<sub-resource>` | 0003 |
| Top-level addressing for an `Application` by id | `/api/applications/{id}/<sub-resource>` | 0004 — reused for `move`/`reject`/`transitions` rather than re-nesting under `/requisitions/{id}` (HLD D-6) |
| Verb-suffixed `POST` for a state-transition action | `/api/requisitions/{id}/publish` | 0003 — reused for `/api/applications/{id}/move` and `/{id}/reject`, which are transitions, not resource CRUD |
| ProblemDetails carries extra machine-readable context beyond `code`/`errors` | `Result`/`ToProblemResult()` gain an optional `Extensions` dictionary, merged into `ProblemDetails.Extensions` | **Established by this spec** (HLD D-7) — used for the move-conflict response's `actualCurrentStageId`/`actualCurrentStageName` |

## 2. Endpoint Summary

| # | Method | Path | Purpose | Auth | AC |
|---|---|---|---|---|---|
| 1 | POST | `/api/requisitions/{requisitionId}/stages` | Add a Stage | `RecruiterOnly` | AC-1, AC-2 |
| 2 | GET | `/api/requisitions/{requisitionId}/stages` | List Stages in pipeline order | `StaffOnly` | AC-9 |
| 3 | PUT | `/api/requisitions/{requisitionId}/stages/{stageId}` | Rename a Stage | `RecruiterOnly` | AC-3, AC-31 |
| 4 | PUT | `/api/requisitions/{requisitionId}/stages/reorder` | Reorder all Stages | `RecruiterOnly` | AC-4 |
| 5 | DELETE | `/api/requisitions/{requisitionId}/stages/{stageId}` | Remove a Stage | `RecruiterOnly` | AC-5, AC-6 |
| 6 | POST | `/api/applications/{applicationId}/move` | Move an Application to another Stage | `RecruiterOnly` | AC-11, AC-12, AC-13, AC-15, AC-16, AC-29 |
| 7 | POST | `/api/applications/{applicationId}/reject` | Reject an Application | `RecruiterOnly` | AC-14, AC-15, AC-16, AC-30 |
| 8 | GET | `/api/requisitions/{requisitionId}/pipeline` | Staff pipeline board grouped by Stage | `StaffOnly` | AC-18, AC-19, AC-27 |
| 9 | GET | `/api/applications/{applicationId}/transitions` | Transition history of one Application | `StaffOnly` | AC-20, AC-21, AC-27, AC-30 |
| 10 | GET | `/api/applications/mine` | *(0004, modified)* Candidate's own Applications — now includes status | `CandidateOnly` | AC-22, AC-23 |

## 3. Endpoint Detail

### 3.1 `POST /api/requisitions/{requisitionId}/stages`

**Purpose.** Adds a Stage to a Requisition's pipeline at a given position (FR-1).

**Request body**

```json
{ "name": "Technical Screen", "position": 1 }
```

| Field | Type | Required | Rule |
|---|---|---|---|
| `name` | string | Yes | Non-empty, ≤200 chars, unique within the Requisition (case-insensitive) |
| `position` | int | No | 0-based; clamped to `[0, currentStageCount]`; omitted ⇒ appended at the end |

**Responses**

| Status | Body | When |
|---|---|---|
| 201 | `StageDto` | Created |
| 400 | ProblemDetails, `code: "stage.add.validation-failed"` | Empty/oversized `name` |
| 401 | ProblemDetails | No/invalid bearer token |
| 403 | ProblemDetails | Token valid but not `Recruiter` (AC-2) |
| 404 | ProblemDetails, `code: "stage.add.requisition-not-found"` | No Requisition with that id |
| 409 | ProblemDetails, `code: "stage.add.requisition-closed"` | Requisition is `closed` (AC-28) |
| 409 | ProblemDetails, `code: "stage.add.duplicate-name"` | Name already used (case-insensitive) in this Requisition (AC-31) |

**Success example (201)**

```json
{ "id": "9c1d...", "requisitionId": "1b2c...", "name": "Technical Screen", "sortOrder": 1 }
```

**Side effects.** Inserts one `Stages` row; shifts `SortOrder` of every Stage at or after
`position` up by one, keeping the sequence contiguous `0..N-1`.

**Idempotency.** Non-idempotent — each call adds a new Stage.

---

### 3.2 `GET /api/requisitions/{requisitionId}/stages`

**Purpose.** Staff (Recruiter or HiringManager) retrieves a Requisition's Stages in pipeline
order (FR-6).

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `StageDto[]` | Always for a valid Requisition id, ordered by `sortOrder` ascending |
| 401 | ProblemDetails | No/invalid bearer token |
| 403 | ProblemDetails | Candidate token |
| 404 | ProblemDetails, `code: "stage.list.requisition-not-found"` | No Requisition with that id |

**Side effects.** None. **Idempotency.** `GET`, naturally idempotent.

---

### 3.3 `PUT /api/requisitions/{requisitionId}/stages/{stageId}`

**Purpose.** Renames a Stage without touching which Applications are assigned to it (FR-2).

**Request body**

```json
{ "name": "Phone Screen" }
```

| Field | Type | Required | Rule |
|---|---|---|---|
| `name` | string | Yes | Non-empty, ≤200 chars, unique within the Requisition excluding this Stage itself |

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `StageDto` | Renamed |
| 400 | ProblemDetails, `code: "stage.rename.validation-failed"` | Empty/oversized `name` |
| 401 / 403 | ProblemDetails | Auth failures, same as 3.1 |
| 404 | ProblemDetails, `code: "stage.rename.not-found"` | No Requisition or Stage with that id, or the Stage does not belong to this Requisition |
| 409 | ProblemDetails, `code: "stage.rename.requisition-closed"` | Requisition is `closed` |
| 409 | ProblemDetails, `code: "stage.rename.duplicate-name"` | Name collides with another Stage in the same Requisition (AC-31) |

**Side effects.** Updates `Name`/`NormalizedName`. `SortOrder` and every Application's
`CurrentStageId` currently pointing at this Stage are unchanged (AC-3).

**Idempotency.** Naturally idempotent — submitting the same name twice leaves the same state.

---

### 3.4 `PUT /api/requisitions/{requisitionId}/stages/reorder`

**Purpose.** Reorders a Requisition's entire Stage set in one call (FR-3).

**Request body**

```json
{ "stageIds": ["a1...", "b2...", "c3...", "d4..."] }
```

| Field | Type | Required | Rule |
|---|---|---|---|
| `stageIds` | uuid[] | Yes | Must be exactly the set of Stage ids currently belonging to this Requisition — same size, no duplicates, no foreign ids. Array order becomes the new `sortOrder` (index 0 first) |

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `StageDto[]` | Reordered, returned in the new order |
| 400 | ProblemDetails, `code: "stage.reorder.invalid-set"` | `stageIds` does not exactly match the Requisition's current Stage set |
| 401 / 403 | ProblemDetails | Auth failures |
| 404 | ProblemDetails, `code: "stage.reorder.requisition-not-found"` | No Requisition with that id |
| 409 | ProblemDetails, `code: "stage.reorder.requisition-closed"` | Requisition is `closed` |

**Side effects.** Updates `SortOrder` on every Stage row for this Requisition in one transaction.

**Idempotency.** Naturally idempotent — submitting the same order twice is a no-op state change.

---

### 3.5 `DELETE /api/requisitions/{requisitionId}/stages/{stageId}`

**Purpose.** Removes a Stage that holds no Applications (FR-4).

**Responses**

| Status | Body | When |
|---|---|---|
| 204 | — | Removed |
| 401 / 403 | ProblemDetails | Auth failures |
| 404 | ProblemDetails, `code: "stage.remove.not-found"` | No Requisition or Stage with that id, or the Stage does not belong to this Requisition |
| 409 | ProblemDetails, `code: "stage.remove.requisition-closed"` | Requisition is `closed` |
| 409 | ProblemDetails, `code: "stage.remove.occupied"` | At least one Application currently has this Stage as its current Stage (AC-6, E-1) |

**Error example (409, occupied)**

```json
{
  "type": "https://d4fape.ats/errors/stage-remove-occupied",
  "title": "Conflict",
  "status": 409,
  "code": "stage.remove.occupied",
  "detail": "This stage still has applications assigned to it."
}
```

**Side effects.** Deletes one `Stages` row; shifts `SortOrder` of every subsequent Stage in the
same Requisition down by one, keeping the sequence contiguous.

**Idempotency.** Not idempotent — a second call against an already-removed id is `404`.

---

### 3.6 `POST /api/applications/{applicationId}/move`

**Purpose.** Moves an Application to a different Stage within its own Requisition's pipeline,
forward or backward (FR-8).

**Request body**

```json
{ "targetStageId": "d4e5...", "expectedCurrentStageId": "b2c3...", "note": "Strong technical round" }
```

| Field | Type | Required | Rule |
|---|---|---|---|
| `targetStageId` | uuid | Yes | Must be a Stage belonging to the same Requisition as the Application |
| `expectedCurrentStageId` | uuid | Yes | The Stage the caller believes the Application currently occupies (FR-22) |
| `note` | string | No | ≤2000 chars, staff-visible only (FR-23) |

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `ApplicationTransitionDto` | Moved |
| 400 | ProblemDetails, `code: "application.move.validation-failed"` | `note` exceeds 2000 chars |
| 401 / 403 | ProblemDetails | Auth failures |
| 404 | ProblemDetails, `code: "application.move.not-found"` | No Application with that id, or `targetStageId` does not exist or belongs to a different Requisition (FR-9, AC-13, E-12 — one unified 404, no existence leak, mirrors `0003`'s `requisition.not-found` collapsing pattern) |
| 409 | ProblemDetails, `code: "application.move.requisition-closed"` | The Application's Requisition is `closed` |
| 409 | ProblemDetails, `code: "application.move.already-rejected"` | The Application is already rejected (FR-11, AC-15) |
| 409 | ProblemDetails, `code: "application.move.conflict"`, extensions `{ actualCurrentStageId, actualCurrentStageName }` | `expectedCurrentStageId` does not match the Application's actual current Stage (AC-29) |

**Success example (200)**

```json
{
  "applicationId": "9c1d...",
  "requisitionId": "1b2c...",
  "currentStageId": "d4e5...",
  "currentStageName": "Interview",
  "isRejected": false,
  "transition": {
    "id": "e5f6...",
    "fromStageId": "b2c3...",
    "fromStageName": "Screening",
    "toStageId": "d4e5...",
    "toStageName": "Interview",
    "kind": "move",
    "actorDisplayLabel": "Jane Recruiter",
    "note": "Strong technical round",
    "occurredAtUtc": "2026-08-06T10:00:00Z"
  }
}
```

**Error example (409, conflict)**

```json
{
  "type": "https://d4fape.ats/errors/application-move-conflict",
  "title": "Conflict",
  "status": 409,
  "code": "application.move.conflict",
  "detail": "This application has already moved. Refresh and try again.",
  "actualCurrentStageId": "d4e5...",
  "actualCurrentStageName": "Interview"
}
```

**Side effects.** Updates `Applications.CurrentStageId`; inserts one `StageTransitions` row
(`Kind = "move"`). Single transaction (NFR-2).

**Idempotency.** Not idempotent — a second identical call (now with a stale
`expectedCurrentStageId`) is a `409`, not a silent no-op.

---

### 3.7 `POST /api/applications/{applicationId}/reject`

**Purpose.** Marks an Application as rejected; it retains the Stage it was rejected from
(FR-10).

**Request body**

```json
{ "note": "Not enough backend depth" }
```

| Field | Type | Required | Rule |
|---|---|---|---|
| `note` | string | No | ≤2000 chars, staff-visible only |

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `ApplicationTransitionDto` | Rejected |
| 400 | ProblemDetails, `code: "application.reject.validation-failed"` | `note` exceeds 2000 chars |
| 401 / 403 | ProblemDetails | Auth failures |
| 404 | ProblemDetails, `code: "application.reject.not-found"` | No Application with that id |
| 409 | ProblemDetails, `code: "application.reject.requisition-closed"` | The Application's Requisition is `closed` |
| 409 | ProblemDetails, `code: "application.reject.already-rejected"` | Already rejected (FR-11, AC-15, E-4) |

**Side effects.** Sets `Applications.IsRejected = true` (`CurrentStageId` unchanged); inserts one
`StageTransitions` row (`Kind = "reject"`, `ToStageId`/`ToStageName = null`).

**Idempotency.** Not idempotent — a second call is `409`.

---

### 3.8 `GET /api/requisitions/{requisitionId}/pipeline`

**Purpose.** Staff view of every configured Stage with its Applications and count, plus a
separate rejected group (FR-15).

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `PipelineBoardDto` | Always for a valid Requisition id — every configured Stage is present even at zero count (AC-19); `[]`/`0` groups, never an error |
| 401 | ProblemDetails | No/invalid bearer token |
| 403 | ProblemDetails | Candidate token (AC-25) |
| 404 | ProblemDetails, `code: "requisition.pipeline.not-found"` | No Requisition with that id |

**Success example (200)**

```json
{
  "requisitionId": "1b2c...",
  "stages": [
    {
      "stageId": "a1...", "stageName": "Applied", "sortOrder": 0, "count": 1,
      "applications": [
        { "applicationId": "9c1d...", "candidateId": "3fa8...", "candidateFirstName": "Jane", "candidateLastName": "Doe", "candidateEmail": "jane.doe@example.com", "submittedAtUtc": "2026-08-06T09:00:00Z" }
      ]
    },
    { "stageId": "b2...", "stageName": "Screening", "sortOrder": 1, "count": 0, "applications": [] }
  ],
  "rejected": { "count": 0, "applications": [] }
}
```

**Side effects.** None — read-only, `AsNoTracking()`. **Idempotency.** `GET`, naturally
idempotent.

---

### 3.9 `GET /api/applications/{applicationId}/transitions`

**Purpose.** Full chronological transition history of one Application (FR-16).

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `StageTransitionDto[]` | Always for a valid Application id; `[]` if none yet (AC-21) |
| 401 | ProblemDetails | No/invalid bearer token |
| 403 | ProblemDetails | Candidate token, regardless of ownership (FR-18, AC-24, AC-25 — this endpoint is `StaffOnly`; the role check short-circuits before any per-Application logic runs, so a Candidate is rejected identically whether or not the id is their own) |
| 404 | ProblemDetails, `code: "application.transitions.not-found"` | No Application with that id |

**Side effects.** None. **Idempotency.** `GET`, naturally idempotent.

---

### 3.10 `GET /api/applications/mine` *(0004, modified)*

**Purpose.** Unchanged from `0004` (a Candidate's own Applications) plus current status
(FR-17).

**Responses.** Unchanged status codes (200/401/403, see `0004` api.md §3.3).

**Success example (200) — new fields in bold-equivalent (`currentStageName`, `isRejected`)**

```json
[
  {
    "id": "9c1d...",
    "requisitionId": "1b2c...",
    "requisitionTitle": "Senior Backend Engineer",
    "submittedAtUtc": "2026-08-06T10:15:00Z",
    "cvDownloadUrl": "/api/applications/9c1d.../cv",
    "currentStageName": "Interview",
    "isRejected": false
  }
]
```

A rejected Application still carries its retained `currentStageName` (FR-10) — the frontend, not
the API, decides to show a rejected indicator instead of the Stage name when `isRejected` is
`true` (AC-23; see `lld.md` §5.3).

## 4. Shared Schemas

```ts
type StageDto = { id: string; requisitionId: string; name: string; sortOrder: number };

type AddStageRequestDto = { name: string; position?: number };
type RenameStageRequestDto = { name: string };
type ReorderStagesRequestDto = { stageIds: string[] };

type MoveApplicationRequestDto = {
  targetStageId: string;
  expectedCurrentStageId: string;
  note?: string;
};
type RejectApplicationRequestDto = { note?: string };

type StageTransitionDto = {
  id: string;
  applicationId: string;
  fromStageId: string | null;
  fromStageName: string;
  toStageId: string | null;
  toStageName: string | null;
  kind: "move" | "reject";
  actorDisplayLabel: string;
  note: string | null;
  occurredAtUtc: string; // ISO-8601 UTC
};

type ApplicationTransitionDto = {
  applicationId: string;
  requisitionId: string;
  currentStageId: string;
  currentStageName: string;
  isRejected: boolean;
  transition: StageTransitionDto;
};

type PipelineBoardApplicationDto = {
  applicationId: string;
  candidateId: string;
  candidateFirstName: string;
  candidateLastName: string;
  candidateEmail: string;
  submittedAtUtc: string;
};

type PipelineStageGroupDto = {
  stageId: string;
  stageName: string;
  sortOrder: number;
  count: number;
  applications: PipelineBoardApplicationDto[];
};

type PipelineBoardDto = {
  requisitionId: string;
  stages: PipelineStageGroupDto[];
  rejected: { count: number; applications: PipelineBoardApplicationDto[] };
};

// CandidateApplicationListItemDto (0004, modified — additive fields):
type CandidateApplicationListItemDto = {
  id: string;
  requisitionId: string;
  requisitionTitle: string;
  submittedAtUtc: string;
  cvDownloadUrl: string;
  currentStageName: string;
  isRejected: boolean;
};

type ProblemDetails = {
  type: string;
  title: string;
  status: number;
  code: string;
  detail?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
  actualCurrentStageId?: string;   // only on application.move.conflict
  actualCurrentStageName?: string; // only on application.move.conflict
};
```

## 5. Authorization Matrix

| Endpoint | Anonymous | Candidate | Recruiter | HiringManager |
|---|---|---|---|---|
| `POST /api/requisitions/{id}/stages` | 401 | 403 | Allowed | 403 |
| `GET /api/requisitions/{id}/stages` | 401 | 403 | Allowed | Allowed |
| `PUT /api/requisitions/{id}/stages/{stageId}` | 401 | 403 | Allowed | 403 |
| `PUT /api/requisitions/{id}/stages/reorder` | 401 | 403 | Allowed | 403 |
| `DELETE /api/requisitions/{id}/stages/{stageId}` | 401 | 403 | Allowed | 403 |
| `POST /api/applications/{id}/move` | 401 | 403 | Allowed | 403 |
| `POST /api/applications/{id}/reject` | 401 | 403 | Allowed | 403 |
| `GET /api/requisitions/{id}/pipeline` | 401 | 403 | Allowed | Allowed |
| `GET /api/applications/{id}/transitions` | 401 | 403 | Allowed | Allowed |
| `GET /api/applications/mine` | 401 | Allowed (own only) | 403 | 403 |

## 6. Events Published

None. No `worker/*` component exists (per `architecture.md`); FR-13's `System`-kind actor is a
column shape only, produced by no code path in this spec.

## 7. Deviations From Inherited Conventions

| Convention | Deviation | Reason |
|---|---|---|
| `ProblemDetails` extensions limited to `code`/`errors` | `application.move.conflict` (3.6) adds `actualCurrentStageId`/`actualCurrentStageName` via a new optional `Result`/`ToProblemResult()` `Extensions` mechanism | AC-29 requires the response to state the Application's actual current Stage; extending `Result` (HLD D-7) follows `0003`'s own precedent for widening `Result` rather than duplicating ProblemDetails-mapping logic per endpoint. Future specs may reuse the same `Extensions` mechanism for similarly-shaped conflicts |
| `DELETE` has not been used by any prior spec | `DELETE /api/requisitions/{id}/stages/{stageId}` returns `204 No Content` | First delete-shaped operation in the project; `204` is the standard REST response for a successful delete with no body, consistent with `errors` semantics elsewhere (no body to convey) |

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0003` (Requisition Management) | 1 | Reused unchanged: `/api` base path, camelCase JSON, ProblemDetails envelope, `/api/requisitions/{id}/<sub-resource>` nesting, verb-suffixed `POST` for lifecycle-style actions (`/publish` → `/move`, `/reject`). Its `Result.Validation(errors, code, message)` extension precedent is reused for this spec's `Extensions` addition (§7). |
| `0004` (Application Submission and CV Upload) | 1 | Reused unchanged: top-level `/api/applications/{id}/<sub-resource>` addressing, `CandidateOnly`/`StaffOnly` policy names, no-existence-leak 404 pattern (reused for `application.move.not-found`). Modifies its `GET /api/applications/mine` response shape (additive fields only). |
| `0002` (User Authentication and Refresh Token Flow) | 1 | Reused unchanged: `Authorization: Bearer` header, `RecruiterOnly`/`StaffOnly`/`CandidateOnly` policy names (all pre-existing — no new policy is added by this spec), 401-vs-403 behaviour. |
