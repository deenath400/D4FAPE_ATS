# Implementation Changelog — 0008 Automated Candidate Screening

What actually shipped, checkpoint by checkpoint. Append-only. This is the record `/validate`
and future specs consult to learn what is really in the code, as opposed to what was designed.

---

## CP-1 — Data layer and Domain · 2026-08-14

**Tasks completed:** T-01, T-02, T-03, T-04, T-05, T-06, T-07, T-08, T-09

**Files created**

| Path | Purpose |
|---|---|
| `backend/src/Db/Applications/ScreeningStatus.cs` | Lifecycle status enum (`Pending`, `Completed`, `Failed`) |
| `backend/src/Db/Applications/ScreeningRecommendation.cs` | Recommendation enum (`Advance`, `Review`) |
| `backend/src/Db/Applications/ScreeningReport.cs` | 1-to-1 screening report entity with score clamping and state transitions |
| `backend/src/Db/Configurations/ScreeningReportConfiguration.cs` | EF Core mapping, unique index on `ApplicationId`, string enum conversions |
| `backend/src/Db/Migrations/20260813232645_AddScreeningReport.cs` | EF Core migration creating `ScreeningReports` table |
| `backend/tests/Ats.UnitTests/Screening/ScreeningReportTests.cs` | Unit tests for `ScreeningReport` invariants and state machine |
| `backend/tests/Ats.UnitTests/Pipeline/StageTransitionSystemMoveTests.cs` | Unit tests for `StageTransition.CreateSystemMove` factory |

**Files modified**

| Path | Change |
|---|---|
| `backend/src/Db/Applications/Application.cs` | Added optional `ScreeningReport?` navigation property |
| `backend/src/Db/AppDbContext.cs` | Registered `DbSet<ScreeningReport>` and applied `ScreeningReportConfiguration` |
| `backend/src/Db/Pipeline/StageTransition.cs` | Added `CreateSystemMove` factory method setting `ActorKind.System` and `ActorUserId = null` |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-1 | Auto-clamp score in `ScreeningReport.Complete` to `[0, 100]` | Guarantees entity domain invariants even if external AI evaluation returns boundary-exceeding numbers |

**Deviations from the LLD**

| LLD section | Designed | Actual | Reason | LLD patched? |
|---|---|---|---|---|
| — | — | — | None. Implementation matches LLD exactly. | Yes |

**Verification run**

```
$ dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test tests/Ats.UnitTests
Test run for C:\D_Drive\D4FAPE-_ATS\backend\tests\Ats.UnitTests\bin\Debug\net10.0\Ats.UnitTests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed:     0, Passed:   186, Skipped:     0, Total:   186, Duration: 2 s - Ats.UnitTests.dll (net10.0)

$ dotnet test tests/Ats.ArchitectureTests
Test run for C:\D_Drive\D4FAPE-_ATS\backend\tests\Ats.ArchitectureTests\bin\Debug\net10.0\Ats.ArchitectureTests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 113 ms - Ats.ArchitectureTests.dll (net10.0)

$ dotnet test tests/Ats.IntegrationTests
Test run for C:\D_Drive\D4FAPE-_ATS\backend\tests\Ats.IntegrationTests\bin\Debug\net10.0\Ats.IntegrationTests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed:     0, Passed:   113, Skipped:     0, Total:   113, Duration: 22 s - Ats.IntegrationTests.dll (net10.0)
```

**Meta updates applied**

- `architecture.md`: updated ER diagram to include `ScreeningReport` (1-to-1 with `Application`); added change log row for 0008 CP-1.
- `tech-stack.md`: no change.
- `coding-standards.md`: no change.

**Known gaps carried into the next checkpoint**

- None from CP-1.

---

## CP-2 — Service Layer (Screening + Pipeline Extension) · 2026-08-14

**Tasks completed:** T-10, T-11, T-12, T-13, T-14, T-15, T-16, T-17, T-18, T-19

**Files created**

