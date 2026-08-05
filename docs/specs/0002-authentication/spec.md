---
id: 0002
slug: authentication
title: User Authentication and Refresh Token Flow
status: validated
components: [shared/auth, api/system, service/system, db/core, ui/bff, ui/portal]
entities: [User, Role, RefreshToken]
depends_on: [0001]
created: 2026-08-05
updated: 2026-08-05
---

# User Authentication and Refresh Token Flow

## Problem & Context

The ATS walking skeleton built in Spec `0001` provides basic infrastructure and unauthenticated system status endpoints, but contains no identity management, authentication endpoints, or token handling mechanisms. Neither recruiters, hiring managers, nor candidates can register, log in, or establish authenticated sessions. Protected API endpoints cannot verify client identity or enforce role-based access control.

In addition, while Spec `0001` created the structural `ui/bff` seam (`backend-invoke.ts` and `/api/proxy/[...path]/route.ts`), outbound calls to the backend currently carry no authentication credentials. Without JWT issuance, refresh token rotation, NextAuth integration on the frontend, and automatic token propagation across the BFF boundary, users cannot securely interact with the system or maintain persistent authenticated sessions across page reloads and access token expirations.

This spec implements end-to-end self-hosted authentication: ASP.NET Core Identity with EF Core SQLite persistence on the backend issuing JWT access tokens and refresh tokens, and NextAuth v5 (Auth.js) on the Next.js frontend proxying requests and automatically refreshing expired tokens.

## Goals

- **G-1** Establish backend ASP.NET Core Identity persistence in SQLite EF Core (`db/core` / `shared/auth`), supporting single-tenant user accounts and distinct roles (`Candidate`, `Recruiter`, `HiringManager`).
- **G-2** Provide secure backend API endpoints for candidate self-registration, credential login, JWT token refresh, token revocation/logout, and current principal (`/me`) inspection.
- **G-3** Implement cryptographically secure refresh token rotation in SQLite, revoking used tokens upon exchange and preventing replay attacks.
- **G-4** Integrate NextAuth v5 (Auth.js) Credentials Provider into the Next.js frontend with JWT session strategy storing backend access and refresh tokens.
- **G-5** Extend the `ui/bff` shared invoke function (`backend-invoke.ts`) to automatically attach `Authorization: Bearer <access_token>` headers and handle seamless background token refresh when access tokens expire or 401 response status is returned.

## Non-Goals

- **Social / OAuth login providers** (Google, GitHub, LinkedIn, Microsoft) — self-hosted ASP.NET Core Identity credentials only; third-party login is deferred.
- **Multi-tenant partitioning** — single-tenant architecture per `architecture.md`; no tenant IDs or tenant switching.
- **Email verification or password recovery emails** — deferred to dedicated account management / email notification spec.
- **Role self-assignment during candidate registration** — public registration endpoint creates `Candidate` role users only.
- **Custom UI component library or styling system** — login and registration forms follow existing Tailwind patterns without adding external UI dependencies.

## Users & Personas

| Persona | Need this feature serves |
|---|---|
| Candidate | Can register an account, log in via portal, maintain a secure session, and access candidate-facing endpoints safely. |
| Recruiter | Can log in with staff credentials, receive staff JWT bearer tokens, and access protected recruiter workspace endpoints. |
| HiringManager | Can log in with staff credentials, receive staff JWT bearer tokens, and access protected hiring manager workspace endpoints. |

## Functional Requirements

