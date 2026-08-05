---
name: initialize-project
description: Bootstrap the spec-driven workflow in this repository — interviews the user (or analyses existing code) and writes the project blueprint under docs/specs/meta/ plus the spec index and root CLAUDE.md. Run once, before the first /specify. Use when the user asks to initialize the project, set up specs, create the blueprint, or when /specify is run and docs/specs/meta/ does not exist.
---

# /initialize-project

Establishes the foundation the whole workflow reads as Tier 0 context. Run once per repository.

Delegates to the **`blueprint-agent`**, which follows `.spec-kit/stages/initialize.md`. You
own the conversation with the user; the agent cannot prompt them.

## 1. Check whether it is safe to run

Check for `docs/specs/meta/architecture.md`.

- **Absent** — proceed.
- **Present** — do not overwrite. Ask the user via `AskUserQuestion` whether to
  (a) refresh the blueprint from current code, (b) leave it and just report what exists, or
  (c) cancel. Only pass a refresh instruction to the agent if they choose (a).

## 2. Phase 1 — dispatch for analysis

Spawn `blueprint-agent` **synchronously** (`run_in_background: false`, `subagent_type:
"blueprint-agent"`). Tell it:

- the absolute repository path
- that this is Phase 1: survey and question only, write nothing
- anything the user already told you about the project in this conversation — pass it through
  verbatim so the agent does not ask what they just said
- whether a refresh was authorised

It returns a repository assessment, what it derived from evidence, and a ranked list of gaps.

## 3. Put the questions to the user

Use `AskUserQuestion`. Rules:

- Maximum four questions per call. Loop if the agent returned more — highest-consequence first.
- Carry the agent's options through as the choices, with its recommendation listed **first**
  and labelled `(Recommended)`.
- Keep each option's description to the consequence of choosing it.
- If the agent returned no questions (a well-documented brownfield repo), skip straight to
  step 4.

For a greenfield project the agent will typically ask about: purpose and primary users,
backend stack, frontend stack, database, architecture style, auth approach, testing
framework, and deployment target.

## 4. Phase 2 — dispatch for writing

Send the answers back to the **same agent instance** with `SendMessage` (its analysis context
is intact — do not spawn a fresh agent and make it re-derive everything).

Include every answer verbatim, plus any extra detail the user volunteered.

It writes:

```
docs/specs/index.md
docs/specs/README.md
docs/specs/meta/project.md
docs/specs/meta/architecture.md
docs/specs/meta/tech-stack.md
docs/specs/meta/coding-standards.md
```

It also edits a spec-workflow section into the root `AGENTS.md` if one is not already there.

## 5. Report

Relay the agent's report — the subagent's output is not shown to the user, so summarise what
matters:

- files created, with line counts
- decisions recorded from their answers
- **assumptions the agent made** and what each would cost to reverse
- anything left `TBD` and which spec will settle it
- the next step: `/specify "<first feature>"`

Do not paste the blueprint contents back at them. Point at the files.

## Notes

- This skill scaffolds documentation only. It never creates application code, installs
  dependencies, or initialises git.
- If the user wants the app scaffolded too, that is the first spec's job — say so and offer
  to run `/specify` next.
