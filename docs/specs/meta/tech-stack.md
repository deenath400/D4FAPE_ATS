# Tech Stack

**Updated:** 2026-08-05 · **Budget:** 80 lines target / 120 hard ceiling

Everything needed to build, run, and test the project. `/validate` reads the Commands
section literally, so it must stay accurate.

**Nothing is scaffolded yet.** Versions marked TBD are pinned when the first spec scaffolds
that part; they are not guesses waiting to be confirmed.

---

## Runtimes

| Runtime | Version | Notes |
|---|---|---|
| .NET | 10 (LTS) | SDKs 9.0.316, 10.0.302, 10.0.400-preview observed on the dev machine |
| Node.js | 24.13.0 | npm 11.6.2; runs the Next.js app |

## Backend

| Concern | Package / Framework | Version | Notes |
|---|---|---|---|
| Web framework | ASP.NET Core | 10 | Modular monolith, single process |
| ORM / data access | EF Core + `Microsoft.EntityFrameworkCore.Sqlite` | 10.x | Pin exact version at scaffold |
| Validation | TBD | — | First API spec picks the library; DTO validation at the boundary is the rule regardless |
| Auth | ASP.NET Core Identity + `JwtBearer` | 10 | Self-hosted, one Identity store for staff and candidates |
| Logging | `Microsoft.Extensions.Logging` | 10 | Built in; no third-party sink chosen |
| Testing | xUnit | TBD | Assumed default, nothing scaffolded |

## Frontend

| Concern | Package / Framework | Version | Notes |
|---|---|---|---|
| Framework | Next.js (App Router), React, TypeScript | TBD | Major version pinned at scaffold |
| Routing | Next.js App Router | — | File-based; route groups separate portal from staff |
| Server state | TBD | — | First UI spec decides |
| Forms | TBD | — | First UI spec decides |
| Styling | TBD | — | First UI spec decides |
| Testing | Vitest + Testing Library | TBD | Assumed default, nothing scaffolded |

## Data & Infrastructure

| Concern | Technology | Notes |
|---|---|---|
| Database | SQLite | File on disk. Single writer; enable WAL. Schema changes are table rebuilds — see `architecture.md` |
| Migrations | EF Core migrations | Review every migration for data loss before running |
| Cache | None | — |
| Queue / background | None | No `worker/*` component |
| Object storage | TBD | CV/attachment store; local filesystem vs. object storage undecided |
| Hosting | TBD | Deployment target deliberately deferred by the user |
| CI | None | No `.github/workflows/` exists |

## Commands

Literal and copy-pasteable. Write `not yet defined` rather than guessing — `/validate` must
be able to tell "no such step" from "step failed".

| Purpose | Command | Working directory |
|---|---|---|
| Install | not yet defined | — |
| Build | not yet defined | — |
| Run (dev) | not yet defined | — |
| Test (unit) | not yet defined | — |
| Test (integration) | not yet defined | — |
| Lint | not yet defined | — |
| Format | not yet defined | — |
| Migrate | not yet defined | — |
| Seed | not yet defined | — |

Every row is `not yet defined` because no manifest exists. The spec that scaffolds each side
fills in its rows in the same turn.

## Required Configuration

| Key | Purpose | Required | Local default |
|---|---|---|---|
| `ConnectionStrings__Default` | SQLite file path | Yes | TBD at scaffold |
| `Jwt__Issuer` | Token issuer | Yes | — |
| `Jwt__Audience` | Token audience | Yes | — |
| `Jwt__SigningKey` | Token signing secret — user-secrets locally, never committed | Yes | — |
| `NEXT_PUBLIC_API_BASE_URL` | Backend base URL for the Next.js app | Yes | — |

> **Assumption:** These key names are the intended convention, derived from the Identity+JWT
> and Next.js decisions. The scaffolding spec confirms or renames them.

Secrets are never committed. Record the key name and where the value comes from, not the value.

## Repository Layout

Intended — only the scaffolding directories exist today.

```
docs/specs/       spec workflow artifacts (this blueprint, then one folder per spec)
.spec-kit/        stage definitions, conventions, templates
```

## Related Specs

None — this is the first artifact in the repository.