- **FR-1** — The backend configures ASP.NET Core Identity backed by EF Core over SQLite (`db/core`), defining mutually exclusive roles for `Candidate`, `Recruiter`, and `HiringManager`.
- **FR-2** — The backend provides an anonymous endpoint `POST /api/auth/register` that validates user inputs, creates candidate identity accounts, and assigns the `Candidate` role by default.
- **FR-3** — The backend provides an anonymous endpoint `POST /api/auth/login` that authenticates credentials, returns a JWT access token (15-min expiry) containing user ID, email, and role claims, and returns a secure refresh token (7-day sliding expiry).
- **FR-4** — The backend provides an anonymous endpoint `POST /api/auth/refresh` that validates an active refresh token, revokes the presented refresh token, issues a new JWT access token and rotated refresh token, and persists rotation history.
- **FR-5** — The backend provides a protected endpoint `POST /api/auth/logout` that revokes the active refresh token for the authenticated user session.
- **FR-6** — The backend provides a protected endpoint `GET /api/auth/me` returning identity summary (user ID, email, roles) for the current `ClaimsPrincipal`.
- **FR-7** — The backend validates JWT bearer tokens on protected endpoints and enforces authorization policies matching defined roles (`Candidate`, `Recruiter`, `HiringManager`).
- **FR-8** — The frontend integrates NextAuth v5 (Auth.js) with a Credentials Provider targeting backend login/refresh endpoints, configuring JWT session callbacks to hold access and refresh tokens.
- **FR-9** — The frontend `ui/bff` shared invoke function (`backend-invoke.ts`) automatically retrieves the active access token from NextAuth session and appends `Authorization: Bearer <access_token>` to all outbound backend HTTP requests.
- **FR-10** — The frontend `ui/bff` shared invoke function automatically triggers refresh token rotation via `POST /api/auth/refresh` when an access token is near expiration or when a backend request returns HTTP status 401.
- **FR-11** — The frontend renders candidate login and registration forms displaying validation errors, field error messages, and loading states without exposing raw server trace details.
- **FR-12** — The backend stores `RefreshToken` entities in SQLite via EF Core tracking token hash, user ID, expiry timestamp, creation timestamp, revoked timestamp, and replaced-by token ID.

## Non-Functional Requirements

- **NFR-1** — Password hashing uses ASP.NET Core Identity defaults (PBKDF2 with HMAC-SHA256 / IdentityV3 format) ensuring zero plaintext password storage.
- **NFR-2** — Refresh token generation uses `RandomNumberGenerator` with at least 256 bits (32 bytes) of cryptographic randomness.
- **NFR-3** — Auth endpoints respond within < 200 ms at p95 for valid requests under local development configuration.
- **NFR-4** — All HTTP error responses from auth endpoints strictly adhere to RFC 7807 ProblemDetails specification, omitting credentials or stack traces.

## Acceptance Criteria

Each criterion is independently testable and traces to one or more FRs.

- **AC-1** *(FR-1, FR-2)*
  - **Given** valid candidate registration details (email, password, first name, last name)
  - **When** an unauthenticated client sends `POST /api/auth/register`
  - **Then** the endpoint returns HTTP 201 Created with user ID and email, a user record exists in SQLite, and the user is assigned the `Candidate` role.

- **AC-2** *(FR-2)*
  - **Given** registration details containing an already registered email address
  - **When** an unauthenticated client sends `POST /api/auth/register`
  - **Then** the endpoint returns HTTP 409 Conflict as RFC 7807 ProblemDetails with error code `auth.register.duplicate-email`.

- **AC-3** *(FR-2)*
  - **Given** a registration request containing a requested role of `Recruiter` or `Admin`
  - **When** sent to `POST /api/auth/register`
  - **Then** the role parameter is ignored or rejected, and the created account receives only the `Candidate` role.

- **AC-4** *(FR-3)*
  - **Given** valid candidate or staff credentials
  - **When** a client sends `POST /api/auth/login`
  - **Then** the endpoint returns HTTP 200 OK containing a valid JWT `accessToken` (expiry 15 minutes), `refreshToken` string (expiry 7 days), and `tokenType: "Bearer"`.

- **AC-5** *(FR-3)*
  - **Given** invalid email or password credentials
  - **When** a client sends `POST /api/auth/login`
  - **Then** the endpoint returns HTTP 401 Unauthorized as RFC 7807 ProblemDetails with error code `auth.login.invalid-credentials` without indicating whether email or password was wrong.

- **AC-6** *(FR-4, FR-12)*
  - **Given** a valid, active refresh token and associated user account
  - **When** a client sends `POST /api/auth/refresh` with the refresh token
  - **Then** the endpoint returns HTTP 200 OK with a new JWT access token and rotated refresh token, marks the previous refresh token as revoked, and records `ReplacedByToken`.

- **AC-7** *(FR-4, FR-12)*
  - **Given** an already revoked or expired refresh token
  - **When** a client sends `POST /api/auth/refresh`
  - **Then** the endpoint returns HTTP 401 Unauthorized ProblemDetails, rejects token issuance, and revokes all descendant refresh tokens if reuse of a revoked token is detected.

- **AC-8** *(FR-5, FR-12)*
  - **Given** an authenticated session with an active refresh token
  - **When** the client sends `POST /api/auth/logout` with bearer access token and refresh token payload
  - **Then** the refresh token is marked as revoked in SQLite with timestamp, and subsequent refresh attempts using it return 401 Unauthorized.

