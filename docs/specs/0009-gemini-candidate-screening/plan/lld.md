# Low-Level Design — 0009 Google Gemini 2.0 Flash Candidate Screening Integration

**Spec:** `../spec.md` · **HLD:** `hld.md` · **Updated:** 2026-08-14

> This file is **living**: when implementation diverges from this design, `/implement`
> patches the affected section here and records the deviation in
> `../implementation/changelog.md`. Silent drift is a defect.

---

## 1. File Manifest

| Action | Path | Purpose |
|---|---|---|
| Create | `backend/src/Service/Screening/GeminiScreeningService.cs` | Typed `HttpClient` integration with Gemini 2.0 Flash REST API |
| Create | `backend/src/Service/Screening/GeminiOptions.cs` | Strongly-typed configuration model for Gemini API settings |
| Create | `backend/src/Service/Screening/GeminiModels.cs` | Request/response DTO models for the Gemini REST API |
| Modify | `backend/src/Service/Screening/ScreeningResult.cs` | Add `SkillsScore`, `ExperienceScore`, `EducationScore` optional parameters |
| Modify | `backend/src/Service/Screening/MockScreeningService.cs` | Return category breakdown scores in `ScreeningResult` |
| Modify | `backend/src/Service/Screening/ScreeningOrchestrator.cs` | Pass category scores to `ScreeningReport.Complete()` |
| Modify | `backend/src/Service/Screening/Dtos/ScreeningReportDto.cs` | Add category score fields |
| Modify | `backend/src/Db/Applications/ScreeningReport.cs` | Add `SkillsScore`, `ExperienceScore`, `EducationScore` nullable int properties |
| Modify | `backend/src/Db/Configurations/ScreeningReportConfiguration.cs` | Map three new columns |
| Create | `backend/src/Db/Migrations/*_AddScreeningCategoryScores.cs` | EF Core migration for three new columns |
| Modify | `backend/src/Service/ServiceCollectionExtensions.cs` | Conditional `IScreeningService` registration based on `Screening:Provider` + `Gemini:ApiKey` |
| Modify | `backend/src/Api/appsettings.json` | Add `Screening:Provider` and `Gemini` config section |
| Modify | `frontend/src/lib/types/screening.ts` | Add category score fields to `ScreeningReportDto` |
| Modify | `frontend/src/components/staff/ScreeningReportCard.tsx` | Render category score bars |
| Modify | `frontend/src/components/staff/ScreeningReportModal.tsx` | Render category score bars |
| Create | `backend/tests/Ats.UnitTests/Screening/GeminiScreeningServiceTests.cs` | Unit tests with mocked `HttpMessageHandler` |
| Modify | `backend/tests/Ats.UnitTests/Screening/ScreeningOrchestratorTests.cs` | Update `ScreeningResult` construction for new fields |
| Create | `backend/tests/Ats.IntegrationTests/Screening/GeminiProviderRegistrationTests.cs` | Integration tests for provider selection/fallback |

## 2. Domain / Data Layer

### 2.1 `ScreeningReport` — `backend/src/Db/Applications/ScreeningReport.cs`

```csharp
public class ScreeningReport
{
    // ... existing properties unchanged ...
    public int? SkillsScore { get; private set; }
    public int? ExperienceScore { get; private set; }
    public int? EducationScore { get; private set; }

    // Modified factory — unchanged, new properties default to null
    public static ScreeningReport CreatePending(Guid applicationId) { /* unchanged */ }

    // Modified Complete method — gains three optional parameters
    public void Complete(
        int score,
        ScreeningRecommendation recommendation,
        string summary,
        string strengths,
        string concerns,
        int? skillsScore = null,
        int? experienceScore = null,
        int? educationScore = null)
    {
        // ... existing validation + assignment ...
        SkillsScore = skillsScore.HasValue ? Math.Clamp(skillsScore.Value, 0, 100) : null;
        ExperienceScore = experienceScore.HasValue ? Math.Clamp(experienceScore.Value, 0, 100) : null;
        EducationScore = educationScore.HasValue ? Math.Clamp(educationScore.Value, 0, 100) : null;
    }
}
```

