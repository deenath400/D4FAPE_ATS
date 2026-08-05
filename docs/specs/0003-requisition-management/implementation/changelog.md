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
