---
description: Stage 3 — execute ONE checkpoint of a planned spec's task list, with tests, then stop for review.
agent: implementation-agent
argument-hint: optional spec id, e.g. 0001
---

# /implement

Read `.spec-kit/stages/implement.md` and follow it exactly. It is the authoritative
definition of this stage.

**Target spec:** the id passed as an argument. With no argument, take the newest spec with
status `planned` or `implementing`.

## Before you start

| Condition | Action |
|---|---|
| Status below `planned` | Stop; the spec needs `/plan` first |
| `plan/tasks.md` missing | Stop; re-run `/plan` |
| All tasks already checked | Report complete; suggest `/validate` |
| Status `implementing` | Normal resume from the first unchecked task |

Identify the checkpoint containing the first unchecked task and **tell the user before you
begin**: "Running CP-2 (API): T-04, T-05, T-06. Will stop at the checkpoint boundary."

## The rule that matters most

**Execute that one checkpoint, then stop.** Do not continue into the next one, even if it
looks trivial. The user re-invokes `/implement` in one keystroke, and the boundary is what
keeps the reviewable surface small.

## Constraints

- Tests are part of each task, not a later checkpoint.
- Tick a checkbox only after the code exists and its tests pass. Never tick a box to make a
  report look clean.
- Write real working code. No stubs, no not-implemented exceptions, no placeholder handlers,
  unless the LLD explicitly defers something to a named later task.
- Record every deviation from the LLD: patch `plan/lld.md`, log it in
  `implementation/changelog.md`, and surface it in your report. Silent drift makes every
  downstream artifact untrustworthy.
- Update `docs/specs/meta/architecture.md` by **surgical edit only** — never regenerate it.
  Most checkpoints change 0–5 lines plus one change-log row.
- Never fabricate command output. Run the real commands from `meta/tech-stack.md`.
- Never weaken or skip an existing test to make a build green.
- No git commits, pushes, or branches unless the user explicitly asked.

## Finish by reporting

Checkpoint completed and tasks ticked; files created and modified; **the verbatim build and
test output**; deviations with reasons and whether the LLD was patched; what changed in
`architecture.md`; anything needing attention; next step — `/implement` again for the next
checkpoint, or `/validate` if every task is now checked.
