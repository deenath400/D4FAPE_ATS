# Low-Level Design — 0002 User Authentication and Refresh Token Flow

**Spec:** `../spec.md` · **HLD:** `hld.md` · **Updated:** 2026-08-05

The *how* of the authentication system. Precise enough that the implementation agent writes code without re-deciding anything. Every file created or modified is named here with signatures and test mappings.

> This file is **living**: when implementation diverges from this design, `/implement` patches the affected section here and records the deviation in `../implementation/changelog.md`. Silent drift is a defect.

---

## 1. File Manifest

Exact paths following the project structure in `meta/architecture.md` and repository layout.

| Action | Path | Purpose |
|---|---|---|
| Create | `backend/src/Shared/Auth/ApplicationUser.cs` | Identity user domain entity extending `IdentityUser<Guid>` |
| Create | `backend/src/Shared/Auth/ApplicationRole.cs` | Identity role domain entity extending `IdentityRole<Guid>` |
| Create | `backend/src/Shared/Auth/RefreshToken.cs` | Entity tracking refresh tokens, expiry, revocation, and rotation |
| Create | `backend/src/Shared/Auth/IJwtTokenGenerator.cs` | Interface for JWT access token generation |
| Create | `backend/src/Shared/Auth/JwtTokenGenerator.cs` | Implementation of JWT token issuance using `System.IdentityModel.Tokens.Jwt` |
| Create | `backend/src/Shared/Auth/AuthConstants.cs` | Roles (`Candidate`, `Recruiter`, `HiringManager`) and Policy constants |
| Modify | `backend/src/Db/AppDbContext.cs` | Inherit `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`, register `RefreshToken` DbSet |
| Create | `backend/src/Db/Configurations/RefreshTokenConfiguration.cs` | EF Core entity mapping for `RefreshToken` table & indexes |
| Create | `backend/src/Db/Migrations/<timestamp>_AddAuthenticationAndRefreshTokens.cs` | EF Core migration for Identity & RefreshToken schema |
| Create | `backend/src/Service/Auth/IAuthService.cs` | Application service interface for authentication workflows |
| Create | `backend/src/Service/Auth/AuthService.cs` | Application service implementing register, login, refresh, revoke, me |
| Create | `backend/src/Service/Auth/Dtos/RegisterRequestDto.cs` | Registration payload DTO |
| Create | `backend/src/Service/Auth/Dtos/LoginRequestDto.cs` | Login payload DTO |
| Create | `backend/src/Service/Auth/Dtos/RefreshTokenRequestDto.cs` | Refresh token request DTO |
| Create | `backend/src/Service/Auth/Dtos/AuthResponseDto.cs` | Access token & refresh token response DTO |
| Create | `backend/src/Service/Auth/Dtos/UserDto.cs` | User summary identity DTO |
| Create | `backend/src/Api/Controllers/AuthController.cs` | Controllers for `/api/auth/register`, `/login`, `/refresh`, `/logout`, `/me` |
| Modify | `backend/src/Api/Program.cs` | Register Identity, JWT Bearer Auth scheme, Options binding, Auth services |
| Create | `backend/tests/Ats.UnitTests/Auth/AuthServiceTests.cs` | Unit tests for `AuthService` workflows |
| Create | `backend/tests/Ats.UnitTests/Auth/JwtTokenGeneratorTests.cs` | Unit tests for `JwtTokenGenerator` |
| Create | `backend/tests/Ats.IntegrationTests/Auth/AuthEndpointsTests.cs` | Integration tests for authentication endpoints |
| Create | `frontend/src/lib/auth.ts` | NextAuth v5 configuration file |
| Create | `frontend/src/app/api/auth/[...nextauth]/route.ts` | NextAuth API route handlers |
| Modify | `frontend/src/lib/server/backend-invoke.ts` | Attach `Authorization: Bearer <accessToken>` and handle 401 refresh |
| Modify | `frontend/src/app/api/proxy/[...path]/route.ts` | Update BFF proxy handler to relay auth headers |
| Create | `frontend/src/app/(auth)/login/page.tsx` | Portal login page component |
| Create | `frontend/src/app/(auth)/register/page.tsx` | Candidate self-registration page component |
| Create | `frontend/src/components/auth/LoginForm.tsx` | Login form component |
| Create | `frontend/src/components/auth/RegisterForm.tsx` | Registration form component |
| Create | `frontend/tests/auth/LoginForm.test.tsx` | Vitest component tests for LoginForm |
| Create | `frontend/tests/auth/RegisterForm.test.tsx` | Vitest component tests for RegisterForm |

