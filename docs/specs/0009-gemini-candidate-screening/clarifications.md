# Clarifications — 0009 Google Gemini 2.0 Flash Candidate Screening Integration

A verbatim log of ambiguities raised during `/specify` and how they were resolved. This is an
audit trail: when someone later asks "why does it work this way", the answer is here.

Do not paraphrase the user's answer into what you wish they had said. Record what they chose.

---

## Round 1 — 2026-08-14

### C-1 — Provider Activation & Offline Fallback Strategy

**Ambiguity.** Whether the system should strictly enforce Gemini API key presence, automatically switch based on environment variables, or support explicit configuration with fallback to the mock screening service for local/CI test runs.

**Options presented.**
1. Explicit configuration (`Screening:Provider` = 'Gemini' | 'Mock') with graceful fallback to Mock if API key is unconfigured in development
2. Automatic activation whenever `Gemini:ApiKey` or `GEMINI_API_KEY` environment variable is present
3. Strict Gemini mode — reject startup or fail screening if `Gemini:ApiKey` is missing

**Answer.** (Recommended) Explicit configuration (`Screening:Provider` = 'Gemini' | 'Mock') with graceful fallback to Mock if API key is unconfigured in development

**Impact.** Determined FR-2, AC-2, AC-9, and `GeminiOptions` configuration schema.

---

### C-2 — HTTP Client Integration vs Third-Party SDK

**Ambiguity.** Whether to use an external SDK package (e.g. `Mscc.GenerativeAI` or `Google.GenAI`) or implement typed `HttpClient` directly against the official Google AI Generative Language REST API (`v1beta/models/gemini-2.0-flash:generateContent`).

**Options presented.**
1. Direct typed `HttpClient` against Gemini REST API (`v1beta/models/gemini-2.0-flash:generateContent`) using native structured JSON schema
2. External .NET SDK package (e.g., `Mscc.GenerativeAI` or `Google.GenAI`)

**Answer.** (Recommended) Direct typed `HttpClient` against Gemini REST API (`v1beta/models/gemini-2.0-flash:generateContent`) using native structured JSON schema

**Impact.** Determined FR-1, FR-3, NFR-1, eliminating unnecessary external NuGet dependencies and keeping ASP.NET Core modular monolith aligned with repository coding standards.

---

### C-3 — Structured Evaluation Schema & Category Breakdown

**Ambiguity.** Whether Gemini 2.0 Flash should produce only the baseline 0008 evaluation structure (overall score, recommendation, summary, strengths, concerns) or expand to include granular category fit sub-scores (Skills Fit, Experience Fit, Education Fit).

**Options presented.**
1. Keep existing 0008 schema: overall Score (0–100), Recommendation (Advance/Review), Executive Summary, Strengths list, and Concerns list
2. Expand schema with category breakdown scores (e.g., Skills Fit, Experience Fit, Education Fit) in addition to overall Score

**Answer.** Expand schema with category breakdown scores (e.g., Skills Fit, Experience Fit, Education Fit) in addition to overall Score

**Impact.** Determined FR-3, FR-4, FR-9, AC-1, AC-8, requiring schema expansion for `ScreeningReport` (SkillsScore, ExperienceScore, EducationScore) and UI card/modal updates.

---

## Assumptions Made Without Asking

Ambiguities resolved by judgement rather than by asking, because a reasonable default existed
and the alternatives would not have changed the work materially. Listed so they can be
challenged.

| # | Assumption | Default chosen | Reversal cost if wrong |
|---|---|---|---|
| A-1 | Gemini model identifier is configurable with default `gemini-2.0-flash` | `gemini-2.0-flash` in `GeminiOptions:Model` | Low — string configuration setting |
| A-2 | Transient retry policy | Up to 2 retries with exponential backoff on HTTP 429/503 before failing report | Low — internal `GeminiScreeningService` retry logic |
| A-3 | Maximum CV text sent to Gemini | Capped at 50,000 characters to prevent excessive memory allocation | Low — parameter constant |

## Deferred

Questions raised but explicitly postponed, with where they were recorded.

| # | Question | Deferred to |
|---|---|---|
| D-1 | Multimodal CV evaluation (parsing scanned image PDFs via Gemini vision) | Future spec — current spec uses extracted plain text via `IPdfTextExtractor` |
| D-2 | Custom scoring rubrics & prompt templates per requisition | Future spec |
