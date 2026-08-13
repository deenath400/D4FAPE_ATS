# Tasks — 0007 Seed Sample User Accounts per Role

**Spec:** `../spec.md` · **LLD:** `lld.md` · **Updated:** 2026-08-14

Execution order for `/implement`. Tasks are grouped into **checkpoints**; `/implement` runs
one checkpoint per invocation, then stops for review.

**Progress:** 0 / 9 tasks · checkpoint CP-1 of 3

---

## How to read this file

- `- [ ]` pending · `- [x]` done · `- [~]` in progress (partial, see changelog)
- Each task names the files it touches and the `AC-n` it serves.
- A checkpoint ends at a state where the project **builds and its tests pass**. Never define
  a checkpoint that leaves the tree broken.
- `/implement` ticks boxes only after the code exists and the checkpoint's tests run green.

---

## CP-1 — Data layer: constants, seeding, migration

*Exit condition: `dotnet build` succeeds; migration applies cleanly to a fresh SQLite file;
`SeedAccountsMigrationTests` (row-count/uniqueness/environment-independence/no-duplicate tests)
pass.*

- [ ] **T-01** — Add `AuthConstants.SeedAccounts` (fixed user GUIDs, seeded emails, shared
  password, pinned `PasswordHasher` output). Includes the one-time hash-generation step
  (LLD §10 step 0): run a throwaway snippet calling
  `new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), "Temp@123")`,
  paste the result into `SharedPasswordHash`, delete the snippet.
  - Files: `backend/src/Shared/Auth/AuthConstants.cs`
  - Covers: FR-3, FR-4, AC-2, AC-9
  - Depends on: —

- [ ] **T-02** — Add `AppDbContext.SeedUsers(ModelBuilder)` (3 `ApplicationUser` `HasData` rows
  + 3 `IdentityUserRole<Guid>` `HasData` rows), call it from `OnModelCreating` after
  `SeedRoles(builder)`. Include the doc-pointer comment referencing `plan/erd.md` §7.
  - Files: `backend/src/Db/AppDbContext.cs`
  - Covers: FR-1, FR-2, FR-3, FR-6, AC-1, AC-6, AC-7, AC-9
  - Depends on: T-01

- [ ] **T-03** — Generate the EF Core migration: `dotnet ef migrations add AddSeedSampleAccounts
  --project src/Db` from `backend/`, then verify `dotnet ef database update --project src/Db`
  applies cleanly against a throwaway local SQLite file.
  - Files: `backend/src/Db/Migrations/{timestamp}_AddSeedSampleAccounts.cs`,
    `backend/src/Db/Migrations/{timestamp}_AddSeedSampleAccounts.Designer.cs`,
    `backend/src/Db/Migrations/AppDbContextModelSnapshot.cs`
  - Covers: FR-1, FR-7, AC-1, AC-8, NFR-1
  - Depends on: T-02

- [ ] **T-04** — Integration tests: fresh-database row counts (exactly 3 users + 3 role rows),
  Recruiter has exactly one role assignment, re-migrating twice does not duplicate rows, and
  seeded accounts still exist when the host environment is overridden to `Production`.
  - Files: `backend/tests/Ats.IntegrationTests/Auth/SeedAccountsMigrationTests.cs`
  - Covers: AC-1, AC-6, AC-7, AC-8, NFR-1, E-4
  - Depends on: T-03

## CP-2 — Behavioural verification: login, hash format, duplicate email

*Exit condition: `dotnet test tests/Ats.IntegrationTests` and `dotnet test tests/Ats.UnitTests`
both green, covering every seeded-credential login, the password hash format check, and the
duplicate-email edge case.*

- [ ] **T-05** — Integration tests: `POST /api/auth/login` succeeds (200, correct `role` claim,
  valid refresh token) for each of the three seeded credentials.
  - Files: `backend/tests/Ats.IntegrationTests/Auth/SeedAccountsLoginTests.cs`
  - Covers: AC-3, AC-4, AC-5
  - Depends on: T-03

- [ ] **T-06** — Unit test: seeded `PasswordHash` is not the literal string `Temp@123` and
  successfully verifies via `PasswordHasher<ApplicationUser>.VerifyHashedPassword` against
  `Temp@123`.
  - Files: `backend/tests/Ats.UnitTests/Auth/SeedAccountsPasswordHashTests.cs`
  - Covers: AC-2
  - Depends on: T-01

- [ ] **T-07** — Integration test: `POST /api/auth/register` with `sample.candidate@d4fape-ats.local`
  returns 409 `auth.register.duplicate-email`.
  - Files: `backend/tests/Ats.IntegrationTests/Auth/SeedAccountsLoginTests.cs`
  - Covers: E-1
  - Depends on: T-03

## CP-3 — Hardening

*Exit condition: full backend suite green (`dotnet build`, `dotnet test tests/Ats.UnitTests`,
`dotnet test tests/Ats.IntegrationTests`, `dotnet test tests/Ats.ArchitectureTests --no-build`);
`meta/architecture.md` reflects the shipped change.*

- [ ] **T-08** — Full-suite regression pass: confirm no existing test (in particular
  `Ats.IntegrationTests/Auth/AuthEndpointsTests.cs` and
  `Ats.IntegrationTests/Pipeline/PipelineMigrationBackfillTests.cs`, which also migrate a fresh
  SQLite file from scratch) broke as a result of the new seeded rows.
  - Files: none (verification only)
  - Covers: NFR-1
  - Depends on: T-04, T-05, T-06, T-07

- [ ] **T-09** — Update `docs/specs/meta/architecture.md` Change Log with the 0007 entry.
  - Files: `docs/specs/meta/architecture.md`
  - Covers: —
  - Depends on: T-08

---

## Coverage Check

Every acceptance criterion must appear in at least one task.

| AC | Covered by |
|---|---|
| AC-1 | T-02, T-03, T-04 |
| AC-2 | T-01, T-06 |
| AC-3 | T-05 |
| AC-4 | T-05 |
| AC-5 | T-05 |
| AC-6 | T-02, T-04 |
| AC-7 | T-02, T-04 |
| AC-8 | T-03, T-04 |
| AC-9 | T-01, T-02 (plus `plan/erd.md` §7, the documentation itself) |
| NFR-1 | T-03, T-04, T-08 |
| E-1 | T-07 |
| E-4 | T-04 |

Any AC with no task is a planning defect — fix it before `/implement` runs.

## Parallelisable

Tasks with no dependency edge between them, safe to do in any order within their checkpoint:
T-05 ‖ T-06 ‖ T-07 after T-03 (T-06 only needs T-01, but has no ordering conflict with the
others). CP-1's T-01 → T-02 → T-03 → T-04 chain is strictly sequential.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0002` (User Authentication and Refresh Token Flow) | 1 | `AppDbContext.SeedRoles`, `AuthConstants.Roles`, `AuthService`, and the `/api/auth/login`/`/register` contract this spec's tasks build directly on top of. |