**Invariants.**
- Category scores are nullable — `null` when the provider doesn't supply them (Mock in legacy mode, or `Failed` reports).
- When non-null, each score is clamped to `[0, 100]`.

**Persistence notes.** Three new nullable INTEGER columns in `ScreeningReports` table. `IsRequired(false)` in EF config.

### 2.2 `ScreeningReportConfiguration` — `backend/src/Db/Configurations/ScreeningReportConfiguration.cs`

Add three property mappings:

```csharp
builder.Property(s => s.SkillsScore).IsRequired(false);
builder.Property(s => s.ExperienceScore).IsRequired(false);
builder.Property(s => s.EducationScore).IsRequired(false);
```

No new indexes — these columns are not queried independently.

## 3. Service / Application Layer

### 3.1 `GeminiOptions` — `backend/src/Service/Screening/GeminiOptions.cs`

```csharp
public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.0-flash";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxCvTextLength { get; set; } = 50_000;
}
```

### 3.2 `GeminiModels` — `backend/src/Service/Screening/GeminiModels.cs`

Internal request/response models matching the Gemini REST API shape:

```csharp
// Request models
internal class GeminiRequest
{
    public List<GeminiContent> Contents { get; set; }
    public GeminiGenerationConfig GenerationConfig { get; set; }
    public GeminiSystemInstruction? SystemInstruction { get; set; }
}

internal class GeminiContent { public string Role { get; set; } public List<GeminiPart> Parts { get; set; } }
internal class GeminiPart { public string Text { get; set; } }
internal class GeminiSystemInstruction { public List<GeminiPart> Parts { get; set; } }
internal class GeminiGenerationConfig
{
    public string ResponseMimeType { get; set; } = "application/json";
    public GeminiResponseSchema ResponseSchema { get; set; }
}

internal class GeminiResponseSchema
{
    public string Type { get; set; } = "object";
    public Dictionary<string, GeminiSchemaProperty> Properties { get; set; }
    public List<string> Required { get; set; }
}

internal class GeminiSchemaProperty
{
    public string Type { get; set; }
    public string? Description { get; set; }
    public int? Minimum { get; set; }
    public int? Maximum { get; set; }
    public List<string>? Enum { get; set; }
    public GeminiSchemaProperty? Items { get; set; }
}

// Response models
internal class GeminiResponse
{
    public List<GeminiCandidate>? Candidates { get; set; }
}

internal class GeminiCandidate { public GeminiCandidateContent? Content { get; set; } }
internal class GeminiCandidateContent { public List<GeminiPart>? Parts { get; set; } }

// Parsed structured output
internal class GeminiScreeningOutput
{
    public int Score { get; set; }
    public int SkillsScore { get; set; }
    public int ExperienceScore { get; set; }
    public int EducationScore { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> Strengths { get; set; } = new();
    public List<string> Concerns { get; set; } = new();
}
```

### 3.3 `GeminiScreeningService.EvaluateAsync` — `backend/src/Service/Screening/GeminiScreeningService.cs`

**Signature**

```csharp
public class GeminiScreeningService : IScreeningService
{
    public GeminiScreeningService(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiScreeningService> logger);
    public Task<ScreeningResult> EvaluateAsync(string requisitionTitle, string requisitionDescription, string cvText, CancellationToken ct = default);
}
```

**Behaviour**

