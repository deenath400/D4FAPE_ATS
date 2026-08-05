# API Design — 0003 Requisition Management

**Spec:** `../spec.md` · **Updated:** 2026-08-05

> **Convention inheritance.** Read `plan/api.md` of `0001` and `0002` first. Both are read in
> full below and reused without modification. This spec is the first to ship a *collection*
> endpoint, so it is also the first to formally establish the pagination envelope
> `0001`/`0002` explicitly left "not established" — and the first to serve both an
> authenticated staff surface and an anonymous surface for the same resource.

---

## 1. Conventions In Force

| Concern | Convention | Established by |
|---|---|---|
| Base path | `/api` | 0001 |
| Casing | camelCase JSON bodies, kebab-case paths | 0001 |
| Auth | `Authorization: Bearer <jwt>` header | 0001 establishes header name; 0002 populates & validates; this spec adds no new auth mechanism, only consumes `RecruiterOnly`/`StaffOnly` |
| Errors | RFC 7807 ProblemDetails for every non-2xx response, `code` field for the machine-readable reason | 0001, reused via `AuthEndpoints.ToProblemResult()` unchanged |
| Dates | ISO-8601 UTC with `Z` | 0001/0002 default, first exercised here for domain content (`createdAtUtc`, `updatedAtUtc`) |
| Pagination | `?page=1&pageSize=20`, envelope `{ items, page, pageSize, total }` | **Established by 0003** — 0001/0002 left this undecided since neither shipped a collection endpoint |
| Public (anonymous) namespace | `/api/public/<resource>` | **Established by 0003** — see HLD Design Decision D-2. Every future anonymous portal endpoint should follow this prefix rather than inventing a new one |
| Idempotency | Not applicable — no endpoint in this spec is retried automatically by a client | — |

## 2. Endpoint Summary

| # | Method | Path | Purpose | Auth | AC |
|---|---|---|---|---|---|
| 1 | POST | `/api/requisitions` | Create a Requisition (status `draft`) | `RecruiterOnly` | AC-1, AC-2 |
| 2 | GET | `/api/requisitions` | List all Requisitions, any status | `StaffOnly` | AC-12, AC-13 |
| 3 | GET | `/api/requisitions/{id}` | Get one Requisition, any status | `StaffOnly` | AC-13 |
| 4 | PUT | `/api/requisitions/{id}` | Edit content fields | `RecruiterOnly` | AC-3, AC-4, AC-5 |
| 5 | POST | `/api/requisitions/{id}/publish` | `draft` → `published` | `RecruiterOnly` | AC-6, AC-11 |
| 6 | POST | `/api/requisitions/{id}/unpublish` | `published` → `draft` | `RecruiterOnly` | AC-7, AC-8, AC-11 |
| 7 | POST | `/api/requisitions/{id}/close` | `published` → `closed` | `RecruiterOnly` | AC-9, AC-10, AC-11 |
| 8 | GET | `/api/public/requisitions` | Search + paginate `published` Requisitions | Anonymous | AC-16, AC-17, AC-18, AC-19, AC-20, AC-24 |
| 9 | GET | `/api/public/requisitions/{id}` | Detail of one `published` Requisition | Anonymous | AC-21, AC-22 |

## 3. Endpoint Detail

### 3.1 `POST /api/requisitions`

**Purpose.** Creates a Requisition in `draft` status.

**Request body**

```json
{ "title": "Senior Backend Engineer", "description": "We are looking for..." }
```

| Field | Type | Required | Rule |
|---|---|---|---|
| `title` | string | Yes | Non-empty, max 200 chars |
| `description` | string | Yes | Non-empty |

**Responses**

| Status | Body | When |
|---|---|---|
| 201 | `RequisitionDto` | Created, status `draft` |
| 400 | ProblemDetails, `code: "requisition.create.validation-failed"` | Empty/oversized `title` or empty `description` |
| 401 | ProblemDetails | No/invalid bearer token |
| 403 | ProblemDetails | Token valid but not `Recruiter` (AC-2) |

**Success example (201)**

```json
{
  "id": "1b2c3d4e-...",
  "title": "Senior Backend Engineer",
  "description": "We are looking for...",
  "status": "draft",
  "createdAtUtc": "2026-08-05T09:00:00Z",
  "updatedAtUtc": "2026-08-05T09:00:00Z"
}
```

**Side effects.** Inserts one `Requisitions` row. No `Stage` rows are created — this spec
ships no default stage seed (Out of Scope, deferred to the pipeline spec).

**Idempotency.** Non-idempotent — each call creates a new Requisition.

---

### 3.2 `GET /api/requisitions`

**Purpose.** Staff listing of every Requisition regardless of status (FR-7).

