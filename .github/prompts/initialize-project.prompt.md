---
description: Bootstrap the spec-driven workflow — interview the user (or analyse existing code) and write the project blueprint under docs/specs/meta/. Run once, before the first /specify.
agent: blueprint-agent
argument-hint: (no arguments)
---

# /initialize-project

Read `.spec-kit/stages/initialize.md` and follow it exactly. It is the authoritative
definition of this stage.

## Before you start

Check whether `docs/specs/meta/architecture.md` already exists. If it does, **do not
overwrite it.** Ask the user whether to refresh the blueprint from current code, leave it and
just report what exists, or cancel — and wait for their answer.

## Running the two phases

1. **Phase 1** — survey the repository and work out what you can derive from evidence.
   Write nothing.
2. **Ask the user** the gaps you could not derive. Present them a few at a time, each with
   2–4 concrete options and your recommendation. Wait for answers before continuing.
3. **Phase 2** — write the blueprint files listed in the stage playbook.

For a greenfield repository the usual questions are: purpose and primary users, backend
stack, frontend stack, database, architecture style, auth approach, testing framework, and
deployment target.

## Constraints

- Documentation only. Never create application code, install dependencies, or initialise git.
- Never create spec folders — allocating spec ids is the `/specify` stage's job.
- Shell access is for read-only inspection only.
- Mark every guess as an explicit Assumption blockquote.

## Finish by reporting

Files created with line counts, decisions recorded, assumptions made and their reversal cost,
contradictions between the user's answers and repository evidence, what is left as TBD, and
the next step: run `/specify`.
