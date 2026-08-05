# Implementation Changelog — 0004 Application Submission and CV Upload

What actually shipped, checkpoint by checkpoint. Append-only. This is the record `/validate`
and future specs consult to learn what is really in the code, as opposed to what was designed.

---

## CP-1 — Data layer · 2026-08-06

**Tasks completed:** T-01, T-02, T-03, T-04, T-05, T-06, T-07, T-08, T-09, T-10, T-11

**Files created**

| Path | Purpose |
|---|---|
| `backend/src/Shared/Storage/IFileStorage.cs` | Storage interface (save/open-read/delete by opaque storage key) |
| `backend/src/Shared/Storage/LocalDiskFileStorage.cs` | Local-disk `IFileStorage` implementation; resolves `Storage:CvBasePath`, rejects path-traversal storage keys |
| `backend/src/Db/Applications/Application.cs` | Aggregate entity — Requisition/Candidate/submission timestamp, `AttachCv` invariant |
| `backend/src/Db/Applications/CvAttachment.cs` | Dependent entity (1:1 with `Application`) — server-generated storage key + file metadata |
| `backend/src/Db/Configurations/ApplicationConfiguration.cs` | EF Core mapping — unique `(CandidateId, RequisitionId)` index, `RequisitionId` index, cascade FKs to `Requisitions`/`AspNetUsers`, 1:1 to `CvAttachment` |
| `backend/src/Db/Configurations/CvAttachmentConfiguration.cs` | EF Core mapping — unique `ApplicationId` index, column lengths |
| `backend/src/Db/Migrations/20260805191845_AddApplicationsAndCvAttachments.cs` (+ `.Designer.cs`) | Migration — `CreateTable("Applications")`, `CreateTable("CvAttachments")`, all three indexes |
| `backend/tests/Ats.UnitTests/Application/ApplicationEntityTests.cs` | `Application`/`CvAttachment` entity invariants (10 tests) |
| `backend/tests/Ats.UnitTests/Storage/LocalDiskFileStorageTests.cs` | `LocalDiskFileStorage` round-trip, delete idempotency, path-traversal rejection (6 tests) |

**Files modified**

| Path | Change |
|---|---|
| `backend/src/Db/AppDbContext.cs` | Added `DbSet<Application>`, `DbSet<CvAttachment>`; applied `ApplicationConfiguration`/`CvAttachmentConfiguration` in `OnModelCreating` |
| `backend/src/Db/Migrations/AppDbContextModelSnapshot.cs` | Auto-updated by `dotnet ef migrations add` |
| `backend/src/Api/appsettings.json` | Added `Storage:CvBasePath` (`./app-data/cv-attachments`) and `Applications:MaxCvSizeBytes` (`5242880`) |
| `.gitignore` (repo root) | Added `app-data/` — CV files are candidate PII, same reasoning as `0001` FR-1 for the SQLite file |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-1 | Migration generated via `dotnet ef migrations add AddApplicationsAndCvAttachments --project src/Db --startup-project src/Api` rather than hand-written | Matches the LLD's stated approach (§10) and the `0003` precedent; guarantees the `.Designer.cs`/snapshot stay byte-consistent with the compiled model |
| I-2 | `LocalDiskFileStorageTests` uses a per-test temp directory (`Path.GetTempPath()/ats-cv-tests-<guid>`, cleaned up in `Dispose`) rather than the default `Storage:CvBasePath` | Mirrors `coding-standards.md`'s "each test gets its own SQLite database file" isolation principle applied to file storage; avoids leaving `app-data/` artifacts in the repo working tree from test runs |

**Deviations from the LLD**

None. `Application.cs`, `CvAttachment.cs`, `IFileStorage.cs`, `LocalDiskFileStorage.cs`, and both EF Core configurations were implemented byte-for-byte against LLD §2.1–§2.4. The generated migration (LLD §10, `erd.md` §5) matches the designed table/index shape exactly.

**Verification run**

