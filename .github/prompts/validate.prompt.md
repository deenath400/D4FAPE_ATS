---
description: Stage 4 — run the project's tests and lint, validate against the design and architecture, trace every acceptance criterion, and write a validation report.
agent: validation-agent
argument-hint: optional spec id, e.g. 0001
---

# /validate

Read `.spec-kit/stages/validate.md` and follow it exactly. It is the authoritative definition
of this stage.

**Target spec:** the id passed as an argument. With no argument, take the newest spec with
status `implemented`; fall back to `implementing` for partial validation, and say so.

## Before you start

| Condition | Action |
|---|---|
| Status below `implementing` | Stop; nothing has been built yet |
| Status `implementing` | Proceed, but validate only completed checkpoints and say so |
| Status `validated` | Re-validation; note the previous verdict and date |

## Constraints

- **You report; you do not fix.** Never edit application source, tests, or configuration —
  not even an obvious one-line fix. Your only writes are `validation/report.md`, the spec's
  status, and its index row. Editing what you are validating destroys your independence.
- Run the literal commands from `meta/tech-stack.md` and paste **verbatim output**. Never
  assert a result you did not observe.
- A command that is `not yet defined` goes under "Commands not run" — "no lint step exists"
  and "lint failed" are different facts and must not be conflated.
- Every `AC-n` gets a row in the traceability matrix: PASS, FAIL, NOT COVERED, or MANUAL.
  An AC with no test is NOT COVERED and is a FAIL-level finding, however correct the code
  looks.
- Check each Layering Rule from `architecture.md` concretely. An undeclared deviation — code
  differing from the LLD with nothing in the changelog — is a High-severity finding.
- Every finding cites `file:line`.
- Never soften a verdict to be agreeable. Never invent findings to look thorough.

## Finish by reporting

Lead with the **verdict** (PASS / PASS-WITH-FINDINGS / FAIL) and the blocking issues. Then the
dimension table, real test output, findings ranked by severity, and — as prominently as the
findings — **what could not be verified**.

Then offer the user a route for the findings: add tasks to `plan/tasks.md` and re-run
`/implement`, fix the small ones directly and re-validate, or defer them to a follow-up
`/specify`. Ask which; do not pick for them.
