# Implement

Stage 3 — execute ONE checkpoint of a planned spec's task list, with tests, then stop for
review.

**Read `.spec-kit/stages/implement.md` and follow it exactly.** It is the authoritative
definition of this stage, shared with the project's other AI tools. Do not substitute your own
procedure for it.

**Target spec:** the id the user typed after the command. With no argument, take the newest
spec with status `planned` or `implementing`.

You are the only stage that touches application source, and therefore the only stage that
changes reality. That makes you responsible for keeping the written record true: the task
list, the deviation log, the changelog, and the architecture snapshot.

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

**One checkpoint, then stop.** Do not continue into the next one, even if it looks trivial,
and do not ask to. The user re-invokes `/implement` in one keystroke, and the boundary is what
keeps the reviewable surface small.

## Constraints

- Tests are part of each task, not a later checkpoint. A task claiming AC coverage without a
  test is not done.
- Tick a checkbox only after the code exists and its tests pass. Never tick a box to make a
  report look clean.
- Write real working code — no stubs, no not-implemented exceptions, no placeholder handlers.
- Before writing a new helper, search for one that already exists. Match the surrounding
  code's idiom, naming, and comment density.
- Record every deviation from the LLD: patch `plan/lld.md`, log it in
  `implementation/changelog.md`, surface it in your report. Silent drift makes every
  downstream artifact untrustworthy.
- Update `docs/specs/meta/architecture.md` by **surgical edit only** — never regenerate it.
  Most checkpoints change 0–5 lines plus one change-log row.
- Never fabricate command output. Run the real commands from `meta/tech-stack.md`.
- Never weaken, skip, or delete an existing test to make a build green.
- No git commits, pushes, or branches unless the user explicitly asked.

## Finish by reporting

Checkpoint completed and tasks ticked; files created and modified; **the verbatim build and
test output**; deviations with reasons and whether the LLD was patched; what changed in
`architecture.md`; anything needing attention; next step — `/implement` again for the next
checkpoint, or `/validate` if every task is now checked.
