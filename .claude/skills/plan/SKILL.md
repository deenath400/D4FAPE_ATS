---
name: plan
description: Stage 2 of the spec workflow. Turns an approved spec into a High-Level Design, Low-Level Design, API contract, ER model, and checkpointed task breakdown under docs/specs/NNNN-slug/plan/. Use when the user runs /plan, or asks to design, architect, or break down an existing spec into tasks.
---

# /plan

Turns a specification into an executable design. Delegates to the **`planning-agent`**, which
follows `.spec-kit/stages/plan.md`.

**Argument:** an optional spec id (`0001`, `1`, or a slug). With no argument, target the
newest spec with `status: specified`; if there is more than one candidate, list them and ask
which.

## 1. Preflight

Read `docs/specs/index.md` and the target spec's frontmatter.

| Condition | Action |
|---|---|
| No blueprint (`meta/architecture.md` missing) | Stop; offer `/initialize-project` |
| No spec matches the argument | List available specs and ask |
| Status is `specified` | Proceed |
| Status is `planned` or beyond | This is a **re-plan**. Confirm with `AskUserQuestion` first — it regresses status and may invalidate completed work |
| Spec has **blocking** open questions | Surface them and ask the user to resolve them before planning |

## 2. Dispatch

Spawn `planning-agent` synchronously (`run_in_background: false`, `subagent_type:
"planning-agent"`). Give it:

- the spec id and folder path
- whether this is a first plan or a re-plan (and if re-planning, what changed and what to
  preserve)
- resolutions to any open questions the user just gave you
- any constraints the user stated in this conversation (deadline, must-use library, "keep it
  minimal")

This stage is single-phase — the spec already resolved the ambiguities, so no clarification
round is normally needed. If the agent comes back reporting it cannot design without an
answer, put that to the user via `AskUserQuestion` and `SendMessage` the reply to the same
agent instance.

## 3. Report

Relay:

- the five files written, with line counts
- **the shape of the work**: N tasks across M checkpoints, files to create vs modify
- whether all ACs are covered by tasks
- conventions inherited from prior specs, and from which
- **decisions that deserve pushback** — layering exceptions, new technologies not in
  `tech-stack.md`, notable trade-offs
- top risks
- next step: `/implement NNNN` runs CP-1 only

## 4. Flag anything needing approval

If the agent proposed a new technology or an exception to the architecture's layering rules,
do not bury it in the summary. Call it out and ask the user to confirm before they run
`/implement`.

## Notes

- The task breakdown is where checkpoints are defined, and `/implement` stops at each one.
  If the user wants coarser or finer review granularity, that is a re-plan of `tasks.md` —
  offer it.
- This skill writes only inside `docs/specs/`.
