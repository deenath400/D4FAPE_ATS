# Spec-Driven Development Toolkit

A four-stage workflow for building features, where each stage is a dedicated agent and every
stage leaves a durable artifact the next stage reads.

```
/initialize-project  →  /specify  →  /plan  →  /implement  →  /validate
     (once)              stage 1     stage 2    stage 3        stage 4
```

## Commands

| Command | Agent | Produces |
|---|---|---|
| `/initialize-project` | `blueprint-agent` | `docs/specs/meta/*`, `index.md`, root `CLAUDE.md` |
| `/specify <request>` | `spec-agent` | `spec.md`, `clarifications.md` |
| `/plan [id]` | `planning-agent` | `plan/hld.md`, `lld.md`, `api.md`, `erd.md`, `tasks.md` |
| `/implement [id]` | `implementation-agent` | Source code, tests, `implementation/changelog.md` |
| `/validate [id]` | `validation-agent` | `validation/report.md` |

Run `/initialize-project` once, before anything else. After that, each feature cycles through
stages 1–4. Ids are optional — each stage defaults to the newest spec at the right status.

## Layout

```
.claude/
  skills/       entry points — own the user conversation, dispatch to agents
  agents/       the five stage agents — do the work
  spec-kit/     the shared contract every agent obeys
    conventions.md        layout, ids, frontmatter, status lifecycle
    context-loading.md    how agents choose which prior specs to read
    meta-maintenance.md   how the architecture snapshot stays accurate and short
    templates/            the shape of every artifact

docs/specs/     created at runtime by /initialize-project
  index.md      one row per spec — read in full by every agent, every time
  meta/         the blueprint: project, architecture, tech-stack, coding-standards
  NNNN-slug/    one folder per feature
```

## The two ideas that make this work

**Tiered context loading.** Feature #7 must inherit the conventions features #1–6 established
without re-reading all of them. Agents always read four small files (Tier 0: the blueprint
plus the one-row-per-spec index), then use the index's `components` and `entities` columns to
select **at most three** genuinely overlapping prior specs, reading only their acceptance
criteria, API design, and data model. Every artifact records what it loaded and why, so the
choice is auditable. See `spec-kit/context-loading.md`.

**A living architecture snapshot.** `docs/specs/meta/architecture.md` is capped at 150 lines
and updated by surgical edit — never regenerated — as each implementation checkpoint ships.
Most checkpoints change nothing there, because it records *structure*, not activity. It stays
short enough that every agent can afford to read it every time, which is the only reason it
is useful. See `spec-kit/meta-maintenance.md`.

## Design notes

**Why skills and agents are separate.** Subagents cannot prompt the user. The skill runs in
the main thread and owns the conversation; the agent does the analysis and writing. For
`/specify` and `/initialize-project` this is a two-phase handshake: the agent returns ranked
ambiguities, the skill puts them to the user via `AskUserQuestion`, and the answers go back to
the *same agent instance* via `SendMessage` so its loaded context is not thrown away.

**Why `/implement` stops.** Task lists are grouped into checkpoints, each ending in a state
where the project builds and its tests pass. One invocation runs one checkpoint. This keeps
the reviewable surface small and gives you a natural place to course-correct.

**Why `/validate` cannot edit code.** An agent that fixes what it finds ends up validating its
own work. It reports with `file:line` and a verdict; fixing is a separate, deliberate step.

## Customising

- **Different checkpoint granularity** — re-plan `tasks.md`; the checkpoint groups are just
  headings.
- **Different artifact shape** — edit `spec-kit/templates/`. Agents read them at run time.
- **Different rules** — `spec-kit/conventions.md` is authoritative over the agents' own
  instructions, so house rules go there.
- **Portability** — copy the whole `.claude/` folder into another repository and run
  `/initialize-project`. Nothing here is specific to this project.
