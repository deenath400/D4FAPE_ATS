# Implementation Changelog — 0003 Requisition Management

What actually shipped, checkpoint by checkpoint. Append-only. This is the record `/validate`
and future specs consult to learn what is really in the code, as opposed to what was designed.

---

## CP-1 — Data layer · 2026-08-05

**Tasks completed:** T-01, T-02, T-03, T-04, T-05, T-06

**Files created**

| Path | Purpose |
|---|---|
| `backend/src/Db/Requisitions/RequisitionStatus.cs` | Lifecycle enum (`Draft`/`Published`/`Closed`) |
| `backend/src/Db/Requisitions/Requisition.cs` | Aggregate entity — content fields, status transitions, owns `Stages` |
| `backend/src/Db/Requisitions/Stage.cs` | Owned-by-Requisition entity, ownership shape only (FR-14) |
| `backend/src/Db/Configurations/RequisitionConfiguration.cs` | EF Core mapping for `Requisitions` table |
| `backend/src/Db/Configurations/StageConfiguration.cs` | EF Core mapping for `Stages` table |
| `backend/src/Db/Migrations/20260805171525_AddRequisitionsAndStages.cs` | Migration — creates `Requisitions`/`Stages`, both indexes, cascade FK |
| `backend/src/Db/Migrations/20260805171525_AddRequisitionsAndStages.Designer.cs` | Migration designer snapshot (generated) |
| `backend/tests/Ats.UnitTests/Requisition/RequisitionEntityTests.cs` | Entity invariant tests: draft-start, content update, all three transitions, Stage ownership (AC-23), Stage validation |

**Files modified**

| Path | Change |
|---|---|
| `backend/src/Db/AppDbContext.cs` | Added `DbSet<Requisition>`, `DbSet<Stage>`; applied `RequisitionConfiguration`/`StageConfiguration` in `OnModelCreating` |
| `backend/src/Db/Migrations/AppDbContextModelSnapshot.cs` | Auto-updated by `dotnet ef migrations add` to include the new entities |
| `backend/global.json` | SDK pin changed from `10.0.400-preview.0.26322.102` to `10.0.302` — see Deviations |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-1 | Implemented `Requisition`/`Stage`/configurations/migration exactly as specified in `plan/lld.md` §2, no field or shape changes | LLD was precise and directly buildable; no ambiguity to resolve |
| I-2 | Test file uses fully-qualified `Ats.Db.Requisitions.Requisition.Create(...)` rather than the bare `Requisition.Create(...)` the LLD's own snippet uses | The test file's namespace is `Ats.UnitTests.Requisition` (mirrors the `Requisition/` folder, per the `Ats.UnitTests.Auth` precedent) — the bare type name `Requisition` is ambiguous with the enclosing namespace segment of the same name. Fully qualifying only the four `Requisition.Create` call sites resolves it without changing the entity's public API or the LLD's design. |

**Deviations from the LLD**

| LLD section | Designed | Actual | Reason | LLD patched? |
|---|---|---|---|---|
| Build environment (`backend/global.json`, not an LLD section) | SDK pin `10.0.400-preview.0.26322.102` | SDK pin `10.0.302` | The pinned preview SDK is no longer resolvable in this environment or on Microsoft's `10.0` release feed (superseded by the stable LTS release); `dotnet build` failed with "SDK not found" before any CP-1 code existed. Repinning to the installed stable SDK on the same .NET 10 major.minor unblocked the build. | Yes — recorded in `plan/lld.md` Deviation Log |

No deviation in the entity/config/migration/test shapes themselves — T-01 through T-06 match `plan/lld.md` §2 and `plan/erd.md` exactly.

**Verification run**

```
$ cd backend && dotnet build
  Determining projects to restore...
  Restored ... (7 projects)
  Ats.Shared -> ...\src\Shared\bin\Debug\net10.0\Ats.Shared.dll
  Ats.Db -> ...\src\Db\bin\Debug\net10.0\Ats.Db.dll
  Ats.Service -> ...\src\Service\bin\Debug\net10.0\Ats.Service.dll
  Ats.UnitTests -> ...\tests\Ats.UnitTests\bin\Debug\net10.0\Ats.UnitTests.dll
  Ats.Api -> ...\src\Api\bin\Debug\net10.0\Ats.Api.dll
  Ats.ArchitectureTests -> ...\tests\Ats.ArchitectureTests\bin\Debug\net10.0\Ats.ArchitectureTests.dll
  Ats.IntegrationTests -> ...\tests\Ats.IntegrationTests\bin\Debug\net10.0\Ats.IntegrationTests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet ef migrations add AddRequisitionsAndStages --project src/Db --startup-project src/Db
Build started...
Build succeeded.
Done. To undo this action, use 'ef migrations remove'

$ ConnectionStrings__Default="Data Source=<fresh-tmp-file>.db" dotnet ef database update --project src/Db --startup-project src/Db
Build started...
Build succeeded.
Acquiring an exclusive lock for migration application. See https://aka.ms/efcore-docs-migrations-lock for more information if this takes too long.
Applying migration '20260805133328_InitialCreate'.
Applying migration '20260805141657_AddAuthenticationAndRefreshTokens'.
Applying migration '20260805171525_AddRequisitionsAndStages'.
Done.

$ dotnet test tests/Ats.UnitTests --no-restore
Test run for ...\tests\Ats.UnitTests\bin\Debug\net10.0\Ats.UnitTests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    27, Skipped:     0, Total:    27, Duration: 2 s - Ats.UnitTests.dll (net10.0)
```

