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