---

## 2. Domain / Data Layer

### 2.1 `ApplicationUser` — `backend/src/Shared/Auth/ApplicationUser.cs`

```csharp
namespace Ats.Shared.Auth;

using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<RefreshToken> RefreshTokens { get; set; } = new();
}
```

### 2.2 `ApplicationRole` — `backend/src/Shared/Auth/ApplicationRole.cs`

```csharp
namespace Ats.Shared.Auth;

using Microsoft.AspNetCore.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() : base() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}
```

### 2.3 `RefreshToken` — `backend/src/Shared/Auth/RefreshToken.cs`

```csharp
namespace Ats.Shared.Auth;

public class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public ApplicationUser User { get; private set; } = null!;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsActive => RevokedAtUtc == null && DateTime.UtcNow < ExpiresAtUtc;

    public static RefreshToken Create(Guid userId, string tokenHash, TimeSpan lifetime)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = DateTime.UtcNow.Add(lifetime),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void Revoke(Guid? replacedByTokenId = null)
    {
        RevokedAtUtc = DateTime.UtcNow;
        ReplacedByTokenId = replacedByTokenId;
    }
}
```

**Invariants**:
- `TokenHash` must be non-empty and stored as a SHA-256 hash.
- A revoked token (`RevokedAtUtc != null`) can never be activated or re-used for exchange.

---

## 3. Service / Application Layer

### 3.1 `IAuthService` — `backend/src/Service/Auth/IAuthService.cs`

```csharp
namespace Ats.Service.Auth;

using Ats.Service.Auth.Dtos;

public interface IAuthService
{
    Task<Result<UserDto>> RegisterCandidateAsync(RegisterRequestDto dto, CancellationToken ct = default);
    Task<Result<AuthResponseDto>> AuthenticateAsync(LoginRequestDto dto, CancellationToken ct = default);
    Task<Result<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto dto, CancellationToken ct = default);
    Task<Result> RevokeTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<Result<UserDto>> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
}
```

**Service Method Behaviour Steps**:
- `RegisterCandidateAsync`:
  1. Check if user with email already exists; return `Conflict` (`auth.register.duplicate-email`) if duplicate.
  2. Create `ApplicationUser` instance (forcing role `Candidate`).
  3. Invoke `UserManager.CreateAsync(user, dto.Password)`. If identity validation fails, return `Validation`.
  4. Invoke `UserManager.AddToRoleAsync(user, AuthConstants.Roles.Candidate)`.
  5. Return `UserDto`.

- `AuthenticateAsync`:
  1. Find user by normalized email; if not found, return `Unauthorized` (`auth.login.invalid-credentials`).
  2. Check password via `SignInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true)`. If locked out or failed, return `Unauthorized`.
  3. Fetch user roles via `UserManager.GetRolesAsync(user)`.
  4. Generate JWT access token via `IJwtTokenGenerator.GenerateAccessToken(user, roles)`.
  5. Generate cryptographically random 32-byte refresh token, hash it, store `RefreshToken` in DB with 7-day expiry.
  6. Return `AuthResponseDto`.

- `RefreshTokenAsync`:
  1. Hash incoming `dto.RefreshToken`.
  2. Query `RefreshToken` from DB by `TokenHash` including `User`.
  3. If token not found or expired, return `Unauthorized` (`auth.refresh.invalid-token`).
  4. If token is revoked, trigger replay mitigation: revoke all active refresh tokens belonging to `user.Id`, save DB changes, return `Unauthorized` (`auth.refresh.token-revoked`).
  5. Create new `RefreshToken` entity (7-day expiry). Revoke old token setting `ReplacedByTokenId = newToken.Id`.
  6. Fetch user roles and generate new JWT access token.
  7. Return `AuthResponseDto`.

**Returns & HTTP Outcomes**:

| Outcome | Result | Maps to HTTP |
|---|---|---|
| Success | `Result.Ok(data)` | 200 / 201 |
| Invalid Credentials / Bad Token | `Result.Unauthorized(code, message)` | 401 |
| Forbidden Role | `Result.Forbidden(code, message)` | 403 |
| Duplicate Email | `Result.Conflict(code, message)` | 409 |
| Validation Failure | `Result.Validation(errors)` | 400 / 422 |

---

## 4. API Layer

