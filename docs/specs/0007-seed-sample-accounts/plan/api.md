# API Design — 0007 Seed Sample User Accounts per Role

**Spec:** `../spec.md` · **Updated:** 2026-08-14

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
| Auth | `Authorization: Bearer <jwt>` | 0001 establishes header name; 0002 populates & validates JWT |
| Errors | RFC 7807 ProblemDetails | 0001 |
| Pagination | Not applicable — no list endpoint touched | — |
| Dates | ISO-8601 UTC with `Z` | 0001 |
| Idempotency | `POST /api/auth/login` and `/register` (0002) — unchanged by this spec | 0002 |

## 2. Endpoint Summary

This spec adds **no new endpoint**. It adds data (three seeded users) that flows through two
existing `0002` endpoints unmodified.

| # | Method | Path | Purpose | Auth | AC |
|---|---|---|---|---|---|
| 1 | POST | `/api/auth/login` (0002, unmodified) | Now also succeeds for the three seeded credentials | Anonymous | AC-3, AC-4, AC-5 |
| 2 | POST | `/api/auth/register` (0002, unmodified) | Now also returns 409 when the requested email matches a seeded account | Anonymous | E-1 |

## 3. Endpoint Detail

Full request/response shapes for both endpoints are already fully specified in
`docs/specs/0002-authentication/plan/api.md` §3.1–3.2 and are **not restated here** — this spec
introduces no field, status code, or schema change to either endpoint. Only the following
worked examples, specific to the seeded data this spec adds, are new.

### 3.1 `POST /api/auth/login` — worked example with a seeded credential

**Request**

```json
{
  "email": "sample.recruiter@d4fape-ats.local",
  "password": "Temp@123"
}
```

**Response — 200 OK**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "4a7f8e9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "user": {
    "id": "d6b4122d-6228-4e08-bf29-43c3d5e23b02",
    "email": "sample.recruiter@d4fape-ats.local",
    "firstName": "Sample",
    "lastName": "Recruiter",
    "roles": ["Recruiter"]
  }
}
```

The JWT's `role` claim (`ClaimTypes.Role`) is `"Recruiter"`, matching AC-4. The same shape
applies with `roles: ["Candidate"]` / `roles: ["HiringManager"]` for the other two seeded
emails (AC-3, AC-5).

### 3.2 `POST /api/auth/register` — worked example against a seeded email

**Request**

```json
{
  "email": "sample.candidate@d4fape-ats.local",
  "password": "AnotherPassword123!",
  "firstName": "Someone",
  "lastName": "Else"
}
```

**Response — 409 Conflict** *(unchanged error shape from 0002)*

```json
{
  "type": "https://d4fape.ats/errors/duplicate-email",
  "title": "Conflict",
  "status": 409,
  "code": "auth.register.duplicate-email",
  "detail": "An account with this email address already exists.",
  "traceId": "00-4b...-01"
}
```

This is `0002`'s existing `AuthService.RegisterCandidateAsync` duplicate-email check — the
seeded row simply occupies the email first (E-1).

## 4. Shared Schemas

No new schemas. `AuthResponseDto`, `UserDto`, `ProblemDetails` are defined in
`docs/specs/0002-authentication/plan/api.md` §4 and are unchanged.

## 5. Authorization Matrix

No new endpoint, so no new row. The existing matrix in `docs/specs/0002-authentication/plan/api.md`
§5 governs `/api/auth/login` and `/api/auth/register` unchanged; the seeded accounts satisfy it
exactly as any other `Candidate`/`Recruiter`/`HiringManager` account would.

## 6. Events Published

None — matches `0002`'s "Events Published: None" and every other spec's auth surface.

## 7. Deviations From Inherited Conventions

| Convention | Deviation | Reason |
|---|---|---|
| <none> | | |

This spec introduces no API-shaped change at all; it is a pure data-seeding spec. Empty is the
expected outcome here.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0002` (User Authentication and Refresh Token Flow) | 1 | Owns `/api/auth/login` and `/api/auth/register`, both exercised unmodified by the seeded accounts this spec adds; reused its conventions table, `UserDto`/`AuthResponseDto` schemas, and error envelope verbatim. |
