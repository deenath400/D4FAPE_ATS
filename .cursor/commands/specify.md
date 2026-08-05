# Specify

Stage 1 — turn a feature request into a functional specification with acceptance criteria.

**Read `.spec-kit/stages/specify.md` and follow it exactly.** It is the authoritative
definition of this stage, shared with the project's other AI tools. Do not substitute your own
procedure for it.

The feature request is whatever the user typed after the command. If they gave nothing, ask
what feature they want specified before doing anything else.

You describe **what and why**. Never how. No file paths, class names, framework choices, or
table columns — those belong to `/plan`, and pre-empting them removes its ability to find a
better design.

## Before you start

Check `docs/specs/meta/architecture.md` exists. If not, tell the user the project has no
blueprint and offer `/initialize-project` first. Do not proceed without it — you would be
specifying against nothing.

## Run the two phases

1. **Phase 1** — load Tier 0 and Tier 1 context per `.spec-kit/context-loading.md`, then
   analyse the request. Write nothing.
2. **Show your Understanding and Proposed Scope first** — a wrong read-back caught here saves
   a whole wasted spec. Then ask your clarifying questions in chat, a few at a time, each with
   2–4 concrete options and a recommendation. Wait for answers.
3. **Phase 2** — write `spec.md` and `clarifications.md`, and update `docs/specs/index.md`.

Ask only where different readings produce materially different work; otherwise decide it
yourself and list it as an assumption. Cap at six questions — beyond that the feature is too
big, so say so and propose the split. If the request is already unambiguous, ask nothing and
say so.

## Constraints

- Write only inside `docs/specs/`. Never touch application source.
- Acceptance criteria are Given/When/Then and objectively checkable. If two competent people
  could disagree about whether one passed, rewrite it.
- Cover the unhappy paths: permission denied, not found, concurrent modification, validation
  failure, empty state.
- Never invent a requirement the user did not ask for — put it under Open Questions instead.

## Finish by reporting

Spec id, slug and path; counts of FRs, ACs and edge cases; prior specs used and why; open
questions remaining; assumptions made without asking; next step: `/plan`.
