---
id: 0007
slug: seed-sample-accounts
title: Seed Sample User Accounts per Role
status: planned
components: [db/core, shared/auth]
entities: [User, Role]
depends_on: [0002]
created: 2026-08-14
updated: 2026-08-14
---

# Seed Sample User Accounts per Role

## Problem & Context

Today only the Candidate role has a working way to get an account: `POST /api/auth/register`,
built by `0002`, is public self-service. `Recruiter` and `HiringManager` exist as seeded rows
in the Identity role table (`AppDbContext.SeedRoles`), but no `ApplicationUser` has ever been
assigned either role — `0002` explicitly deferred "administrative user management UI for staff
account creation" to "a CLI/seed script for now," and that script was never built. Anyone
standing up the system from a fresh migration — a new developer, CI, or a demo — has no way to
sign in as a Recruiter or HiringManager and exercise the staff workspace (`0003`, `0004`,
`0005`) without hand-crafting a user row directly in the database.

Even the one path that does work, Candidate self-registration, requires filling out a
registration form before there is anything to log into — there is no ready-made Candidate
credential either. This friction slows down every manual check of a role-gated feature and
blocks any automated smoke test that needs a known login per role.

This spec closes that gap the same way the role rows themselves are already seeded: one fixed
sample account per role, created automatically alongside the schema, sharing a well-known
password. It does not build the general-purpose staff-provisioning tooling `0002` deferred — it
only guarantees one working login exists per role today.

## Goals

- **G-1** A developer or tester can authenticate as a Candidate, a Recruiter, and a
  HiringManager immediately after applying database migrations, with no registration step and
  no manual account creation.
- **G-2** The seeded credentials are discoverable without reading migration source code.

## Non-Goals

- **Administrative account-provisioning UI or CLI** — still deferred by `0002`; this spec adds
  three fixed accounts, not a general provisioning tool.
- **An `Admin` role or account** — no `Admin` role exists anywhere in the system today; creating
  one is a separate decision outside this request.
- **Password reset, forced first-login change, or lockout exceptions for seeded accounts** — no
  such mechanism exists anywhere in the system yet, for any account.
- **Environment-conditional seeding** — the project has no production environment defined yet
  (`meta/project.md`), so there is nothing to gate against; seeding is unconditional.
- **A runtime/idempotent seeder** — seeding happens once, via migration, matching the existing
  `SeedRoles` convention; it does not self-heal at every application start.

## Users & Personas

| Persona | Need this feature serves |
|---|---|
| Recruiter | Can log in immediately with a seeded credential to exercise the staff workspace and pipeline features without a provisioning step. |
| HiringManager | Can log in immediately with a seeded credential to review the hiring-manager surface without a provisioning step. |
| Candidate | Can log in immediately with a seeded credential to exercise the portal without registering a new account first. |

## Functional Requirements

- **FR-1** — The system provisions exactly one user account for each of the `Candidate`,
  `Recruiter`, and `HiringManager` roles automatically when the database schema migrations are
  applied, with no separate seed script or manual step.
- **FR-2** — Each seeded account is assigned exactly the one role matching its name, consistent
  with the mutually-exclusive role rule established by `0002`.
- **FR-3** — The seeded accounts use the email addresses `sample.candidate@d4fape-ats.local`,
  `sample.recruiter@d4fape-ats.local`, and `sample.hiringmanager@d4fape-ats.local` respectively.
- **FR-4** — All three seeded accounts share one password, `Temp@123`; it is never stored in
  plaintext, using the same password hashing as every account created through registration.
- **FR-5** — A seeded account authenticates successfully via `POST /api/auth/login` using its
  seeded email and the shared password, receiving a JWT access token and refresh token pair
  whose role claim matches the account's seeded role.
- **FR-6** — The seeded accounts are present in every environment the application runs in;
  their creation is not conditional on any environment setting.
- **FR-7** — Re-applying the full migration history to a brand-new database always yields
  exactly these three accounts, with no duplicates and no unique-constraint violations.