(27 = 17 pre-existing + 10 new `RequisitionEntityTests`.) Also ran `dotnet test tests/Ats.ArchitectureTests --no-build` for extra confidence (not part of CP-1's stated exit condition): `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4`.

The fresh-database test file was created under a temp name, verified, and deleted after the check — no `.db` artifact was committed.

**Meta updates applied**

- `architecture.md`: added `REQUISITION ||--o{ STAGE` to the Data Model ER diagram; Change Log row appended for CP-1.
- `tech-stack.md`: Runtimes table `.NET` row updated from `10.0.400-preview` to `10.0.302` to reflect the SDK pin fix (Deviations).
- `coding-standards.md`: no change — no new project-wide convention established this checkpoint.

**Known gaps carried into the next checkpoint**

- `Stage.Create` has no caller yet — no Stage CRUD endpoint ships in this spec (by design, per Non-Goals); it exists solely so `RequisitionEntityTests` can prove FR-14/AC-23 and so the future pipeline spec has a stable factory.
- CP-2 (Service/API layer) depends on this checkpoint's `Requisitions`/`Stages` tables and `AppDbContext` registration, both in place.

---

## CP-2 — Service / API layer · 2026-08-05

**Tasks completed:** T-07, T-08, T-09, T-10, T-11, T-12, T-13, T-14, T-15, T-16, T-17

**Files created**

| Path | Purpose |
|---|---|
| `backend/src/Service/Common/PagedResult.cs` | Generic pagination envelope (`Items`, `Page`, `PageSize`, `Total`) — first use in the project |
| `backend/src/Service/Requisition/Dtos/RequisitionDto.cs` | Staff-facing DTO (any status) |
| `backend/src/Service/Requisition/Dtos/PublicRequisitionDto.cs` | Anonymous-facing DTO (no `status` field, by construction) |
| `backend/src/Service/Requisition/Dtos/CreateRequisitionRequestDto.cs` | Create request body |
| `backend/src/Service/Requisition/Dtos/UpdateRequisitionRequestDto.cs` | Edit request body |
| `backend/src/Service/Requisition/IRequisitionService.cs` | Service contract — 9 methods per LLD §3.1 |
| `backend/src/Service/Requisition/RequisitionService.cs` | Implementation: create, edit, publish/unpublish/close (with transition guards), staff get/list, public detail, public paginated keyword search |
| `backend/src/Api/RequisitionEndpoints.cs` | Staff endpoints 1–7 (`api.md` §2) — `RecruiterOnly` for writes, `StaffOnly` for reads |
| `backend/src/Api/PublicRequisitionEndpoints.cs` | Public endpoints 8–9 — anonymous, manual `page`/`pageSize` parsing with 400-before-query on invalid `page` (AC-24) |
| `backend/tests/Ats.UnitTests/Requisition/RequisitionServiceTests.cs` | 21 tests covering every `RequisitionService` method and every AC/NFR in scope |
| `backend/tests/Ats.IntegrationTests/Requisition/RequisitionEndpointsTests.cs` | 15 HTTP tests over the staff surface (create/edit/lifecycle/list/authorization) |
| `backend/tests/Ats.IntegrationTests/Requisition/PublicRequisitionEndpointsTests.cs` | 11 HTTP tests over the public surface (search, pagination, detail, invalid page, NFR-1 clamp) |

**Files modified**

| Path | Change |
|---|---|
| `backend/src/Service/Common/Result.cs` | Added a three-argument `Validation(errors, code, message)` overload to `Result` and `Result<T>`, alongside the existing two-argument overload — see Deviations |
| `backend/src/Service/ServiceCollectionExtensions.cs` | Registered `IRequisitionService` → `RequisitionService` as scoped |
| `backend/src/Api/Program.cs` | Added `app.MapRequisitionEndpoints();` and `app.MapPublicRequisitionEndpoints();` |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-3 | Namespace-collision avoidance: `RequisitionService.cs` uses `using RequisitionEntity = Ats.Db.Requisitions.Requisition;` and a `RequisitionStatus` alias rather than fully-qualifying every call site | The service file's namespace is `Ats.Service.Requisition` (singular) — same collision class as CP-1's `Ats.UnitTests.Requisition` vs. `Ats.Db.Requisitions.Requisition`. An alias reads cleaner than repeated full qualification across a file with this many entity references; `RequisitionServiceTests.cs` doesn't reference the entity type directly, so no alias was needed there. |
| I-4 | `PublicRequisitionEndpoints` reads `Requisitions:DefaultPageSize` directly via an injected `IConfiguration` parameter, rather than the service resolving the default | `SearchPublicAsync`'s interface signature (LLD §3.1) takes non-nullable `int page, int pageSize` — the default must already be resolved before the service is called. The service still owns the `MaxPageSize` clamp (NFR-1 ceiling). Both config keys remain functional exactly as `plan/lld.md` §9 documents; this is a responsibility split within the wiring the LLD's own endpoint table (§4) already assigns to the API layer ("manual `page`/`pageSize` parse"), not a contract change. |
| I-5 | Integration tests create Recruiter/HiringManager users directly via `UserManager<ApplicationUser>` + `AddToRoleAsync`, then log in through `/api/auth/login` for a real bearer token, instead of using `/api/auth/register` | `/api/auth/register` (spec 0002) unconditionally assigns the `Candidate` role — there is no staff-registration endpoint in this codebase (out of scope for 0002 and 0003 alike). Roles are pre-seeded via `AppDbContext.SeedRoles` (CP-1), so `AddToRoleAsync` succeeds without any new seeding code. |

**Deviations from the LLD**

| LLD section | Designed | Actual | Reason | LLD patched? |
|---|---|---|---|---|
| §3.2 `SearchPublicAsync` step 3 | `.Where(r => r.Title.Contains(keyword) \|\| r.Description.Contains(keyword))` | `.Where(r => EF.Functions.Like(r.Title, $"%{keyword}%") \|\| EF.Functions.Like(r.Description, $"%{keyword}%"))` | EF Core 10's Sqlite provider translates `string.Contains` to `instr()`, which is case-sensitive — not the case-insensitive `LIKE` the LLD assumed. Caught by a failing unit test (`SearchPublicAsync_WithMatchingKeyword_ReturnsOnlyPublished`, keyword `"engineer"` vs. title `"Senior Engineer"`) before any endpoint code was written. `EF.Functions.Like` restores the intended case-insensitive match (AC-16, AC-20). | Yes — §3.2 and Deviation Log |
| §3.2 "Returns" table / shared `Result` class | `Result<T>.Validation(errors)` (two-arg, no code) | Added `Result<T>.Validation(errors, code, message)` (three-arg) to `Ats.Service.Common.Result`/`Result<T>` | The two-arg overload cannot carry an error code, so `ToProblemResult()` would emit the generic `"auth.error"` code for every 400 instead of `api.md`'s documented `requisition.create.validation-failed`/`requisition.update.validation-failed`. Backward-compatible addition — the existing two-arg overload and `AuthService`'s calls to it are untouched. | Yes — §3.2 and Deviation Log |

**Verification run**

```
$ cd backend && dotnet build
  Determining projects to restore...
  All projects are up-to-date for restore.
  Ats.Shared -> ...\src\Shared\bin\Debug\net10.0\Ats.Shared.dll
  Ats.Db -> ...\src\Db\bin\Debug\net10.0\Ats.Db.dll
  Ats.Service -> ...\src\Service\bin\Debug\net10.0\Ats.Service.dll
  Ats.UnitTests -> ...\tests\Ats.UnitTests\bin\Debug\net10.0\Ats.UnitTests.dll
  Ats.Api -> ...\src\Api\bin\Debug\net10.0\Ats.Api.dll
  Ats.ArchitectureTests -> ...\tests\Ats.ArchitectureTests\bin\Debug\net10.0\Ats.ArchitectureTests.dll
  Ats.IntegrationTests -> ...\tests\Ats.IntegrationTests\bin\Debug\net10.0\Ats.IntegrationTests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test tests/Ats.UnitTests --no-restore
Test run for ...\tests\Ats.UnitTests\bin\Debug\net10.0\Ats.UnitTests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    50, Skipped:     0, Total:    50, Duration: 2 s - Ats.UnitTests.dll (net10.0)

$ dotnet test tests/Ats.IntegrationTests --no-restore
Test run for ...\tests\Ats.IntegrationTests\bin\Debug\net10.0\Ats.IntegrationTests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    38, Skipped:     0, Total:    38, Duration: 7 s - Ats.IntegrationTests.dll (net10.0)
```

(50 unit tests = 27 pre-existing [17 Auth + 10 `RequisitionEntityTests`] + 23 new `RequisitionServiceTests`. 38 integration tests = 12 pre-existing [9 `AuthEndpointsTests` + 3 `SystemStatusEndpointTests`] + 15 new `RequisitionEndpointsTests` [11 `[Fact]` + 2 `[Theory]` cases × 2] + 11 new `PublicRequisitionEndpointsTests` [8 `[Fact]` + 1 `[Theory]` × 3 cases].)

Also ran `dotnet test tests/Ats.ArchitectureTests --no-build` for extra confidence (not part of CP-2's stated exit condition, but confirms the new `Ats.Api` → `Ats.Service` → `Ats.Db` layering wasn't violated): `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4`.

**Meta updates applied**

- `architecture.md`: Component Map rows for `api/requisition`, `service/requisition` now show real endpoints/service instead of "not yet built"; Change Log row appended for CP-2. See diff below.
- `tech-stack.md`: no change — no new dependency, command, or config key (the two `Requisitions:*` keys were already documented in `plan/lld.md` §9, which is spec-local, not `meta/tech-stack.md`).
- `coding-standards.md`: no change — no new project-wide convention; the `Result.Validation` three-arg overload is a shared-infra addition, not a new rule to codify.

**Known gaps carried into the next checkpoint**

- CP-3 (Frontend) depends on this checkpoint's `RequisitionDto`/`PublicRequisitionDto` JSON shapes and the `/api/requisitions`, `/api/public/requisitions` routes, both in place and verified against `api.md`.
- `Requisitions:DefaultPageSize`/`Requisitions:MaxPageSize` are read with `int.TryParse`-with-fallback (`20`/`50`) exactly like `Jwt:*` keys elsewhere — no explicit values are set in `appsettings.json`, matching the "No" required column in `plan/lld.md` §9.
- NFR-2 (public reads never open a write transaction) is structurally true — `GetPublicByIdAsync`/`SearchPublicAsync` never call `SaveChangesAsync` — but the dedicated assertion test is CP-4's T-41, not written here.

---

## CP-3 — Frontend · 2026-08-05

**Tasks completed:** T-18, T-19, T-20, T-21, T-22, T-23, T-24, T-25, T-26, T-27, T-28, T-29, T-30, T-31, T-32, T-33, T-34, T-35, T-36, T-37, T-38, T-39

**Files created**

| Path | Purpose |
|---|---|
| `frontend/src/lib/types/requisition.ts` | Shared TS types mirroring `api.md` §4 (`RequisitionDto`, `PublicRequisitionDto`, `Paged<T>`, request DTOs) |
| `frontend/src/lib/auth-guards.ts` | Pure role-check helpers (`isStaffRole`, `isRecruiter`), no NextAuth/Next.js imports — usable from Edge middleware and Server Components alike |
| `frontend/src/middleware.ts` | `/staff/*` route gating (FR-9) — redirects an anonymous session to `/login` and a Candidate session to `/`, closing `0002`'s E-9 |
| `frontend/src/app/staff/layout.tsx` | Staff workspace shell — reuses `HeaderNav`, replaces the `0001` `(staff)` placeholder route group |
| `frontend/src/components/staff/RequisitionForm.tsx` | Create/edit client form — client-side validation (title required/≤200 chars, description required), POST/PUT via the `ui/bff` proxy |
| `frontend/src/components/staff/RequisitionLifecycleActions.tsx` | Publish/unpublish/close buttons; renders nothing when `canWrite=false` (HiringManager) |
| `frontend/src/app/staff/requisitions/page.tsx` | Staff requisition list (AC-12), status badges, "New Requisition" CTA, empty state |
| `frontend/src/app/staff/requisitions/loading.tsx` | Skeleton loading state |
| `frontend/src/app/staff/requisitions/error.tsx` | Error boundary with retry |
| `frontend/src/app/staff/requisitions/new/page.tsx` | Create page (AC-1) |
| `frontend/src/app/staff/requisitions/[id]/page.tsx` | Detail/edit/lifecycle page (AC-3–AC-11); resolves `canWrite` from the session server-side |
| `frontend/src/app/staff/requisitions/[id]/loading.tsx` | Skeleton loading state |
| `frontend/src/app/staff/requisitions/[id]/error.tsx` | Error boundary with retry |
| `frontend/src/components/portal/JobSearchForm.tsx` | Progressive-enhancement `<form method="get">` keyword search — no client JS |
| `frontend/src/components/portal/JobList.tsx` | Presentational card list + Prev/Next pagination, empty state |
| `frontend/src/app/(portal)/jobs/page.tsx` | Public jobs list (AC-16–AC-20, AC-24); `export const dynamic` not needed — reading `searchParams` already opts the route into per-request rendering |
| `frontend/src/app/(portal)/jobs/loading.tsx` | Skeleton loading state |
| `frontend/src/app/(portal)/jobs/[id]/page.tsx` | Public job detail (AC-21, AC-22); `notFound()` on a 404 `BackendInvokeError` |
| `frontend/src/app/(portal)/jobs/[id]/loading.tsx` | Skeleton loading state |
| `frontend/tests/lib/auth-guards.test.ts` | `isStaffRole`/`isRecruiter` unit tests (AC-14, AC-15) |
| `frontend/tests/staff/requisition-form.test.tsx` | Validation, create redirect, edit refresh, 409-on-closed banner (AC-1, AC-3, AC-5) |
| `frontend/tests/staff/requisition-lifecycle-actions.test.tsx` | `canWrite=false` renders nothing, per-status button sets, 409 banner (AC-6, AC-7, AC-9, AC-10, AC-11) |
| `frontend/tests/portal/job-search-form.test.tsx` | `JobSearchForm` GET-form shape, `JobList` empty state and pagination (AC-16, AC-17, AC-20) |

**Files modified**

| Path | Change |
|---|---|
| `frontend/src/components/HeaderNav.tsx` | Added a "Browse Jobs" link (all sessions) and a "Staff Workspace" link gated by `isStaffRole` (authenticated staff sessions only) |
| `frontend/package-lock.json` | Synced by `npm install` — the committed lock file predated this checkpoint and was stale relative to `package.json` (see Deviations); no dependency versions changed, `package.json` is untouched |

**Files deleted**

| Path | Reason |
|---|---|
| `frontend/src/app/(staff)/.gitkeep` | Replaced by the real `frontend/src/app/staff/` route segment (T-22, LLD D-4) |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-6 | `middleware.ts` wraps the same `auth()` export `lib/auth.ts` already provides (`export default auth((req) => {...})`), rather than a hand-rolled JWT decode | This is NextAuth v5's documented middleware pattern and reuses the exact session shape (`session.user.roles`) `auth-guards.ts` and every Server Component already expect — no second source of truth for "what roles does this session have." |
| I-7 | `(portal)/jobs/page.tsx` and `(portal)/jobs/[id]/page.tsx` carry no explicit `export const dynamic` | Next.js 15 already opts a route into per-request (dynamic) rendering when it reads `searchParams` (`/jobs`) or has an unresolved dynamic segment with no `generateStaticParams` (`/jobs/[id]`, `/staff/requisitions/[id]`). Confirmed by the build's route table (`ƒ` = dynamic) — adding a redundant `dynamic` export would be dead code. |
| I-8 | `frontend/src/app/staff/requisitions/page.tsx` carries an explicit `export const dynamic = "force-dynamic"` | Unlike the two routes above, this page has no `searchParams` and no dynamic segment, so Next.js's default build-time static-generation pass tried to prerender it and called `invokeBackend` with no backend reachable and no `API_BASE_URL` configured — see Deviations. The explicit dynamic export documents *why* it must never be prerendered (session/role-dependent staff data), not just silences the build failure. |
| I-9 | `RequisitionForm`/`RequisitionLifecycleActions` read/report backend errors via `problem?.detail \|\| problem?.title`, matching `RegisterForm.tsx`'s existing pattern exactly | LLD §5.1 says "mirrors `RegisterForm.tsx`'s structure" — reusing the same error-extraction shape means both forms behave identically for every `ProblemDetails` code this spec's endpoints (`api.md` §3) can return (`requisition.*.validation-failed`, `requisition.update.closed`, `requisition.*.invalid-transition`). |

**Deviations from the LLD**

| LLD section | Designed | Actual | Reason | LLD patched? |
|---|---|---|---|---|
| Build precondition (not an LLD section — build environment) | `npm ci` installs cleanly from the committed lock file | `npm ci` failed ("can only install packages when your package.json and package-lock.json ... are in sync"); `npm install` was run instead, which updated `package-lock.json` (34 insertions/24 deletions, integrity/resolved metadata only) | The committed lock file predated this checkpoint's `npm install` and had drifted out of sync with `package.json` (no dependency version or `package.json` change caused this — confirmed via `git diff frontend/package.json`, empty). This is a pre-existing environment gap, the same class of issue CP-1 hit with the backend SDK pin, not a CP-3 code defect. `npm install` is the documented recovery per npm's own error message; no new dependency was added. | No — build-environment fact, not a design decision |
| §5.1 File Manifest (implicit — LLD doesn't call out `export const dynamic`) | Not specified | Added `export const dynamic = "force-dynamic"` to `frontend/src/app/staff/requisitions/page.tsx` | `npm run build` failed prerendering `/staff/requisitions`: Next.js's default static-generation pass called `invokeBackend` (no `API_BASE_URL` set at build time, and no backend process running during `next build` regardless). Every other new data-fetching page in this checkpoint already reads `searchParams` or a dynamic route segment, which Next.js 15 auto-detects as request-dependent; the staff list page has neither, so it needed the explicit opt-out. Functionally correct either way — staff data must never be statically cached across sessions — this only makes the existing intent (LLD §5.3: "Normal SSR on navigation") build-time explicit. | Yes — LLD §5.1 file manifest row annotated below |

**Verification run**

```
$ cd frontend && npm install
added 504 packages, and audited 505 packages in 3m
3 vulnerabilities (2 high, 1 critical) — pre-existing in `next@15.1.7` (CVE-2025-66478),
unrelated to this checkpoint's code; no dependency was upgraded or added by CP-3.

$ npm run build
   ▲ Next.js 15.1.7
 ⚠ Compiled with warnings
   (jose/dist/webapi/lib/deflate.js: CompressionStream/DecompressionStream not supported in the
   Edge Runtime — transitively pulled in by next-auth's `auth()` wrapper now used in
   `middleware.ts`; does not fail the build, no JWE encryption is used by this project's tokens)
 ✓ Compiled successfully
   Linting and checking validity of types ...
 ✓ Generating static pages (9/9)

Route (app)                              Size     First Load JS
┌ ○ /                                    1.51 kB         114 kB
├ ○ /_not-found                          979 B           106 kB
├ ƒ /api/auth/[...nextauth]              152 B           106 kB
├ ƒ /api/bff/proxy/[...path]             152 B           106 kB
├ ƒ /api/bff/system-status               152 B           106 kB
├ ƒ /jobs                                908 B           113 kB
├ ƒ /jobs/[id]                           908 B           113 kB
├ ○ /login                               1.47 kB         114 kB
├ ○ /register                            1.75 kB         114 kB
├ ƒ /staff/requisitions                  171 B           109 kB
├ ƒ /staff/requisitions/[id]             1.79 kB         107 kB
└ ○ /staff/requisitions/new              1.37 kB         107 kB
ƒ Middleware                             85 kB

$ npm test
 Test Files  8 passed (8)
      Tests  31 passed (31)

$ npm run lint
✔ No ESLint warnings or errors
```

(31 tests = 3 pre-existing [`client-status-panel`] + 2 [`HeaderNav`] + 3 [`LoginForm`] + 3
[`RegisterForm`] = 11 pre-existing, + 7 new `auth-guards.test.ts` + 4 new
`requisition-form.test.tsx` + 5 new `requisition-lifecycle-actions.test.tsx` + 4 new
`job-search-form.test.tsx` = 20 new.)

`npm run format` was also run for extra confidence (not part of CP-3's stated exit condition):
it reports pre-existing formatting drift across 41 files, including files this checkpoint never
touched (`package.json`, `.prettierrc.json`, `tsconfig.json`, `vitest.config.ts`) — a
pre-existing baseline condition (likely CRLF line endings on this Windows environment vs.
Prettier's LF expectation), not introduced by CP-3. Left as-is per "do not attribute a
pre-existing failure to your work" — flagged for CP-4/`/validate` attention.

**Meta updates applied**

- `architecture.md`: `ui/staff` Component Map row now lists owning spec `0003` (was `—`);
  `ui/portal` row gained `0003` alongside `0001`. One Change Log row appended for CP-3.
- `tech-stack.md`: no change — no new dependency, command, or config key.
- `coding-standards.md`: no change — no new project-wide convention; `RequisitionForm`/
  `RequisitionLifecycleActions` follow the existing `RegisterForm.tsx` error-banner/spinner
  idiom rather than establishing a new one.

**Known gaps carried into the next checkpoint**

- The pre-existing `npm run format` drift (41 files, see Verification) is unresolved — CP-4 or
  `/validate` should decide whether to normalize it or record it as an accepted environment
  fact (Windows line endings).
- The Edge Runtime `CompressionStream`/`DecompressionStream` build warning from `jose` (via
  `next-auth`'s `auth()` wrapper now exercised in `middleware.ts`) is a warning, not a build
  failure, and does not affect JWT signing/verification (only unused JWE compression paths);
  not addressed here as it would require either an experimental Next.js Node.js-runtime
  middleware flag (not in `tech-stack.md`) or a `next-auth` upgrade (not authorised for CP-3).
- CP-4 (Hardening) depends on this checkpoint's `ui/staff`/`ui/portal` pages and `middleware.ts`
  being in place; `meta/architecture.md`'s final CP-4 pass should confirm the Data Model diagram
  and Component Map need no further CP-3-driven changes beyond what was applied here.

---

## CP-4 — Hardening · 2026-08-05

**Tasks completed:** T-40, T-41, T-42

**Files created:** None — CP-4 added one test method to an existing file and made
formatting-only edits; no new production or test files were needed.

**Files modified**

| Path | Change |
|---|---|
| `backend/tests/Ats.UnitTests/Requisition/RequisitionServiceTests.cs` | Added `PublicReads_GetByIdAndSearch_NeverOpenATransaction` (T-41, NFR-2) and its private `TransactionTrackingInterceptor` helper |
| `backend/src/Db/Migrations/20260805171525_AddRequisitionsAndStages.cs` | Formatting-only: removed the UTF-8 BOM `dotnet ef migrations add` wrote when this file was generated in CP-1, so it satisfies `.editorconfig`'s `charset = utf-8` and `dotnet format --verify-no-changes`; no code change |
| `frontend/src/app/(portal)/jobs/[id]/page.tsx`, `frontend/src/app/staff/layout.tsx`, `frontend/src/app/staff/requisitions/[id]/page.tsx`, `frontend/src/app/staff/requisitions/loading.tsx`, `frontend/src/components/staff/RequisitionForm.tsx`, `frontend/src/components/staff/RequisitionLifecycleActions.tsx`, `frontend/src/components/HeaderNav.tsx`, `frontend/tests/staff/requisition-form.test.tsx`, `frontend/tests/staff/requisition-lifecycle-actions.test.tsx` | Formatting-only: `prettier --write` (line-wrap/spacing only, e.g. a long `||`-chained error-message expression); no logic change. These are the only 9 of the 41 `npm run format`-flagged files this spec owns — see Deviations |
| `docs/specs/0003-requisition-management/plan/tasks.md` | T-40/T-41/T-42 ticked `[x]`; `**Progress:**` line updated to `42 / 42 tasks · all checkpoints complete` |
| `docs/specs/0003-requisition-management/spec.md` | Frontmatter `status: implementing` → `status: implemented` |
| `docs/specs/index.md` | `0003` row status `implementing` → `implemented` |
| `docs/specs/meta/architecture.md` | One Change Log row appended for CP-4 (see Meta Updates — Component Map and ER diagram needed no edits, both were already accurate from CP-1–CP-3) |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-10 | T-40 required no new test code | `GET_public_requisitions_PageSize1000_Returns50Items` in `PublicRequisitionEndpointsTests.cs` — requesting `pageSize=1000` and asserting `200 OK` with `pageSize: 50` and 50 items returned — already exists, added ahead of schedule during CP-2's T-17 (the CP-2 changelog explicitly lists "NFR-1 clamp" among that checkpoint's 11 public-endpoint tests). It already demonstrates exactly what T-40 asks: the request is *clamped*, not *rejected* (a `400`/`422` would be the "rejected" alternative this test rules out). Re-ran it as part of this checkpoint's full-suite verification rather than duplicating it. |
| I-11 | T-41's new test asserts both `dbContext.Database.CurrentTransaction == null` (the LLD's literal suggestion) and that no transaction was ever *opened* during the call, via a `Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor` attached to a second, instrumented `AppDbContext` sharing the same in-memory SQLite connection | The bare `CurrentTransaction == null` check only proves no transaction is open *after* the call returns — it would not catch a transaction that was opened and then committed/rolled back and disposed *during* the call. The interceptor records every `TransactionStarted`/`TransactionStartedAsync` callback EF Core fires (whether from an explicit `BeginTransactionAsync()` or the implicit transaction `SaveChangesAsync` wraps writes in), so the assertion actually exercises the mechanism NFR-2 cares about, not just its end state. This is additive to, not a replacement of, the LLD's designed assertion. |
| I-12 | `dotnet format --verify-no-changes` / `npm run format`: fixed only the files this spec created or modified; left all pre-existing 0001/0002 drift untouched | `dotnet format --verify-no-changes` failed on 41 backend `.cs` files before any CP-4 edit — 40 of them are pre-existing 0001/0002 files (CRLF line endings or a BOM `dotnet ef migrations add` wrote into every migration, including 0001's and 0002's, not just 0003's) that predate this checkpoint and that this checkpoint's tasks never name. The one file among the 41 that CP-1 of *this* spec created (`AddRequisitionsAndStages.cs`) was fixed (BOM removed). Symmetrically, `npm run format` failed on 41 frontend files; 9 of them are files this spec created or modified (a `prettier --write` line-wrap fix, no logic change) and were fixed; the remaining 32 are 0001/0002 files or repo config (`package.json`, `tsconfig.json`, etc.) and were left as-is. Reformatting ~72 files this spec never touched would violate "never rewrite unrelated code" for a purely cosmetic, pre-existing condition (same class as CP-1's SDK-pin fix and CP-3's `npm install` lock-file drift — an environment fact, not a CP-4 code defect). Recorded here per the explicit instruction to be transparent about this call; a future spec or a dedicated formatting-normalization task should decide whether to reformat the remaining 32+32 files repo-wide. |

**Deviations from the LLD:** None. T-41's test is an *addition* to the LLD §11 Test Plan's
suggested assertion (`GetPublicByIdAsync_NeverOpensTransaction`), not a substitution — the LLD
itself offered `dbContext.Database.CurrentTransaction == null` as one acceptable shape ("a
dedicated `RequisitionServiceTests` case asserting..."); the interceptor is a stronger
implementation of exactly that intent, not a design change. No `plan/lld.md` edit was needed.

**Verification run**

```
$ cd backend && dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test tests/Ats.UnitTests --no-restore
Passed!  - Failed:     0, Passed:    51, Skipped:     0, Total:    51, Duration: 3 s - Ats.UnitTests.dll (net10.0)

$ dotnet test tests/Ats.IntegrationTests --no-restore
Passed!  - Failed:     0, Passed:    38, Skipped:     0, Total:    38, Duration: 8 s - Ats.IntegrationTests.dll (net10.0)

$ dotnet test tests/Ats.ArchitectureTests --no-build
Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 67 ms - Ats.ArchitectureTests.dll (net10.0)

$ dotnet format --verify-no-changes
# After removing the BOM from AddRequisitionsAndStages.cs (this spec's own file):
# 40 remaining ENDOFLINE/CHARSET errors, all in pre-existing 0001/0002 files
# (e.g. Auth*, RefreshToken*, SystemStatus*, CustomWebApplicationFactory.cs,
# LayeringRuleTests.cs, the 0001/0002 migrations) — none owned by 0003. See Deviations.
```

```
$ cd frontend && npm run build
✓ Compiled successfully, 9/9 static pages generated (unchanged route table from CP-3)

$ npm test
 Test Files  8 passed (8)
      Tests  31 passed (31)

$ npm run lint
✔ No ESLint warnings or errors

$ npm run format
# After `prettier --write` on the 9 files this spec owns:
# 32 remaining files flagged, all pre-existing 0001/0002 files or repo config
# (package.json, tsconfig.json, RegisterForm.tsx, backend-invoke.ts, etc.) — none
# owned by 0003. See Deviations.
```

(51 unit tests = 50 pre-existing + 1 new `PublicReads_GetByIdAndSearch_NeverOpenATransaction`.
38 integration tests unchanged from CP-2 — `GET_public_requisitions_PageSize1000_Returns50Items`
was already counted there.)

**NFR demonstration**

- **NFR-1** (`pageSize` above 50 is clamped, not rejected): `GET_public_requisitions_PageSize1000_Returns50Items` in `PublicRequisitionEndpointsTests.cs` requests `?pageSize=1000` and asserts `200 OK` with `pageSize: 50` and exactly 50 items returned (55 published requisitions seeded) — proving the request succeeds and is silently clamped rather than rejected with a `4xx`.
- **NFR-2** (public reads never open a write transaction): `PublicReads_GetByIdAndSearch_NeverOpenATransaction` in `RequisitionServiceTests.cs` calls `GetPublicByIdAsync` and `SearchPublicAsync` through an instrumented `AppDbContext` carrying a `DbTransactionInterceptor`, then asserts the interceptor never observed a `TransactionStarted`/`TransactionStartedAsync` callback and that `Database.CurrentTransaction` is `null` afterward.

**Meta updates applied**

- `architecture.md`: one Change Log row appended for CP-4. The Component Map (owning spec
  `0003` for `api/requisition`, `service/requisition`, `db/requisition`, `ui/staff`, and
  `ui/portal`'s `0001, 0003` note) and the Data Model ER diagram (`Requisition ||--o{ Stage`)
  were already fully accurate from CP-1–CP-3's incremental edits — verified by re-reading the
  file in full before editing, per the consistency check (`.spec-kit/meta-maintenance.md` §8).
  No other line changed. File is 167 lines — within the 200-line hard ceiling (over the
  150-line target, unchanged from before this checkpoint's one-line addition).
- `tech-stack.md`: no change — no new dependency, command, or config key.
- `coding-standards.md`: no change — no new project-wide convention.

**Known gaps**

- Pre-existing `dotnet format`/`npm run format` drift in 40 backend and 32 frontend files from
  specs 0001/0002 (plus repo config files) remains unresolved — see Deviations. Recommend a
  dedicated hygiene task/spec if the team wants it normalized repo-wide, since fixing it here
  would touch far more surface area than this checkpoint's named tasks.
- The Edge Runtime `CompressionStream`/`DecompressionStream` build warning noted in CP-3
  remains, for the same reason recorded there.

All 42 tasks across CP-1–CP-4 are now complete. Spec `0003` is `implemented`.

---
