---
trigger: always_on
description: The spec-driven development workflow for this repository — how to orient, when a feature needs a spec, and the non-negotiable rules for every stage.
---

# Spec-Driven Workflow

Full working agreement: `@AGENTS.md` at the repository root. Stage definitions:
`@.spec-kit/stages/`. This rule is the short version that is always in context.

## Orient before editing

1. `docs/specs/meta/architecture.md` — what exists and how it fits together
2. `docs/specs/meta/coding-standards.md` — binding on every line you write
3. `docs/specs/meta/tech-stack.md` — frameworks and the literal build/test/lint commands
4. `docs/specs/index.md` — one row per feature spec, with current status

If `docs/specs/` does not exist, the project is uninitialised — run `/initialize-project`.

## Feature work goes through the four stages

```
/specify  →  /plan  →  /implement  →  /validate
```

Workflows live in `.agents/workflows/`. Each points at its stage playbook in `.spec-kit/stages/`.

Small fixes — a typo, a one-line bug, a rename — do not need a spec. Anything that adds
behaviour, changes an API, or touches the data model does. If you are about to build a feature
without a spec, stop and suggest `/specify`.

## Reading prior specs efficiently

Do not read every prior spec; do not read none. Per `@.spec-kit/context-loading.md`:

- **Always:** the four orientation files above — budget-capped to stay affordable.
- **At most 3 prior specs:** use the `components` and `entities` columns in `index.md` to find
  genuine overlap, then read only their acceptance criteria, `plan/api.md`, and `plan/erd.md`.
  That is where reusable conventions live.
- **On demand:** a prior LLD or changelog, when modifying its code.

Record what you loaded in the `## Related Specs` section of whatever you write.

## Non-negotiables

- **Never fabricate command output.** If you did not run it, do not report a result.
- **Read before you write.** Never overwrite a file you have not read.
- **`/implement` does one checkpoint, then stops.** Never run the whole task list in one go.
- **Only `/implement` writes application source.** The other three stages write only inside
  `docs/specs/`.
- **`/validate` never edits code.** It reports findings with `file:line`.
- **Keep `docs/specs/index.md` current** whenever a spec's frontmatter changes. A stale index
  degrades every future spec's context loading.
- **Update `architecture.md` by surgical edit**, never regeneration. It is capped at 150 lines
  and records structure, not activity.
- **Record deviations** from the LLD rather than letting the design drift silently.
- **`.spec-kit/conventions.md` is authoritative** on ids, frontmatter, status lifecycle, and
  component paths.