| Path | Purpose |
|---|---|
| `backend/src/Service/Screening/IPdfTextExtractor.cs` | Interface for PDF text extraction |
| `backend/src/Service/Screening/PdfTextExtractor.cs` | Extracts plain text from PDF stream using `PdfPig` |
| `backend/src/Service/Screening/IScreeningService.cs` | Pluggable AI screening evaluation abstraction |
| `backend/src/Service/Screening/ScreeningResult.cs` | Record holding evaluation score, recommendation, summary, strengths, and concerns |
| `backend/src/Service/Screening/MockScreeningService.cs` | Deterministic keyword-matching test/dev implementation with configurable threshold |
| `backend/src/Service/Screening/IScreeningOrchestrator.cs` | Orchestrator interface for running screening and fetching reports |
| `backend/src/Service/Screening/ScreeningOrchestrator.cs` | Orchestrates text extraction, AI evaluation with retry, report persistence, and conditional auto-advance |
| `backend/src/Service/Screening/Dtos/ScreeningReportDto.cs` | Staff-facing screening report DTO |
| `backend/tests/Ats.UnitTests/Screening/MockScreeningServiceTests.cs` | Unit tests for deterministic mock scoring and threshold evaluation |
| `backend/tests/Ats.UnitTests/Screening/PdfTextExtractorTests.cs` | Unit tests for PDF text extraction error handling |
| `backend/tests/Ats.UnitTests/Screening/ScreeningOrchestratorTests.cs` | Unit tests for orchestrator happy path, failure retry, empty CV handling, auto-advance, and report retrieval |

**Files modified**

| Path | Change |
|---|---|
| `backend/src/Service/Ats.Service.csproj` | Added `PdfPig` (0.1.9) package reference |
| `backend/src/Service/Pipeline/IPipelineService.cs` | Added `SystemMoveToNextStageAsync` method |
| `backend/src/Service/Pipeline/PipelineService.cs` | Implemented `SystemMoveToNextStageAsync` recording `StageTransition.CreateSystemMove` with `ActorKind.System` |
| `backend/src/Service/Application/ApplicationService.cs` | Injected `IServiceScopeFactory?` and added background fire-and-forget screening trigger in `SubmitAsync` |
| `backend/src/Service/ServiceCollectionExtensions.cs` | Registered `MockScreeningService`, `PdfTextExtractor`, and `ScreeningOrchestrator` in DI |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-2 | Added `IPdfTextExtractor` interface implemented by `PdfTextExtractor` | Allows clean, decoupled unit testing of `ScreeningOrchestrator` without generating physical PDF binaries in memory |

**Deviations from the LLD**

| LLD section | Designed | Actual | Reason | LLD patched? |
|---|---|---|---|---|
| §1 / §3.3 | Static `PdfTextExtractor` | `IPdfTextExtractor` interface + implementation registered as singleton in DI | Allows mocking extraction results in orchestrator unit tests while keeping runtime behaviour identical | Yes |

**Verification run**

```
$ dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test tests/Ats.UnitTests
Test run for C:\D_Drive\D4FAPE-_ATS\backend\tests\Ats.UnitTests\bin\Debug\net10.0\Ats.UnitTests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed:     0, Passed:   201, Skipped:     0, Total:   201, Duration: 2 s - Ats.UnitTests.dll (net10.0)

$ dotnet test tests/Ats.ArchitectureTests
Test run for C:\D_Drive\D4FAPE-_ATS\backend\tests\Ats.ArchitectureTests\bin\Debug\net10.0\Ats.ArchitectureTests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 316 ms - Ats.ArchitectureTests.dll (net10.0)

$ dotnet test tests/Ats.IntegrationTests
Test run for C:\D_Drive\D4FAPE-_ATS\backend\tests\Ats.IntegrationTests\bin\Debug\net10.0\Ats.IntegrationTests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed:     0, Passed:   113, Skipped:     0, Total:   113, Duration: 17 s - Ats.IntegrationTests.dll (net10.0)
```

**Meta updates applied**

- `architecture.md`: added `service/screening` to Component Map; appended change log row for 0008 CP-2.
- `tech-stack.md`: added `PdfPig` (0.1.9) under Packages.
- `coding-standards.md`: no change.

**Known gaps carried into the next checkpoint**

- HTTP endpoints in `ApplicationEndpoints.cs` and pipeline board DTO badge fields scheduled for CP-3.