1. Truncate `cvText` to `GeminiOptions.MaxCvTextLength` characters (E-2).
2. Build system instruction: a standardized ATS screening rubric prompt instructing the model to evaluate the CV against the job title/description and produce scores in Skills Fit, Experience Fit, Education Fit categories, plus overall score, recommendation, summary, strengths, concerns.
3. Build `GeminiRequest` with user content containing the job title, description, and CV text. Set `GenerationConfig.ResponseMimeType` to `"application/json"` and provide a `ResponseSchema` enforcing the `GeminiScreeningOutput` shape.
4. Serialize and `POST` to `{BaseUrl}/v1beta/models/{Model}:generateContent?key={ApiKey}`.
5. On HTTP 429 or 503: wait with exponential backoff (1s, then 2s), retry up to 2 times. On persistent failure: throw.
6. On HTTP 401/403: throw immediately with `"Gemini authentication failed: check API key"` (no retry for auth errors).
7. On HTTP 400 or other non-2xx: throw with status code and response body excerpt.
8. On 200: deserialize `GeminiResponse`, extract the first candidate's first part text, deserialize as `GeminiScreeningOutput`.
9. If JSON deserialization fails (E-3): throw with `"Invalid structured JSON response from AI provider"`.
10. Map `GeminiScreeningOutput.Recommendation` to `ScreeningRecommendation` enum. If unrecognised, default to `Review`.
11. Return `ScreeningResult` with all fields including category scores.

**Returns**

| Outcome | Result | Upstream maps to |
|---|---|---|
| Success | `ScreeningResult` with all fields | Orchestrator calls `report.Complete(...)` |
| Transient failure (after retries) | throws `Exception` | Orchestrator catches, calls `report.Fail(...)` |
| Auth failure | throws `Exception` | Orchestrator catches, calls `report.Fail(...)` |
| Malformed response | throws `Exception` | Orchestrator catches, calls `report.Fail(...)` |

### 3.4 `ScreeningResult` — `backend/src/Service/Screening/ScreeningResult.cs`

```csharp
public record ScreeningResult(
    int Score,
    ScreeningRecommendation Recommendation,
    string Summary,
    string Strengths,
    string Concerns,
    int? SkillsScore = null,
    int? ExperienceScore = null,
    int? EducationScore = null);
```

Backward-compatible: existing callers passing 5 positional args still compile; category scores default to `null`.

### 3.5 `MockScreeningService` — modification

Update to produce deterministic category scores (derived from the existing keyword-match logic):

```csharp
var skillsScore = Math.Clamp(matchCount * 12, 0, 100);
var experienceScore = Math.Clamp(matchCount * 10, 0, 100);
var educationScore = Math.Clamp(matchCount * 8, 0, 100);

return Task.FromResult(new ScreeningResult(
    score, recommendation, summary, strengthsJson, concernsJson,
    skillsScore, experienceScore, educationScore));
```

### 3.6 `ScreeningOrchestrator.RunScreeningAsync` — modification

The `report.Complete(...)` call gains three new arguments:

```csharp
report.Complete(
    screeningResult.Score,
    screeningResult.Recommendation,
    screeningResult.Summary,
    screeningResult.Strengths,
    screeningResult.Concerns,
    screeningResult.SkillsScore,
    screeningResult.ExperienceScore,
    screeningResult.EducationScore);
```

### 3.7 `ScreeningOrchestrator.ToDto` — modification

Map the new properties:

```csharp
return new ScreeningReportDto(
    report.Id, report.ApplicationId, report.Score,
    report.Recommendation.ToString(), report.Summary,
    strengths, concerns, report.Status.ToString(),
    report.FailureReason, report.EvaluatedAtUtc,
    report.SkillsScore, report.ExperienceScore, report.EducationScore);
```

### 3.8 `ServiceCollectionExtensions.AddSystemService` — modification

Replace the hardcoded `MockScreeningService` registration with provider selection logic:

