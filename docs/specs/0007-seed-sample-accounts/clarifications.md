# Clarifications — 0007 Seed Sample User Accounts per Role

A verbatim log of ambiguities raised during `/specify` and how they were resolved. This is an
audit trail: when someone later asks "why does it work this way", the answer is here.

---

## Round 1 — 2026-08-14

### C-1 — Seeded email addresses / naming convention

**Ambiguity.** The feature request named the three roles but not the literal email addresses
to seed. Different conventions carry different collision risk with future real registrations
and different developer discoverability.

**Options presented.**
1. Role-prefixed, `.local` TLD (e.g. `sample.candidate@d4fape-ats.local`) — obviously
   non-production, zero collision risk with real user emails, greppable.
2. `<role>@example.com` style — also fake-looking, but `example.com` is a shared convention
   across many unrelated systems, slightly less greppable as "belongs to this project."
3. Realistic-looking names (e.g. `jane.candidate@digital400.com`) — reads like a real person,
   higher risk of confusion with genuine staff email addresses on the organisation's own domain.

**Answer.** Option 1 — role-prefixed `.local` pattern: `sample.candidate@d4fape-ats.local`,
`sample.recruiter@d4fape-ats.local`, `sample.hiringmanager@d4fape-ats.local`.

**Impact.** FR-3, AC-1, AC-3, AC-4, AC-5, AC-9.

---

### C-2 — Environment gating

**Ambiguity.** Whether the three sample accounts should exist in every environment the
application runs in, or only be seeded when `ASPNETCORE_ENVIRONMENT=Development`. This
determines whether an environment check needs to be built at all.

**Options presented.**
1. Always seeded, no environment check — simplest, matches the fact that no production
   environment is defined for this project yet (`meta/project.md`).
2. Seeded only in `Development` — adds an explicit safeguard against a weak, committed
   credential ever being reachable once a production environment exists.

**Answer.** Option 1 — always seeded, no environment check. No production environment is
defined for this project yet.

**Impact.** FR-6, AC-7; recorded as a Non-Goal (environment-conditional seeding) so it is not
silently added later without a fresh decision.

---

### C-3 — Password scheme

**Ambiguity.** Whether the three accounts share one password or each gets a distinct one.
Both are "a basic password like Temp@123" — the request's wording didn't disambiguate.

**Options presented.**
1. One shared password (`Temp@123`) for all three accounts — simplest to document and
   remember, matches the singular phrasing of the request.
2. A distinct password per role (e.g. `Recruiter@123`) — marginally better isolation for
   testing, more to document and remember.

**Answer.** Option 1 — one shared password, `Temp@123`, for all three accounts.

**Impact.** FR-4, AC-2, AC-9.

---

### C-4 — Seed mechanism

**Ambiguity.** Whether seeding happens once via EF Core migration `HasData` (the same
mechanism `AppDbContext.SeedRoles` already uses for roles) or via an idempotent seeder that
runs at every application startup and can self-heal a deleted account. This changes what
restores a seeded account if it's ever deleted from a local database.

**Options presented.**
1. Migration-only, via `HasData`, same mechanism as `SeedRoles` — matches the user's explicit
   "using ef migrations," matches the existing precedent in `AppDbContext`, no extra
   startup-path complexity.
2. Idempotent startup seeder — self-heals a deleted seeded account on every boot, at the cost
   of a new runtime code path that doesn't exist anywhere else in the system today.

**Answer.** Option 1 — migration-only via `HasData`, same mechanism already used for
`SeedRoles` in `AppDbContext.cs`. No idempotent startup seeder.

**Impact.** FR-1, FR-7, NFR-1, AC-8; Edge Case E-2 (a manually deleted seeded account does not
reappear on its own); recorded as a Non-Goal (no runtime/startup seeder).

---

## Assumptions Made Without Asking

Ambiguities resolved by judgement rather than by asking, because a reasonable default existed
and the alternatives would not have changed the work materially. Listed so they can be
challenged.

| # | Assumption | Default chosen | Reversal cost if wrong |
|---|---|---|---|
| A-1 | One seeded account per role, not additional test fixtures | Exactly three accounts total (one per role) | Low — additive, more rows can be seeded later |
| A-2 | No `Admin` role or account is created | Only the three existing roles are seeded | Low — a future spec adds `Admin` if ever needed |
| A-3 | Seeded accounts get placeholder identity fields, not real personal data | e.g. `FirstName: "Sample"`, `LastName: "Recruiter"` | Low — cosmetic |
| A-4 | `EmailConfirmed` and lockout fields use ordinary account defaults | Same defaults any Identity-created account gets | None — verified in code that no `RequireConfirmedEmail`/`RequireConfirmedAccount` gate exists today, so this is inert either way |
| A-5 | `Temp@123` satisfies the existing Identity password policy | No policy change needed | None — verified: `RequiredLength=8`, requires digit/upper/lower/non-alphanumeric, all satisfied by `Temp@123` (`backend/src/Service/ServiceCollectionExtensions.cs`) |

## Deferred

None — no questions were postponed; all four clarifying questions were resolved in Round 1.
