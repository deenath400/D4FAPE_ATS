# API Design — NNNN <Title>

**Spec:** `../spec.md` · **Updated:** YYYY-MM-DD

> **Convention inheritance.** Before designing anything here, read `plan/api.md` of the
> Tier-1 specs selected per `spec-kit/context-loading.md`. Reuse their URL shapes, error
> envelope, pagination, versioning, and auth headers. Deviate only with a stated reason in
> §7. Inventing a second convention for the same concern is a design defect.

---

## 1. Conventions In Force

Restate the inherited conventions this spec follows, so the file is self-contained.

| Concern | Convention | Established by |
|---|---|---|
| Base path | `/api` | 0001 |
| Casing | camelCase JSON bodies, kebab-case paths | 0001 |
| Auth | `Authorization: Bearer <jwt>` | 0001 |
| Errors | RFC 7807 ProblemDetails | 0001 |
| Pagination | `?page=1&pageSize=50`, envelope `{ items, page, pageSize, total }` | 0001 |
| Dates | ISO-8601 UTC with `Z` | 0001 |
| Idempotency | `Idempotency-Key` header on POST | 0002 |

## 2. Endpoint Summary

| # | Method | Path | Purpose | Auth | AC |
|---|---|---|---|---|---|
| 1 | GET | `/api/requisitions/{id}/pipeline` | Board data | `RequisitionRead` | AC-1 |
| 2 | PATCH | `/api/candidates/{id}/stage` | Move candidate | `RequisitionOwner` | AC-2, AC-3 |

## 3. Endpoint Detail

### 3.1 `PATCH /api/candidates/{id}/stage`

**Purpose.** Move a candidate to a different pipeline stage.

**Path parameters**

| Name | Type | Notes |
|---|---|---|
| `id` | uuid | Candidate id |

**Request body**

```json
{
  "stageId": "6f1c…",
  "rowVersion": "AAAAAAAAB9E="
}
```

| Field | Type | Required | Rule |
|---|---|---|---|
| `stageId` | uuid | Yes | Must belong to the candidate's requisition |
| `rowVersion` | base64 | Yes | Optimistic concurrency token |

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `CandidateDto` | Moved |
| 401 | ProblemDetails | No/invalid token |
| 403 | ProblemDetails | Not the requisition owner |
| 404 | ProblemDetails | Candidate not found |
| 409 | ProblemDetails `candidate.stage.conflict` | `rowVersion` stale |
| 422 | ProblemDetails + `errors` | Stage not in requisition |

**Success example**

```json
{
  "id": "0f2e…",
  "fullName": "Ada Lovelace",
  "stageId": "6f1c…",
  "updatedAt": "2026-08-05T09:41:22Z",
  "rowVersion": "AAAAAAAAB9I="
}
```

**Error example (422)**

```json
{
  "type": "https://d4fape.ats/errors/validation",
  "title": "Validation failed",
  "status": 422,
  "code": "candidate.stage.foreign",
  "errors": { "stageId": ["Stage does not belong to this requisition."] }
}
```

**Side effects.** Appends a `StageHistory` row; enqueues `CandidateStageChanged`.

**Idempotency.** Naturally idempotent — moving to the current stage returns 200 with no
history row appended.

---

### 3.2 `<next endpoint>`

<same structure>

## 4. Shared Schemas

```ts
type ProblemDetails = {
  type: string; title: string; status: number;
  code: string; detail?: string;
  errors?: Record<string, string[]>;
  traceId: string;
};

type Paged<T> = { items: T[]; page: number; pageSize: number; total: number };
```

## 5. Authorization Matrix

| Endpoint | Recruiter | Hiring Manager | Admin |
|---|---|---|---|
| `GET /api/requisitions/{id}/pipeline` | own | assigned | all |
| `PATCH /api/candidates/{id}/stage` | own | no | all |

## 6. Events Published

| Event | Payload | Consumer | Delivery |
|---|---|---|---|
| `CandidateStageChanged` | `{ candidateId, fromStageId, toStageId, at }` | `worker/email` | at-least-once |

## 7. Deviations From Inherited Conventions

| Convention | Deviation | Reason |
|---|---|---|
| <none> | | |

If this table is non-empty, the deviation should also be reflected in
`meta/coding-standards.md` so future specs know which convention now applies.

## Related Specs

<Per spec-kit/context-loading.md §4.>
