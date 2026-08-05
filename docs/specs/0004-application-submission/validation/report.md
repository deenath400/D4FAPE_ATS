# Validation Report — 0004 Application Submission and CV Upload

**Spec:** `../spec.md` · **Validated:** 2026-08-06 · **Verdict:** PASS-WITH-FINDINGS

| Dimension | Result |
|---|---|
| Build | PASS — backend 0 warnings/0 errors, architecture tests 4/4; frontend `next build` succeeded |
| Unit tests | 96 passed, 0 failed (backend `Ats.UnitTests`); 44 passed, 0 failed (frontend Vitest) |
| Integration tests | 61 passed, 0 failed (backend `Ats.IntegrationTests`) |
| Lint | Backend: 0 warnings/errors (`dotnet build`); Frontend: 0 ESLint warnings/errors |
| Acceptance criteria | 22 of 22 covered and passing |
| Architectural conformance | 0 findings |
| Coding standards | 2 findings (both Medium), 1 Low |

All commands ran green and every AC is covered; three findings exist (F-1, F-2 Medium; F-3
Low), none High — verdict is PASS-WITH-FINDINGS.

---

## 1. Test Execution

No stale `Ats.Api.exe` process was found holding a file lock before any backend command ran.

### Backend — Build
```
$ cd backend && dotnet build
Build succeeded. 0 Warning(s), 0 Error(s). Time Elapsed 00:00:03.11

$ dotnet test tests/Ats.ArchitectureTests --no-build
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 45 ms
```

### Backend — Unit tests
```
$ dotnet test tests/Ats.UnitTests
Passed! - Failed: 0, Passed: 96, Skipped: 0, Total: 96, Duration: 2 s
```

### Backend — Integration tests
```
$ dotnet test tests/Ats.IntegrationTests
Passed! - Failed: 0, Passed: 61, Skipped: 0, Total: 61, Duration: 13 s
```
Includes `POST_applications_SlowFileWrite_DoesNotExtendSqliteWriteLockDuration` (NFR-3) and
`POST_applications_TwoNearSimultaneousSubmissions_ExactlyOneSurvives` (E-1), both passed.

### Backend — Lint
Per tech-stack.md, lint = `dotnet build` (above) — 0 warnings, 0 errors.

### Backend — Migration sanity check (independent, beyond the standard Commands table)
```
$ dotnet ef database update --project src/Db --startup-project src/Api --connection "Data Source=.../validate-0004-fresh.db"
Applying migration '20260805133328_InitialCreate'.
Applying migration '20260805141657_AddAuthenticationAndRefreshTokens'.
Applying migration '20260805171525_AddRequisitionsAndStages'.
Applying migration '20260805191845_AddApplicationsAndCvAttachments'.
Done.
```
(temp file deleted after check; not part of application source or git history)

### Frontend — Build
```
$ npm run build
✓ Compiled successfully / ✓ Generating static pages (9/9)
```

### Frontend — Unit tests
```
$ npm test
Test Files 11 passed (11), Tests 44 passed (44), Duration 3.88s
```

### Frontend — Lint
```
$ npm run lint
✔ No ESLint warnings or errors
```

**Commands not run**

| Command | Why |
|---|---|
| `dotnet restore --use-lock-file` / `npm ci` | Not needed — bin/obj/node_modules already present, both builds succeeded without restore |
| `dotnet format --verify-no-changes` / `npm run format` | Not part of the stage-mandated sequence (install/build/unit/integration/lint); changelog already discloses pre-existing CRLF-vs-LF drift unrelated to 0004's new files |
| `dotnet ef database update` against the real dev database | Substituted with an independent fresh-database run against a temp file, to avoid touching the working tree's backend/data/app.db |

## 2. Acceptance Criteria Traceability

