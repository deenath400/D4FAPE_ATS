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
