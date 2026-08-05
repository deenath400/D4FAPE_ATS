# Validation Report — NNNN <Title>

**Spec:** `../spec.md` · **Validated:** YYYY-MM-DD · **Verdict:** PASS | PASS-WITH-FINDINGS | FAIL

| Dimension | Result |
|---|---|
| Build | PASS / FAIL |
| Unit tests | 34 passed, 0 failed, 2 skipped |
| Integration tests | 11 passed, 1 failed |
| Lint | 0 errors, 3 warnings |
| Acceptance criteria | 6 of 7 covered and passing |
| Architectural conformance | 1 finding |
| Coding standards | 2 findings |

**Verdict rule.** `FAIL` if the build breaks, any test fails, or any AC is uncovered or
failing. `PASS-WITH-FINDINGS` if everything runs green but findings of severity Medium or
below exist. `PASS` only when there are no findings above Low.

---

## 1. Test Execution

Real captured output. If a command could not be run, say why — never assume a result.

### Build

```
$ dotnet build
<verbatim output>
```

### Unit tests

```
$ dotnet test --filter Category=Unit
<verbatim output>
```

### Integration tests

```
$ dotnet test --filter Category=Integration
<verbatim output>
```

### Lint

```
$ npm run lint
<verbatim output>
```

**Commands not run**

| Command | Why |
|---|---|
| `npm run db:migrate` | Requires a live database; not available in this environment |

## 2. Acceptance Criteria Traceability

| AC | Requirement | Covering test(s) | Result |
|---|---|---|---|
| AC-1 | Board shows candidates grouped by stage | `PipelineBoard.test.tsx::rendersColumns` | PASS |
| AC-2 | Recruiter can move a candidate | `MoveStageEndpointTests::MovesCandidate` | PASS |
| AC-3 | Foreign stage rejected | `MoveToStageTests::ForeignStage_ReturnsValidation` | PASS |
| AC-7 | Stage history visible in timeline | — | **NOT COVERED** |

Uncovered ACs are always a FAIL-level finding. An AC covered only by a manual check is
recorded as `MANUAL` with a note on how it was checked.

## 3. Architectural Conformance

Checked against `plan/hld.md`, `plan/lld.md`, and `docs/specs/meta/architecture.md`.

| Check | Result | Note |
|---|---|---|
| Files match the LLD manifest | PASS | 14 of 14 present |
| Layering respected (no API → Infrastructure direct calls) | FAIL | See F-1 |
| No unauthorized cross-component dependency | PASS | |
| Component map in architecture.md reflects reality | PASS | |
| ER diagram in architecture.md reflects the shipped schema | PASS | |
| Deviations recorded in the changelog and patched into the LLD | PASS | 1 deviation, both recorded |

## 4. Coding Standards Conformance

Checked against `docs/specs/meta/coding-standards.md`.

| Rule | Result | Note |
|---|---|---|
| Naming conventions | PASS | |
| Error envelope used consistently | FAIL | See F-2 |
| No secrets in source | PASS | |
| Structured logging with correlation id | PASS | |
| Public API documented | PASS | |
| Test naming convention | PASS | |

## 5. Findings

Ranked most severe first. Every finding names a file and line.

### F-1 — Controller calls DbContext directly *(Severity: High · Architecture)*

**Location:** `src/Api/Controllers/CandidatesController.cs:88`

**Problem.** The stage-move handler queries `AtsDbContext` directly, bypassing
`PipelineService`. The LLD §4 routes all mutations through the service layer, and
`architecture.md` states the API layer must not depend on Infrastructure.

**Impact.** Authorization and the outbox publish in `PipelineService` are skipped on this
path, so a moved candidate produces no notification.

**Suggested fix.** Call `PipelineService.MoveToStageAsync` and map the `Result`.

### F-2 — Inconsistent error shape *(Severity: Medium · Standards)*

**Location:** `src/Api/Controllers/CandidatesController.cs:104`

**Problem.** Returns `BadRequest("invalid stage")` — a bare string — where the project
convention (`coding-standards.md`, established by 0001) is RFC 7807 ProblemDetails.

**Impact.** Clients parsing the error envelope break on this endpoint.

**Suggested fix.** Return `ValidationProblem` with code `candidate.stage.foreign`.

### F-3 — <title> *(Severity: Low · Quality)*

...

## 6. Coverage Gaps

| Area | Gap | Risk |
|---|---|---|
| `StageHistory` timeline | No test exercises the read path | AC-7 unverifiable |

## 7. Recommended Actions

Ordered. Each maps to a finding.

1. Fix F-1 — route through the service layer *(blocks PASS)*
2. Add a test for AC-7 *(blocks PASS)*
3. Fix F-2 — error envelope
4. Consider F-3 in a follow-up

## 8. Status Decision

<e.g. "Verdict FAIL: AC-7 uncovered and F-1 is a High architectural violation. Spec status
remains `implemented`. Re-run /validate after the recommended actions.">

## Related Specs

<Per spec-kit/context-loading.md §4 — including any prior spec whose conventions were used
as the yardstick here.>