**Query parameters.** None — no pagination/filter was requested for the staff list (only the
public list needed it, per Clarification C-5).

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `RequisitionDto[]` | Always, for a valid staff caller; empty array if none exist |
| 401 | ProblemDetails | No/invalid bearer token |
| 403 | ProblemDetails | Candidate token (AC-13) |

**Success example (200)**

```json
[
  { "id": "1b2c...", "title": "Senior Backend Engineer", "description": "...", "status": "draft", "createdAtUtc": "2026-08-05T09:00:00Z", "updatedAtUtc": "2026-08-05T09:00:00Z" }
]
```

**Side effects.** None — read-only.

**Idempotency.** `GET`, naturally idempotent.

---

### 3.3 `GET /api/requisitions/{id}`

**Purpose.** Staff detail of a single Requisition regardless of status.

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `RequisitionDto` | Found |
| 401 | ProblemDetails | No/invalid bearer token |
| 403 | ProblemDetails | Candidate token |
| 404 | ProblemDetails, `code: "requisition.not-found"` | No Requisition with that id |

**Side effects.** None. **Idempotency.** `GET`, naturally idempotent.

---

### 3.4 `PUT /api/requisitions/{id}`

**Purpose.** Edits content fields. Allowed while `draft` or `published`; rejected while
`closed` (Clarification C-4, A-7).

**Request body.** Same shape as 3.1.

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `RequisitionDto` | Updated, status unchanged |
| 400 | ProblemDetails, `code: "requisition.update.validation-failed"` | Invalid `title`/`description` |
| 401 / 403 | ProblemDetails | Auth failures, same as 3.1 |
| 404 | ProblemDetails, `code: "requisition.not-found"` | No Requisition with that id |
| 409 | ProblemDetails, `code: "requisition.update.closed"` | Requisition is `closed` (AC-5) |

**Error example (409)**

```json
{
  "type": "https://d4fape.ats/errors/requisition-update-closed",
  "title": "Conflict",
  "status": 409,
  "code": "requisition.update.closed",
  "detail": "A closed requisition cannot be edited."
}
```

**Side effects.** Updates `Title`, `Description`, `UpdatedAtUtc`. Does not touch `Status`.

**Idempotency.** Naturally idempotent — submitting the same body twice produces the same
persisted state (only `UpdatedAtUtc` changes on each call).

---

### 3.5 `POST /api/requisitions/{id}/publish`

**Purpose.** `draft` → `published` (FR-3). Immediately visible on `ui/portal`.

**Request body.** None.

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `RequisitionDto`, `status: "published"` | Was `draft` |
| 401 / 403 | ProblemDetails | Auth failures |
| 404 | ProblemDetails, `code: "requisition.not-found"` | No Requisition with that id |
| 409 | ProblemDetails, `code: "requisition.publish.invalid-transition"` | Was `published` or `closed` |

**Side effects.** `Status` → `Published`, `UpdatedAtUtc` refreshed. No separate
publish-readiness gate beyond the validation already applied at create/edit time (E-11) — a
Requisition can only reach this state with a non-empty title/description already persisted.

**Idempotency.** Not idempotent by design — a second call while already `published` is a
`409`, not a silent no-op, because FR-6 treats it as an invalid transition.

---

### 3.6 `POST /api/requisitions/{id}/unpublish`

**Purpose.** `published` → `draft` (FR-4). Immediately removes it from `ui/portal`.

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `RequisitionDto`, `status: "draft"` | Was `published` |
| 401 / 403 | ProblemDetails | Auth failures |
| 404 | ProblemDetails, `code: "requisition.not-found"` | No Requisition with that id |
| 409 | ProblemDetails, `code: "requisition.unpublish.invalid-transition"` | Was `draft` or `closed` (AC-11) |

**Side effects.** `Status` → `Draft`, `UpdatedAtUtc` refreshed. No content change (spec
Assumption A-9) — content edited while `draft` is preserved and shown on re-publish (AC-8).

**Idempotency.** Not idempotent — same reasoning as 3.5.

---

### 3.7 `POST /api/requisitions/{id}/close`

**Purpose.** `published` → `closed` (FR-5), terminal.

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `RequisitionDto`, `status: "closed"` | Was `published` |
| 401 / 403 | ProblemDetails | Auth failures |
| 404 | ProblemDetails, `code: "requisition.not-found"` | No Requisition with that id |
| 409 | ProblemDetails, `code: "requisition.close.invalid-transition"` | Was `draft` (AC-10) or already `closed` (AC-11) |

**Side effects.** `Status` → `Closed`, `UpdatedAtUtc` refreshed. Removed from the public
portal (FR-13).

**Idempotency.** Not idempotent — a second call while already `closed` is `409`.

---

### 3.8 `GET /api/public/requisitions`

**Purpose.** Anonymous, paginated, keyword-searchable list of `published` Requisitions only
(FR-10, FR-11).

**Query parameters**

