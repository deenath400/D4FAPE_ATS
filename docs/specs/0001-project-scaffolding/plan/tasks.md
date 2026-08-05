# Tasks — 0001 Project Scaffolding and Walking Skeleton

**Spec:** `../spec.md` · **LLD:** `lld.md` · **Updated:** 2026-08-05

Execution order for `/implement`. Tasks are grouped into **checkpoints**; `/implement` runs
one checkpoint per invocation, then stops for review.

**Progress:** 0 / 29 tasks · checkpoint CP-1 of 5

---

## How to read this file

- `- [ ]` pending · `- [x]` done · `- [~]` in progress (partial, see changelog)
- Each task names the files it touches and the `AC-n` it serves.
- A checkpoint ends at a state where the project **builds and its tests pass**. Never define
  a checkpoint that leaves the tree broken.
- `/implement` ticks boxes only after the code exists and the checkpoint's tests run green.

---

## CP-1 — Repository tooling & backend data layer

*Exit condition: `backend/` solution builds (`dotnet build`); `dotnet ef database update`
against `src/Db` succeeds against a fresh path; a fresh `git status` after that run shows no
tracked/untracked database or build-output file.*

- [x] **T-01** — Repository-level ignore rules and editor config
  - Files: `.gitignore`, `.editorconfig`
  - Covers: AC-1, AC-2
  - Depends on: —

- [x] **T-02** — Backend solution skeleton with project-topology layering enforcement
  - Files: `backend/global.json`, `backend/Ats.sln`, `backend/Directory.Build.props`,
    `backend/.config/dotnet-tools.json`, `backend/src/Db/Ats.Db.csproj`,
    `backend/src/Service/Ats.Service.csproj`, `backend/src/Api/Ats.Api.csproj`,
    `backend/src/Shared/Ats.Shared.csproj`
  - Covers: AC-7 (project-reference half), NFR-2
  - Depends on: T-01

- [x] **T-03** — `db/core` context, WAL/busy-timeout interceptor, health check, DI extension, design-time factory
  - Files: `backend/src/Db/AppDbContext.cs`, `backend/src/Db/SqlitePragmaConnectionInterceptor.cs`,
    `backend/src/Db/DatabaseHealth.cs`, `backend/src/Db/EfDatabaseHealthCheck.cs`,
    `backend/src/Db/DbServiceCollectionExtensions.cs`, `backend/src/Db/AppDbContextFactory.cs`
  - Covers: FR-9, NFR-1
  - Depends on: T-02

- [x] **T-04** — Empty initial migration
  - Files: `backend/src/Db/Migrations/*_InitialCreate.cs`,
    `backend/src/Db/Migrations/AppDbContextModelSnapshot.cs`
  - Covers: AC-8, AC-9
  - Depends on: T-03

- [x] **T-05** — Architecture test project: the four AC-7 layering checks
  - Files: `backend/tests/Ats.ArchitectureTests/Ats.ArchitectureTests.csproj`,
    `backend/tests/Ats.ArchitectureTests/LayeringRuleTests.cs`
  - Covers: AC-7
  - Depends on: T-02

## CP-2 — Backend service/API layer

*Exit condition: `dotnet build && dotnet test tests/Ats.ArchitectureTests --no-build`
succeeds; `dotnet test tests/Ats.UnitTests` and `dotnet test tests/Ats.IntegrationTests`
both pass; the backend serves `GET /api/system/status` locally.*

- [x] **T-06** — `service/system`: result type, contract, implementation, DI extension
  - Files: `backend/src/Service/SystemStatusResult.cs`,
    `backend/src/Service/ISystemStatusService.cs`, `backend/src/Service/SystemStatusService.cs`,
    `backend/src/Service/IVersionProvider.cs`, `backend/src/Service/ServiceCollectionExtensions.cs`
  - Covers: FR-6
  - Depends on: T-03

- [x] **T-07** — `api/system` composition root: fail-fast config, ProblemDetails, logging
  - Files: `backend/src/Api/Program.cs`, `backend/src/Api/appsettings.json`,
    `backend/src/Api/appsettings.Development.json`
  - Covers: FR-12, AC-2, AC-20
  - Depends on: T-06

- [x] **T-08** — `SystemStatusEndpoints` minimal API, DTOs, 200/503 mapping
  - Files: `backend/src/Api/SystemStatusEndpoints.cs`, `backend/src/Api/SystemStatusDto.cs`
  - Covers: FR-6, AC-10, AC-11, AC-27
  - Depends on: T-07