- **AC-9** *(FR-6, FR-7)*
  - **Given** a request carrying a valid JWT bearer access token header
  - **When** `GET /api/auth/me` is called
  - **Then** the endpoint returns HTTP 200 OK with the authenticated user's ID, email, and role list.

- **AC-10** *(FR-7)*
  - **Given** a request carrying an expired, tampered, or missing JWT bearer access token header
  - **When** `GET /api/auth/me` or any protected endpoint is called
  - **Then** the endpoint returns HTTP 401 Unauthorized as RFC 7807 ProblemDetails.

- **AC-11** *(FR-7)*
  - **Given** an authenticated `Candidate` user token
  - **When** attempting to reach a staff-restricted policy endpoint (e.g. `[Authorize(Roles = "Recruiter")]`)
  - **Then** the API returns HTTP 403 Forbidden as RFC 7807 ProblemDetails.

- **AC-12** *(FR-8, FR-11)*
  - **Given** the frontend portal login page
  - **When** the user submits valid credentials through NextAuth sign-in
  - **Then** NextAuth establishes an authenticated session holding access and refresh tokens, and redirects the user to the portal dashboard/landing.

- **AC-13** *(FR-8, FR-11)*
  - **Given** the frontend candidate registration page
  - **When** the user submits valid registration fields
  - **Then** the account is created via API proxy, NextAuth automatically signs in the candidate, and the authenticated session is established.

- **AC-14** *(FR-9)*
  - **Given** an active NextAuth session containing a valid JWT access token
  - **When** `backend-invoke.ts` executes an HTTP call to the backend API (server-side or via proxy route handler)
  - **Then** the outbound HTTP request header includes `Authorization: Bearer <accessToken>`.

- **AC-15** *(FR-10)*
  - **Given** a NextAuth session whose access token is expired but has a valid refresh token
  - **When** `backend-invoke.ts` issues a request or detects an expired token state
  - **Then** it automatically invokes backend `POST /api/auth/refresh`, updates the NextAuth session with the new token pair, and completes the request seamlessly.

- **AC-16** *(FR-10)*
  - **Given** a NextAuth session whose refresh token has been revoked or expired
  - **When** automatic refresh fails with 401 Unauthorized
  - **Then** NextAuth clears the invalid session and redirects the browser user to the login page cleanly.

