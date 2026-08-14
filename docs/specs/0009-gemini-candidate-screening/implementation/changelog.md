# Implementation Changelog — 0009 Google Gemini 2.0 Flash Candidate Screening Integration

What actually shipped, checkpoint by checkpoint. Append-only. This is the record `/validate`
and future specs consult to learn what is really in the code, as opposed to what was designed.

---

## CP-1 — Data layer and ScreeningResult expansion · 2026-08-14

**Tasks completed:** T-01, T-02, T-03, T-04, T-05, T-06, T-07

**Files created**

| Path | Purpose |
|---|---|
| `backend/src/Db/Migrations/20260814033359_AddScreeningCategoryScores.cs` | EF Core migration adding `SkillsScore`, `ExperienceScore`, `EducationScore` nullable columns |
| `backend/src/Db/Migrations/20260814033359_AddScreeningCategoryScores.Designer.cs` | EF Core migration designer metadata |
| `backend/tests/Ats.UnitTests/Screening/ScreeningReportEntityTests.cs` | Unit tests for `ScreeningReport` category scores and clamping behavior |

**Files modified**

| Path | Change |
|---|---|
| `backend/src/Db/Applications/ScreeningReport.cs` | Added `SkillsScore`, `ExperienceScore`, `EducationScore` nullable properties; updated `Complete` method with clamping |
| `backend/src/Db/Configurations/ScreeningReportConfiguration.cs` | Added EF Core property mappings for `SkillsScore`, `ExperienceScore`, `EducationScore` |
| `backend/src/Db/Migrations/AppDbContextModelSnapshot.cs` | Updated snapshot with new `ScreeningReport` columns |
| `backend/src/Service/Screening/ScreeningResult.cs` | Added optional `SkillsScore`, `ExperienceScore`, `EducationScore` record fields |
| `backend/src/Service/Screening/Dtos/ScreeningReportDto.cs` | Added `SkillsScore`, `ExperienceScore`, `EducationScore` fields |
| `backend/src/Service/Screening/ScreeningOrchestrator.cs` | Passed category scores in `report.Complete()` and mapped them in `ToDto()` |
| `backend/tests/Ats.UnitTests/Screening/ScreeningOrchestratorTests.cs` | Added category scores test case |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-1 | Preserved default `null` values for optional category score parameters across `ScreeningResult` and `ScreeningReportDto` | Guarantees full backward compatibility for any existing callers and tests |

**Deviations from the LLD**

| LLD section | Designed | Actual | Reason | LLD patched? |
|---|---|---|---|---|
| — | — | — | None. | N/A |

**Verification run**

```
$ dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test tests/Ats.UnitTests
Test run for C:\D_Drive\D4FAPE-_ATS\backend\tests\Ats.UnitTests\bin\Debug\net10.0\Ats.UnitTests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed:     0, Passed:   206, Skipped:     0, Total:   206, Duration: 2 s - Ats.UnitTests.dll (net10.0)

$ dotnet test tests/Ats.ArchitectureTests
Test run for C:\D_Drive\D4FAPE-_ATS\backend\tests\Ats.ArchitectureTests\bin\Debug\net10.0\Ats.ArchitectureTests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 68 ms - Ats.ArchitectureTests.dll (net10.0)

$ dotnet test tests/Ats.IntegrationTests
Test run for C:\D_Drive\D4FAPE-_ATS\backend\tests\Ats.IntegrationTests\bin\Debug\net10.0\Ats.IntegrationTests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed:     0, Passed:   121, Skipped:     0, Total:   121, Duration: 21 s - Ats.IntegrationTests.dll (net10.0)

$ npm test (frontend)
Test Files  17 passed (17)
     Tests  68 passed (68)
```

**Meta updates applied**

- `docs/specs/meta/architecture.md`: Change Log row appended for CP-1.
- `tech-stack.md`: no change.
- `coding-standards.md`: no change.

**Known gaps carried into the next checkpoint**

- None for data and screening backend logic. Frontend category score rendering is planned for CP-3.

---

## CP-2 — Gemini service implementation and provider selection · 2026-08-14

**Tasks completed:** T-08, T-09, T-10, T-11, T-12, T-13

**Files created**

| Path | Purpose |
|---|---|
| `backend/src/Service/Screening/GeminiOptions.cs` | Strongly-typed configuration options for Gemini API |
| `backend/src/Service/Screening/GeminiModels.cs` | Request and response schema models for Gemini 2.0 Flash REST API structured output |
| `backend/src/Service/Screening/GeminiScreeningService.cs` | Typed `HttpClient` implementation of `IScreeningService` calling Gemini 2.0 Flash with retry and schema enforcement |
| `backend/tests/Ats.UnitTests/Screening/GeminiScreeningServiceTests.cs` | Unit tests for `GeminiScreeningService` covering success, auth failure, rate limit, service unavailable, malformed JSON, and CV truncation |

**Files modified**

| Path | Change |
|---|---|
| `backend/src/Service/Screening/MockScreeningService.cs` | Updated keyword-matching evaluator to return computed category breakdown scores |
| `backend/src/Service/ServiceCollectionExtensions.cs` | Added conditional `IScreeningService` provider selection (`Gemini` vs `Mock`) with fallback if API key is missing |
| `backend/src/Api/appsettings.json` | Added `Screening:Provider` and `Gemini` configuration sections |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-2 | Marked internal Gemini request/response models as `sealed` | Adheres to Roslyn code quality rule CA1852 |
| I-3 | Implemented exponential backoff for HTTP 429 / 503 within `GeminiScreeningService` | Handles transient rate limits and server hiccups before bubbling to outer orchestrator retry |

**Deviations from the LLD**

| LLD section | Designed | Actual | Reason | LLD patched? |
|---|---|---|---|---|
| — | — | — | None. | N/A |

**Verification run**

```
$ dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test tests/Ats.UnitTests
Test run for C:\D_Drive\D4FAPE-_ATS\backend\tests\Ats.UnitTests\bin\Debug\net10.0\Ats.UnitTests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed:     0, Passed:   213, Skipped:     0, Total:   213, Duration: 3 s - Ats.UnitTests.dll (net10.0)

$ dotnet test tests/Ats.ArchitectureTests
Test run for C:\D_Drive\D4FAPE-_ATS\backend\tests\Ats.ArchitectureTests\bin\Debug\net10.0\Ats.ArchitectureTests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 46 ms - Ats.ArchitectureTests.dll (net10.0)

$ dotnet test tests/Ats.IntegrationTests
Test run for C:\D_Drive\D4FAPE-_ATS\backend\tests\Ats.IntegrationTests\bin\Debug\net10.0\Ats.IntegrationTests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed:     0, Passed:   121, Skipped:     0, Total:   121, Duration: 22 s - Ats.IntegrationTests.dll (net10.0)
```

**Meta updates applied**

- `docs/specs/meta/architecture.md`: Appended Change Log row for CP-2.
- `tech-stack.md`: Added `Screening:Provider` and `Gemini:ApiKey` configuration keys.
- `coding-standards.md`: no change.

**Known gaps carried into the next checkpoint**

- `ui/staff` category score rendering in `ScreeningReportCard` and `ScreeningReportModal` is planned for CP-3.
