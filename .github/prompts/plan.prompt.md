---
description: Stage 2 — turn an approved spec into an HLD, LLD, API contract, ER model, and a checkpointed task breakdown under docs/specs/NNNN-slug/plan/.
agent: planning-agent
argument-hint: optional spec id, e.g. 0001
---

# /plan

Read `.spec-kit/stages/plan.md` and follow it exactly. It is the authoritative definition of
this stage.

**Target spec:** the id passed as an argument. With no argument, take the newest spec with
`status: specified`. If more than one qualifies, list them and ask which.

## Before you start

| Condition | Action |
|---|---|
| `docs/specs/meta/architecture.md` missing | Stop; offer `/initialize-project` |
| No spec matches the argument | List available specs and ask |
| Status is `specified` | Proceed |
| Status is `planned` or beyond | This is a **re-plan** — confirm with the user first, since it regresses status and may invalidate completed work |
| Spec has **blocking** open questions | Surface them and ask the user to resolve them before planning |

## Constraints

- Write only inside `docs/specs/`. You design; you do not implement.
- **Tier 1 reading is mandatory here.** Read the selected prior specs' `plan/api.md` and
  `plan/erd.md` in full — that is how project conventions propagate, and it is the reason
  feature seven does not invent a second error envelope.
- Never contradict the Layering Rules in `architecture.md`. If the feature genuinely requires
  it, make it an explicit Design Decision in the HLD and flag it — never do it quietly.
- Never introduce a technology absent from `tech-stack.md` without raising it for approval.
- Every checkpoint in `tasks.md` must end in a state where the project builds and its tests
  pass.
- Every acceptance criterion must be covered by at least one task. An uncovered AC is a
  defect to fix before finishing, not to report.

## Finish by reporting

The five files with line counts; the shape of the work (N tasks across M checkpoints, files
to create vs modify); whether all ACs are covered; conventions inherited and from which spec;
**decisions that deserve pushback**; top risks; next step: run `/implement`, which will
execute CP-1 only.

Call out layering exceptions and new technologies prominently — do not bury them in a summary.