| Route | Handler | Auth Policy | Result Mapping |
|---|---|---|---|
| `POST /api/auth/register` | `AuthController.Register` | Anonymous | 201 / 400 / 409 |
| `POST /api/auth/login` | `AuthController.Login` | Anonymous | 200 / 400 / 401 |
| `POST /api/auth/refresh` | `AuthController.Refresh` | Anonymous | 200 / 400 / 401 |
| `POST /api/auth/logout` | `AuthController.Logout` | Protected (`Bearer`) | 200 / 401 |
| `GET /api/auth/me` | `AuthController.Me` | Protected (`Bearer`) | 200 / 401 |

---

## 5. Frontend Architecture

### 5.1 Components & NextAuth Integration

- **`frontend/src/lib/auth.ts`**: Configures NextAuth v5 (Auth.js) with `Credentials` provider. In `jwt` callback, persists `accessToken`, `refreshToken`, and `accessTokenExpires` in JWT session token.
- **`backend-invoke.ts`**: Modified `backendInvoke<T>()` function:
  1. Reads active session via NextAuth `auth()`.
  2. If session contains `accessToken`, appends `Authorization: Bearer <accessToken>` header.
  3. If response status is 401, invokes backend `/api/auth/refresh` using `refreshToken`.
  4. On successful refresh, updates session tokens and retries original request transparently.
  5. If refresh fails, signs out and redirects user to `/login`.

### 5.2 UI States for Auth Forms

| State | `LoginForm.tsx` Treatment | `RegisterForm.tsx` Treatment |
|---|---|---|
| Initial | Clean inputs, submit button enabled | Clean inputs, submit button enabled |
| Loading | Disabled inputs, spinner inside submit button | Disabled inputs, spinner inside submit button |
| Error | Red RFC 7807 alert banner above form, field-level inline error text | Red RFC 7807 alert banner above form, field-level inline error text |
| Success | Redirects to dashboard/landing page | Automatically signs in candidate and redirects to portal dashboard |

---

## 6. DTOs & Contracts

```ts
export type RegisterRequestDto = {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
};

export type LoginRequestDto = {
  email: string;
  password: string;
};

export type RefreshTokenRequestDto = {
  refreshToken: string;
};

export type AuthResponseDto = {
  accessToken: string;
  refreshToken: string;
  tokenType: string; // "Bearer"
  expiresIn: number; // 900 seconds (15 mins)
  user: UserDto;
};

export type UserDto = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
};
```

---

## 7. Validation Rules

| Field | Rule | Message | AC |
|---|---|---|---|
| `email` | Required, valid email format | "A valid email address is required." | AC-1, AC-19 |
| `password` | Required, min 8 chars, uppercase, lowercase, digit, special char | "Password must be at least 8 characters and include uppercase, lowercase, digit, and special character." | AC-1, AC-19 |
| `firstName` | Required, max 100 chars | "First name is required." | AC-1, AC-19 |
| `lastName` | Required, max 100 chars | "Last name is required." | AC-1, AC-19 |
| `refreshToken` | Required | "Refresh token is required." | AC-22 |

---

## 8. Error Handling

All authentication HTTP failure responses return standard RFC 7807 ProblemDetails objects:

| Condition | Error Code | HTTP Status | Log Level | User-facing Message |
|---|---|---|---|---|
| Duplicate Email | `auth.register.duplicate-email` | 409 Conflict | Information | "An account with this email address already exists." |
| Invalid Credentials | `auth.login.invalid-credentials` | 401 Unauthorized | Warning | "Invalid email or password." |
| Account Locked | `auth.login.account-locked` | 401 Unauthorized | Warning | "Account locked due to multiple failed login attempts. Please try again later." |
| Invalid / Expired Refresh Token | `auth.refresh.invalid-token` | 401 Unauthorized | Warning | "Invalid or expired refresh token." |
| Revoked Token Reuse | `auth.refresh.token-revoked` | 401 Unauthorized | Warning | "Refresh token has been revoked." |
| Unauthorized / Missing Token | `auth.bearer.missing-or-invalid` | 401 Unauthorized | Information | "Authentication required." |
| Forbidden Policy | `auth.forbidden.insufficient-role` | 403 Forbidden | Warning | "You do not have permission to perform this action." |

---

## 9. Configuration

