# API Design — 0001 Project Scaffolding and Walking Skeleton

**Spec:** `../spec.md` · **Updated:** 2026-08-05

> **Convention inheritance.** This is the first API in the project. There is nothing to
> inherit from a prior spec — the conventions below are being *established* here, chosen
> deliberately and mainstream, because everything after this spec inherits them.

---

## 1. Conventions In Force

| Concern | Convention | Established by |
|---|---|---|
| Base path | `/api` | 0001 |
| Casing | camelCase JSON bodies, kebab-case paths | 0001 (per `coding-standards.md`, already decided at blueprint time) |
| Auth | `Authorization: Bearer <jwt>` header, once issued | 0001 establishes the header name for `0002` to populate; no endpoint in this spec requires or reads it |
| Errors | RFC 7807 ProblemDetails for every non-2xx response, `code` field for the machine-readable reason, `traceId` from the request's trace identifier | 0001 |
| Pagination | Not established — no collection endpoint exists yet. The first spec that ships one decides the envelope | — |
| Dates | Not established — no endpoint in this spec returns a date field. The first spec that needs one decides the format (ISO-8601 UTC with `Z` is the expected default, per `coding-standards.md`, but not exercised here) | — |
| Idempotency | Not established — no mutating endpoint exists yet | — |

## 2. Endpoint Summary

| # | Method | Path | Purpose | Auth | AC |
|---|---|---|---|---|---|
| 1 | GET | `/api/system/status` | Backend version + database reachability/schema-currency | Anonymous | AC-10, AC-11, AC-27 |

## 2b. Frontend BFF Surface (not the backend API)

This spec also ships one frontend-internal route, listed here because it is structurally
part of the contract this spec establishes (FR-8, FR-16), even though it runs inside the
Next.js process, not the backend.

| # | Method | Path | Origin | Purpose | Auth | AC |
|---|---|---|---|---|---|---|
| 1 | GET | `/api/bff/system-status` | Frontend (`ui/bff` proxy handler) | Relays `/api/system/status` to the browser without exposing the backend origin | Anonymous | AC-12, AC-13, AC-15 |

## 3. Endpoint Detail

### 3.1 `GET /api/system/status`

**Purpose.** Reports the backend's own version and whether its database is reachable and
schema-current, unauthenticated, so the walking skeleton and future health checks have
something to call.

**Path parameters.** None.

**Query parameters.** None.

**Request body.** None.

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `SystemStatusDto` | Database reachable and no pending migrations |
| 503 | ProblemDetails, `code: "system.status.database-unavailable"`, `extensions.version`, `extensions.database` | Database unreachable, or reachable with a pending migration |

**Success example (200)**

```json
{
  "version": "1.0.0",
  "database": {
    "reachable": true,
    "schemaCurrent": true
  }
}
```

**Error example (503)**

```json
{
  "type": "https://d4fape.ats/errors/system-status-unavailable",
  "title": "System status degraded",
  "status": 503,
  "code": "system.status.database-unavailable",
  "version": "1.0.0",
  "database": {
    "reachable": false,
    "schemaCurrent": false
  },
  "traceId": "00-4b...-01"
}
```

The body never contains a file path or a connection string, in either response shape
(AC-11) — only the two booleans and the version cross the `service/system` → `api/system`
boundary.

**Side effects.** None — read-only.

**Idempotency.** Naturally idempotent; a `GET` with no observable side effects.

---

### 3.1b `GET /api/bff/system-status` (frontend route, not backend)

**Purpose.** The browser's only path to backend status. Calls the shared invoke function,
which in turn calls §3.1 server-to-server; never lets the browser learn the backend's
origin.

**Responses**

| Status | Body | When |
|---|---|---|
| 200 | `SystemStatusDto` (relayed verbatim from §3.1's 200) | Backend reachable and reported healthy |
| 502 | `{ "message": "Unable to reach the backend service." }` | The shared invoke function threw a `BackendInvokeError` — backend down, unreachable, or itself returned a non-2xx |

**Error example (502)**

```json
{ "message": "Unable to reach the backend service." }
```

No `type`/`title`/`status`/`code` fields — this is a frontend-internal relay error, not a
backend ProblemDetails response, and it deliberately does not adopt the backend's envelope
so a reader can immediately tell "the browser's own server failed to relay" apart from "the
backend itself returned an error." (Deviation, recorded in §7.)

**Side effects.** None.

**Idempotency.** `GET`, no side effects.

## 4. Shared Schemas

```ts
type SystemStatusDto = {
  version: string;
  database: {
    reachable: boolean;
    schemaCurrent: boolean;
  };
};

type ProblemDetails = {
  type: string;
  title: string;
  status: number;
  code: string;
  detail?: string;
  errors?: Record<string, string[]>;
  traceId: string;
};
```

## 5. Authorization Matrix

No role exists yet — `shared/auth` ships in `0002`. Both endpoints are anonymous to every
caller, staff or candidate alike, and neither reads a `ClaimsPrincipal`.

| Endpoint | Recruiter | Hiring Manager | Candidate | Unauthenticated |
|---|---|---|---|---|
| `GET /api/system/status` | anonymous | anonymous | anonymous | anonymous |
| `GET /api/bff/system-status` | anonymous | anonymous | anonymous | anonymous |

## 6. Events Published

None. No `worker/*` component exists (per `architecture.md`), and this spec's endpoint has
no side effects to announce.

## 7. Deviations From Inherited Conventions

| Convention | Deviation | Reason |
|---|---|---|
| ProblemDetails for every non-2xx response | `GET /api/bff/system-status`'s `502` uses a plain `{ message }` body, not ProblemDetails | It is a frontend route handler, not a backend endpoint — it has no `traceId`, no ASP.NET Core ProblemDetails middleware, and intentionally does not borrow the backend's envelope so the two failure sources stay visually distinct. This is the only endpoint the frontend ever serves in this spec; revisit if a second frontend-originated error response is ever added, at which point a frontend-side envelope convention should be decided deliberately rather than repeated ad hoc |

Since this table is non-empty, note for future specs: the frontend's own routes are **not**
bound by the backend's ProblemDetails convention. The backend's endpoints always are.

## Related Specs

None — this is the first spec touching these components.
