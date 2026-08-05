---
name: specify
description: Stage 1 of the spec workflow. Turns a feature request into a functional specification with acceptance criteria under docs/specs/NNNN-slug/. Asks clarifying questions first. Use when the user runs /specify, or asks to spec out, define, or write requirements for a feature.
---

# /specify

Turns a feature request into a specification. Delegates to the **`spec-agent`**, which follows
`.spec-kit/stages/specify.md`. You own the clarification conversation because subagents cannot
prompt the user.

**Argument:** the feature request, in the user's own words. If they invoked `/specify` with
nothing, ask what feature they want specified before doing anything else.

## 1. Preflight

Check `docs/specs/meta/architecture.md` exists. If not, tell the user the project has no
blueprint and offer to run `/initialize-project` first. Do not proceed without it — the
agent would be specifying against nothing.

## 2. Phase 1 — dispatch for analysis

Spawn `spec-agent` synchronously (`run_in_background: false`, `subagent_type: "spec-agent"`).
Give it:

- the feature request **verbatim** — do not paraphrase, summarise, or "improve" it
- any relevant context from earlier in this conversation
- instruction that this is Phase 1: load context, analyse, question. Write nothing.

It returns: its understanding of the request, the prior specs it found relevant, proposed
scope, draft requirements, ranked clarifications, and assumptions it intends to make.

## 3. Show the read-back, then ask

**First, show the user the agent's Understanding and Proposed Scope** in two or three lines.
A wrong read-back caught here saves a whole wasted spec.

Then `AskUserQuestion`:

- Four questions maximum per call; loop for more, highest-consequence first.
- Options come from the agent, its recommendation first and labelled `(Recommended)`.
- Each option's description states the consequence of that choice.
- If the agent returned no clarifications because the request was already unambiguous, say so
  and go straight to step 4. Do not manufacture questions for form's sake.

If the agent reported the request is really several features, put that to the user as a
choice — split into separate specs, or narrow this one — before continuing.

## 4. Phase 2 — dispatch for writing

`SendMessage` the answers to the **same agent instance**. Its analysis and loaded context are
intact; spawning a fresh agent would waste all of it.

Pass every answer verbatim, plus any extra detail the user added while answering.

It writes `spec.md` and `clarifications.md`, and updates `docs/specs/index.md`.

## 5. Report

Relay concisely:

- the spec id, slug, title, and path
- counts: FRs, ACs, edge cases
- prior specs it built on and why
- **open questions that remain**, especially blocking ones
- assumptions it made without asking
- next step: `/plan NNNN`

Offer to walk through the acceptance criteria if the feature is substantial — the ACs are
what everything downstream is measured against, so they are worth the user's eyes.

## Notes

- Specs describe **what and why**, never how. If the user pushes implementation detail into
  the request, capture it as a constraint in the spec rather than as a requirement, and let
  `/plan` decide the design.
- This skill writes only inside `docs/specs/`.
