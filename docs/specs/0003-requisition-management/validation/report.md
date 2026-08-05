# Validation Report — 0003 Requisition Management

**Spec:** `../spec.md` · **Date:** 2026-08-06 · **Validated by:** validation-agent (full validation)

**Verdict:** PASS-WITH-FINDINGS

---

## Dimension Table

| Dimension | Result |
|---|---|
| Build | PASS (backend + frontend) |
| Unit tests | 51 passed, 0 failed (backend) · 31 passed, 0 failed (frontend) |
| Integration tests | 38 passed, 0 failed |
| Lint | Backend: 0 errors (`dotnet build`) · Frontend: 0 ESLint warnings/errors |
| AC coverage | 24 of 24 + NFR-1/NFR-2 |
| Architecture | 0 findings — `architecture.md` Component Map, ER diagram, and Change Log accurately reflect what CP-1–CP-4 shipped |
| Standards | 5 findings (0 High, 2 Medium, 3 Low) |

## Blocking Issues

None functionally — all commands green, all 24 ACs + NFR-1/NFR-2 covered by tests.

## Findings

| # | Severity | Area | Location | Issue |
|---|---|---|---|---|
| F-1 | Medium | Standards / record accuracy | `implementation/changelog.md:361,388-390`; `backend/src/Service/Common/Result.cs` | CP-4 changelog claims "40 remaining... all pre-existing 0001/0002 files, none owned by 0003" after `dotnet format --verify-no-changes`; independent re-run found 41 flagged files, and `Result.cs` (modified by this spec's CP-2/T-10 to add the 3-arg `Validation` overload) plus `AppDbContext.cs`, `Program.cs`, `ServiceCollectionExtensions.cs` (all touched by this spec) are among them — not purely "0001/0002" files as characterized. |
| F-2 | Medium | Coverage depth | `frontend/src/middleware.ts:8-17`; `frontend/tests/lib/auth-guards.test.ts` | AC-14/AC-15 are tested only at the `isStaffRole`/`isRecruiter` predicate level; no test drives `middleware.ts` itself, so the anonymous→`/login` vs. Candidate→`/` redirect branch is never exercised by CI. Declared as "logic level" in the LLD, so not a silent gap. |
| F-3 | Low | Quality/UX | `frontend/src/app/staff/requisitions/[id]/page.tsx:51-56`; `RequisitionForm.tsx` | `RequisitionForm` renders a fully-editable, submittable form for HiringManager sessions (read-only per spec), unlike `RequisitionLifecycleActions` which correctly hides itself via `canWrite`. Backend `RecruiterOnly` policy still blocks the submit (not a security issue), just a UX inconsistency. Same pattern on the "New Requisition" CTA in `staff/requisitions/page.tsx:37-43`. |
| F-4 | Low | Documentation | `docs/specs/meta/coding-standards.md:28`; `plan/hld.md:151` (D-1) | This spec's own HLD flags that `Requisitions`/`Stages` PascalCase table names contradict `coding-standards.md`'s written snake_case rule and says "flagged for a future correction" — no correction was made; the Naming table is now inconsistent with twice-established practice (0002 and 0003). |
| F-5 | Low | Coverage depth | `backend/tests/Ats.IntegrationTests/Requisition/RequisitionEndpointsTests.cs:312-328` | `ANY_requisitionsEndpoint_AsCandidate_Returns403` only exercises `GET`/`POST /api/requisitions`, not the other 5 staff routes, despite its name. Policy attributes are structurally uniform across all 7 routes (confirmed by source read), so this is a naming/coverage-literalness gap, not a real risk. |

## Not Verified

- **`middleware.ts`'s actual redirect execution** (F-2) — verified by source inspection only (`middleware.ts:8-17`), not by a test that invokes it.
- **`npm ci`/`dotnet restore` from a clean state** — not re-run in this session; `node_modules` and `bin/obj` were already present and both `dotnet build`/`npm run build` succeeded without a restore step, which confirms the dependency graph is intact but doesn't confirm a from-scratch install works identically to the changelog's CP-1/CP-3 runs (which did report clean-install issues that were resolved — SDK repin, `npm install` lock-file drift — both already fixed on disk).
- **The exact severity/impact of the 40 pre-existing (non-0003) `dotnet format` / 32 `npm run format` files** — confirmed their existence and that none belong to 0003's owned directories, but did not audit each individually; treated as out-of-scope per the pre-existing-drift framing from 0001/0002.

## Recommended Actions (non-blocking, at the team's discretion)

1. Correct the CP-4 changelog's characterization of the format-drift file set (F-1).
2. Add a test that exercises `middleware.ts` directly for the anonymous/Candidate redirect paths (F-2).
3. Gate `RequisitionForm`'s editability on `canWrite`/role the same way `RequisitionLifecycleActions` does (F-3).
4. Resolve the PascalCase-vs-snake_case table-naming inconsistency in `coding-standards.md` (F-4).
5. Broaden or rename `ANY_requisitionsEndpoint_AsCandidate_Returns403` to match what it actually tests, or extend it to cover all 7 staff routes (F-5).

None of the above require re-running `/validate` before proceeding to the next spec.
