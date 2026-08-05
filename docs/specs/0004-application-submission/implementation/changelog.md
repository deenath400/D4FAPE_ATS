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

## CP-4 — Hardening · 2026-08-06

**Tasks completed:** T-42, T-43, T-44, T-45, T-46

**Files created:** None — CP-4 strengthened one existing test, added two new integration test
methods (plus one private test-only helper class) to an existing file, and made surgical edits
to two meta files; no new production or test files were needed.

**Files modified**

| Path | Change |
|---|---|
| `backend/tests/Ats.UnitTests/Application/ApplicationServiceTests.cs` | `SubmitAsync_StorageThrows_ReturnsErrorNoRowWritten` (already existed from CP-2/T-14) annotated as the dedicated NFR-1 verification test (T-42) with an explanatory comment, and strengthened with `Assert.Empty(_dbContext.CvAttachments)` alongside the existing `Assert.Empty(_dbContext.Applications)` |
| `backend/tests/Ats.IntegrationTests/Application/ApplicationEndpointsTests.cs` | Added `POST_applications_SlowFileWrite_DoesNotExtendSqliteWriteLockDuration` (T-43, NFR-3) and its private `DelayedFileStorage` helper; added `POST_applications_TwoNearSimultaneousSubmissions_ExactlyOneSurvives` (T-44, E-1) |
| `docs/specs/0004-application-submission/plan/lld.md` | §11 Test Plan gained two rows for the new integration tests (no other change — no deviation from the designed behaviour) |
| `docs/specs/meta/architecture.md` | Removed the stale "Nothing below is built yet" blueprint-initialisation note (inaccurate since 0001 shipped; caught while re-reading the file in full per the consistency check); one Change Log row appended for CP-4. Component Map and Data Model ER diagram needed no edits — both were already fully accurate from CP-1's incremental updates (`shared/storage`'s "backing store TBD" was resolved there, not deferred to CP-4 as `tasks.md`'s T-45 wording literally suggests) |
| `docs/specs/meta/tech-stack.md` | Resolved the "Object storage: TBD" row in Data & Infrastructure to `LocalDiskFileStorage`; added `Storage:CvBasePath` and `Applications:MaxCvSizeBytes` to Required Configuration |
| `docs/specs/0004-application-submission/plan/tasks.md` | T-42–T-46 ticked `[x]`; `**Progress:**` line updated to `46 / 46 tasks · all checkpoints (CP-1–CP-4) complete` |
| `docs/specs/0004-application-submission/spec.md` | Frontmatter `status: implementing` → `status: implemented` |
| `docs/specs/index.md` | `0004` row status `implementing` → `implemented` |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-9 | T-42 required no new test method, only strengthening an existing one | `SubmitAsync_StorageThrows_ReturnsErrorNoRowWritten` (CP-2/T-14, tagged for E-4 in the LLD's original Test Plan) already exercises exactly what T-42 asks: a storage-write failure and an assertion that no `Application` row exists afterward. Rather than duplicate it under a new name (same class of situation as `0003`'s CP-4 I-10), it was annotated with an explicit NFR-1-citing comment and strengthened with a `CvAttachments`-empty assertion, so both E-4 and NFR-1 point at one dedicated, unambiguous test rather than two near-duplicates. |
| I-10 | T-43's NFR-3 test proves the claim indirectly, via SQLite's single-writer lock, rather than instrumenting `SaveChangesAsync` directly | `ApplicationService.SubmitAsync` already structures the write so the CV file write (step 8) completes *before* the row insert (steps 9–10) — there is no explicit transaction spanning both to instrument directly at the service layer the way `0003`'s NFR-2 test did (that test asserted a transaction was *never* opened; this one needs to assert *when* one opens). Instead, the test makes one submission's `IFileStorage.SaveAsync` artificially slow (a `DelayedFileStorage` decorator swapped in via `WithWebHostBuilder`/`ConfigureServices`) and, while it is still in flight, issues a second, wholly unrelated SQLite write (a candidate registration, which never touches `IFileStorage`) on a separate `HttpClient` against the same database file. SQLite's real single-writer lock (`journal_mode=WAL`, `busy_timeout=5000` — both already configured by `SqlitePragmaConnectionInterceptor`) is the actual mechanism NFR-3 exists to protect; the unrelated write finishing well before the artificial file-write delay elapses is direct proof the write transaction never held that lock during the file write. This is closer to what NFR-3 actually claims (SQLite lock contention under a public-portal write spike) than a would-be interceptor-only assertion. |
| I-11 | T-44's regression test asserts outcomes (exactly one `201`, one `409`, one surviving `Application` row) rather than forcing a specific code path | Two near-simultaneous requests over independent `HttpClient`s against the same in-process `TestServer` are genuinely concurrent, but nothing in this test *forces* both to pass the `AnyAsync` duplicate pre-check before either commits (the scenario that specifically exercises the `DbUpdateException` structural fallback, which `SubmitAsync_SaveChangesThrowsDbUpdateException_DeletesFileReturnsConflict` already isolates deterministically by forcing the throw). Asserting the outcome — never two survivors, regardless of interleaving — is what E-1 actually requires ("enforced structurally... not by application-level check timing alone") and holds true under any interleaving the test runner produces, avoiding a flaky test that depends on winning a scheduling race to be meaningful. |
| I-12 | Removed the "Nothing below is built yet" paragraph from the top of `architecture.md` | Left over from `/initialize-project`; false since `0001` first shipped, and increasingly misleading by CP-4 of the fourth spec to ship into the file (the very next section, Component Map, lists four owning specs). Fixing it is a one-paragraph deletion, well inside "surgical edit," and directly serves the file's stated purpose ("what exists and how it fits together") — leaving it would have been a known, avoidable defect going into `/validate`. |