| Key | Type | Default | Required | Where Consumed |
|---|---|---|---|---|
| `Jwt:Issuer` | string | `D4FAPE-ATS` | Yes | `JwtTokenGenerator.cs`, `Program.cs` |
| `Jwt:Audience` | string | `D4FAPE-ATS-App` | Yes | `JwtTokenGenerator.cs`, `Program.cs` |
| `Jwt:SigningKey` | string | `[User-Secrets / Min 256-bit Key]` | Yes | `JwtTokenGenerator.cs`, `Program.cs` |
| `Jwt:AccessTokenExpirationMinutes` | int | `15` | No | `JwtTokenGenerator.cs` |
| `Jwt:RefreshTokenExpirationDays` | int | `7` | No | `AuthService.cs` |
| `AUTH_SECRET` | string | `[NextAuth Secret]` | Yes | `frontend/src/lib/auth.ts` |

---

## 10. Database Migration

| Step | Change | Reversible |
|---|---|---|
| 1 | Inherit `IdentityDbContext` in `AppDbContext` and add `RefreshTokens` DbSet. | Yes |
| 2 | Run `dotnet ef migrations add AddAuthenticationAndRefreshTokens --project src/Db`. | Yes |
| 3 | Run `dotnet ef database update --project src/Db`. | Yes (`dotnet ef database update InitialCreate`) |

---

## 11. Test Plan

| Test | Type | Covers | Path |
|---|---|---|---|
| `RegisterCandidate_WithValidPayload_CreatesUserAndAssignsCandidateRole` | Unit | AC-1, AC-3 | `backend/tests/Ats.UnitTests/Auth/AuthServiceTests.cs` |
| `RegisterCandidate_WithDuplicateEmail_ReturnsConflict` | Unit | AC-2 | `backend/tests/Ats.UnitTests/Auth/AuthServiceTests.cs` |
| `Authenticate_WithValidCredentials_ReturnsTokens` | Unit | AC-4 | `backend/tests/Ats.UnitTests/Auth/AuthServiceTests.cs` |
| `Authenticate_WithInvalidCredentials_ReturnsUnauthorized` | Unit | AC-5 | `backend/tests/Ats.UnitTests/Auth/AuthServiceTests.cs` |
| `RefreshToken_WithValidActiveToken_RotatesTokenPair` | Unit | AC-6, AC-18 | `backend/tests/Ats.UnitTests/Auth/AuthServiceTests.cs` |
| `RefreshToken_WithRevokedToken_TriggersReplayRevocation` | Unit | AC-7, E-1 | `backend/tests/Ats.UnitTests/Auth/AuthServiceTests.cs` |
| `RevokeToken_WithActiveToken_RevokesToken` | Unit | AC-8 | `backend/tests/Ats.UnitTests/Auth/AuthServiceTests.cs` |
| `JwtTokenGenerator_GeneratesValidTokenWithClaims` | Unit | AC-4, AC-23 | `backend/tests/Ats.UnitTests/Auth/JwtTokenGeneratorTests.cs` |
| `AuthEndpoints_RegisterLoginRefreshMe_IntegrationFlow` | Integration | AC-1, AC-4, AC-6, AC-9, AC-20, AC-21, AC-22, AC-25 | `backend/tests/Ats.IntegrationTests/Auth/AuthEndpointsTests.cs` |
| `AuthEndpoints_ProtectedEndpoint_Returns401WhenUnauthenticated` | Integration | AC-10 | `backend/tests/Ats.IntegrationTests/Auth/AuthEndpointsTests.cs` |
| `AuthEndpoints_StaffEndpoint_Returns403ForCandidateToken` | Integration | AC-11 | `backend/tests/Ats.IntegrationTests/Auth/AuthEndpointsTests.cs` |
| `LoginForm_ValidationAndSubmission_Behaviors` | Component | AC-12, AC-19 | `frontend/tests/auth/LoginForm.test.tsx` |
| `RegisterForm_ValidationAndSubmission_Behaviors` | Component | AC-13, AC-19 | `frontend/tests/auth/RegisterForm.test.tsx` |

---

## 12. Implementation Notes

- **Password Hasher**: Uses standard ASP.NET Core Identity dependency injection without altering default security settings.
- **Token Hashing**: Refresh tokens are stored in the database hashed via SHA-256 (`System.Security.Cryptography.SHA256`) to ensure plaintext refresh tokens never reside in SQLite.
- **Seeding Roles**: The initial migration / app startup ensures roles `Candidate`, `Recruiter`, and `HiringManager` exist in the database via `RoleManager<ApplicationRole>`.

---

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0001` (Project Scaffolding) | Tier 1 | Analyzed `ui/bff` proxy route structure, `backend-invoke.ts` pattern, backend `api/system` routing, and database setup conventions. |

---

## Deviation Log

| Date | Task | Section | Designed | Actual | Reason |
|---|---|---|---|---|---|
