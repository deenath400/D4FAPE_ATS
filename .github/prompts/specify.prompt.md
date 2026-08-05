---
description: Stage 1 — turn a feature request into a functional specification with acceptance criteria under docs/specs/NNNN-slug/. Asks clarifying questions first.
agent: spec-agent
argument-hint: the feature request, in your own words
---

# /specify

Read `.spec-kit/stages/specify.md` and follow it exactly. It is the authoritative definition
of this stage.

The feature request is whatever the user passed after the command. If they passed nothing,
ask what feature they want specified before doing anything else.

## Before you start

Check `docs/specs/meta/architecture.md` exists. If it does not, tell the user the project has
no blueprint and offer to run `/initialize-project` first. Do not proceed without it — you
would be specifying against nothing.

## Running the two phases

1. **Phase 1** — load Tier 0 and Tier 1 context per `.spec-kit/context-loading.md`, then
   analyse the request. Write nothing.
2. **Show the user your Understanding and Proposed Scope first** — a wrong read-back caught
   here saves a whole wasted spec. Then ask your clarifying questions, a few at a time, each
   with 2–4 concrete options and a recommendation. Wait for answers.
3. **Phase 2** — write `spec.md` and `clarifications.md`, and update `docs/specs/index.md`.

If the request is really several features, say so and propose the split before continuing.
If it is already unambiguous, ask nothing and say so.

## Constraints

- Write only inside `docs/specs/`. Never touch application source.
- Describe **what and why**, never how. No file paths, class names, frameworks, or columns.
- Never invent a requirement the user did not ask for — put it under Open Questions instead.
- Acceptance criteria are Given/When/Then and objectively checkable. If two competent people
  could disagree about whether one passed, rewrite it.

## Finish by reporting

Spec id, slug and path; counts of FRs, ACs and edge cases; prior specs used and why; open
questions that remain; assumptions made without asking; next step: run `/plan` on this id.
