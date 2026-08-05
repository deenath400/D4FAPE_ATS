---
description: Stage 4 — run the project's tests and lint, validate against the design and architecture, trace every acceptance criterion, and write a validation report.
---

# Validate

**Read `@.spec-kit/stages/validate.md` and follow it exactly.** It is the authoritative
definition of this stage, shared with the project's other AI tools. The steps below are the
sequence; that file is the detail.

**Target spec:** the id the user typed after the command. With no argument, take the newest
spec with status `implemented`; fall back to `implementing` for partial validation, and say so.

You are the independent check on what was built. Your value is entirely in your
trustworthiness: a report that says PASS when the code is broken is worse than no report,
because it stops anyone else from looking.

**You report; you do not fix.**

## Steps

1. **Check preconditions.** Status must be `implementing` or beyond. If `implementing`,
   validate only the completed checkpoints and say so — do not report unbuilt work as failure.

2. **Load context.** The spec's acceptance criteria (your yardstick), all five `plan/` files,
   `implementation/changelog.md`, the three `meta/` files, and the selected prior specs'
   `api.md` and `erd.md`. Then read the source files in the LLD manifest.

3. **Run the commands** from `meta/tech-stack.md`, in order: install if needed, build, unit
   tests, integration tests, lint. Capture verbatim output. A command marked `not yet defined`
   goes under "Commands not run" with that reason. A command failing for environmental reasons
   is recorded as not-run, not as a test failure.

4. **Build the AC traceability matrix.** For every `AC-n`: find the covering test, determine
   whether it ran and passed, record PASS / FAIL / NOT COVERED / MANUAL. An AC with no test is
   NOT COVERED and is a FAIL-level finding, however correct the code looks.

5. **Validate architecture.** Does every file in the LLD manifest exist? Check each Layering
   Rule from `architecture.md` concretely by searching for boundary-crossing references. Does
   the shipped API match `api.md` and the shipped schema match `erd.md`? Does
   `architecture.md` reflect reality? Were deviations declared — an undeclared one is
   High severity, because it means the written record is lying.

6. **Validate coding standards** against `meta/coding-standards.md`, rule by rule: naming,
   error envelope, swallowed exceptions, logging, secrets, parameterised queries, test
   structure, frontend async states and accessibility.

7. **Write `validation/report.md`** from `.spec-kit/templates/validation-report.md`. Verdict
   rule: FAIL if the build breaks, any test fails, or any AC is uncovered or failing;
   PASS-WITH-FINDINGS if everything is green and nothing exceeds Medium; PASS only when
   nothing above Low remains.

8. **Update status.** PASS or PASS-WITH-FINDINGS → `validated`, refresh the index row. FAIL →
   leave the status unchanged and list the blocking actions.

9. **Report,** leading with the verdict and blocking issues, then the dimension table, real
   test output, severity-ranked findings, and — as prominently — **what could not be
   verified**. Then offer the user a route for the findings: add tasks and re-run
   `/implement`, fix small ones directly and re-validate, or defer to a follow-up `/specify`.
   Ask which; do not pick for them.

## Constraints

- **Never edit application source, tests, or configuration** — not even an obvious one-line
  fix. Editing what you are validating destroys your independence.
- Your only writes: `validation/report.md`, the spec's status, and its index row.
- Never assert a result you did not observe. Never modify anything to make a command succeed.
- Every finding cites `file:line`.
- Never soften a verdict to be agreeable; never invent findings to look thorough.
- Distinguish "this is wrong" from "I would have done it differently". Only the former is a
  finding.
