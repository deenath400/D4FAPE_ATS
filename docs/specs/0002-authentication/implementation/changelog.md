# Implementation Changelog — 0002 User Authentication and Refresh Token Flow

What actually shipped, checkpoint by checkpoint. Append-only. This is the record `/validate` and future specs consult to learn what is really in the code, as opposed to what was designed.

---

## CP-1 — Backend Identity Data Layer & Domain Models · 2026-08-05

**Tasks completed:** T-01, T-02, T-03

**Files created**

| Path | Purpose |
|---|---|
| `backend/src/Shared/Auth/ApplicationUser.cs` | Identity user domain entity extending `IdentityUser<Guid>` |
| `backend/src/Shared/Auth/ApplicationRole.cs` | Identity role domain entity extending `IdentityRole<Guid>` |
| `backend/src/Shared/Auth/RefreshToken.cs` | Entity tracking refresh tokens, expiry, revocation, and rotation |
| `backend/src/Shared/Auth/AuthConstants.cs` | Roles (`Candidate`, `Recruiter`, `HiringManager`) and Policy constants |
| `backend/src/Db/Configurations/RefreshTokenConfiguration.cs` | EF Core entity mapping for `RefreshToken` table and indexes |
| `backend/src/Db/Migrations/20260805141657_AddAuthenticationAndRefreshTokens.cs` | EF Core migration for Identity & RefreshToken schema |
| `backend/tests/Ats.UnitTests/Auth/RefreshTokenTests.cs` | Unit tests for RefreshToken entity invariants |

**Files modified**

| Path | Change |
|---|---|
| `backend/src/Shared/Ats.Shared.csproj` | Added `Microsoft.AspNetCore.Identity.EntityFrameworkCore` package reference |
| `backend/src/Db/Ats.Db.csproj` | Added ProjectReference to `Ats.Shared` and `Microsoft.AspNetCore.Identity.EntityFrameworkCore` package |
| `backend/src/Api/Ats.Api.csproj` | Added `Microsoft.EntityFrameworkCore.Design` package reference for EF Core CLI tools |
| `backend/src/Db/AppDbContext.cs` | Inherited `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`, registered `RefreshTokens` DbSet, added default role seeding |
| `backend/Directory.Build.props` | Added `CA1716` and `CA1861` suppressions to `NoWarn` for namespace keyword and auto-generated EF Core migration arrays |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-1 | Seeded fixed GUIDs for default roles (`Candidate`, `Recruiter`, `HiringManager`) in `AppDbContext.OnModelCreating` | Ensures roles exist deterministically in SQLite database upon migration execution. |

**Deviations from the LLD**

| LLD section | Designed | Actual | Reason | LLD patched? |
|---|---|---|---|---|
| — | — | — | None. | — |

**Verification run**

```
$ dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:06.59

$ dotnet ef database update --project src/Db --startup-project src/Api
Build started...
Build succeeded.
Applying migration '20260805141657_AddAuthenticationAndRefreshTokens'.
Done.

$ dotnet test tests/Ats.UnitTests
Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 32 ms - Ats.UnitTests.dll (net10.0)
```

**Meta updates applied**

- `architecture.md`: Updated `db/core` and `shared/auth` owning specs to `0002`, added `ApplicationUser` and `RefreshToken` entities to ER diagram, appended CP-1 Change Log row.
- `tech-stack.md`: no change.
- `coding-standards.md`: no change.

**Known gaps carried into the next checkpoint**

- None.

---

## CP-2 — Backend Authentication Service & Controller Endpoints · 2026-08-05

**Tasks completed:** T-04, T-05, T-06, T-07, T-08

**Files created**

| Path | Purpose |
|---|---|
| `backend/src/Shared/Auth/IJwtTokenGenerator.cs` | JWT token generator interface |
| `backend/src/Shared/Auth/JwtTokenGenerator.cs` | JWT token generator implementation using HMAC-SHA256 |
| `backend/src/Service/Common/Result.cs` | Generic Service Result pattern handling outcomes and ProblemDetails mappings |
| `backend/src/Service/Auth/IAuthService.cs` | Authentication application service interface |
| `backend/src/Service/Auth/AuthService.cs` | Auth service handling candidate registration, login, refresh rotation, and revocation |
| `backend/src/Service/Auth/Dtos/*.cs` | Request/Response DTOs (`RegisterRequestDto`, `LoginRequestDto`, `RefreshTokenRequestDto`, `AuthResponseDto`, `UserDto`) |
| `backend/src/Api/AuthEndpoints.cs` | Minimal API endpoint routes (`/api/auth/register`, `/login`, `/refresh`, `/logout`, `/me`, `/staff-test`) |
| `backend/tests/Ats.UnitTests/Auth/JwtTokenGeneratorTests.cs` | Unit tests for JWT issuance and claim verification |
| `backend/tests/Ats.UnitTests/Auth/AuthServiceTests.cs` | Unit tests for AuthService workflows and replay revocation |
| `backend/tests/Ats.IntegrationTests/Auth/AuthEndpointsTests.cs` | Integration tests verifying end-to-end auth HTTP endpoints and authorization policies |