- **FR-8** — The seeded email addresses and shared password are recorded in a location a
  developer can find without reading migration source (this spec's documentation).

## Non-Functional Requirements

- **NFR-1** — Seeding a brand-new database produces exactly three seeded `ApplicationUser` rows
  and exactly three role-assignment rows; running the full migration history never produces
  duplicates or a unique-index violation.

## Acceptance Criteria

Each criterion is independently testable and traces to one or more FRs.

- **AC-1** *(FR-1, FR-2, FR-3)*
  - **Given** a brand-new database with the full migration history applied
  - **When** the seeded data is queried
  - **Then** exactly one user row exists for each of `sample.candidate@d4fape-ats.local`
    (role `Candidate`), `sample.recruiter@d4fape-ats.local` (role `Recruiter`), and
    `sample.hiringmanager@d4fape-ats.local` (role `HiringManager`).

- **AC-2** *(FR-4)*
  - **Given** a seeded account's stored password hash
  - **When** it is inspected
  - **Then** it does not equal the literal string `Temp@123` and matches the same ASP.NET Core
    Identity hash format used by every other account's stored password.

- **AC-3** *(FR-5)*
  - **Given** the seeded Candidate credentials (`sample.candidate@d4fape-ats.local` / `Temp@123`)
  - **When** `POST /api/auth/login` is called
  - **Then** the response is HTTP 200 OK with a JWT access token whose role claim is
    `"Candidate"` and a valid refresh token, matching `0002`'s AC-4/AC-23 login contract.

- **AC-4** *(FR-5)*
  - **Given** the seeded Recruiter credentials (`sample.recruiter@d4fape-ats.local` / `Temp@123`)
  - **When** `POST /api/auth/login` is called
  - **Then** the response is HTTP 200 OK with a JWT access token whose role claim is
    `"Recruiter"` and a valid refresh token.

- **AC-5** *(FR-5)*
  - **Given** the seeded HiringManager credentials (`sample.hiringmanager@d4fape-ats.local` /
    `Temp@123`)
  - **When** `POST /api/auth/login` is called
  - **Then** the response is HTTP 200 OK with a JWT access token whose role claim is
    `"HiringManager"` and a valid refresh token.

- **AC-6** *(FR-2)*
  - **Given** the seeded Recruiter account
  - **When** its role assignments are queried
  - **Then** it holds exactly one role, `Recruiter`, and neither `Candidate` nor
    `HiringManager`.

- **AC-7** *(FR-6)*
  - **Given** the application configured with different `ASPNETCORE_ENVIRONMENT` values
    (e.g. `Development` and `Production`)
  - **When** the database migrations are applied in each configuration
  - **Then** all three seeded accounts are present in both, with no environment-conditional
    branch controlling their creation.

- **AC-8** *(FR-7, NFR-1)*
  - **Given** a brand-new, empty database
  - **When** `dotnet ef database update` is run once
  - **Then** exactly three seeded `ApplicationUser` rows and exactly three role-assignment
    rows exist, with no unique-index violation on email.

- **AC-9** *(FR-8)*
  - **Given** a developer looking for the seeded credentials
  - **When** they read this spec's documentation
  - **Then** all three seeded email addresses and the one shared password are listed together
    in one place, without needing to open migration source.

## Edge Cases & Error States

| # | Situation | Expected behaviour |
|---|---|---|
| E-1 | Someone attempts `POST /api/auth/register` with one of the seeded email addresses | The existing unique-email rule applies unchanged: the request returns HTTP 409 Conflict `auth.register.duplicate-email` per `0002`'s AC-2, since the seeded row already occupies that email. |
| E-2 | A developer manually deletes a seeded account row from a local database | The row does not automatically reappear — seeding is migration-only (no runtime seeder); it is restored only by rolling the seeding migration back and reapplying it. |
| E-3 | A future spec changes the seeded email pattern, password, or role IDs | A new migration is required to update the seeded rows; databases already migrated keep the old seeded values until that migration is applied, the same behaviour `SeedRoles` already has today. |
| E-4 | The full migration history is applied twice, or reapplied after a rollback/redo cycle | EF Core's migration-history tracking prevents an already-applied migration from reinserting its `HasData` rows, so no duplicate-key error occurs (AC-8). |

## Data Touched

| Entity | New / Existing | Notes |
|---|---|---|
| `User` (`AspNetUser`) | Existing | Three new seeded rows: fixed id, seeded email/normalized email, Identity-hashed `Temp@123` password, and placeholder first/last name (not a real person's data). |
| `UserRole` (`AspNetUserRoles`) | Existing | Three new rows, each linking one seeded user to its one seeded role. |

## Impacted Components

| Component | Change |
|---|---|
| `db/core` | New migration extends the existing role-seeding pattern in `AppDbContext` with three seeded user rows and their role assignments. |
| `shared/auth` | Possibly modified — may gain named constants for the seeded emails/shared password so the migration and any documentation reference one source of truth. |

## Out of Scope

- Creating an `Admin` role or account.
- Any UI, API, or CLI tool for staff account provisioning beyond these three fixed sample
  accounts.
- Forcing a password change, requiring email confirmation, or any lockout-policy exception for
  seeded accounts.
- Gating seeded-account creation by environment.
- A runtime/startup seeder that restores a seeded account if it is deleted.
- Additional sample accounts beyond one per role.

## Open Questions

None — all clarifications resolved, see `clarifications.md`.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0002` (User Authentication and Refresh Token Flow) | 1 | Owns `shared/auth`/`db/core`, defines `ApplicationUser`/`ApplicationRole`/`AuthConstants` and the `SeedRoles` `HasData` pattern this spec extends; its Non-Goals line ("staff accounts created via CLI/seed script for now") is the exact gap closed here; its login ACs (AC-4, AC-23) define the login contract seeded accounts must satisfy. |

Tier 0 was read in full: `meta/architecture.md`, `meta/tech-stack.md`, `meta/coding-standards.md`,
`index.md`. Also read `meta/project.md` for persona names and `backend/src/Db/AppDbContext.cs`,
`backend/src/Shared/Auth/AuthConstants.cs`, `backend/src/Shared/Auth/ApplicationUser.cs`, and
`backend/src/Service/ServiceCollectionExtensions.cs` (password policy) since the feature
extends existing source directly.

Considered and skipped: `0001`, `0003`, `0004`, `0005`, `0006` — none share the `User`/`Role`
entities or the `shared/auth`/`db/core` components.
Cap reached: no (one prior spec scored above threshold).