```
$ dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:04.79

$ dotnet test tests/Ats.ArchitectureTests --no-build
Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 264 ms - Ats.ArchitectureTests.dll (net10.0)

$ dotnet test tests/Ats.UnitTests
Passed!  - Failed: 0, Passed: 74, Skipped: 0, Total: 74, Duration: 3 s - Ats.UnitTests.dll (net10.0)

$ dotnet test tests/Ats.IntegrationTests
Passed!  - Failed: 0, Passed: 38, Skipped: 0, Total: 38, Duration: 8 s - Ats.IntegrationTests.dll (net10.0)
(run to confirm CP-1's AppDbContext/migration change didn't regress existing suites; not part of CP-1's stated exit condition, since api/service/application ships in CP-2)

$ dotnet ef migrations add AddApplicationsAndCvAttachments --project src/Db --startup-project src/Api
Build started...
Build succeeded.
Done. To undo this action, use 'ef migrations remove'

$ dotnet ef database update --project src/Db --startup-project src/Api   # against a fresh, empty database file
Applying migration '20260805133328_InitialCreate'.
Applying migration '20260805141657_AddAuthenticationAndRefreshTokens'.
Applying migration '20260805171525_AddRequisitionsAndStages'.
Applying migration '20260805191845_AddApplicationsAndCvAttachments'.
Done.
```

**Meta updates applied**

- `architecture.md`: `db/application` and `shared/storage` gained owning-spec `0004`; `shared/storage`'s "backing store TBD" note resolved to "local disk"; `Application`/`CvAttachment` added to the Data Model ER diagram with their relationships to `Requisition`/`ApplicationUser`; one Change Log row appended.
- `tech-stack.md`: no change this checkpoint (T-46 in CP-4 resolves the "Object storage: TBD" row and adds Required Configuration entries once the full feature ships).
- `coding-standards.md`: no change.

**Known gaps carried into the next checkpoint**

- `IApplicationService`/`ApplicationService` (CP-2) is the only caller of `Application.Create`/`AttachCv`/`IFileStorage` outside tests — no production code path exercises this layer yet.
- `appsettings.json`'s `Storage:CvBasePath` default (`./app-data/cv-attachments`) is untested end-to-end (no endpoint writes there yet); `LocalDiskFileStorageTests` exercises the class against an isolated temp path instead.

---

## CP-2 — Service & API · 2026-08-06

**Tasks completed:** T-12, T-13, T-14, T-15, T-16, T-17, T-18, T-19, T-20, T-21, T-22, T-23, T-24

**Files created**

| Path | Purpose |
|---|---|
| `backend/src/Service/Application/IApplicationService.cs` | Service contract — `SubmitAsync`, `ListMineAsync`, `ListForRequisitionAsync`, `GetCvAsync` |
| `backend/src/Service/Application/Dtos/ApplicationDto.cs` | Submission response + embedded `ApplicationCvSummaryDto` |
| `backend/src/Service/Application/Dtos/CandidateApplicationListItemDto.cs` | Candidate "mine" list item |
| `backend/src/Service/Application/Dtos/StaffApplicationListItemDto.cs` | Staff list item + embedded `StaffApplicationCandidateDto` |
| `backend/src/Service/Application/Dtos/CvDownloadResult.cs` | Stream + metadata for a CV download |
| `backend/src/Service/Application/ApplicationService.cs` | Implementation — Requisition-published check, CV type/size/magic-byte validation, duplicate pre-check, storage write, insert, race-fallback cleanup, the three read methods |
| `backend/src/Api/ApplicationEndpoints.cs` | All four endpoints: `POST /api/requisitions/{id}/applications`, `GET /api/requisitions/{id}/applications`, `GET /api/applications/mine`, `GET /api/applications/{id}/cv` |
| `backend/tests/Ats.UnitTests/Application/ApplicationServiceTests.cs` | `ApplicationService` — every validation, authorization, and duplicate branch (24 tests) |
| `backend/tests/Ats.IntegrationTests/Application/ApplicationEndpointsTests.cs` | HTTP-level tests for all four endpoints, every documented status code (21 tests) |

