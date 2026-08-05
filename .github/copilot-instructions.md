# Copilot Instructions

**Read [`AGENTS.md`](../AGENTS.md) at the repository root.** It carries the full working
agreement for this project and applies to every AI tool used here. This file adds only what is
Copilot-specific.

## Orient before you edit

1. `docs/specs/meta/architecture.md` — what exists and how it fits together
2. `docs/specs/meta/coding-standards.md` — binding on every line you write
3. `docs/specs/meta/tech-stack.md` — frameworks and the literal build/test/lint commands
4. `docs/specs/index.md` — one row per feature spec, with current status

If `docs/specs/` does not exist, run `/initialize-project` first.

## Feature work goes through the spec workflow

Do not implement features ad hoc. Use the slash commands in this order:

```
/specify  →  /plan  →  /implement  →  /validate
```

Each is a prompt file in `.github/prompts/` that delegates to a custom agent in
`.github/agents/`. The stage definitions themselves live in `.spec-kit/stages/` and are shared
with the project's other AI tools — that is where behaviour is defined, not in the prompt
files.

Small fixes — a typo, a one-line bug, a rename — do not need a spec. Anything that adds
behaviour, changes an API, or touches the data model does.

## Non-negotiables

- **Never fabricate command output.** If you did not run it, do not report a result.
- **Read before you write.** Never overwrite a file you have not read.
- **`/implement` does one checkpoint, then stops.** Do not run the whole task list.
- **Only `/implement` writes application source.** The other three stages write only inside
  `docs/specs/`.
- **Keep `docs/specs/index.md` current** whenever a spec's frontmatter changes.
- **Update `architecture.md` by surgical edit**, never regeneration. It is capped at 150 lines.
- **Record deviations** from the LLD rather than letting the design drift silently.

## Note on duplicate agents

VS Code scans both `.github/agents` and `.claude/agents` for custom agents, and this
repository has both (the `.claude` copies exist for Claude Code). If the stage agents appear
twice in your picker, narrow the scan with the `chat.agentFilesLocations` setting.
