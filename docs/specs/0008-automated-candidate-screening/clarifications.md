# Clarifications — 0008 Automated Candidate Screening

A verbatim log of ambiguities raised during `/specify` and how they were resolved. This is an
audit trail: when someone later asks "why does it work this way", the answer is here.

Do not paraphrase the user's answer into what you wish they had said. Record what they chose.

---

## Round 1 — 2026-08-14

### C-1 — Screening Trigger Mechanism

**Ambiguity.** Whether screening runs automatically upon candidate application submission, on-demand by staff, or is configurable per job listing.

**Options presented.**
1. (Recommended) Automatic on Application submission + optional manual "Re-run Screening" button for Staff in the UI
2. Automatic on submission only (no manual re-run)
3. Manual trigger only (Recruiter clicks "Run AI Screening" when ready)
4. Configurable per Requisition (toggle auto-screening on/off per job listing)

**Answer.** (Recommended) Automatic on Application submission + optional manual "Re-run Screening" button for Staff in the UI

**Impact.** Determined FR-1, FR-7, FR-8, AC-1, AC-5.

---

### C-2 — Automated Pipeline Action Upon Screening Completion

**Ambiguity.** What pipeline action the system takes once the agent produces an evaluation score.

**Options presented.**
1. Advisory only: Generate AI evaluation report (score, summary, strengths, concerns, recommendation) without auto-moving stages
2. (Selected) Auto-advance qualified candidates above threshold to next stage, but leave borderline/low scores in Screening for human review
3. Full auto-progression: Auto-advance high scores to next stage and auto-reject low scores based on threshold

**Answer.** Auto-advance qualified candidates above threshold to next stage, but leave borderline/low scores in Screening for human review

**Impact.** Determined FR-3, FR-4, FR-5, Non-Goals (no auto-rejection), AC-2, AC-3.

---

### C-3 — AI Provider Architecture

**Ambiguity.** How the backend modular monolith should integrate with AI / LLM capabilities.

**Options presented.**
1. (Recommended) Pluggable AI service interface (IScreeningService) with external LLM API (OpenAI/Anthropic/Gemini) plus a deterministic Mock provider for local development & automated test suites
2. Mock/Rule-based screening only (heuristic keyword matching, no external API keys needed)
3. Direct OpenAI/Azure OpenAI integration only

**Answer.** (Recommended) Pluggable AI service interface (IScreeningService) with external LLM API (OpenAI/Anthropic/Gemini) plus a deterministic Mock provider for local development & automated test suites

**Impact.** Determined FR-12, NFR-2, AC-10.

---

### C-4 — Evaluation Criteria and Input Source

**Ambiguity.** What source material the screening agent evaluates to score the applicant.

**Options presented.**
1. (Recommended) Requisition Title & Description matched against extracted text from candidate's uploaded PDF CV
2. Requisition Details + Custom Screening Rubric/Criteria (optional per-requisition prompt) + candidate PDF CV
3. Fixed system-wide rubric applied uniformly to all job listings

**Answer.** (Recommended) Requisition Title & Description matched against extracted text from candidate's uploaded PDF CV

**Impact.** Determined FR-1, Non-Goals (no per-requisition custom prompt UI in initial version).

---

### C-5 — Frontend Staff Visibility

**Ambiguity.** Where and how recruiters and hiring managers view the screening results in the UI.

**Options presented.**
1. (Recommended) Both: Score & recommendation badge on the Staff Pipeline Board / Applications list + full detailed Screening Report drawer/section on Application detail page
2. Application detail page only (detailed report view)
3. Pipeline Board badge only (compact score/status)

**Answer.** (Recommended) Both: Score & recommendation badge on the Staff Pipeline Board / Applications list + full detailed Screening Report drawer/section on Application detail page

**Impact.** Determined FR-9, FR-10, AC-7.

---

## Assumptions Made Without Asking

Ambiguities resolved by judgement rather than by asking, because a reasonable default existed
and the alternatives would not have changed the work materially. Listed so they can be
challenged.

| # | Assumption | Default chosen | Reversal cost if wrong |
|---|---|---|---|
| A-1 | Qualification threshold score | Numerical score ≥ 75 out of 100 qualifies for `Advance` | Low — configurable in service options |
| A-2 | Candidate access policy | Candidates cannot view screening reports or scores (`StaffOnly` policy) | Low — authorization policy adjustment |
| A-3 | Async processing | Screening runs asynchronously upon submission so application submission HTTP response is not delayed | Low — orchestrator implementation |
| A-4 | Text extraction format | Plain text extraction from standard PDF documents (without heavy OCR for scanned images) | Low — parser package swap |

## Deferred

Questions raised but explicitly postponed, with where they were recorded.

| # | Question | Deferred to |
|---|---|---|
| D-1 | Custom prompt templates & scoring rubrics per Requisition | Future enhancement spec |
| D-2 | Automated candidate rejection based on disqualification questions | Future enhancement spec |
| D-3 | OCR for scanned image PDFs | Future enhancement spec |
