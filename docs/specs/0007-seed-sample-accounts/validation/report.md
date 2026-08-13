# Validation Report — 0007 Seed Sample User Accounts per Role

**Spec:** `../spec.md` · **Validated:** 2026-08-14 · **Verdict:** PASS

| Dimension | Result |
|---|---|
| Build | PASS |
| Unit tests | 162 passed (backend), 59 passed (frontend) |
| Integration tests | 113 passed, 0 failed |
| Architecture tests | 4 passed, 0 failed |
| Lint | 0 errors, 0 warnings (frontend & backend) |
| Acceptance criteria | 9 of 9 covered and passing (8 automated, 1 manual doc inspection) |
| Architectural conformance | 0 findings |
| Coding standards | 0 findings |

**Verdict rule.** `FAIL` if the build breaks, any test fails, or any AC is uncovered or failing. `PASS-WITH-FINDINGS` if everything runs green but findings of severity Medium or below exist. `PASS` only when there are no findings above Low.

---

## 1. Test Execution

Verbatim captured output from literal commands defined in `meta/tech-stack.md`.

### Build

```
$ cd backend && dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:03.75
```

### Architecture tests

```
$ dotnet test tests/Ats.ArchitectureTests --no-build
Test run for C:\D_Drive\D4FAPE-_ATS\backend\tests\Ats.ArchitectureTests\bin\Debug\net10.0\Ats.ArchitectureTests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 42 ms - Ats.ArchitectureTests.dll (net10.0)
```

### Unit tests

```
$ dotnet test tests/Ats.UnitTests
Test run for C:\D_Drive\D4FAPE-_ATS\backend\tests\Ats.UnitTests\bin\Debug\net10.0\Ats.UnitTests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:   162, Skipped:     0, Total:   162, Duration: 1 s - Ats.UnitTests.dll (net10.0)
```

```
$ cd frontend && npm test
 RUN  v3.2.7 C:/D_Drive/D4FAPE-_ATS/frontend

 Test Files  15 passed (15)
      Tests  59 passed (59)
   Duration  3.10s
```

### Integration tests

```
$ cd backend && dotnet test tests/Ats.IntegrationTests
Test run for C:\D_Drive\D4FAPE-_ATS\backend\tests\Ats.IntegrationTests\bin\Debug\net10.0\Ats.IntegrationTests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:   113, Skipped:     0, Total:   113, Duration: 17 s - Ats.IntegrationTests.dll (net10.0)
```

### Lint & Frontend Build

```
$ cd frontend && npm run lint
> ats-frontend@0.1.0 lint
> npx --no-install next lint

✔ No ESLint warnings or errors
```

```
$ npm run build
✓ Compiled successfully
✓ Generating static pages (9/9)
```

### Migrations

```
$ cd backend && dotnet ef database update --project src/Db
Applying migration '20260805133328_InitialCreate'.
Applying migration '20260805141657_AddAuthenticationAndRefreshTokens'.
Applying migration '20260805171525_AddRequisitionsAndStages'.
Applying migration '20260805191845_AddApplicationsAndCvAttachments'.
Applying migration '20260806080934_AddPipelineProgression'.
Applying migration '20260813210515_AddSeedSampleAccounts'.
Done.
```

**Commands not run**

None — all relevant commands executed.

---

## 2. Acceptance Criteria Traceability