- **AC-17** *(FR-1)*
  - **Given** the backend database schema update command
  - **When** `dotnet ef database update` is executed
  - **Then** Identity tables (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`) and custom `RefreshTokens` table are created in SQLite.

- **AC-18** *(FR-12, NFR-2)*
  - **Given** refresh token generation logic
  - **When** a new refresh token is issued
  - **Then** it is generated via secure random bytes, hashed before comparison, and stored with UTC timestamps.

- **AC-19** *(FR-11)*
  - **Given** invalid registration inputs (short password, invalid email format)
  - **When** submitting the candidate registration form
  - **Then** field-level validation messages are displayed immediately in the UI without sending invalid payloads to the API.

- **AC-20** *(FR-6)*
  - **Given** unauthenticated call to `GET /api/auth/me`
  - **When** request is received without `Authorization` header
  - **Then** response is HTTP 401 Unauthorized ProblemDetails.

- **AC-21** *(FR-3)*
  - **Given** login request with missing email or password body parameters
  - **When** `POST /api/auth/login` is called
  - **Then** response is HTTP 400 Bad Request ProblemDetails with validation failure details.

- **AC-22** *(FR-4)*
  - **Given** token refresh request with empty token string
  - **When** `POST /api/auth/refresh` is called
  - **Then** response is HTTP 400 Bad Request ProblemDetails.

- **AC-23** *(FR-1, FR-3)*
  - **Given** a seeded or provisioned staff account with role `Recruiter`
  - **When** logging in via `POST /api/auth/login`
  - **Then** returned JWT access token contains claim `role: "Recruiter"`.

- **AC-24** *(FR-8, FR-9)*
  - **Given** frontend SSR component rendering a protected page
  - **When** retrieving session server-side in Next.js App Router
  - **Then** `backend-invoke.ts` accesses server session and attaches the access token server-to-server without client bundle leakage.

- **AC-25** *(FR-7, NFR-4)*
  - **Given** any authentication API failure
  - **When** inspecting response payload
  - **Then** body format strictly matches RFC 7807 ProblemDetails schema with `type`, `title`, `status`, `detail`, `instance`, and `code`.

## Edge Cases & Error States

| # | Situation | Expected behaviour |
|---|---|---|
| E-1 | Revoked refresh token reuse attempt | Immediately revokes all active child/descendant refresh tokens for that user family and returns HTTP 401 (AC-7). |
| E-2 | Access token expires mid-session during frontend action | `backend-invoke.ts` transparently performs refresh token exchange, updates session, and retries request without throwing client error (AC-15). |
| E-3 | Simultaneous concurrent refresh requests with same refresh token | Database lock / token rotation grace period or single-writer transaction serializes exchange, returning valid updated token pair or 401 if already rotated (NFR-3). |
| E-4 | Duplicate registration email submission | API returns 409 Conflict ProblemDetails `auth.register.duplicate-email` without leaking candidate account internals (AC-2). |
| E-5 | JWT signing secret missing from backend configuration | Backend startup fails immediately naming `Jwt:SigningKey` missing configuration key per `tech-stack.md`. |
| E-6 | NextAuth secret missing from frontend `.env` | Next.js server fails startup or session initialization naming missing `AUTH_SECRET` environment key. |
| E-7 | Invalid password attempts exceeding threshold | ASP.NET Core Identity lockout policy locks account temporarily and returns 401 ProblemDetails `auth.login.account-locked`. |
| E-8 | User role changed or revoked on backend while active refresh token exists | Upon next `/refresh` exchange, new JWT reflects updated backend roles; if account disabled, refresh fails with 401. |
| E-9 | Candidate attempts direct navigation to `/staff` route group | NextAuth middleware checks session role; if missing `Recruiter`/`HiringManager` role, redirects to 403 / access denied page. |
| E-10 | Client submits malformed JSON body to auth endpoint | Global ASP.NET Core exception/validation middleware intercepts and returns HTTP 400 RFC 7807 ProblemDetails. |

## Data Touched

| Entity | New / Existing | Notes |
|---|---|---|
| `User` (`AspNetUser`) | New | ASP.NET Core Identity user entity (`id`, `email`, `password_hash`, `first_name`, `last_name`, `created_at`). |
| `Role` (`AspNetRole`) | New | Identity roles (`Candidate`, `Recruiter`, `HiringManager`). |
| `UserRole` (`AspNetUserRoles`) | New | Identity user-role join table. |
| `RefreshToken` | New | Custom EF Core entity (`id`, `user_id`, `token_hash`, `expires_at`, `created_at`, `revoked_at`, `replaced_by_token_id`). |

## Impacted Components

| Component | Change |
|---|---|
| `shared/auth` | New. Identity DbContext configuration, JWT token generator service, password validation, role policy definitions. |
| `db/core` | Modified. Includes ASP.NET Core Identity entities and `RefreshToken` entity in SQLite EF Core migration context. |
| `api/system` | Modified. Registers JWT authentication bearer scheme, Identity services, authorization middleware, and routes for auth controllers (`/api/auth/*`). |
| `service/system` | Modified. Auth service business logic for user registration, token generation, refresh token rotation, and revocation. |
| `ui/bff` | Modified. `backend-invoke.ts` updated to extract NextAuth access tokens, append `Authorization` bearer headers, and trigger refresh on 401 / expiry. Next.js proxy route handler updated for auth endpoints. |
| `ui/portal` | Modified. Integrates NextAuth v5 provider, login page (`/login`), registration page (`/register`), and authenticated session header / navigation control. |

## Out of Scope

- Third-party social authentication providers (OAuth2 / OIDC).
- Multifactor authentication (TOTP / SMS MFA).
- Password reset email delivery or token verification email links.
- Tenant isolation or multi-tenant database partitioning.
- Administrative user management UI for staff account creation (staff accounts created via CLI/seed script for now).

## Open Questions

None — all clarifications resolved, see `clarifications.md`.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0001` (Project Scaffolding) | Tier 1 | Analyzed `ui/bff` proxy route structure, `backend-invoke.ts` pattern, backend `api/system` routing, and database setup conventions. |

Tier 0 was read in full: `meta/architecture.md`, `meta/tech-stack.md`, `meta/coding-standards.md`, `index.md`.
Considered and skipped: none.
Cap reached: no (1 prior spec loaded).