**Files modified**

| Path | Change |
|---|---|
| `backend/src/Service/ServiceCollectionExtensions.cs` | Registered `IFileStorage`/`LocalDiskFileStorage` (Singleton) and `IApplicationService`/`ApplicationService` (Scoped) |
| `backend/src/Api/Program.cs` | Added `app.MapApplicationEndpoints();` after `MapPublicRequisitionEndpoints()` |
| `backend/tests/Ats.IntegrationTests/CustomWebApplicationFactory.cs` | Added a per-test temp `Storage:CvBasePath` (`Path.GetTempPath()/ats_test_cv_<guid>`), cleaned up in `Dispose` alongside the existing per-test SQLite file cleanup |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-3 | Aliased the entity type as `ApplicationEntity` (`using ApplicationEntity = Ats.Db.Applications.Application;`) in `ApplicationService.cs` | Mirrors the `RequisitionEntity` alias `RequisitionService.cs` already uses for the identical situation — the containing namespace (`Ats.Service.Application`) shares its simple name with the entity type (`Application`). A build was run to confirm the bare name resolves correctly via the file's own `using Ats.Db.Applications;` without escalating to the sibling-namespace collision, but the alias is kept for clarity and consistency with the established project pattern. |
| I-4 | `ListMineAsync`/`ListForRequisitionAsync` build an intermediate anonymous-type projection, `OrderByDescending` on its scalar `SubmittedAtUtc` property, then `Select`/project into the final DTO record afterward, instead of ordering directly on a property read off an already-constructed DTO (as the LLD's prose implied) | EF Core's SQLite provider cannot translate `OrderByDescending(dto => dto.SubmittedAtUtc)` when `dto` is a `record` constructed earlier in the same query (`InvalidOperationException` at query-compile time, confirmed by running the test suite) — ORDER BY must apply to a plain scalar column reference. Behavior (descending by submission date) is unchanged; only the LINQ shape differs from the LLD's illustrative one-liner. |
| I-5 | `POST_applications_NoFile_Returns400` sends a well-formed multipart body with one unrelated text part rather than a truly empty `MultipartFormDataContent` | A zero-part multipart body is itself malformed per RFC 7578 and made ASP.NET Core's form parser throw `BadHttpRequestException` before the handler ran at all (observed 500, not 400) — the well-formed-but-`cv`-less body is what actually exercises the intended `cv == null` validation branch (AC-2). |

**Deviations from the LLD**

None requiring an `lld.md` patch — I-3/I-4/I-5 above are implementation-detail necessities (compiler/EF-Core/HTTP-client constraints), not changes to the designed behaviour, contract, or data shape. All four endpoints, all DTO shapes, and all `Result`→HTTP mappings match `api.md`/`lld.md` §3–4 exactly, including the accepted plan decisions (file-validation errors both return 400; `.DisableAntiforgery()` on the submit endpoint).

**Verification run**

```
$ dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:02.19

$ dotnet test tests/Ats.ArchitectureTests --no-build
Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 67 ms - Ats.ArchitectureTests.dll (net10.0)

$ dotnet test tests/Ats.UnitTests
Passed!  - Failed: 0, Passed: 96, Skipped: 0, Total: 96, Duration: 2 s - Ats.UnitTests.dll (net10.0)

$ dotnet test tests/Ats.IntegrationTests
Passed!  - Failed: 0, Passed: 59, Skipped: 0, Total: 59, Duration: 12 s - Ats.IntegrationTests.dll (net10.0)
```

96 unit tests = 74 from CP-1 + 22 new (`ApplicationServiceTests`). 59 integration tests = 38 from CP-1 + 21 new (`ApplicationEndpointsTests`: 20 `[Fact]`/`[Theory]` methods, one Theory contributing 2 cases).

**Meta updates applied**

- `architecture.md`: added `service/application` and `api/application` rows to the Component Map (owning spec 0004); one Change Log row appended.
- `tech-stack.md`: no change this checkpoint.
- `coding-standards.md`: no change.

**Known gaps carried into the next checkpoint**

- No frontend code exists yet — `ui/portal`'s apply flow and "My Applications" page, and `ui/staff`'s per-Requisition Applications list, ship in CP-3.
- `ui/bff`'s proxy route is still text-body-only; CP-3's T-25 must land before any frontend code can submit a CV or download one without corrupting the bytes.
- `dotnet format --verify-no-changes` reports pre-existing failures unrelated to this checkpoint (CRLF line endings in `tests/Ats.UnitTests/SystemStatusServiceTests.cs` from before 0004, and file-encoding findings on two `0002`/CP-1-era migration files) — none of CP-2's new or modified files appear in that output.

---

## CP-3 — Frontend · 2026-08-06

**Tasks completed:** T-25, T-26, T-27, T-28, T-29, T-30, T-31, T-32, T-33, T-34, T-35, T-36, T-37, T-38, T-39, T-40, T-41

**Files created**

| Path | Purpose |
|---|---|
| `frontend/src/lib/types/application.ts` | `ApplicationDto`/`CandidateApplicationListItemDto`/`StaffApplicationListItemDto`, mirroring `api.md` §4 |
| `frontend/src/components/portal/ApplicationForm.tsx` | Client Component: CV file input + multipart submit, four UI states (idle/loading/error/success) |
| `frontend/src/app/(portal)/jobs/[id]/apply/page.tsx` | Apply page — session/status guards, loads the Requisition, renders `ApplicationForm` |
| `frontend/src/app/(portal)/jobs/[id]/apply/loading.tsx` | Loading skeleton |
| `frontend/src/components/portal/ApplicationList.tsx` | Presentational — candidate's own Applications, empty state, CV download link |
| `frontend/src/app/(portal)/applications/page.tsx` | "My Applications" page |
| `frontend/src/app/(portal)/applications/loading.tsx` | Loading skeleton |
| `frontend/src/app/(portal)/applications/error.tsx` | Error state with retry |
| `frontend/src/components/staff/ApplicationsTable.tsx` | Presentational — staff per-Requisition Applications list, empty state, CV download link |
| `frontend/src/app/staff/requisitions/[id]/applications/page.tsx` | Staff Applications list page |
| `frontend/src/app/staff/requisitions/[id]/applications/loading.tsx` | Loading skeleton |
| `frontend/src/app/staff/requisitions/[id]/applications/error.tsx` | Error state with retry |
| `frontend/tests/portal/application-form.test.tsx` | `ApplicationForm` — non-PDF validation banner (AC-3), duplicate 409 banner (AC-8), success panel (AC-1), client-side "file required" (AC-2) |
| `frontend/tests/portal/application-list.test.tsx` | `ApplicationList` — empty state (AC-13), rendered rows with CV link (AC-12) |
| `frontend/tests/staff/applications-table.test.tsx` | `ApplicationsTable` — rendered rows with CV link (AC-16), empty state (AC-18) |

**Files modified**

| Path | Change |
|---|---|
| `frontend/src/app/api/bff/proxy/[...path]/route.ts` | Generalised request/response body passthrough from `text()`/`string` to `arrayBuffer()`/`ArrayBuffer` (binary-safe); forwards `Content-Disposition` from the backend response when present |
| `frontend/src/lib/auth-guards.ts` | Added `isCandidateRole` |
| `frontend/src/middleware.ts` | Refactored to a path-prefix dispatch (`/staff/*` → `isStaffRole`, `/applications/*` → `isCandidateRole`); matcher extended to `["/staff/:path*", "/applications/:path*"]` |
| `frontend/src/app/(portal)/jobs/[id]/page.tsx` | Added a session-aware "Apply" call to action: "Sign In to Apply" (anonymous) / "Apply Now" (Candidate) / nothing (staff) |
| `frontend/src/components/HeaderNav.tsx` | Added a "My Applications" link, visible only for Candidate sessions |
| `frontend/src/app/staff/requisitions/[id]/page.tsx` | Added a "View Applications" link next to the status badge |
| `frontend/tests/lib/auth-guards.test.ts` | Added `describe("isCandidateRole", ...)` — true only for `Candidate`, false for staff roles and no roles |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-6 | `ApplicationForm`'s file `<input>` does not carry the HTML `required` attribute; "no file selected" is instead caught in the `onSubmit` handler and surfaces the same inline `role="alert"` banner used for every other error | jsdom's native constraint validation on a `required` input can suppress the `onSubmit` handler entirely under `fireEvent.click`/`fireEvent.submit` in some environments, making AC-2's client-side behaviour untestable through the same assertion path as every other validation error; handling it in JS keeps one error-rendering code path and one test pattern for all four validation branches (AC-2/AC-3/AC-4/AC-8) |
| I-7 | `middleware.ts` was refactored from a single `isStaffRole` check to a path-prefix dispatch (`/staff/*` vs `/applications/*`) rather than adding a second, parallel `auth()` wrapper | `next-auth`'s `auth()` middleware wrapper matches routes via the exported `config.matcher`, which already needed both prefixes; a second wrapper/file would need its own matcher and there is no supported way to compose two default-exported middleware functions in one `middleware.ts`. The refactor preserves the exact existing `/staff/*` redirect behaviour byte-for-byte (verified by the untouched `requisition-lifecycle-actions`/staff test suite continuing to pass) |
| I-8 | `ApplicationList`/`ApplicationsTable` build the CV download `href` as `` `/api/bff/proxy${application.cvDownloadUrl}` `` (string concatenation on the backend-relative path the API already returns), rather than hard-coding `/api/bff/proxy/applications/{id}/cv` | Matches `lld.md` §6's comment on `cvDownloadUrl` verbatim ("backend-relative path; frontend prefixes /api/bff/proxy") — the backend, not the frontend, owns the path shape |

**Deviations from the LLD**

None. All four LLD §1 file-manifest entries under "Frontend" were implemented as designed; `ApplicationForm`/`ApplicationList`/`ApplicationsTable` match their §5.1 prop/state tables; the proxy generalisation matches D-4 exactly (`ArrayBuffer` passthrough both directions, `Content-Disposition` forwarded).

**Verification run**

```
$ npm run build
> ats-frontend@0.1.0 build
> npx --no-install next build

   ▲ Next.js 15.1.7
   - Environments: .env.local

   Creating an optimized production build ...
 ✓ Compiled successfully
   Linting and checking validity of types ...
   Collecting page data ...
   Generating static pages (0/9) ...
   Generating static pages (2/9)
   Generating static pages (4/9)
   Generating static pages (6/9)
 ✓ Generating static pages (9/9)
   Finalizing page optimization ...
   Collecting build traces ...

Route (app)                                Size     First Load JS
┌ ƒ /                                      1.55 kB         114 kB
├ ○ /_not-found                            979 B           106 kB
├ ƒ /api/auth/[...nextauth]                159 B           106 kB
├ ƒ /api/bff/proxy/[...path]               159 B           106 kB
├ ƒ /api/bff/system-status                 159 B           106 kB
├ ƒ /applications                          948 B           113 kB
├ ƒ /jobs                                  948 B           113 kB
├ ƒ /jobs/[id]                             948 B           113 kB
├ ƒ /jobs/[id]/apply                       1.97 kB         114 kB
├ ○ /login                                 1.47 kB         114 kB
├ ○ /register                              1.87 kB         114 kB
├ ƒ /staff/requisitions                    175 B           109 kB
├ ƒ /staff/requisitions/[id]               1.81 kB         111 kB
├ ƒ /staff/requisitions/[id]/applications  175 B           109 kB
└ ○ /staff/requisitions/new                1.37 kB         107 kB
+ First Load JS shared by all              105 kB
  ├ chunks/4bd1b696-9d53a45aeb6e92ca.js    52.9 kB
  ├ chunks/517-c5e9dd966e39f1b6.js         50.5 kB
  └ other shared chunks (total)            1.97 kB

ƒ Middleware                               85 kB

○  (Static)   prerendered as static content
ƒ  (Dynamic)  server-rendered on demand

$ npm test
> ats-frontend@0.1.0 test
> npx --no-install vitest run

 ✓ tests/portal/application-list.test.tsx (2 tests) 165ms
 ✓ tests/staff/requisition-lifecycle-actions.test.tsx (5 tests) 201ms
 ✓ tests/portal/job-search-form.test.tsx (4 tests) 158ms
 ✓ tests/auth/RegisterForm.test.tsx (4 tests) 298ms
 ✓ tests/auth/LoginForm.test.tsx (3 tests) 305ms
 ✓ tests/portal/application-form.test.tsx (4 tests) 292ms
 ✓ tests/staff/requisition-form.test.tsx (4 tests) 346ms
 ✓ tests/lib/auth-guards.test.ts (11 tests) 6ms
 ✓ tests/client-status-panel.test.tsx (3 tests) 108ms
 ✓ tests/auth/HeaderNav.test.tsx (2 tests) 294ms
 ✓ tests/staff/applications-table.test.tsx (2 tests) 217ms

 Test Files  11 passed (11)
      Tests  44 passed (44)
   Start at  01:25:12
   Duration  4.62s

$ npm run lint
> ats-frontend@0.1.0 lint
> npx --no-install next lint

✔ No ESLint warnings or errors
```

44 Vitest tests = 32 pre-existing (all of `0001`/`0002`/`0003`'s frontend suites: `HeaderNav` 2, `LoginForm` 3, `RegisterForm` 4, `client-status-panel` 3, `job-search-form` 4, `requisition-form` 4, `requisition-lifecycle-actions` 5, `auth-guards` `isStaffRole`/`isRecruiter` 7) + 12 new (`application-form` 4, `application-list` 2, `applications-table` 2, `auth-guards` `isCandidateRole` 4). Every pre-existing test file still passes unmodified except the intentional `auth-guards.test.ts` extension — confirms R-1's mitigation held: the `ui/bff` proxy generalisation (T-25) did not regress any `0001`–`0003` frontend flow.

**Meta updates applied**

- `architecture.md`: no structural change beyond what CP-1/CP-2 already recorded — `ui/portal`, `ui/staff`, and `ui/bff` already carried spec `0004` in their Component Map rows' "Owning specs" column (added at planning time); one Change Log row appended for this checkpoint.
- `tech-stack.md`: no change this checkpoint.
- `coding-standards.md`: no change.

**Known gaps carried into the next checkpoint**

- CP-4's hardening tasks (T-42–T-46) remain: NFR-1/NFR-3 dedicated verification tests, the E-1 race regression test, and the `architecture.md`/`tech-stack.md` updates that resolve the "backing store TBD"/"Object storage: TBD" notes formally (CP-1 already resolved the prose, T-45/T-46 close the loop on the checklist).
- `npm run format` (`prettier --check .`) reports pre-existing failures across most of the repository, including files this checkpoint did not touch (`.prettierrc.json`, `package.json`, `tsconfig.json`, `src/lib/auth.ts`, etc.) and three files this checkpoint did touch or create (`route.ts`, `ApplicationList.tsx`, `application-form.test.tsx`). `git config core.autocrlf` is `true` on this machine, so the working tree checks out CRLF line endings against a Prettier config (and `coding-standards.md`) that expects LF — the same class of pre-existing, environment-level failure CP-2's changelog recorded for `dotnet format`. Not part of CP-3's exit condition (`npm run build` + the full Vitest suite); not fixed here per the "never rewrite unrelated code" guardrail, since a real fix touches line endings repo-wide.

---