| AC | Requirement | Covering test(s) | Result |
|---|---|---|---|
| AC-1 | Brand-new database with full migration history yields exactly 1 user row per role (`Candidate`, `Recruiter`, `HiringManager`) | `SeedAccountsMigrationTests.cs::Migrate_FreshDatabase_SeedsExactlyThreeUsersAndThreeRoleAssignments` | PASS |
| AC-2 | Stored password hash is not plaintext `Temp@123` and verifies via `PasswordHasher<ApplicationUser>` | `SeedAccountsPasswordHashTests.cs::SeededPasswordHash_IsNotPlaintextAndVerifiesAgainstSharedPassword` | PASS |
| AC-3 | `POST /api/auth/login` with Candidate credentials succeeds (200, role `"Candidate"`, valid refresh token) | `SeedAccountsLoginTests.cs::Login_SeededCandidateCredentials_Returns200WithCandidateRoleClaim` | PASS |
| AC-4 | `POST /api/auth/login` with Recruiter credentials succeeds (200, role `"Recruiter"`, valid refresh token) | `SeedAccountsLoginTests.cs::Login_SeededRecruiterCredentials_Returns200WithRecruiterRoleClaim` | PASS |
| AC-5 | `POST /api/auth/login` with HiringManager credentials succeeds (200, role `"HiringManager"`, valid refresh token) | `SeedAccountsLoginTests.cs::Login_SeededHiringManagerCredentials_Returns200WithHiringManagerRoleClaim` | PASS |
| AC-6 | Seeded Recruiter account holds exactly one role (`Recruiter`) | `SeedAccountsMigrationTests.cs::Migrate_RecruiterAccount_HasExactlyOneRoleAssignment` | PASS |
| AC-7 | Seeded accounts are present regardless of environment configuration | `SeedAccountsMigrationTests.cs::Migrate_WithProductionEnvironment_StillSeedsAllThreeAccounts` | PASS |
| AC-8 | Migrating from scratch produces exactly 3 users / 3 role assignments with no duplicate or unique-index violations | `SeedAccountsMigrationTests.cs::Migrate_FreshDatabase_SeedsExactlyThreeUsersAndThreeRoleAssignments`, `SeedAccountsMigrationTests.cs::Migrate_TwiceInARow_DoesNotDuplicateSeededRows` | PASS |
| AC-9 | Seeded credentials documented together in one place without reading migration source | Inspected `spec.md` FR-3/FR-4/AC-9, `plan/erd.md` §7, and comments in `AuthConstants.SeedAccounts` & `AppDbContext.SeedUsers` | MANUAL (PASS) |

---

## 3. Architectural Conformance

Checked against `plan/hld.md`, `plan/lld.md`, and `docs/specs/meta/architecture.md`.

| Check | Result | Note |
|---|---|---|
| Files match the LLD manifest | PASS | All 8 files in manifest present as created or modified |
| Layering respected | PASS | `Ats.ArchitectureTests` passed 4/4; `shared/auth` and `db/core` respect layer rules |
| No unauthorized cross-component dependency | PASS | Uses existing `User` and `Role` entities |
| Component map in `architecture.md` reflects reality | PASS | No new components introduced |
| ER diagram in `architecture.md` reflects the shipped schema | PASS | No new entities or relations introduced |
| Deviations recorded in the changelog and patched into the LLD | PASS | 1 deviation recorded (DI container test setup in T-04 instead of `WebApplicationFactory` override due to host exit timing) |

---

## 4. Coding Standards Conformance

Checked against `docs/specs/meta/coding-standards.md`.

| Rule | Result | Note |
|---|---|---|
| Naming conventions | PASS | Follows C# PascalCase conventions and xUnit `Method_Scenario_Expected` test pattern |
| Error envelope used consistently | PASS | Edge case E-1 returns standard RFC 7807 409 ProblemDetails `auth.register.duplicate-email` |
| No secrets in source | PASS | Only well-known dev seed credentials `Temp@123` pinned |
| Structured logging with correlation id | PASS | Handled by existing auth endpoints |
| Public API documented | PASS | Login contract unchanged |
| Test naming convention | PASS | Follows project convention |

---

## 5. Findings

None. No findings of High, Medium, or Low severity.

---

## 6. Coverage Gaps

None.

---

## 7. Recommended Actions

None. Spec is fully validated.

---

## 8. Status Decision

Verdict: **PASS**. All 9 ACs are verified and passing, test suites are 100% green, architectural and coding standards rules are satisfied with zero findings. Spec status is transitioned to `validated`.

---

## 9. Related Specs

- `0002` (User Authentication and Refresh Token Flow): Auth entities, `AuthConstants.Roles`, and login endpoint contract.