```csharp
// Provider selection (0009)
var screeningProvider = configuration["Screening:Provider"] ?? "Mock";
services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));

if (string.Equals(screeningProvider, "Gemini", StringComparison.OrdinalIgnoreCase))
{
    var apiKey = configuration["Gemini:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        services.AddHttpClient<IScreeningService, GeminiScreeningService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(
                configuration.GetValue("Gemini:TimeoutSeconds", 30));
        });
    }
    else
    {
        // Graceful fallback — log warning at startup
        services.AddScoped<IScreeningService, MockScreeningService>();
        // Warning logged via ILogger in the service registration phase
    }
}
else
{
    services.AddScoped<IScreeningService, MockScreeningService>();
}
```

> **Note:** The startup warning for fallback is logged during first service resolution, not during registration. `GeminiScreeningService` constructor is never reached if Mock is selected.

## 4. API Layer

Endpoint shapes are unchanged from 0008. Only the DTO widens:

| Route | Handler | Auth policy | Change |
|---|---|---|---|
| `GET /api/staff/applications/{id}/screening-report` | existing | `StaffOnly` | Response DTO gains `skillsScore`, `experienceScore`, `educationScore` |
| `POST /api/staff/applications/{id}/screen` | existing | `RecruiterOnly` | Response DTO gains category score fields |

No new routes. No new handlers. No change to authorization.

## 5. Frontend

### 5.1 Components

| Component | Path | Change | Notes |
|---|---|---|---|
| `ScreeningReportCard` | `frontend/src/components/staff/ScreeningReportCard.tsx` | Add category score progress bars between the header score and the Summary | Three horizontal bars: Skills, Experience, Education |
| `ScreeningReportModal` | `frontend/src/components/staff/ScreeningReportModal.tsx` | Add category score section between the score banner and the Summary | Same three-bar layout, larger |

### 5.2 Data Access

No new hooks. Existing `fetch` calls in both components already retrieve the full `ScreeningReportDto` — the new fields are purely additive in the JSON response.

### 5.3 UI States

No new async surfaces. The four states (loading, empty, error, success) are already handled by both components. The category scores render only in the success state when `report.status === "Completed"` and scores are non-null.

| State | Treatment |
|---|---|
| Category scores present | Three labeled progress bars with percentage labels |
| Category scores null | Section hidden (graceful — pre-0009 reports or Mock reports without category scores) |

## 6. DTOs & Contracts

### Backend

```csharp
public record ScreeningReportDto(
    Guid Id,
    Guid ApplicationId,
    int Score,
    string Recommendation,
    string Summary,
    string[] Strengths,
    string[] Concerns,
    string Status,
    string? FailureReason,
    DateTime EvaluatedAtUtc,
    int? SkillsScore,        // NEW
    int? ExperienceScore,    // NEW
    int? EducationScore);    // NEW
```

### Frontend

```ts
export type ScreeningReportDto = {
  applicationId: string;
  score: number;
  recommendation: ScreeningRecommendation;
  summary: string;
  strengths: string[];
  concerns: string[];
  status: ScreeningStatus;
  failureReason: string | null;
  screenedAtUtc: string;
  skillsScore: number | null;       // NEW
  experienceScore: number | null;   // NEW
  educationScore: number | null;    // NEW
};
```

## 7. Validation Rules

| Field | Rule | Message | AC |
|---|---|---|---|
| `Gemini:ApiKey` | Non-empty when `Screening:Provider` is `Gemini` | Warning log: "Gemini API key not configured, falling back to MockScreeningService" | AC-2 |
| `GeminiScreeningOutput.Score` | 0-100 integer (enforced by JSON schema + `Math.Clamp`) | N/A (server-side) | AC-1 |
| `GeminiScreeningOutput.Recommendation` | `"Advance"` or `"Review"` (enforced by JSON schema enum) | Falls back to `Review` if unrecognised | AC-3, AC-4 |
| CV text length | Truncated to `GeminiOptions.MaxCvTextLength` | N/A (silent truncation) | E-2 |

## 8. Error Handling

