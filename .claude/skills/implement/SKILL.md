---
name: implement
description: Stage 3 of the spec workflow. Executes one checkpoint of a planned spec's task list — writes backend and frontend code plus tests, runs the build, and updates the architecture snapshot, then stops for review. Use when the user runs /implement, or asks to build, code, or execute a spec's plan.
---

# /implement

Builds one checkpoint of a planned spec, then stops. Delegates to the
**`implementation-agent`**, which follows `.spec-kit/stages/implement.md`.

**Argument:** an optional spec id. With no argument, target the newest spec with status
`planned` or `implementing`; if ambiguous, list candidates and ask.

## 1. Preflight

Read `docs/specs/index.md`, the target spec's frontmatter, and its `plan/tasks.md`.

| Condition | Action |
|---|---|
| Status below `planned` | Stop; the spec needs `/plan` first |
| `plan/tasks.md` missing | Stop; re-run `/plan` |
| All tasks already checked | Report complete; suggest `/validate NNNN` |
| Status `implementing` | Normal resume — continue from the first unchecked task |

**Identify the checkpoint that will run** — the one containing the first unchecked task — and
tell the user before dispatching: "Running CP-2 (API): T-04, T-05, T-06. Will stop at the
checkpoint boundary." No surprises about scope.

## 2. Dispatch

Spawn `implementation-agent` synchronously (`run_in_background: false`, `subagent_type:
"implementation-agent"`). Give it:

- the spec id and folder path
- the checkpoint id and its task ids — its scope, explicitly
- the instruction that it stops at the boundary and does **not** continue into the next
  checkpoint
- any review feedback the user gave on the previous checkpoint
- whether the user has authorised git operations (default: no)

## 3. Report

Relay:

- checkpoint completed, tasks ticked, new spec status
- files created and modified
- **the actual build and test output** — paste it, do not characterise it. If something
  failed, lead with that
- deviations from the LLD, with reasons and whether the LLD was patched
- what changed in `architecture.md`
- anything the agent flagged as needing attention
- next step: `/implement NNNN` for the next checkpoint, or `/validate NNNN` if done

## 4. Pause for review

This is the point of the checkpoint boundary. Do not auto-continue into the next checkpoint,
even if the user seems to be in a hurry — they can invoke `/implement` again in one keystroke,
and the reviewable surface stays small.

If the user explicitly asks to run several checkpoints back to back, invoke this skill
repeatedly, reporting between each. Never tell the agent to ignore its boundary.

## Notes

- If the agent stopped mid-checkpoint on a blocker, relay the blocker precisely and do not
  attempt to work around it yourself — resolve it with the user, then re-run.
- If a deviation changed the API contract or data model, mention it prominently: those are
  what the next spec inherits.
- Git: the agent does not commit unless the user asked. If they want a commit per checkpoint,
  do it yourself after reporting.
