---
description: Bootstrap the spec-driven workflow — survey the repo, interview the user, and write the project blueprint under docs/specs/meta/. Run once, before the first /specify.
---

# Initialize Project

**Read `@.spec-kit/stages/initialize.md` and follow it exactly.** It is the authoritative
definition of this stage, shared with the project's other AI tools. The steps below are the
sequence; that file is the detail.

You establish the foundation every other stage depends on — all four stages read
`docs/specs/meta/` on every invocation, so an error here propagates into every feature the
project ever builds. Precision over completeness: eight accurate rows and three honest `TBD`
markers beat a file padded with plausible guesses.

## Steps

1. **Check for an existing blueprint.** If `docs/specs/meta/architecture.md` exists, do not
   overwrite it. Ask the user whether to refresh from current code, leave it and report what
   exists, or cancel. Wait for their answer.

2. **Survey the repository.** Look for manifests (`package.json`, `*.csproj`, `pom.xml`,
   `requirements.txt`, `go.mod`, …), existing docs (`README*`, `AGENTS.md`, `docs/**`), and —
   if source exists — the top-level structure plus a few representative files to infer
   layering, naming, error handling, and test conventions. Classify the repo as greenfield or
   brownfield. **Write nothing in this step.**

3. **Report what you derived from evidence**, in a table with the evidence cited.

4. **Ask the user the gaps you could not derive.** A few at a time, each with 2–4 concrete
   options and your recommendation with a one-clause reason. Cap at eight; anything less
   consequential becomes a documented assumption. Never ask what a manifest already answers.
   Wait for answers.

   For a greenfield repo the usual set is: purpose and primary users, backend stack, frontend
   stack, database, architecture style, auth approach, testing framework, deployment target.

5. **Write the blueprint** — the files listed in the stage playbook:
   `docs/specs/index.md`, `docs/specs/README.md`, and `docs/specs/meta/{project,architecture,
   tech-stack,coding-standards}.md`. Reconcile every answer against your step-2 evidence; flag
   contradictions rather than silently picking one.

6. **Ensure the root `AGENTS.md`** has a spec-workflow section pointing future sessions at
   `docs/specs/meta/architecture.md`. Edit it in; never overwrite the user's content.

7. **Report:** files created with line counts, decisions recorded, assumptions and their
   reversal cost, contradictions found, what is left as TBD, and the next step: `/specify`.

## Constraints

- Documentation only. Never create application code, install dependencies, or initialise git.
- Never create spec folders — allocating spec ids belongs to `/specify`.
- Terminal access is for read-only inspection only.
- `architecture.md` is capped at 150 lines. Check the count before finishing.
- Mark every guess as an explicit Assumption blockquote.
