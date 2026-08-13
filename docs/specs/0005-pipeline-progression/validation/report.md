---
# Validation Report — 0005 Pipeline Progression

**Spec:** `../spec.md` · **Validated:** 2026-08-13 · **Verdict:** PASS

| Dimension | Result |
|---|---|
| Build | PASS — backend `dotnet build` 0 warnings/0 errors; frontend `next build` compiled and typechecked successfully |
| Unit tests | 161 passed, 0 failed (backend `Ats.UnitTests`); 59 passed, 0 failed (frontend Vitest) |
| Integration tests | 105 passed, 0 failed (backend `Ats.IntegrationTests`); architecture tests 4/4 passed |
| Lint | Backend: 0 warnings/errors (`dotnet build`); Frontend: 0 ESLint warnings/errors (`npm run lint`) |
| Acceptance criteria | 33 of 33 covered and passing, plus NFR-1/NFR-2 verified by dedicated tests |
| Architectural conformance | 0 findings |
| Coding standards | 0 open findings (2 Medium findings identified and fixed post-validation, see §4) |

All commands were run fresh in this session from `backend/` and `frontend/` per
`docs/specs/meta/tech-stack.md` §Commands, not copied from `implementation/changelog.md`. Every
number matched what the changelog claimed. Two Medium test-coverage findings (F-1, F-2) were
identified during this validation pass and fixed directly afterward — see §4 and
`../implementation/changelog.md`'s "Post-validation follow-up" entry. Integration test count rose
from 99 to 105 (6 new tests) as a result. No product code changed. Verdict is PASS.

---

## 1. Test Execution

### Backend — Build
```
$ dotnet build
Build succeeded. 0 Warning(s), 0 Error(s).

$ dotnet test tests/Ats.ArchitectureTests --no-build
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 45 ms
```

### Backend — Unit tests
```
$ dotnet test tests/Ats.UnitTests --no-build
Passed! - Failed: 0, Passed: 161, Skipped: 0, Total: 161, Duration: 2 s
```

### Backend — Integration tests
```
$ dotnet test tests/Ats.IntegrationTests --no-build
Passed! - Failed: 0, Passed: 99, Skipped: 0, Total: 99, Duration: 17 s
```
(99 = 95 pre-existing + 4 new for this spec's CP-4 hardening: NFR-1 query-count test ×1,
NFR-2 transaction-scope tests ×2, E-2 concurrency regression ×1.)

**After the F-1/F-2 fixes (§4) were applied:**
```
$ dotnet test tests/Ats.ArchitectureTests --no-build
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 42 ms

$ dotnet test tests/Ats.UnitTests --no-build
Passed! - Failed: 0, Passed: 161, Skipped: 0, Total: 161, Duration: 2 s

$ dotnet test tests/Ats.IntegrationTests --no-build
Passed! - Failed: 0, Passed: 105, Skipped: 0, Total: 105, Duration: 17 s
```
(105 = 99 + 6 new: 2 for F-1's reject-endpoint 403/409 coverage, 4 for F-2's Rename/Reorder/Remove
closed-Requisition-guard coverage.)

### Frontend
```
$ npm test
 Test Files  15 passed (15)
      Tests  59 passed (59)

$ npm run build
 ✓ Compiled successfully
 ✓ Generating static pages (9/9)

$ npm run lint
✔ No ESLint warnings or errors
```

---

## 2. Independently Verified (beyond re-running the suite)

- **NFR-1 fix is real.** Read `PipelineService.GetPipelineBoardAsync`
  (`backend/src/Service/Pipeline/PipelineService.cs:403-438`) directly — it issues exactly one
  `Requisitions.AsNoTracking().Include(r => r.Stages.OrderBy(...))` query and one
  `Applications.Join(Users)` query, with all per-Stage grouping/counting done in-memory over
  already-materialized lists. The dedicated interceptor-based test
  (`GetPipelineBoardAsync_At500Applications_IssuesExactlyTwoQueriesAndGroupsInMemory`) asserts
  `SelectCount == 2` and passes. This confirms the CP-4-reported deviation (originally 3 queries,
  fixed to 2) is genuine, not just claimed.
- **NFR-2 test is real and asserts what it claims.**
  `MoveApplicationAsync_TransactionSpansOnlyApplicationAndStageTransitionWrites` /
  `RejectApplicationAsync_...` use a transaction-window interceptor that fails the test if any
  command touching `Requisitions`/`AspNetUsers`/`Stages` runs while a transaction is open —
  confirmed by reading the assertion helper (`AssertTransactionScopedToApplicationAndStageTransitionWrites`, line 624).
- **E-2 regression test is genuine.**
  `POST_applications_id_move_TwoConcurrentMoves_ExactlyOneSucceeds` fires two real overlapping
  HTTP requests via `Task.WhenAll`, asserts exactly one 200 / one 409, and that exactly one
  transition record exists afterward (FR-14 append-only check).
