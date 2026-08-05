# Implementation Changelog — 0001 Project Scaffolding and Walking Skeleton

**Spec:** `../spec.md` · **Started:** 2026-08-05

Record of changes made by `/implement` per checkpoint.

---

## CP-1 — Repository tooling & backend data layer

**Completed:** 2026-08-05 · **Tasks:** T-01, T-02, T-03, T-04, T-05

### Summary

- Added root `.gitignore` and `.editorconfig` excluding build outputs, dependencies, SQLite database files, WAL/shm sidecars, and local environment files.
- Scaffolded backend solution `Ats.sln` with `.NET 10` SDK pinning in `global.json`, shared `Directory.Build.props` (warnings as errors, nullable enable, implicit usings), local `dotnet-tools.json` pinning `dotnet-ef`, and four projects (`Ats.Db`, `Ats.Service`, `Ats.Api`, `Ats.Shared`).
- Enforced project topology layering: `Api` -> `Service` -> `Db`, and `Shared` standalone.
- Implemented `db/core`: `AppDbContext`, `SqlitePragmaConnectionInterceptor` (WAL mode + busy timeout 5000ms + automatic parent directory creation), `DatabaseHealth`/`EfDatabaseHealthCheck`, `DbServiceCollectionExtensions`, and `AppDbContextFactory`.
- Created initial EF Core migration (`InitialCreate`) and applied it to SQLite database.
- Created architecture test project `Ats.ArchitectureTests` with unit tests verifying all four layering rules.

### Tests & Verification

- `dotnet build`: Succeeded with 0 warnings and 0 errors.
- `dotnet test tests/Ats.ArchitectureTests`: Passed 4/4 architecture layering tests.
- `dotnet ef database update --project src/Db`: Successfully applied `InitialCreate` migration.
- `git status`: Clean check verified no database or build output files are tracked or untracked.

### Deviations

- None.

---

## CP-2 — Backend service/API layer

**Completed:** 2026-08-05 · **Tasks:** T-06, T-07, T-08, T-09, T-10, T-11, T-12, T-13

### Summary

- Implemented `service/system`: `IVersionProvider`, `SystemStatusResult`, `ISystemStatusService`, `SystemStatusService` (calling `IDatabaseHealthCheck` and `IVersionProvider`), and `ServiceCollectionExtensions.AddSystemService`.
- Implemented `AssemblyVersionProvider` reading executing assembly version metadata.
- Implemented `SystemStatusDto` and `DatabaseStatusDto` records.
- Implemented `SystemStatusEndpoints` mapping `GET /api/system/status` with 200 OK (healthy) and RFC 7807 503 ProblemDetails (degraded) mappings, explicitly anonymous.
- Built composition root in `Program.cs` with fail-fast configuration check on `ConnectionStrings:Default`, ASP.NET Core ProblemDetails exception handling, and `AddSystemService`.
- Added `appsettings.json` and `appsettings.Development.json` with local SQLite connection string defaults.
- Created `Ats.UnitTests` test project with unit tests for `SystemStatusService` using fake health checks.
- Created `Ats.IntegrationTests` test project with `CustomWebApplicationFactory` using isolated per-test temp SQLite files and real HTTP assertion suites (200 OK, 503 Service Unavailable without path disclosure, and anonymous access).
- Configured Roslyn code style enforcement (`EnforceCodeStyleInBuild`) in `Directory.Build.props` and generated NuGet lockfiles across all projects.

### Tests & Verification

- `dotnet build`: Succeeded with 0 warnings and 0 errors.
- `dotnet test tests/Ats.ArchitectureTests --no-build`: Passed 4/4 tests.
- `dotnet test tests/Ats.UnitTests`: Passed 2/2 tests.
- `dotnet test tests/Ats.IntegrationTests`: Passed 3/3 tests.

### Deviations

- None.

---

## CP-3 — Frontend scaffold & BFF

**Completed:** 2026-08-05 · **Tasks:** T-14, T-15, T-16, T-17, T-18

### Summary