| AC | Requirement | Covering test(s) | Result |
|---|---|---|---|
| AC-1 | Valid PDF submission → 201, Application + CV persisted | ApplicationEntityTests.Application_Create_SetsFieldsAndStartsWithNoCv; ApplicationServiceTests.SubmitAsync_ValidPdf_ReturnsCreatedWithPersistedCv; ApplicationEndpointsTests.POST_applications_AsCandidateValidPdf_Returns201; application-form.test.tsx "AC-1" | PASS |
| AC-2 | No CV file → 400, no row | SubmitAsync_NoFile_ReturnsValidationNoRowWritten; POST_applications_NoFile_Returns400; application-form.test.tsx "AC-2" | PASS |
| AC-3 | Non-PDF file → 400 (or 415) | SubmitAsync_NonPdfContentType_ReturnsValidation, SubmitAsync_PdfExtensionWrongMagicBytes_ReturnsValidation; POST_applications_NonPdf_Returns400; application-form.test.tsx "AC-3" | PASS |
| AC-4 | Oversized PDF → 400 (or 413) | SubmitAsync_OversizedFile_ReturnsValidation; POST_applications_OversizedPdf_Returns400 | PASS |
| AC-5 | Draft Requisition → 404 | SubmitAsync_DraftRequisition_ReturnsNotFound; POST_applications_DraftRequisition_Returns404 | PASS |
| AC-6 | Closed Requisition → 404 | SubmitAsync_ClosedRequisition_ReturnsNotFound; POST_applications_ClosedRequisition_Returns404IdenticalToDraft | PASS |
| AC-7 | Missing Requisition → 404, identical body | SubmitAsync_MissingRequisition_ReturnsNotFoundIdenticalToClosed; POST_applications_MissingRequisition_Returns404IdenticalToDraft | PASS |
| AC-8 | Duplicate submission → 409 | SubmitAsync_SecondSubmissionSameCandidateSameRequisition_ReturnsConflict; POST_applications_SecondSubmission_Returns409; application-form.test.tsx "AC-8" | PASS |
| AC-9 | Two distinct Candidates, same Requisition → both succeed | SubmitAsync_TwoDistinctCandidatesSameRequisition_BothSucceed | PASS |
| AC-10 | Anonymous submit → 401 | POST_applications_Anonymous_Returns401 | PASS |
| AC-11 | Recruiter/HiringManager submit → 403 | POST_applications_AsRecruiterOrHiringManager_Returns403 (Theory, both roles) | PASS |
| AC-12 | Candidate list, 2 Applications → 200 | ListMineAsync_TwoApplications_ReturnsBothWithRequisitionTitle; GET_applications_mine_ReturnsOwnApplicationsOnly; application-list.test.tsx "AC-12" | PASS |
| AC-13 | Candidate list, 0 Applications → 200, [] | ListMineAsync_NoApplications_ReturnsEmptyList; application-list.test.tsx "AC-13" | PASS |
| AC-14 | Owner downloads own CV → 200 | GetCvAsync_Owner_ReturnsStream; GET_applications_id_cv_AsOwner_Returns200WithPdfBytes | PASS |
| AC-15 | Non-owner Candidate downloads → 403 | GetCvAsync_NonOwnerCandidate_ReturnsForbidden; GET_applications_id_cv_AsNonOwnerCandidate_Returns403 | PASS |
| AC-16 | Staff list, 2 Applications → 200 | ListForRequisitionAsync_TwoApplications_ReturnsBothWithCandidateIdentity; GET_requisitions_id_applications_AsRecruiter_Returns200; applications-table.test.tsx "AC-16" | PASS |
| AC-17 | HiringManager list → 200 | GET_requisitions_id_applications_AsHiringManager_Returns200 | PASS |
| AC-18 | Staff list, 0 Applications → 200, [] | ListForRequisitionAsync_NoApplications_ReturnsEmptyList; GET_requisitions_id_applications_NoApplications_Returns200EmptyList; applications-table.test.tsx "AC-18" | PASS |
| AC-19 | Candidate calls staff list → 403 | GET_requisitions_id_applications_AsCandidate_Returns403 | PASS |
| AC-20 | Staff downloads any CV → 200 | GetCvAsync_Staff_ReturnsStreamRegardlessOfOwnership; GET_applications_id_cv_AsRecruiter_Returns200 | PASS |
| AC-21 | Candidate A cannot reach Candidate B's data | GetCvAsync_NonOwnerCandidate_ReturnsForbidden; GET_applications_id_cv_AsNonOwnerCandidate_Returns403 | PASS |
| AC-22 | Correct Requisition/Candidate id + UTC timestamp | Application_Create_SetsFieldsAndStartsWithNoCv | PASS |
| NFR-1 | Submission atomic w.r.t. valid, persisted CV | SubmitAsync_StorageThrows_ReturnsErrorNoRowWritten (asserts both Applications and CvAttachments empty) | PASS |
| NFR-2 | CV reachable only by authorized identity | LocalDiskFileStorageTests.ResolvePath_RejectsPathTraversalKeys; GetCvAsync_NonOwnerCandidate_ReturnsForbidden; manual code-path review (§5) | PASS |
| NFR-3 | Write lock held only for row insert, not file write | POST_applications_SlowFileWrite_DoesNotExtendSqliteWriteLockDuration — see F-3 | PASS (methodology caveat, F-3) |