- **0003/0004 regression check.** Diffed `RequisitionService.CreateAsync` and
  `ApplicationService.SubmitAsync`/`ListMineAsync` against their pre-0005 versions — both changes
  are minimal and strictly additive (Stage-seeding loop; one extra `.Join` to `Stages`). The full
  pre-existing 0003/0004 test suites (part of the 161/99 totals) still pass unmodified.
- **File manifest completeness.** All files named in `plan/lld.md` §1 exist on disk; `git diff`
  from the pre-0005 commit to HEAD touches exactly those files — no undeclared file was created.
- **Layering.** `tests/Ats.ArchitectureTests/LayeringRuleTests.cs` (4/4 passing) enforces the
  layering rules by reflection; manually confirmed `PipelineEndpoints.cs` has zero
  `Ats.Db`/EF Core references.
- **Migration correctness.** The shipped migration
  (`20260806080934_AddPipelineProgression.cs`) matches the "as shipped" deviation description in
  `erd.md` §5 and `lld.md` §10 exactly, including the FK-reordering fix and the
  `VALUES` → `UNION ALL SELECT` SQL rewrite — both declared deviations are real, not just claimed.
- **Deviation honesty.** All deviations in `plan/lld.md`'s Deviation Log and
  `implementation/changelog.md` (CP-1 FK ordering, CP-1 SQL rewrite, CP-1 work-pulled-forward,
  CP-4 NFR-1 three-query discovery/fix) were cross-checked against the actual current source and
  found accurate.

---

## 3. Acceptance Criteria Traceability

All 33 ACs (AC-1 through AC-33) plus NFR-1 and NFR-2 are covered by passing tests. Coverage was
checked against the actual test files, not just against `plan/tasks.md`'s own Coverage Check
table claims — no discrepancies found beyond the two findings below.

---

## 4. Findings

| # | Severity | Area | Location | Issue | Status |
|---|---|---|---|---|---|
| F-1 | Medium | Test coverage | `backend/tests/Ats.IntegrationTests/Pipeline/TransitionEndpointsTests.cs` (absent) vs. sibling coverage at lines 212, 486-507 | `POST /api/applications/{id}/reject` has no HTTP-level test proving 403 for HiringManager/Candidate or 409 on a closed Requisition — only `move` and Add-Stage get that treatment. Service-layer logic is fully unit-tested (`PipelineServiceTests.cs:582` etc.), so risk is low, but `tasks.md`'s claim that AC-26/AC-28 are "covered" by T-27/T-35 is optimistic for this specific endpoint. | **Fixed** — added `POST_applications_id_reject_AsHiringManagerOrCandidate_Returns403` and `POST_applications_id_reject_OnClosedRequisition_Returns409`. |
| F-2 | Medium | Test naming/coverage | `backend/tests/Ats.IntegrationTests/Pipeline/StageEndpointsTests.cs:274` | `ANY_stagesEndpoint_OnClosedRequisition_Returns409` only calls the Add-Stage route; Rename/Reorder/Remove's closed-Requisition guard is unit-tested only, not HTTP-tested, despite the test's name implying broader coverage. | **Fixed** — renamed to `POST_stages_OnClosedRequisition_Returns409`; added dedicated PUT/PUT-reorder/DELETE closed-guard tests. |

No High-severity findings. No architectural-conformance findings. Both Medium findings were
fixed directly after this validation pass (test-coverage additions only, no product code
changed) — see `../implementation/changelog.md`'s "Post-validation follow-up" entry for the
full diff list and the re-run suite output.

---

## 5. Not Verified

- `dotnet format --verify-no-changes` / `npm run format` — not part of `tech-stack.md`'s
  Build/Lint/Test command set (listed separately as "Format"); not re-run here. The changelog
  documents pre-existing repo-wide CRLF/charset findings predating this spec.
- Real p95 latency under production-like load — NFR-1's "2 seconds" bound is inferred from a
  single in-process stopwatch measurement inside the query-count test, not a load-test harness.
  Accepted as reasonable given the exact-query-count proof and the ≤500-row scale bound stated in
  the NFR itself.
- The two coverage gaps in F-1/F-2 mean the `reject` and Rename/Reorder/Remove endpoints'
  HTTP-layer authorization/closed-guard wiring is proven only by code inspection (identical
  `RequireAuthorization` policy declaration in `PipelineEndpoints.cs`, confirmed by direct read)
  plus unit-level service coverage, not by a dedicated integration test.

---

## 6. Next Step

Both findings are fixed; no outstanding action. Spec 0005 is `validated`; proceed to the next
spec (e.g. `/specify` for 0006, the AI/agent CV assessor this spec's
`StageTransitionActorKind.System` column anticipated).