**Files modified**

| Path | Change |
|---|---|
| `backend/src/Shared/Ats.Shared.csproj` | Added `Microsoft.AspNetCore.Authentication.JwtBearer` package reference |
| `backend/src/Service/Ats.Service.csproj` | Added `Microsoft.AspNetCore.Authentication.JwtBearer` package reference |
| `backend/src/Api/Ats.Api.csproj` | Added `Microsoft.AspNetCore.Authentication.JwtBearer` package reference |
| `backend/tests/Ats.UnitTests/Ats.UnitTests.csproj` | Added `Moq` and `Microsoft.Data.Sqlite` package references |
| `backend/src/Service/ServiceCollectionExtensions.cs` | Registered Identity, `IJwtTokenGenerator`, `IAuthService`, and `AuthService` |
| `backend/src/Api/Program.cs` | Configured JWT Bearer authentication scheme, authorization policies (`CandidateOnly`, `StaffOnly`, `RecruiterOnly`, `HiringManagerOnly`), `UseAuthentication()`, `UseAuthorization()`, and mapped `AuthEndpoints` |
| `backend/src/Api/appsettings.json` | Added default `Jwt` configuration keys (`Issuer`, `Audience`, `SigningKey`, expiration) |
| `backend/tests/Ats.IntegrationTests/CustomWebApplicationFactory.cs` | Added default test JWT configuration keys in in-memory test setup |
| `backend/Directory.Build.props` | Added `CA1000` suppression to `NoWarn` for generic static factory methods |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-2 | Implemented `AuthEndpoints.cs` using Minimal APIs matching `SystemStatusEndpoints.cs` in `Ats.Api` | Ensures consistent HTTP endpoint mapping structure across the backend host. |

**Deviations from the LLD**

| LLD section | Designed | Actual | Reason | LLD patched? |
|---|---|---|---|---|
| — | — | — | None. | — |

**Verification run**

```
$ dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:06.26

$ dotnet test tests/Ats.UnitTests
Passed!  - Failed:     0, Passed:    17, Skipped:     0, Total:    17, Duration: 1 s - Ats.UnitTests.dll (net10.0)

$ dotnet test tests/Ats.IntegrationTests
Passed!  - Failed:     0, Passed:    12, Skipped:     0, Total:    12, Duration: 3 s - Ats.IntegrationTests.dll (net10.0)
```

**Meta updates applied**

- `architecture.md`: Updated `api/system` and `service/system` owning specs to `0001, 0002`, appended CP-2 Change Log row.
- `tech-stack.md`: Added required `Jwt:SigningKey`, `Jwt:Issuer`, and `Jwt:Audience` configuration keys.
- `coding-standards.md`: no change.

**Known gaps carried into the next checkpoint**

- None.

---

## CP-3 — Frontend NextAuth Integration & BFF Proxy Seam · 2026-08-05

**Tasks completed:** T-09, T-10, T-11

**Files created**

| Path | Purpose |
|---|---|
| `frontend/src/types/next-auth.d.ts` | NextAuth TypeScript module declarations for session and JWT token types |
| `frontend/src/lib/auth.ts` | NextAuth v5 (Auth.js) configuration with Credentials provider, JWT session strategy, and refresh token callback |
| `frontend/src/app/api/auth/[...nextauth]/route.ts` | NextAuth v5 API route handler |
| `frontend/src/app/api/bff/proxy/[...path]/route.ts` | Generic BFF catch-all proxy route handler attaching bearer tokens to outbound API calls |

**Files modified**

| Path | Change |
|---|---|
| `frontend/package.json` | Installed `next-auth@beta` dependency |
| `frontend/src/lib/server/backend-invoke.ts` | Exported `getBackendBaseUrl()`, updated `invokeBackend` to attach `Authorization: Bearer <accessToken>` from session and handle automatic 401 token refresh |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-3 | Exported `getBackendBaseUrl()` from `backend-invoke.ts` for use in `lib/auth.ts` and `bff/proxy/[...path]/route.ts` | Satisfies ESLint rule FR-16 / AC-26 (`Reading process.env.API_BASE_URL outside src/lib/server/backend-invoke.ts is forbidden`). |

**Deviations from the LLD**

| LLD section | Designed | Actual | Reason | LLD patched? |
|---|---|---|---|---|
| 5.1 | `src/app/api/proxy/[...path]/route.ts` | `src/app/api/bff/proxy/[...path]/route.ts` | Placed proxy route handler under `/api/bff/proxy/` to align with component boundary `ui/bff`. | Yes |

**Verification run**

```
$ npm run build (frontend)
✓ Compiled successfully
  Generating static pages (5/5)

$ npm run test (frontend)
Test Files  1 passed (1)
     Tests  3 passed (3)
```

**Meta updates applied**

- `architecture.md`: Updated `ui/bff` owning specs to `0001, 0002`, appended CP-3 Change Log row.
- `tech-stack.md`: no change.
- `coding-standards.md`: no change.

**Known gaps carried into the next checkpoint**

- None.
