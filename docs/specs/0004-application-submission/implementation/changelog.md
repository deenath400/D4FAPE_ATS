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
