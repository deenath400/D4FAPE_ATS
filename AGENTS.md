# Agent Instructions

Read this before making changes. It applies to every AI coding tool used in this repository —
Claude Code, GitHub Copilot, Cursor, and Google Antigravity.

## Orient first

1. **`docs/specs/meta/architecture.md`** — what exists and how it fits together. Read it
   before touching code, every session.
2. **`docs/specs/meta/coding-standards.md`** — binding on every line you write.
3. **`docs/specs/meta/tech-stack.md`** — frameworks, and the literal build/test/lint commands.
4. **`docs/specs/index.md`** — one row per feature spec, with current status.

If `docs/specs/` does not exist yet, the project has not been initialised. Run
`/initialize-project` before anything else.

## Feature work goes through the spec workflow

Do not implement features ad hoc. Four stages, each leaving a durable artifact the next stage
reads:

```
/specify  →  /plan  →  /implement  →  /validate
```

| Command | Does | Produces |
|---|---|---|
| `/specify <request>` | Clarifies the request, writes requirements and acceptance criteria | `spec.md`, `clarifications.md` |
| `/plan [id]` | HLD, LLD, API contract, ER model, checkpointed task list | `plan/*.md` |
| `/implement [id]` | Builds **one checkpoint**, with tests, then stops for review | source, tests, `implementation/changelog.md` |
| `/validate [id]` | Runs tests, checks architecture and standards, traces every AC | `validation/report.md` |

The stage definitions live in **`.spec-kit/stages/`** and are the single source of truth. The
per-tool command files are thin pointers to them.

Small fixes — a typo, a one-line bug, a rename — do not need a spec. Anything that adds
behaviour, changes an API, or touches the data model does.

## Rules that hold regardless of stage

- **`.spec-kit/conventions.md` is authoritative.** Ids, frontmatter, status lifecycle,
  component paths, the index-sync rule. When it conflicts with your own instincts, it wins.
- **Read before you write.** Never overwrite a file you have not read.
- **Keep the index current.** Any change to a spec's frontmatter updates its row in
  `docs/specs/index.md` in the same turn. A stale index degrades every future spec's context.
- **Keep the architecture snapshot short and true.** `architecture.md` is capped at 150 lines
  and updated by surgical edit, never regenerated. See `.spec-kit/meta-maintenance.md`.
- **Never fabricate command output.** If you did not run it, do not report a result.
- **Record deviations.** If the implementation diverges from the LLD, patch the LLD and log
  it. Silent drift makes every artifact untrustworthy.
- **Only `/implement` writes application source.** The other three stages write only inside
  `docs/specs/`.

## Reading prior specs efficiently

Before planning or implementing, load context in tiers — full protocol in
`.spec-kit/context-loading.md`:

- **Tier 0, always:** the four orientation files above. They are budget-capped so this is
  always affordable.
- **Tier 1, at most 3 specs:** use the `components` and `entities` columns in `index.md` to
  find genuine overlap, then read only those specs' acceptance criteria, `plan/api.md`, and
  `plan/erd.md` — that is where reusable conventions live.
- **Tier 2, on demand:** a prior LLD or changelog, when you are modifying its code.

Record what you loaded in the `## Related Specs` section of whatever you write. Do not read
every prior spec; do not read none.

## Tool setup

| Tool | Commands defined in |
|---|---|
| Claude Code | `.claude/skills/` + `.claude/agents/` |
| GitHub Copilot | `.github/prompts/` + `.github/agents/` |
| Cursor | `.cursor/commands/` + `.cursor/rules/` |
| Antigravity | `.agents/workflows/` + `.agents/rules/` |

See `.spec-kit/adapters.md` for formats and for how to add another tool.
