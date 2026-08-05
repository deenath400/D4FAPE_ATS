# The spec workflow

This directory holds every design artifact for D4FAPE_ATS. Features are built through four
commands, each leaving a document the next one reads. The same workflow runs in Claude Code,
GitHub Copilot, Cursor, and Antigravity — the stage definitions in `.spec-kit/stages/` are
shared, and the per-tool command files just point at them.

## The four stages

```
/specify  →  /plan  →  /implement  →  /validate
```

| Command | What it does | What it writes |
|---|---|---|
| `/specify <request>` | Asks clarifying questions, then writes requirements and acceptance criteria | `NNNN-slug/spec.md`, `clarifications.md` |
| `/plan [id]` | High- and low-level design, API contract, data model, checkpointed tasks | `NNNN-slug/plan/*.md` |
| `/implement [id]` | Builds **one checkpoint** with tests, then stops for review | source, tests, `implementation/changelog.md` |
| `/validate [id]` | Runs the tests, checks architecture and standards, traces every acceptance criterion | `validation/report.md` |

`/implement` stopping after each checkpoint is deliberate. Review the diff, then run it again
for the next checkpoint.

Small changes — a typo, a one-line fix, a rename — do not need a spec. Anything that adds
behaviour, changes an API, or touches the data model does.

## Where things land

```
docs/specs/
  README.md          this file
  index.md           one row per spec — the cheap context tier
  meta/              the blueprint, read by every stage on every run
    project.md         what the product is, who uses it, glossary
    architecture.md    what exists and how it fits together (capped at 150 lines)
    tech-stack.md      frameworks, versions, and the literal build/test commands
    coding-standards.md the rules /implement follows and /validate checks
  0001-some-feature/ one folder per spec, created by /specify
```

Spec ids are sequential four-digit numbers. `meta/` is not a spec and never gets an id.

## How the index and architecture stay honest

Two rules do most of the work:

- **Index sync.** Any stage that changes a spec's frontmatter updates that spec's row in
  `index.md` in the same turn. A stale index quietly degrades every later spec, because
  `index.md` is how agents find related prior work.
- **Surgical architecture edits.** `architecture.md` is never regenerated. `/implement`
  amends the affected lines and appends one Change Log row per checkpoint. Most checkpoints
  change nothing there — it records structure, not activity.

If `architecture.md` ever grows past 150 lines, compress it rather than letting it sprawl.
Its value comes from being short enough that everyone actually reads it.

## Picking up an in-flight spec

Check the `Status` column in `index.md`:

| Status | Next command |
|---|---|
| `specified` | `/plan 0007` |
| `planned` | `/implement 0007` |
| `implementing` | `/implement 0007` — continues at the next unchecked checkpoint |
| `implemented` | `/validate 0007` |
| `validated` | Done |

Each command takes an optional id. Without one it picks the obvious candidate and tells you
which it chose. Progress within a spec lives in `plan/tasks.md`, where completed tasks are
checked off; that file is the resume point.

## Current state

The blueprint was initialised on 2026-08-05 and no feature spec exists yet. Nothing is
scaffolded — there is no application code, no manifest, and no build command. The first
`/specify` run starts spec `0001`.
