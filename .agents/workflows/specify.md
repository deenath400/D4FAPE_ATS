---
description: Stage 1 — turn a feature request into a functional specification with acceptance criteria under docs/specs/NNNN-slug/. Asks clarifying questions first.
---

# Specify

**Read `@.spec-kit/stages/specify.md` and follow it exactly.** It is the authoritative
definition of this stage, shared with the project's other AI tools. The steps below are the
sequence; that file is the detail.

The feature request is whatever the user typed after the command. If they gave nothing, ask
what feature they want specified before doing anything else.

You describe **what and why**. Never how. No file paths, class names, framework choices, or
table columns — those belong to `/plan`, and pre-empting them removes its ability to find a
better design.

## Steps

1. **Check the blueprint exists.** If `docs/specs/meta/architecture.md` is missing, tell the
   user and offer `/initialize-project` first. Do not proceed — you would be specifying
   against nothing.

2. **Load context** per `@.spec-kit/context-loading.md`. Tier 0 always: `architecture.md`,
   `tech-stack.md`, `coding-standards.md`, `docs/specs/index.md`. Then Tier 1: score prior
   specs on entity and component overlap, take **at most three**, and read their acceptance
   criteria (plus `plan/erd.md` if shared entities are involved).

3. **Analyse and report — write nothing yet.** Give the user your Understanding of the
   request in the project's domain language, the prior art you found, your proposed scope
   (in and out), and draft one-line requirements. A wrong read-back caught here saves a whole
   wasted spec.

4. **Ask the clarifying questions.** A few at a time, each with 2–4 concrete options and a
   recommendation. Ask only where different readings produce materially different work;
   otherwise decide it yourself and list it as an assumption. Cap at six — beyond that the
   feature is too big, so say so and propose the split. Wait for answers.

5. **Write the spec.** Allocate the next four-digit id, choose a kebab-case slug, and write
   `docs/specs/NNNN-slug/spec.md` and `clarifications.md` from the templates in
   `.spec-kit/templates/`.

6. **Update `docs/specs/index.md`** — append the row, bump the Next id.

7. **Report:** spec id, slug and path; counts of FRs, ACs and edge cases; prior specs used and
   why; open questions remaining; assumptions made without asking; next step: `/plan`.

## Constraints

- Write only inside `docs/specs/`. Never touch application source.
- Acceptance criteria are Given/When/Then and objectively checkable. If two competent people
  could disagree about whether one passed, rewrite it.
- Cover the unhappy paths: permission denied, not found, concurrent modification, validation
  failure, empty state.
- Every FR has at least one AC; every AC cites an FR. State that you checked.
- `components` and `entities` in the frontmatter are how future specs find this one — reuse
  existing names rather than coining synonyms.
- Never invent a requirement the user did not ask for. Put it under Open Questions instead.
