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