22 of 22 ACs covered and passing; all 3 NFRs have a dedicated test.

Edge cases: E-1 covered at both outcome level (integration) and deterministic branch level
(unit test forcing the DbUpdateException fallback) — PASS. E-2 not independently tested but
verified by code inspection to share the exact SubmitAsync query as AC-5/AC-6 (no separate
"stale form" code path exists). E-3 — see F-2, no test exists. E-4, E-5, E-6, E-7 — covered
(SubmitAsync_StorageThrows..., ListForRequisitionAsync_MissingRequisition_ReturnsNotFound,
GetCvAsync_MissingApplication_ReturnsNotFound, SubmitAsync_SameCandidateDifferentRequisitions_BothSucceed).

## 3. Architectural Conformance

| Check | Result | Note |
|---|---|---|
| Files match the LLD manifest | PASS | All 31 named files exist; git diff --stat between "plan 0004" and "impl 0004 cp-4" shows exactly 48 files touched, all accounted for — no undeclared file created |
| Layering respected | PASS | Enforced by LayeringRuleTests.cs (4 automated tests), all green. Manually confirmed ApplicationEndpoints.cs has no DbContext/EF Core reference; ApplicationService.cs has no Microsoft.AspNetCore.Http/ClaimsPrincipal reference; Application.cs/CvAttachment.cs reference neither |
| No unauthorized cross-component dependency | PASS | service/application calls db/application, shared/storage, and read-only db/requisition (sanctioned by HLD context diagram) |
| Migration matches erd.md §5 | PASS | 20260805191845_AddApplicationsAndCvAttachments.cs matches columns/FKs/indexes exactly |
| API matches api.md | PASS | All four endpoints match routes, auth policies, and Result→HTTP mapping exactly |
| architecture.md reflects reality | PASS | Component Map, Data Model diagram, Change Log all accurate |
| tech-stack.md reflects reality | PASS | "Object storage: TBD" resolved; config keys added |
| Deviations declared | PASS | I-1 through I-12 all recorded in changelog with rationale; none change a contract, shape, or designed behaviour |

### 3.1 `ui/bff` proxy generalization — independent diff review (risk area 1)

Reviewed `git diff 21e83ba 22e3624 -- frontend/src/app/api/bff/proxy/[...path]/route.ts` (last
0002 version vs. 0004 CP-3 version) directly, not just the HLD's description:

- `req.text()` → `req.arrayBuffer()`, `res.text()` → `res.arrayBuffer()`, both byte-preserving;
  a JSON body round-trips through ArrayBuffer with identical bytes.
