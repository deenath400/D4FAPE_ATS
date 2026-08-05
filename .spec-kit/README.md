# Spec-Driven Development Toolkit

A four-stage workflow for building features, where each stage is a dedicated agent and every
stage leaves a durable artifact the next stage reads. It runs identically in **Claude Code,
GitHub Copilot, Cursor, and Google Antigravity**.

```
/initialize-project  →  /specify  →  /plan  →  /implement  →  /validate
     (once)              stage 1     stage 2    stage 3        stage 4
```

| Command | Produces |
|---|---|
| `/initialize-project` | `docs/specs/meta/*`, `index.md`, root `AGENTS.md` section |
| `/specify <request>` | `spec.md`, `clarifications.md` |
| `/plan [id]` | `plan/hld.md`, `lld.md`, `api.md`, `erd.md`, `tasks.md` |
| `/implement [id]` | Source code, tests, `implementation/changelog.md` |
| `/validate [id]` | `validation/report.md` |

Run `/initialize-project` once. After that, each feature cycles through stages 1–4. Ids are
optional — each stage defaults to the newest spec at the right status.

## Layout

```
.spec-kit/                  SINGLE SOURCE OF TRUTH — tool-neutral
  stages/                   what each stage does. Edit behaviour HERE.
  conventions.md            layout, ids, frontmatter, status lifecycle
  context-loading.md        how agents choose which prior specs to read
  meta-maintenance.md       how the architecture snapshot stays accurate and short
  templates/                the shape of every artifact
  adapters.md               per-tool formats, and how to add another tool

AGENTS.md                   universal entry point, read by all four tools

.claude/skills/  .claude/agents/          Claude Code adapters
.github/prompts/ .github/agents/          Copilot adapters (+ copilot-instructions.md)
.cursor/commands/ .cursor/rules/          Cursor adapters
.agents/workflows/ .agents/rules/         Antigravity adapters

docs/specs/                 created at runtime by /initialize-project
  index.md                  one row per spec — read in full by every agent, every time
  meta/                     the blueprint: project, architecture, tech-stack, standards
  NNNN-slug/                one folder per feature
```

**Adapters are thin pointers.** Every one of them says "read `.spec-kit/stages/<stage>.md` and
follow it exactly", then adds only tool-specific mechanics. Change behaviour in
`.spec-kit/stages/`; never in an adapter. A duplicated playbook drifts within weeks, and then
the tools disagree about how the project works.

## The two ideas that make this work

**Tiered context loading.** Feature #7 must inherit the conventions features #1–6 established
without re-reading all of them. Agents always read four small files (Tier 0: the blueprint plus
the one-row-per-spec index), then use the index's `components` and `entities` columns to select
**at most three** genuinely overlapping prior specs, reading only their acceptance criteria,
API design, and data model. Every artifact records what it loaded and why, so the choice is
auditable. See `context-loading.md`.

**A living architecture snapshot.** `docs/specs/meta/architecture.md` is capped at 150 lines
and updated by surgical edit — never regenerated — as each implementation checkpoint ships.
Most checkpoints change nothing there, because it records *structure*, not activity. It stays
short enough that every agent can afford to read it every time, which is the only reason it is
useful. See `meta-maintenance.md`.

## Design notes

**Why `/implement` stops.** Task lists are grouped into checkpoints, each ending in a state
where the project builds and its tests pass. One invocation runs one checkpoint. This keeps the
reviewable surface small and gives you a natural place to course-correct.

**Why `/validate` cannot edit code.** An agent that fixes what it finds ends up validating its
own work. It reports with `file:line` and a verdict; fixing is a separate, deliberate step.

**Subagents vs inline.** Claude Code and Copilot can run a stage in a dedicated agent with its
own context window; Cursor and Antigravity run it inline. The workflow depends on the artifacts
on disk, not on isolation, so both work — isolation just keeps the main thread cleaner.

**Asking the user.** Stages 0 and 1 have a question phase. Claude Code surfaces it through a
structured picker and resumes the same subagent afterwards; the other tools ask in chat. Same
conversation, different chrome.

## Customising

- **Stage behaviour** — edit `.spec-kit/stages/`. All four tools pick it up immediately.
- **Artifact shape** — edit `.spec-kit/templates/`. Agents read them at run time.
- **House rules** — `.spec-kit/conventions.md` is authoritative over the agents' own
  instructions, so project-wide rules go there.
- **Checkpoint granularity** — re-plan `tasks.md`; checkpoint groups are just headings.
- **Another tool** — see `adapters.md`. Five thin command files and one instructions pointer.
- **Portability** — copy `.spec-kit/`, `AGENTS.md`, and the adapter folders for the tools you
  use into any repository, then run `/initialize-project`. Nothing here is specific to this
  project.