| Name | Type | Required | Rule |
|---|---|---|---|
| `keyword` | string | No | Matched case-insensitively against `title` and `description` (Assumption A-8). Absent/empty ⇒ no filter |
| `page` | string→int | No, default `1` | Must parse as a positive integer if present; otherwise `400` **before** any query runs (AC-24) |
| `pageSize` | string→int | No, default `20` | Clamped to `[1, 50]` — never an error, per HLD Design Decision D-6 |

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `Paged<PublicRequisitionDto>` | Always for a syntactically valid request, including zero matches (AC-17) and an out-of-range page (AC-19) |
| 400 | ProblemDetails, `code: "requisition.list.invalid-page"` | `page` is present and non-numeric or `< 1` (AC-24) |

**Success example (200)**

```json
{
  "items": [
    { "id": "1b2c...", "title": "Senior Backend Engineer", "description": "...", "updatedAtUtc": "2026-08-05T09:10:00Z" }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 37
}
```

**Empty-result example (200, AC-17/AC-19)**

```json
{ "items": [], "page": 1, "pageSize": 20, "total": 0 }
```

**Error example (400, AC-24)**

```json
{
  "type": "https://d4fape.ats/errors/requisition-list-invalid-page",
  "title": "Bad Request",
  "status": 400,
  "code": "requisition.list.invalid-page",
  "detail": "page must be a positive integer."
}
```

**Side effects.** None — read-only, `AsNoTracking()`, no transaction opened (NFR-2).

**Idempotency.** `GET`, naturally idempotent.

---

### 3.9 `GET /api/public/requisitions/{id}`

**Purpose.** Anonymous detail of one `published` Requisition (FR-12).

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `PublicRequisitionDto` | Requisition exists and is `published` |
| 404 | ProblemDetails, `code: "requisition.not-found"` | Requisition doesn't exist, **or** exists but is `draft`/`closed` — byte-identical response either way (AC-22, E-10, spec Assumption A-5) |

**Side effects.** None. **Idempotency.** `GET`, naturally idempotent.

## 4. Shared Schemas

```ts
type RequisitionStatus = "draft" | "published" | "closed";

type RequisitionDto = {
  id: string;
  title: string;
  description: string;
  status: RequisitionStatus;
  createdAtUtc: string; // ISO-8601 UTC
  updatedAtUtc: string; // ISO-8601 UTC
};

type PublicRequisitionDto = {
  id: string;
  title: string;
  description: string;
  updatedAtUtc: string; // ISO-8601 UTC
};

type Paged<T> = { items: T[]; page: number; pageSize: number; total: number };

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

`PublicRequisitionDto` deliberately has no `status` field — it is only ever `published` by
construction of the query that produced it, so echoing the value back would be redundant and
is one fewer thing an anonymous response can leak.

## 5. Authorization Matrix

| Endpoint | Anonymous | Candidate | Recruiter | HiringManager |
|---|---|---|---|---|
| `POST /api/requisitions` | 401 | 403 | Allowed | 403 |
| `GET /api/requisitions` | 401 | 403 | Allowed | Allowed |
| `GET /api/requisitions/{id}` | 401 | 403 | Allowed | Allowed |
| `PUT /api/requisitions/{id}` | 401 | 403 | Allowed | 403 |
| `POST /api/requisitions/{id}/publish` | 401 | 403 | Allowed | 403 |
| `POST /api/requisitions/{id}/unpublish` | 401 | 403 | Allowed | 403 |
| `POST /api/requisitions/{id}/close` | 401 | 403 | Allowed | 403 |
| `GET /api/public/requisitions` | Allowed | Allowed | Allowed | Allowed |
| `GET /api/public/requisitions/{id}` | Allowed | Allowed | Allowed | Allowed |

The two public endpoints are `AllowAnonymous()` — an authenticated staff or candidate bearer
token is accepted but irrelevant; the response is identical regardless of caller identity.

## 6. Events Published

None. No `worker/*` component exists (per `architecture.md`), and no side effect in this spec
needs to be announced beyond the row it mutates.

## 7. Deviations From Inherited Conventions

| Convention | Deviation | Reason |
|---|---|---|
| — | None | This spec strictly follows `/api` base path, camelCase JSON, ProblemDetails, and Bearer auth from `0001`/`0002`. Pagination and the `/api/public/*` namespace are new *establishments*, not deviations — see §1. |

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0002` (User Authentication and Refresh Token Flow) | 1 | Reused unchanged: `RecruiterOnly`/`StaffOnly` policy names, ProblemDetails mapping via `AuthEndpoints.ToProblemResult()`, 401-vs-403 behaviour for authenticated-wrong-role vs unauthenticated. |
| `0001` (Project Scaffolding and Walking Skeleton) | 1 | Reused unchanged: `/api` base path, camelCase casing, ProblemDetails envelope shape, `traceId` convention. |
