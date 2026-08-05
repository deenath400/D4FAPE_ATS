# Initialize Project

Bootstrap the spec-driven workflow in this repository. Run once, before the first `/specify`.

**Read `.spec-kit/stages/initialize.md` and follow it exactly.** It is the authoritative
definition of this stage, shared with the project's other AI tools. Do not substitute your own
procedure for it.

You establish the foundation every other stage depends on — all four stages read
`docs/specs/meta/` on every invocation, so an error here propagates into every feature the
project ever builds. Precision over completeness: eight accurate rows and three honest `TBD`
markers beat a file padded with plausible guesses.

## Before you start

Check whether `docs/specs/meta/architecture.md` already exists. If it does, **do not
overwrite it.** Ask the user whether to refresh from current code, leave it and just report
what exists, or cancel. Wait for their answer.

## Run the two phases

1. **Phase 1** — survey the repository: manifests, existing docs, source structure. Work out
   what you can derive from evidence. Write nothing.
2. **Ask the user** the gaps you could not derive — in chat, a few at a time, each with 2–4
   concrete options and your recommendation. Wait for answers.
3. **Phase 2** — write the blueprint files listed in the stage playbook.

For a greenfield repository the usual questions are: purpose and primary users, backend stack,
frontend stack, database, architecture style, auth approach, testing framework, deployment
target.

## Constraints

- Documentation only. Never create application code, install dependencies, or initialise git.
- Never create spec folders — allocating spec ids belongs to `/specify`.
- Shell access is for read-only inspection only.
- Mark every guess as an explicit Assumption blockquote.

## Finish by reporting

Files created with line counts, decisions recorded, assumptions and their reversal cost,
contradictions between the user's answers and repository evidence, what is left as TBD, and
the next step: `/specify`.