- Scaffolded Next.js 15 (App Router), React 19, TypeScript (strict mode), Tailwind CSS, PostCSS, ESLint, Prettier, and Vitest in `frontend/`.
- Configured ESLint (`eslint.config.mjs`) with custom rule enforcing FR-16: `process.env.API_BASE_URL` may only be read in `src/lib/server/backend-invoke.ts`.
- Configured Prettier (`.prettierrc.json`, `.prettierignore`) and verified zero formatting violations across frontend code.
- Created `frontend/.env.example` documenting `API_BASE_URL` with no default value.
- Implemented `invokeBackend` in `src/lib/server/backend-invoke.ts` handling `process.env.API_BASE_URL` verification, `cache: "no-store"` requests, `BackendInvokeError` error wrapping (without disclosing backend origin), and token attachment placeholder seam for spec 0002.
- Implemented proxy route handler in `src/app/api/bff/system-status/route.ts` relaying `/api/system/status` responses to browser clients with generic 502 error mapping.
- Created empty `ui/staff` route group placeholder `src/app/(staff)/.gitkeep`.

### Tests & Verification

- `npm ci && npm run build`: Succeeded with zero type errors and zero build errors.
- `npm run lint`: Passed with zero ESLint warnings or errors.
- `npm run format`: Passed with zero Prettier formatting issues.

### Deviations

- None.

---

## CP-4 — Frontend landing page & tests

**Completed:** 2026-08-05 · **Tasks:** T-19, T-20, T-21, T-22, T-23, T-24

### Summary

- Created `SystemStatusDto` type in `frontend/src/lib/types/system-status.ts`.
- Implemented `StatusSkeleton` component in `src/components/StatusSkeleton.tsx` for visual pulse loading.
- Implemented async Server Component `ServerStatusSection` in `src/components/ServerStatusSection.tsx` performing direct server-to-server invocation via `invokeBackend` with inline error handling.
- Implemented Client Component `ClientStatusPanel` in `src/components/ClientStatusPanel.tsx` retrieving status via browser fetch to `/api/bff/system-status` with explicit loading, error, and success states.
- Implemented public candidate portal landing page in `src/app/(portal)/page.tsx` composing and visually distinguishing both status sections.
- Created `ClientStatusPanel` component tests in `frontend/tests/client-status-panel.test.tsx` and Vitest setup file `tests/setup.ts`.

### Tests & Verification

- `npm run build`: Succeeded with zero type errors.
- `npm run lint`: Passed with zero ESLint warnings or errors.
- `npm run format`: Passed with zero Prettier formatting issues.
- `npm test` (`vitest`): Passed 3/3 component tests.

### Deviations

- None.

---

## CP-5 — Hardening & documentation

**Completed:** 2026-08-05 · **Tasks:** T-25, T-26, T-27, T-28, T-29

### Summary

- Updated `docs/specs/meta/tech-stack.md` with literal build/test/lint/format/run/migrate commands and required configuration keys for both backend and frontend deployables.
- Updated `docs/specs/meta/coding-standards.md` with Project-Specific Rules established by spec 0001 (`process.env.API_BASE_URL` isolation, `ui/bff` proxy route rule, `.editorconfig`/ESLint/Prettier code style enforcement).
- Updated `docs/specs/meta/architecture.md` Component Map and appended CP-5 change log entry.
- Conducted full fresh-clone end-to-end verification across both deployables (`dotnet build`, `dotnet test`, `dotnet format`, `dotnet ef database update`, `npm run build`, `npm test`, `npm run lint`, `npm run format`).
- Verified zero untracked database or build output files in `git status`.
- Verified package version pins and lockfiles (`packages.lock.json` and `package-lock.json`).

### Tests & Verification

- Backend: `dotnet build`, `dotnet test` (Unit: 2/2, Integration: 3/3, Architecture: 4/4), `dotnet format --verify-no-changes`, `dotnet ef database update` — all succeeded cleanly.
- Frontend: `npm run build`, `npm test` (3/3), `npm run lint`, `npm run format` — all succeeded cleanly.
- Repository: `git status` clean with zero untracked files.

### Deviations

- None.
