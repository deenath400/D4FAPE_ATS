---
description: Stage 3 — execute ONE checkpoint of a planned spec's task list, with tests, then stop for review.
---

# Implement

**Read `@.spec-kit/stages/implement.md` and follow it exactly.** It is the authoritative
definition of this stage, shared with the project's other AI tools. The steps below are the
sequence; that file is the detail.

**Target spec:** the id the user typed after the command. With no argument, take the newest
spec with status `planned` or `implementing`.

You are the only stage that touches application source, and therefore the only stage that
changes reality. That makes you responsible for keeping the written record true: the task
list, the deviation log, the changelog, and the architecture snapshot.

## Steps

1. **Check preconditions.** Status must be `planned` or `implementing`; `plan/tasks.md` must
   exist with at least one unchecked task. If all tasks are checked, report complete and
   suggest `/validate`. If the tree is already broken before you start, say so — do not
   attribute a pre-existing failure to your work.

2. **Determine scope.** Find the first unchecked `- [ ]` task. Its checkpoint is your entire
   scope. **Tell the user before you begin:** "Running CP-2 (API): T-04, T-05, T-06. Will stop
   at the checkpoint boundary."

3. **Load context.** `plan/tasks.md` and `plan/lld.md` in full, the relevant parts of
   `plan/api.md` and `plan/erd.md`, the spec's acceptance criteria, the three `meta/` files,
   and previous checkpoints' `implementation/changelog.md` if this is not CP-1.

4. **Execute the tasks in dependency order.** For each: read the LLD sections it references,
   read the files it modifies in full, write the code, write the tests named in the LLD Test
   Plan, run those tests, then tick the box. Before writing a new helper, search for one that
   already exists. Match the surrounding code's idiom and naming.

5. **Record deviations honestly.** Where the LLD is wrong or worse than an obvious
   alternative: implement the better thing, patch `plan/lld.md` and its Deviation Log, log it
   in the changelog, and surface it in your report. If the deviation changes the API contract
   or data model, patch `plan/api.md` or `plan/erd.md` too — those are what the next spec
   inherits.

6. **Verify the checkpoint.** Run the literal build, test, and lint commands from
   `meta/tech-stack.md`. Capture the real output. If a test fails and you cannot fix it within
   scope, stop, report it, and leave the task unticked.

7. **Update the record.** Append a checkpoint section to `implementation/changelog.md`. Apply
   a **surgical** edit to `docs/specs/meta/architecture.md` per `@.spec-kit/meta-maintenance.md`
   — read it in full first, change only what a new developer would now need to know (usually
   0–5 lines), and append exactly one change-log row. Update `tech-stack.md` only if a
   dependency or command changed. Set the spec status (`implementing` or `implemented`) and
   refresh the index row.

8. **Stop and report.** Do not start the next checkpoint. Do not ask to.

## Constraints

- **One checkpoint per invocation.** The hardest rule and the most important — the boundary is
  what keeps the reviewable surface small.
- Tests are part of each task, not a later checkpoint.
- Tick a checkbox only after the code exists and its tests pass.
- Write real working code — no stubs, no not-implemented exceptions, no placeholder handlers.
- Never fabricate command output.
- Never weaken, skip, or delete an existing test to make a build green.
- Never install a dependency that is not in the plan.
- No git commits, pushes, or branches unless the user explicitly asked.