- [x] **T-09** — `AssemblyVersionProvider`
  - Files: `backend/src/Api/AssemblyVersionProvider.cs`
  - Covers: FR-6
  - Depends on: T-02

- [x] **T-10** — Unit tests for `SystemStatusService` with a fake health check
  - Files: `backend/tests/Ats.UnitTests/Ats.UnitTests.csproj`,
    `backend/tests/Ats.UnitTests/SystemStatusServiceTests.cs`
  - Covers: AC-17
  - Depends on: T-06

- [x] **T-11** — Integration tests: in-process host, own SQLite file per test, 200 and 503 cases
  - Files: `backend/tests/Ats.IntegrationTests/Ats.IntegrationTests.csproj`,
    `backend/tests/Ats.IntegrationTests/CustomWebApplicationFactory.cs`,
    `backend/tests/Ats.IntegrationTests/SystemStatusEndpointTests.cs`
  - Covers: AC-18
  - Depends on: T-08

- [x] **T-12** — Backend lint/format wiring
  - Files: `backend/Directory.Build.props` (extend), no new files
  - Covers: AC-22, AC-23
  - Depends on: T-02

- [x] **T-13** — Backend NuGet lockfiles and version pin audit
  - Files: `backend/src/Db/packages.lock.json`, `backend/src/Service/packages.lock.json`,
    `backend/src/Api/packages.lock.json`
  - Covers: AC-3
  - Depends on: T-02

## CP-3 — Frontend scaffold & BFF

*Exit condition: `npm ci && npm run build` succeeds with zero type errors; `npm run lint`
reports zero violations; the proxy route and invoke function compile and are reachable
locally (backend running).*

- [ ] **T-14** — Frontend project scaffold: package manifest, TypeScript, Next config,
  Tailwind, ESLint (incl. the FR-16 rules), Prettier
  - Files: `frontend/package.json`, `frontend/package-lock.json`, `frontend/tsconfig.json`,
    `frontend/next.config.ts`, `frontend/eslint.config.mjs`, `frontend/.prettierrc.json`,
    `frontend/tailwind.config.ts`, `frontend/postcss.config.mjs`, `frontend/vitest.config.ts`,
    `frontend/src/app/layout.tsx`, `frontend/src/app/globals.css`
  - Covers: FR-4, FR-13, FR-14, NFR-3, AC-3, AC-4, AC-6, AC-14, AC-22, AC-23, AC-26
  - Depends on: T-01

- [ ] **T-15** — Frontend configuration keys
  - Files: `frontend/.env.example`
  - Covers: AC-2
  - Depends on: T-14

- [ ] **T-16** — Shared server-side invoke function
  - Files: `frontend/src/lib/server/backend-invoke.ts`
  - Covers: FR-16, FR-17, AC-21, AC-27
  - Depends on: T-15

- [ ] **T-17** — Proxy route handler
  - Files: `frontend/src/app/api/bff/system-status/route.ts`
  - Covers: FR-8, FR-16
  - Depends on: T-16

- [ ] **T-18** — Empty `ui/staff` route group placeholder
  - Files: `frontend/src/app/(staff)/.gitkeep`
  - Covers: — (Impacted Components requirement, no direct AC)
  - Depends on: T-14

## CP-4 — Frontend landing page & tests

*Exit condition: landing page renders both status sections against a running backend;
`npm test` passes; loading/error/success states are all reachable.*

- [ ] **T-19** — Shared DTO type
  - Files: `frontend/src/lib/types/system-status.ts`
  - Covers: FR-7
  - Depends on: T-14

- [ ] **T-20** — Loading skeleton component
  - Files: `frontend/src/components/StatusSkeleton.tsx`
  - Covers: AC-16
  - Depends on: T-14

- [ ] **T-21** — Server-rendered status section (no Suspense, inline await/catch)
  - Files: `frontend/src/components/ServerStatusSection.tsx`
  - Covers: FR-17, AC-16, AC-28, AC-30
  - Depends on: T-16, T-19, T-20

- [ ] **T-22** — Client-rendered status panel (loading/error/success via `useState`/`useEffect`)
  - Files: `frontend/src/components/ClientStatusPanel.tsx`
  - Covers: FR-7, AC-12, AC-13, AC-15, AC-16
  - Depends on: T-17, T-19, T-20