| Condition | Code | Logged at | User-facing message |
|---|---|---|---|
| Gemini API key invalid (401/403) | N/A (internal) | Error | ScreeningReport.FailureReason: "Gemini authentication failed: check API key" |
| Gemini rate limit (429) | N/A (internal) | Warning (per retry) | ScreeningReport.FailureReason: "AI service unavailable after retry: ..." |
| Gemini malformed JSON response | N/A (internal) | Warning | ScreeningReport.FailureReason: "Invalid structured JSON response from AI provider" |
| Gemini quota exhaustion | N/A (internal) | Error | ScreeningReport.FailureReason: "Gemini API quota exhausted" |

All errors are caught by the existing orchestrator's try/catch. No new HTTP error codes at the API boundary — the ScreeningReport records the failure, and existing 200/404 responses carry the `status: "Failed"` field.

## 9. Configuration

| Key | Type | Default | Required | Where consumed |
|---|---|---|---|---|
| `Screening:Provider` | string | `"Mock"` | No | `ServiceCollectionExtensions` — selects `IScreeningService` implementation |
| `Gemini:ApiKey` | string | `""` | Only when Provider=Gemini | `GeminiScreeningService` — appended to API URL as `?key=` |
| `Gemini:Model` | string | `"gemini-2.0-flash"` | No | `GeminiScreeningService` — model identifier in URL path |
| `Gemini:BaseUrl` | string | `"https://generativelanguage.googleapis.com"` | No | `GeminiScreeningService` — API base URL |
| `Gemini:TimeoutSeconds` | int | `30` | No | `HttpClient.Timeout` at registration |
| `Gemini:MaxCvTextLength` | int | `50000` | No | `GeminiScreeningService` — truncation limit for CV text |

## 10. Database Migration

Migration name: `AddScreeningCategoryScores`

| Step | Change | Reversible |
|---|---|---|
| 1 | `AddColumn("SkillsScore", "INTEGER", nullable: true)` to `ScreeningReports` | Yes — `DropColumn` |
| 2 | `AddColumn("ExperienceScore", "INTEGER", nullable: true)` to `ScreeningReports` | Yes — `DropColumn` |
| 3 | `AddColumn("EducationScore", "INTEGER", nullable: true)` to `ScreeningReports` | Yes — `DropColumn` |

No backfill needed — `null` is correct for existing rows (they were evaluated by the Mock provider without category scores).

**Rollback plan.** `dotnet ef database update AddScreeningReport --project src/Db` drops the three new columns.

## 11. Test Plan

