---
name: validate
description: Stage 4 of the spec workflow. Runs the project's tests and lint, validates the implementation against the design and architecture, checks coding standards, builds an acceptance-criteria traceability matrix, and writes a validation report. Use when the user runs /validate, or asks to verify, check, or QA an implemented spec.
---

# /validate

Independently checks what was built against what was specified. Delegates to the
**`validation-agent`**, which follows `.spec-kit/stages/validate.md`.

**Argument:** an optional spec id. With no argument, target the newest spec with status
`implemented`; fall back to `implementing` (partial validation) and say so.

## 1. Preflight

| Condition | Action |
|---|---|
| Status below `implementing` | Stop; nothing has been built yet |
| Status `implementing` | Proceed, but tell the user only completed checkpoints will be validated |
| Status `validated` | Re-validation; note the previous verdict and date |

## 2. Dispatch

Spawn `validation-agent` synchronously (`run_in_background: false`, `subagent_type:
"validation-agent"`). Give it:

- the spec id and folder path
- whether this is partial (status `implementing`) or full validation
- any specific concern the user raised — "check the authorization especially" — as an
  additional focus, not a replacement for the standard checks
- environment facts it cannot discover, e.g. whether a database is available for integration
  tests

Validation runs commands and can take a while. It is fine to let it work.

## 3. Report

Lead with the **verdict** — `PASS`, `PASS-WITH-FINDINGS`, or `FAIL` — and the blocking issues
if any. Then:

- the dimension table: build, unit tests, integration tests, lint, AC coverage, architecture,
  standards
- **real test output**, not a characterisation of it
- findings ranked by severity, each with its `file:line` and why it matters
- **what could not be verified** — commands that could not run, ACs checked only manually.
  This is as important as the findings; do not bury it
- the new spec status (or that it was left unchanged on a FAIL)
- next step

Never soften a FAIL. An honest failing report is the product working correctly.

## 4. Offer follow-up

The validation agent deliberately does not fix anything, so findings need somewhere to go.
Offer, in order of preference:

1. **Fix within this spec** — if the findings are gaps in tasks that should have been done,
   add tasks to `plan/tasks.md`, set status back to `implementing`, and run `/implement`.
2. **Fix directly** — for small, unambiguous findings, offer to fix them here and re-run
   `/validate`.
3. **Defer** — record them as a follow-up spec via `/specify`.

Ask which they want rather than picking for them.

## Notes

- The agent never edits source. If the user asks it to "validate and fix", split that: run
  validation first, then act on the report.
- A `PASS` with zero findings is a legitimate outcome — do not treat an empty findings list as
  a sign the validation was shallow. Check the "Not Verified" section instead; that is where
  shallowness would show.