- [ ] **T-23** — Landing page composing and labelling both sections
  - Files: `frontend/src/app/(portal)/page.tsx`
  - Covers: FR-7, AC-29
  - Depends on: T-21, T-22

- [ ] **T-24** — Component test for `ClientStatusPanel`
  - Files: `frontend/tests/client-status-panel.test.tsx`
  - Covers: AC-19
  - Depends on: T-22

## CP-5 — Hardening & documentation

*Exit condition: every command in the `tech-stack.md` Commands table (except `Seed`) is
literal and exits successfully in documented order from a fresh clone; `coding-standards.md`
and `architecture.md` reflect what shipped; `git status` after a full install/build/run/
migrate cycle on both deployables is clean.*

- [ ] **T-25** — Fill `tech-stack.md` Commands and Required Configuration tables
  - Files: `docs/specs/meta/tech-stack.md`
  - Covers: AC-24, AC-25
  - Depends on: T-13, T-14 (all commands must exist and work first)

- [ ] **T-26** — Update `coding-standards.md`: remove superseded prose rules, add
  Project-Specific Rules citing 0001
  - Files: `docs/specs/meta/coding-standards.md`
  - Covers: FR-15
  - Depends on: T-12, T-14

- [ ] **T-27** — Update `architecture.md` Component Map and Change Log
  - Files: `docs/specs/meta/architecture.md`
  - Covers: — (required by the Plan stage's own instructions, not a spec AC)
  - Depends on: all prior tasks in this checkpoint's siblings

- [ ] **T-28** — Fresh-clone end-to-end verification: install, build, run, migrate, test,
  lint, format on both deployables; confirm `git status` clean
  - Files: — (verification pass; no new files)
  - Covers: AC-1, AC-25
  - Depends on: T-25

- [ ] **T-29** — Final pinned-version and lockfile audit across both deployables
  - Files: `backend/**/*.csproj`, `backend/**/packages.lock.json`, `frontend/package.json`,
    `frontend/package-lock.json` (review only — edits only if a drift is found)
  - Covers: AC-3, AC-4, AC-5, AC-6, NFR-2, NFR-3
  - Depends on: T-28

---

## Coverage Check

Every acceptance criterion must appear in at least one task.

| AC | Covered by |
|---|---|
| AC-1 | T-01, T-28 |
| AC-2 | T-01, T-07, T-15 |
| AC-3 | T-13, T-14, T-29 |
| AC-4 | T-14 |
| AC-5 | T-02, T-29 |
| AC-6 | T-14, T-29 |
| AC-7 | T-02, T-05 |
| AC-8 | T-04 |
| AC-9 | T-04 |
| AC-10 | T-08 |
| AC-11 | T-08 |
| AC-12 | T-22, T-23 |
| AC-13 | T-22 |
| AC-14 | T-14 |
| AC-15 | T-22 |
| AC-16 | T-20, T-21, T-22 |
| AC-17 | T-10 |
| AC-18 | T-11 |
| AC-19 | T-24 |
| AC-20 | T-07 |
| AC-21 | T-16 |
| AC-22 | T-12, T-14 |
| AC-23 | T-12, T-14 |
| AC-24 | T-25 |
| AC-25 | T-25, T-28 |
| AC-26 | T-14 |
| AC-27 | T-08, T-16 |
| AC-28 | T-21 |
| AC-29 | T-23 |
| AC-30 | T-21 |
| NFR-1 | T-03 |
| NFR-2 | T-02, T-29 |
| NFR-3 | T-14, T-29 |

Any AC with no task is a planning defect — fix it before `/implement` runs. None found: all
30 ACs and all 3 NFRs are covered above.

## Parallelisable

- T-09 (version provider) can run alongside T-06 once T-02 is done — both only need the
  project skeleton, not each other.
- T-12 (backend lint/format wiring) and T-13 (backend lockfiles) have no edge between them —
  either order, both only need T-02.
- CP-3 (T-14–T-18) has no dependency on CP-2 (T-06–T-13) — a second implementer could start
  the frontend scaffold as soon as CP-1 lands, though `/implement` runs checkpoints serially
  by design.
- T-18 (`ui/staff` placeholder) is independent of T-15–T-17 once T-14 is done.

## Related Specs

None — this is the first spec touching these components.