| Test | Type | Covers | Path |
|---|---|---|---|
| `EvaluateAsync_ValidResponse_ReturnsScreeningResultWithCategoryScores` | Unit | AC-1 | `tests/Ats.UnitTests/Screening/GeminiScreeningServiceTests.cs` |
| `EvaluateAsync_Http401_ThrowsWithAuthMessage` | Unit | AC-6, E-1 | `tests/Ats.UnitTests/Screening/GeminiScreeningServiceTests.cs` |
| `EvaluateAsync_Http429_RetriesThenThrows` | Unit | AC-5, E-5 | `tests/Ats.UnitTests/Screening/GeminiScreeningServiceTests.cs` |
| `EvaluateAsync_Http503_RetriesThenThrows` | Unit | AC-5 | `tests/Ats.UnitTests/Screening/GeminiScreeningServiceTests.cs` |
| `EvaluateAsync_MalformedJson_ThrowsWithParseMessage` | Unit | AC-6, E-3 | `tests/Ats.UnitTests/Screening/GeminiScreeningServiceTests.cs` |
| `EvaluateAsync_LongCvText_TruncatedBeforeSending` | Unit | E-2 (assumption A-3) | `tests/Ats.UnitTests/Screening/GeminiScreeningServiceTests.cs` |
| `MockScreeningService_ReturnsResultWithCategoryScores` | Unit | AC-9 | `tests/Ats.UnitTests/Screening/MockScreeningServiceTests.cs` (new or existing) |
| `ScreeningReport_Complete_SetsCategoryScores` | Unit | AC-1 | `tests/Ats.UnitTests/Screening/ScreeningReportEntityTests.cs` (new or existing) |
| `ScreeningReport_Complete_NullCategoryScores_StaysNull` | Unit | AC-9 | `tests/Ats.UnitTests/Screening/ScreeningReportEntityTests.cs` |
| `ProviderRegistration_GeminiWithKey_RegistersGeminiService` | Integration | AC-1 | `tests/Ats.IntegrationTests/Screening/GeminiProviderRegistrationTests.cs` |
| `ProviderRegistration_GeminiNoKey_FallsBackToMock` | Integration | AC-2 | `tests/Ats.IntegrationTests/Screening/GeminiProviderRegistrationTests.cs` |
| `ProviderRegistration_Mock_RegistersMockService` | Integration | AC-2, AC-9 | `tests/Ats.IntegrationTests/Screening/GeminiProviderRegistrationTests.cs` |
| `ScreeningEndpoint_WithGeminiMock_ReturnsReportWithCategoryScores` | Integration | AC-7, AC-8 | `tests/Ats.IntegrationTests/Screening/ScreeningEndpointTests.cs` (modify existing) |
| `RunScreening_AdvanceScore_AutoAdvancesAndRecordsCategoryScores` | Integration | AC-3 | `tests/Ats.IntegrationTests/Screening/ScreeningEndpointTests.cs` (modify existing) |
| `RunScreening_ReviewScore_StaysInStageWithCategoryScores` | Integration | AC-4 | `tests/Ats.IntegrationTests/Screening/ScreeningEndpointTests.cs` (modify existing) |
| `OrchestratorTests_ExistingTests_UpdatedForNewResultShape` | Unit | AC-9 | `tests/Ats.UnitTests/Screening/ScreeningOrchestratorTests.cs` (modify) |

Every `AC-n` in the spec appears at least once in this table.

## 12. Implementation Notes

- **`GeminiScreeningService` retry is internal to the service** (for 429/503), separate from the orchestrator's outer retry. The orchestrator retries any `IScreeningService` failure once; `GeminiScreeningService` internally retries transient HTTP errors up to 2 times with exponential backoff. This means a worst-case of 3 HTTP attempts per screening (1 original + 2 internal retries) before the orchestrator's outer retry fires.
- **The system prompt** should instruct the model to act as an expert ATS recruiter, evaluate the CV against the job criteria, and produce the structured output. The prompt is hardcoded in `GeminiScreeningService` — the spec explicitly excludes per-requisition prompt customization.
- **JSON property naming** in the Gemini request/response uses camelCase (`System.Text.Json` default with `JsonSerializerOptions.PropertyNameCaseInsensitive = true` for response deserialization).
- **The `HttpClient` is registered via `AddHttpClient<TInterface, TImplementation>`** to leverage `IHttpClientFactory` connection pooling and lifetime management.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0008` (Automated Candidate Screening) | 1 | Read `plan/hld.md`, `plan/api.md`, `plan/erd.md` — the service interface, orchestrator, entity, and endpoint shapes this spec extends. |
| `0004` (Application Submission and CV Upload) | 1 | Read `plan/api.md`, `plan/erd.md` — `Application`, `CvAttachment`, `StaffApplicationListItemDto` conventions. |
| `0005` (Pipeline Progression) | 1 | Read `plan/api.md`, `plan/erd.md` — `StageTransition`, `PipelineBoardApplicationDto` conventions. |

Tier 0 read in full.
Considered and skipped: `0001`, `0002`, `0003`, `0006`, `0007`.
Cap reached: no.

## Deviation Log

Appended by `/implement` when reality diverged from this design.

| Date | Task | Section | Designed | Actual | Reason |
|---|---|---|---|---|---|