- `content-disposition` now forwarded; all other header handling unchanged.
- **One genuine but inert behavioural difference found:** old code `body: body || undefined` —
  an empty string body collapsed to `undefined` (no body sent). New code assigns the
  ArrayBuffer directly; an empty ArrayBuffer is truthy, so a POST with no body now sends an
  explicit 0-byte ArrayBuffer. Traced every current proxy caller: `RequisitionLifecycleActions.tsx`
  POSTs publish/unpublish/close with no body, and the corresponding backend endpoints
  (`RequisitionEndpoints.cs:46,52,58`) bind no request-body parameter at all — ASP.NET Core
  never reads the body for these routes, so this is unobservable server-side. Full pre-existing
  Vitest suite (`requisition-lifecycle-actions.test.tsx`, 5 tests) passed unmodified. Not a
  functional regression, but `api.md`'s claim of being "strictly backward-compatible... unchanged"
  is not literally byte-for-byte true for the empty-body case — true in every externally
  observable way today. Not raised as a numbered finding; recorded per the explicit instruction
  to check the diff, not just the test suite.

## 4. Coding Standards Conformance

| Rule | Result | Note |
|---|---|---|
| Naming conventions | PASS | |
| Error envelope used consistently | PASS | RFC 7807 ProblemDetails throughout, codes follow `<entity>.<operation>.<condition>` |
| No swallowed exceptions / empty catch (production) | PASS | Only test-teardown code (CustomWebApplicationFactory.Dispose, pre-existing from 0001/0002) has best-effort catch blocks |
| Structured logging; no secrets/PII in logs | F-1 (Medium) | See below |
| No secrets in source | PASS | |
| Parameterised queries only | PASS | All LINQ-to-EF-Core |
| No sleep-based waits | PASS with note | No Thread.Sleep; Task.Delay used deliberately for genuine SQLite lock contention in the NFR-3 test — see F-3 |
| Frontend: four async states, no business logic in components, accessibility | PASS | ApplicationForm covers idle/loading/error/success with role="alert"/"status", labelled input, disabled states; ApplicationList/ApplicationsTable cover empty + populated |
| Uploaded files never served from client-controlled path | PASS | StorageKey always server-generated GUID; OriginalFileName never used for filesystem access, only Content-Disposition |

### F-1 — No structured logging in service/application or api/application (Severity: Medium · Standards)

**Location:** `backend/src/Service/Application/ApplicationService.cs` (whole file, zero
ILogger usage); `backend/src/Api/ApplicationEndpoints.cs` (whole file, zero ILogger usage).

