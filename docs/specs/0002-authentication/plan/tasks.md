# Tasks — 0002 User Authentication and Refresh Token Flow

**Spec:** `../spec.md` · **LLD:** `lld.md` · **Updated:** 2026-08-05

Execution order for `/implement`. Tasks are grouped into **checkpoints**; `/implement` runs one checkpoint per invocation, then stops for human review.

**Progress:** 0 / 15 tasks · checkpoint CP-1 of 4

---

## How to read this file

- `- [ ]` pending · `- [x]` done · `- [~]` in progress (partial, see changelog)
- Each task names the files it touches and the `AC-n` it serves.
- A checkpoint ends at a state where the project **builds and its tests pass**.
- `/implement` ticks boxes only after the code exists and the checkpoint's tests run green.

---

## CP-1 — Backend Identity Data Layer & Domain Models

*Exit condition: EF Core migrations apply cleanly to SQLite `app.db`, entity unit tests pass, `dotnet build` succeeds.*

- [x] **T-01** — Create Identity & RefreshToken domain entities and configuration
  - Files: `backend/src/Shared/Auth/ApplicationUser.cs`, `backend/src/Shared/Auth/ApplicationRole.cs`, `backend/src/Shared/Auth/RefreshToken.cs`, `backend/src/Shared/Auth/AuthConstants.cs`, `backend/src/Db/Configurations/RefreshTokenConfiguration.cs`
  - Covers: AC-1, AC-17, AC-18
  - Depends on: —

- [x] **T-02** — Inherit `IdentityDbContext` in `AppDbContext` and generate EF Core migration
  - Files: `backend/src/Db/AppDbContext.cs`, `backend/src/Db/Migrations/*_AddAuthenticationAndRefreshTokens.cs`
  - Covers: AC-17
  - Depends on: T-01

- [x] **T-03** — Seed default identity roles and write unit tests for `RefreshToken` invariants
  - Files: `backend/src/Db/AppDbContext.cs`, `backend/tests/Ats.UnitTests/Auth/RefreshTokenTests.cs`
  - Covers: AC-1, AC-18
  - Depends on: T-01, T-02

---

## CP-2 — Backend Authentication Service & Controller Endpoints

*Exit condition: Backend auth controllers return documented HTTP response shapes; unit & integration tests pass with zero compiler warnings.*

- [x] **T-04** — Implement `IJwtTokenGenerator` and `JwtTokenGenerator` in `shared/auth` with unit tests
  - Files: `backend/src/Shared/Auth/IJwtTokenGenerator.cs`, `backend/src/Shared/Auth/JwtTokenGenerator.cs`, `backend/tests/Ats.UnitTests/Auth/JwtTokenGeneratorTests.cs`
  - Covers: AC-4, AC-23
  - Depends on: T-01

- [x] **T-05** — Implement `IAuthService` and `AuthService` in `service/system` with unit tests
  - Files: `backend/src/Service/Auth/IAuthService.cs`, `backend/src/Service/Auth/AuthService.cs`, `backend/src/Service/Auth/Dtos/*.cs`, `backend/tests/Ats.UnitTests/Auth/AuthServiceTests.cs`
  - Covers: AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8, AC-18, E-1
  - Depends on: T-03, T-04

- [x] **T-06** — Create `AuthController` exposing `/api/auth/*` endpoints with RFC 7807 ProblemDetails handling
  - Files: `backend/src/Api/Controllers/AuthController.cs`
  - Covers: AC-1, AC-2, AC-4, AC-5, AC-6, AC-8, AC-9, AC-20, AC-21, AC-22, AC-25
  - Depends on: T-05

- [x] **T-07** — Register Identity, JWT Bearer Auth scheme, and authorization policies in `Program.cs`
  - Files: `backend/src/Api/Program.cs`
  - Covers: AC-7, AC-10, AC-11, AC-20
  - Depends on: T-06

- [x] **T-08** — Add integration test suite for authentication endpoints in `tests/Ats.IntegrationTests`
  - Files: `backend/tests/Ats.IntegrationTests/Auth/AuthEndpointsTests.cs`
  - Covers: AC-1, AC-2, AC-4, AC-5, AC-6, AC-7, AC-8, AC-9, AC-10, AC-11, AC-20, AC-21, AC-22, AC-23, AC-25, E-1
  - Depends on: T-07

---

