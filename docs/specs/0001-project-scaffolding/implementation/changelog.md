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