**Problem.** coding-standards.md requires ILogger<T> structured logging at
Debug/Information/Warning/Error levels for rejected input, recovered failures, and unhandled
errors. This spec's own plan/lld.md §8 documents a specific log level for every error
condition returned (e.g. `application.submit.storage-failed` → Error,
`application.submit.duplicate` → Information, `application.cv.forbidden` → Warning). None of
it is implemented. Grepping the entire backend/src tree (excluding bin/obj) for ILogger usage
in actual source finds exactly one file in the whole project — `Db/EfDatabaseHealthCheck.cs`
(from 0001); no service/* or api/* code anywhere logs anything.

**Impact.** Pre-existing gap across 0001–0003 too, not introduced by this spec — not a
regression. But 0004's LLD commits to specific log levels per condition, and the CP-2
changelog's Deviation Log ("None requiring an lld.md patch") is true for
behavioural/contract deviations but doesn't surface that the logging design was never built.
In production, a storage failure (E-4, disk full) or a race-condition conflict (E-1) would be
invisible in logs — the only signal is the HTTP response the caller already sees.

**Suggested fix.** Inject `ILogger<ApplicationService>` and add the eight log calls lld.md §8
already specifies. Consider scoping as a project-wide fix across 0001–0004 rather than
0004-only, since the gap predates this spec.

### F-2 — Edge case E-3 (CV upload interrupted mid-transfer) has no test (Severity: Medium · Standards / Coverage)

**Location:** `backend/tests/Ats.IntegrationTests/Application/ApplicationEndpointsTests.cs`
(absent); `docs/specs/0004-application-submission/plan/lld.md` §11 Test Plan (E-3 not listed);
`plan/tasks.md` Coverage Check table (only E-1, E-4, E-5, E-6, E-7 appear; E-2/E-3 do not).

**Problem.** spec.md's Edge Cases table requires: "CV upload is interrupted mid-transfer
(network drop) → No Application record is created; a partially received file is not
referenced by any record and is not retrievable." No test exercises a truncated/aborted
multipart upload. The LLD's own Test Plan never named this case — its absence was never
planned for, not a slip in execution.

**Impact.** The likely-correct behaviour (ASP.NET Core's form binder faults before the
handler runs, or IFormFile.Length reflects only bytes actually received, failing size/magic-
byte checks — or in the worst case silently accepting a truncated PDF whose first bytes still
happen to be %PDF-) is plausible by construction but genuinely unverified. This is exactly
"correct-looking code with no test is unverified code."

**Suggested fix.** Add an integration test that truncates a multipart body mid-write and
asserts no Application/CvAttachment row exists afterward and no orphaned file remains.

### F-3 — NFR-3 verification test is timing-based (Severity: Low · Quality)

**Location:** `backend/tests/Ats.IntegrationTests/Application/ApplicationEndpointsTests.cs:534-599`
(POST_applications_SlowFileWrite_DoesNotExtendSqliteWriteLockDuration).

**Problem.** Proves NFR-3 via genuine SQLite lock contention: one submission's
IFileStorage.SaveAsync is made artificially slow (1000ms), a 150ms head start is given, then
an unrelated write (candidate registration) is issued on a separate HttpClient against the
same database file; asserts the unrelated write finishes under 1000ms. Sound approach — tests
the real mechanism rather than mocking/inspecting SaveChangesAsync call order.

**Impact.** Wall-clock-dependent: the 150ms head start is a scheduler-behaviour assumption,
not a guarantee. Margin is wide (~6.7x gap), changelog records three additional back-to-back
runs with no flakiness, and it passed in this session too. On a heavily loaded CI runner it
could in principle produce a false failure (never a false pass).

**Suggested fix.** No action required now given the wide margin and observed stability; widen
the delay/head-start ratio further if it is ever seen to flake, rather than removing the
coverage (NFR-3 has no other test).

## 5. Risk-Area Review (per the validation brief)

1. **ui/bff proxy binary-safe passthrough** — reviewed the actual diff (§3.1), not just test
   results. Byte-preserving both directions; the one behavioural difference found (empty POST
   body sent as 0-byte ArrayBuffer instead of omitted) is inert given every current caller's
   backend endpoint either doesn't bind a request body or genuinely sends one. No finding.

2. **File-validation failures return 400, not 415/413** — api.md §3.1 documents 400 for both
   `application.submit.invalid-file-type` and `application.submit.file-too-large` explicitly,
   matching shipped code (ApplicationService.cs:69-82) exactly. spec.md AC-3/AC-4 both permit
   "400 (or 415/413)" explicitly. hld.md D-3 records the rationale (one Result→HTTP mapping
   per ResultStatus, not forking the envelope for two edge cases). Consistent; no finding.

3. **NFR-3 test soundness** — see F-3: sound methodology, Low-severity residual timing risk,
   not a defect.

4. **E-1 duplicate-submission fallback path coverage** — confirmed the DbUpdateException catch
   branch (ApplicationService.cs:118-129) IS exercised deterministically, not just implied by
   the outcome-only concurrency test. ApplicationServiceTests.cs:344-369
   (SubmitAsync_SaveChangesThrowsDbUpdateException_DeletesFileReturnsConflict) uses a
   ThrowingSaveChangesDbContext subclass forcing SaveChangesAsync to throw DbUpdateException on
   demand, asserting both the 409 result AND that the just-written file was deleted. The
   integration test correctly doesn't try to force this specific interleaving (forcing a
   specific race outcome under Task.WhenAll would itself be unreliable) and instead asserts the
   outcome invariant. Together: real deterministic branch coverage + genuine-concurrency
   outcome check. Sound combination, not a gap. No finding.

5. **CV path traversal and download authorization** — LocalDiskFileStorage.ResolvePath
   (LocalDiskFileStorage.cs:55-65) rejects any storage key containing `..`, `/`, `\`; the
   storage key is always server-generated (`{Guid.NewGuid():N}.pdf`, ApplicationService.cs:100)
   — OriginalFileName (client-supplied) is stored for display/Content-Disposition only and
   never passed to ResolvePath or any filesystem API (confirmed by reading every call site).
   GET /api/applications/{id}/cv (ApplicationEndpoints.cs:91-113) requires
   RequireAuthorization() (401 for anonymous) then re-checks ownership server-side inside
   GetCvAsync (ApplicationService.cs:198-220): a Candidate gets the file only if
   application.CandidateId == requestingUserId; Staff (Recruiter/HiringManager role) is
   unconditionally allowed, matching FR-11/AC-20's "any Application" grant — confirmed against
   0003's spec.md/erd.md that no per-requisition "ownership"/"assignment" concept exists
   anywhere in this single-org system, so "any Staff, any Application" is the correct and only
   sanctioned model, not a narrowing gap relative to a model that was never specified. No
   finding.

## 6. Coverage Gaps

| Area | Gap | Risk |
|---|---|---|
| E-3 (upload interrupted mid-transfer) | No test exercises a truncated/aborted multipart upload (F-2) | Low-to-Medium — plausible-by-construction but unverified |
| Structured logging | No log statements anywhere in service/application or api/application despite lld.md §8's design (F-1) | Medium — operational visibility gap, not a correctness defect |

## 7. Recommended Actions

1. Add the ILogger<ApplicationService> calls lld.md §8 already specifies (F-1) — consider
   scoping as a project-wide fix across 0001–0004.
2. Add an integration test for E-3 (F-2).
3. No action required for F-3 unless observed to flake in CI.

## 8. Status Decision

Verdict PASS-WITH-FINDINGS: every command run in this session was green (backend build + 4
architecture tests + 96 unit tests + 61 integration tests; frontend build + 44 Vitest tests +
clean lint), all 22 ACs plus all 3 NFRs are covered by a passing test, architecture and
layering conform with zero findings, and the two Medium standards findings (F-1 logging gap,
F-2 untested E-3 edge case) are coverage/quality gaps, not incorrect behaviour, security
issues, or architectural violations. No finding reaches High.

Spec status: implemented → validated. Updated 2026-08-06. docs/specs/index.md
row for 0004 updated in the same turn.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| 0003 (Requisition Management) | 1 | Used its spec.md/erd.md to confirm no per-requisition staff "ownership" concept exists anywhere in the system (risk area 5) — StaffOnly is a flat role check. Also source of RequisitionLifecycleActions.tsx/RequisitionEndpoints.cs read to assess the ui/bff proxy diff's one behavioural difference (§3.1). |
| 0002 (User Authentication and Refresh Token Flow) | 1 | Read to confirm CandidateOnly/StaffOnly policy semantics and the JWT-bearer-only auth model that makes .DisableAntiforgery() on the multipart submit endpoint sound rather than a security gap. |
| 0001 (Project Scaffolding and Walking Skeleton) | 1 | git diff'd the pre-0004 version of the ui/bff proxy route (last touched by 0002) against 0004's version to independently verify the binary-safe passthrough generalization (risk area 1). |

Cap reached: no (3 specs loaded). meta/architecture.md, meta/tech-stack.md,
meta/coding-standards.md, and index.md were read in full (Tier 0), per protocol.