## CP-3 — Frontend NextAuth Integration & BFF Proxy Seam

*Exit condition: Next.js builds cleanly with zero type errors (`npm run build`), NextAuth session provider initialized, `backend-invoke.ts` handles token attachment and 401 refresh.*

- [x] **T-09** — Install NextAuth v5 (Auth.js) and configure session strategy in `src/lib/auth.ts` and route handler
  - Files: `frontend/package.json`, `frontend/src/lib/auth.ts`, `frontend/src/app/api/auth/[...nextauth]/route.ts`
  - Covers: AC-8, AC-12, AC-13, AC-24
  - Depends on: T-07

- [x] **T-10** — Update `src/lib/server/backend-invoke.ts` to attach `Authorization` header and trigger token refresh
  - Files: `frontend/src/lib/server/backend-invoke.ts`
  - Covers: AC-14, AC-15, AC-16, AC-24
  - Depends on: T-09

- [x] **T-11** — Update BFF proxy handler `src/app/api/proxy/[...path]/route.ts` to relay bearer token headers
  - Files: `frontend/src/app/api/proxy/[...path]/route.ts`
  - Covers: AC-14, AC-15
  - Depends on: T-10

---

## CP-4 — Frontend Auth UI Components, Hardening & Architecture Sync

*Exit condition: Full backend & frontend test suites pass green, zero lint/type errors, architecture snapshot updated.*

- [ ] **T-12** — Implement `RegisterForm.tsx` and candidate registration page `/register` with Vitest tests
  - Files: `frontend/src/components/auth/RegisterForm.tsx`, `frontend/src/app/(auth)/register/page.tsx`, `frontend/tests/auth/RegisterForm.test.tsx`
  - Covers: AC-13, AC-19
  - Depends on: T-11

- [ ] **T-13** — Implement `LoginForm.tsx` and portal login page `/login` with Vitest tests
  - Files: `frontend/src/components/auth/LoginForm.tsx`, `frontend/src/app/(auth)/login/page.tsx`, `frontend/tests/auth/LoginForm.test.tsx`
  - Covers: AC-12, AC-19
  - Depends on: T-11

- [ ] **T-14** — Update header navigation to display authenticated user session status and sign-out action
  - Files: `frontend/src/components/HeaderNav.tsx`
  - Covers: AC-8, AC-9, AC-12
  - Depends on: T-13

- [ ] **T-15** — Update `docs/specs/meta/architecture.md` Component Map and ER diagram for Spec 0002
  - Files: `docs/specs/meta/architecture.md`
  - Covers: —
  - Depends on: T-01, T-05, T-14

---

## Coverage Check

Every acceptance criterion is mapped to at least one task:

| AC | Covered by |
|---|---|
| AC-1 | T-01, T-03, T-05, T-06, T-08 |
| AC-2 | T-05, T-06, T-08 |
| AC-3 | T-05 |
| AC-4 | T-04, T-05, T-06, T-08 |
| AC-5 | T-05, T-06, T-08 |
| AC-6 | T-05, T-06, T-08 |
| AC-7 | T-05, T-07, T-08 |
| AC-8 | T-05, T-06, T-08, T-09, T-14 |
| AC-9 | T-06, T-08, T-14 |
| AC-10 | T-07, T-08 |
| AC-11 | T-07, T-08 |
| AC-12 | T-09, T-13, T-14 |
| AC-13 | T-09, T-12 |
| AC-14 | T-10, T-11 |
| AC-15 | T-10, T-11 |
| AC-16 | T-10 |
| AC-17 | T-01, T-02 |
| AC-18 | T-01, T-03, T-05, T-08 |
| AC-19 | T-12, T-13 |
| AC-20 | T-06, T-07, T-08 |
| AC-21 | T-06, T-08 |
| AC-22 | T-06, T-08 |
| AC-23 | T-04, T-08 |
| AC-24 | T-09, T-10 |
| AC-25 | T-06, T-08 |
| E-1 | T-05, T-08 |

---

## Parallelisable Tasks

- `T-04` (JwtTokenGenerator) can be built in parallel with `T-02` / `T-03` after `T-01`.
- `T-12` (`RegisterForm.tsx`) and `T-13` (`LoginForm.tsx`) can be built in parallel after `T-11`.

---

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0001` (Project Scaffolding) | Tier 1 | Derived checkpoint structure, test suite integration, and architecture update tasks. |
