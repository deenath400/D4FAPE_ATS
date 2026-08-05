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
