# Tech Stack

**Updated:** 2026-08-14 · **Budget:** 80 lines target / 120 hard ceiling

Everything needed to build, run, and test the project. `/validate` reads the Commands
section literally, so it must stay accurate.

---

## Runtimes

| Runtime | Version | Notes |
|---|---|---|
| .NET | 10.0.302 | SDK pin (`backend/global.json`) moved off the preview build to the released .NET 10 LTS SDK — the preview is no longer resolvable (est. 0003) |
| Node.js | 24.13.0 | npm 11.6.2; runs the Next.js app |

## Backend

| Concern | Package / Framework | Version | Notes |
|---|---|---|---|
| Web framework | ASP.NET Core | 10.0.10 | Modular monolith, single process |
| ORM / data access | EF Core + `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.10 | SQLite datastore |
| Validation | TBD | — | First API spec picks the library |
| Auth | ASP.NET Core Identity + `JwtBearer` | — | Deferred to spec 0002 |
| Logging | `Microsoft.Extensions.Logging` | 10.0.10 | Built in; structured logging |
| Testing | xUnit | 2.9.3 | Unit, integration & architecture tests |

## Frontend

| Concern | Package / Framework | Version | Notes |
|---|---|---|---|
| Framework | Next.js (App Router), React, TypeScript | 15.1.7 / 19.0 / 5.7 | Strict TypeScript mode |
| Routing | Next.js App Router | 15.1.7 | Route groups separate portal from staff |
| Server state | TBD | — | First UI spec decides |
| Forms | TBD | — | First UI spec decides |
| Styling | Tailwind CSS | 3.4.17 | PostCSS + Tailwind |
| Testing | Vitest + Testing Library | 3.0.5 | Component unit tests |

## Data & Infrastructure

| Concern | Technology | Notes |
|---|---|---|
| Database | SQLite | File on disk. Single writer; WAL enabled |
| Migrations | EF Core migrations | InitialCreate empty migration |
| Cache | None | — |
| Queue / background | None | No `worker/*` component |
| Object storage | Local disk, behind `shared/storage`'s `IFileStorage` (`LocalDiskFileStorage`) | CVs only so far; base path via `Storage:CvBasePath` (est. 0004). Swapping to a cloud backend is an implementation change behind the interface, not a contract change |
| Hosting | TBD | Deployment target deliberately deferred |
| Orchestration | .NET Aspire (`src/AppHost`) | Development-only local process orchestration for both deployables (est. 0006); does not pre-empt Hosting, which remains TBD |
| CI | None | No `.github/workflows/` exists |

## Commands

| Purpose | Command | Working directory |
|---|---|---|
| Install | `dotnet restore --use-lock-file` / `npm ci` | `backend` / `frontend` |
| Build | `dotnet build && dotnet test tests/Ats.ArchitectureTests --no-build` / `npm run build` | `backend` / `frontend` |
| Run (dev) | `dotnet run --project src/Api` / `npm run dev` | `backend` / `frontend` (independent, primary; remain fully supported alongside Aspire — est. 0006) |
| Run (orchestrated dev) | `dotnet run --project src/AppHost` | `backend` (starts both services + dashboard; development-only — est. 0006) |
| Test (unit) | `dotnet test tests/Ats.UnitTests` / `npm test` | `backend` / `frontend` |
| Test (integration) | `dotnet test tests/Ats.IntegrationTests` | `backend` |
| Lint | `dotnet build` / `npm run lint` | `backend` / `frontend` |
| Format | `dotnet format --verify-no-changes` / `npm run format` | `backend` / `frontend` |
| Migrate | `dotnet ef database update --project src/Db` | `backend` |
| Seed | not yet defined | — |

## Required Configuration

| Key | Purpose | Required | Local default |
|---|---|---|---|
| `ConnectionStrings:Default` | SQLite file path | Yes | `Data Source=./data/app.db` |
| `API_BASE_URL` | Backend base URL for Next.js server calls | Yes | `http://localhost:5000` |
| `Jwt:SigningKey` | Secret key for signing JWT access tokens | Yes | `DevelopmentSuperSecretKeyWithAtLeast32BytesLengthForHmacSha256!` |
| `Jwt:Issuer` | Valid JWT token issuer string | Yes | `D4FAPE-ATS` |
| `Jwt:Audience` | Valid JWT token audience string | Yes | `D4FAPE-ATS-App` |
| `Storage:CvBasePath` | Local-disk base path CV attachments are written to/read from | No | `./app-data/cv-attachments` |
| `Applications:MaxCvSizeBytes` | Maximum accepted CV upload size, in bytes | No | `5242880` (5 MB) |

Secrets are never committed. Record the key name and where the value comes from, not the value.

## Repository Layout

```
docs/specs/       spec workflow artifacts
.spec-kit/        stage definitions, conventions, templates
backend/          ASP.NET Core modular monolith solution
frontend/         Next.js candidate portal application
```

## Related Specs

- `0001` (Project Scaffolding and Walking Skeleton)
- `0006` (Local Service Orchestration with Aspire)
