# Clarifications — 0002 Authentication

A verbatim log of ambiguities raised during `/specify` and how they were resolved. This is an
audit trail: when someone later asks "why does it work this way", the answer is here.

---

## Round 1 — 2026-08-05

### C-1 — NextAuth Version & Integration Pattern

**Ambiguity.** The Next.js application could use NextAuth v4 or NextAuth v5 (Auth.js) with a credentials provider to manage ASP.NET Core JWT tokens and session lifecycle.

**Options presented.**
1. (Recommended) NextAuth v5 (Auth.js) Credentials Provider with JWT session strategy, storing backend JWT access & refresh tokens in NextAuth session callbacks.
2. NextAuth v4 Credentials Provider with JWT session strategy.

**Answer.** (Recommended) NextAuth v5 (Auth.js) Credentials Provider with JWT session strategy, storing backend JWT access & refresh tokens in NextAuth session callbacks.

**Impact.** Determines Next.js auth dependencies, `@auth/core` / NextAuth configuration structure, session callbacks, and BFF proxy session retrieval.

---

### C-2 — Candidate Registration vs Staff User Creation

**Ambiguity.** Public candidate registration could share an endpoint with staff provisioning or be strictly candidate-only to prevent privilege escalation.

**Options presented.**
1. (Recommended) `/api/auth/register` creates `Candidate` users by default. Staff accounts (`Recruiter`, `HiringManager`) are seeded or provisioned via admin endpoints/CLI.
2. Single `/api/auth/register` endpoint accepting a `role` field (validating permissions for staff role requests).

**Answer.** (Recommended) `/api/auth/register` creates `Candidate` users by default. Staff accounts (`Recruiter`, `HiringManager`) are seeded or provisioned via admin endpoints/CLI.

**Impact.** Guarantees public registration cannot self-assign staff roles; isolates public candidate sign-up from internal staff provisioning. Determines FR-2 and AC-3.

---

### C-3 — Refresh Token Storage & Rotation Strategy

**Ambiguity.** Refresh tokens could be stored in ASP.NET Core Identity's standard `AspNetUserTokens` table or in a dedicated EF Core entity tracking token rotation, expiration, and explicit revocation.

**Options presented.**
1. (Recommended) Dedicated EF Core `RefreshToken` entity with rotation, expiration check, and revocation tracking.
2. ASP.NET Core Identity `AspNetUserTokens` table.

**Answer.** (Recommended) Dedicated EF Core RefreshToken entity with rotation, expiration check, and revocation tracking.

**Impact.** Establishes the `RefreshToken` DB entity schema, rotation semantics on refresh, and revocation state tracking. Determines FR-3, FR-4, FR-5 and ERD.

---

### C-4 — Next.js Token Proxying & Auto-Refresh Mechanism

**Ambiguity.** Access token attachment to backend calls could be implemented in Next.js Middleware or within the shared server-side invoke function (`backend-invoke.ts`).

**Options presented.**
1. (Recommended) `backend-invoke.ts` extracts access token from NextAuth session and attaches it to outbound headers; triggers refresh on 401 or token expiration.
2. Next.js Middleware intercepts `/api/proxy/*` routes to append bearer tokens and trigger refresh before forwarding.

**Answer.** (Recommended) `backend-invoke.ts` extracts access token from NextAuth session and attaches it to outbound headers; triggers refresh on 401 or token expiration.

**Impact.** Fulfills FR-16 from Spec `0001`. Centralizes bearer token attachment and refresh logic inside `ui/bff`'s `backend-invoke.ts` seam for both proxy routes and Server Components.

---

## Assumptions Made Without Asking

| # | Assumption | Default chosen | Reversal cost if wrong |
|---|---|---|---|
| A-1 | Access token lifetime is short (15 mins); Refresh token lifetime is longer (7 days). | Access: 15 minutes, Refresh: 7 days | Low — configuration settings in appsettings.json |
| A-2 | Password complexity uses standard ASP.NET Core Identity requirements (min 8 chars, uppercase, lowercase, digit, non-alphanumeric). | ASP.NET Core Identity defaults | Low — configurable in Identity options |
| A-3 | Unauthenticated requests to protected endpoints return standard RFC 7807 401 Unauthorized ProblemDetails. | RFC 7807 401 ProblemDetails | Low — consistent with `coding-standards.md` |

---

## Deferred

| # | Question | Deferred to |
|---|---|---|
| D-1 | Password reset and email confirmation workflows | Own spec — candidate account lifecycle / email notification spec |
| D-2 | Multi-factor authentication (MFA) | Own spec — security enhancements |