**Deviations from the LLD:** None. T-42's strengthened assertion and T-43/T-44's new integration
tests are additive to `plan/lld.md` §11's Test Plan (which is patched to list the two new test
names), not substitutions for anything designed. No production code changed this checkpoint.

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
  Ats.IntegrationTests -> ...\tests\Ats.IntegrationTests\bin\Debug\net10.0\Ats.IntegrationTests.dll
  Ats.ArchitectureTests -> ...\tests\Ats.ArchitectureTests\bin\Debug\net10.0\Ats.ArchitectureTests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test tests/Ats.ArchitectureTests --no-build
Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 146 ms - Ats.ArchitectureTests.dll (net10.0)

$ dotnet test tests/Ats.UnitTests --no-build
Passed!  - Failed: 0, Passed: 96, Skipped: 0, Total: 96, Duration: 1 s - Ats.UnitTests.dll (net10.0)

$ dotnet test tests/Ats.IntegrationTests --no-build
Passed!  - Failed: 0, Passed: 61, Skipped: 0, Total: 61, Duration: 13 s - Ats.IntegrationTests.dll (net10.0)
```

(96 unit tests unchanged from CP-2/CP-3 — T-42 strengthened an existing test rather than adding
one. 61 integration tests = 59 pre-existing + 2 new: `POST_applications_SlowFileWrite_...` and
`POST_applications_TwoNearSimultaneousSubmissions_...`.) The two new, timing/concurrency-sensitive
integration tests were also run in isolation three additional times back-to-back to check for
flakiness before being folded into the full suite — all three runs passed with no observed
timing-margin failures.

```
$ cd frontend && npm run build
   ▲ Next.js 15.1.7
 ✓ Compiled successfully
   Linting and checking validity of types ...
 ✓ Generating static pages (9/9)

Route (app)                                Size     First Load JS
┌ ƒ /                                      1.55 kB         114 kB
├ ○ /_not-found                            979 B           106 kB
├ ƒ /api/auth/[...nextauth]                159 B           106 kB
├ ƒ /api/bff/proxy/[...path]                159 B           106 kB
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
ƒ Middleware                               85 kB

$ npm test
 Test Files  11 passed (11)
      Tests  44 passed (44)
```

No frontend production or test code changed this checkpoint (CP-4's tasks are backend-only plus
meta updates) — the frontend suite was re-run in full to satisfy the exit condition ("full
backend and frontend suites green") and confirms zero regression from CP-3's counts.

**NFR demonstration**

- **NFR-1** (submission is atomic with respect to a valid, persisted CV — no orphaned `Application`
  row): `SubmitAsync_StorageThrows_ReturnsErrorNoRowWritten` in `ApplicationServiceTests.cs`
  forces `IFileStorage.SaveAsync` to throw `IOException`, then asserts `SubmitAsync` returns a
  `500`-mapped `Error` result and that both `_dbContext.Applications` and
  `_dbContext.CvAttachments` remain empty.
- **NFR-3** (the write transaction spans only the row insert, not the CV file write):
  `POST_applications_SlowFileWrite_DoesNotExtendSqliteWriteLockDuration` in
  `ApplicationEndpointsTests.cs` makes one submission's file write artificially slow (1000ms) and,
  while it is in flight, issues an unrelated SQLite write (candidate registration) on a separate
  `HttpClient`; the unrelated write completing well under 1000ms proves SQLite's write lock was
  never held across the file write.

**Meta updates applied**

- `architecture.md`: removed a stale, inaccurate blueprint-init note (see I-12); one Change Log
  row appended for CP-4. Component Map (`api/application`, `service/application`, `db/application`,
  `shared/storage` all carrying owning spec `0004`, "backing store TBD" already resolved) and the
  Data Model ER diagram (`Application`/`CvAttachment` already present) needed no further edits —
  verified accurate by re-reading the file in full before editing, per the consistency check
  (`.spec-kit/meta-maintenance.md` §8). File is 174 lines — within the 200-line hard ceiling.
- `tech-stack.md`: "Object storage: TBD" row resolved; `Storage:CvBasePath` and
  `Applications:MaxCvSizeBytes` added to Required Configuration. File is 90 lines — within the
  120-line hard ceiling.
- `coding-standards.md`: no change — no new project-wide convention established this checkpoint.

**Known gaps**

- The pre-existing `dotnet format`/`npm run format` drift recorded in CP-2/CP-3's changelogs
  (CRLF line endings vs. the LF-expecting Prettier/`.editorconfig` config) remains unresolved —
  same reasoning as `0003`'s CP-4: fixing it here would touch files this checkpoint's tasks never
  named, across far more surface area than CP-4's scope.
- `docs/specs/meta/architecture.md`'s "Token handling across the HTTP boundary is unresolved"
  note (Cross-Cutting Concerns) is now stale — `0002` settled this with NextAuth v5 session
  storage — but it is `0002`'s territory, not named by any of this checkpoint's tasks, and
  outside `0004`'s component set; left for `/validate` or a future checkpoint of `0002` to close,
  flagged here rather than silently fixed to stay within this checkpoint's surgical-edit scope.

All 46 tasks across CP-1–CP-4 are now complete. Spec `0004` is `implemented`.

---
