# Spec Kit — Tool Adapters

The workflow is defined once, tool-neutrally, in `.spec-kit/stages/`. Each supported tool gets
thin adapter files that do nothing but declare a slash command and point at the stage
playbook. **Behaviour changes go in `.spec-kit/stages/`, never in an adapter.**

## Supported tools

| Tool | Slash commands | Agent/persona files | Always-on instructions |
|---|---|---|---|
| Claude Code | `.claude/skills/<name>/SKILL.md` | `.claude/agents/*.md` | `AGENTS.md` |
| GitHub Copilot (VS Code / Visual Studio) | `.github/prompts/*.prompt.md` | `.github/agents/*.agent.md` | `.github/copilot-instructions.md`, `AGENTS.md` |
| Cursor | `.cursor/commands/*.md` | — | `.cursor/rules/spec-workflow.mdc`, `AGENTS.md` |
| Google Antigravity | `.agents/workflows/*.md` | — | `.agents/rules/spec-workflow.md`, `AGENTS.md` |

All five commands — `/initialize-project`, `/specify`, `/plan`, `/implement`, `/validate` —
exist in every tool and behave identically.

## Format reference

What each tool expects, so adapters stay valid.

### Claude Code

- **Skills:** `.claude/skills/<name>/SKILL.md`, frontmatter `name`, `description`.
  Invoked `/<name>`.
- **Agents:** `.claude/agents/<name>.md`, frontmatter `name`, `description`, `tools`, `model`.
  Spawned as subagents with isolated context; continued via `SendMessage`.

### GitHub Copilot

- **Prompt files:** `.github/prompts/<name>.prompt.md`. Frontmatter: `description`, `name`,
  `argument-hint`, `agent` (`ask` | `agent` | `plan` | a custom agent name), `model`, `tools`.
  Invoked `/<name>` in Copilot Chat; arguments pass as `/<name> some text`.
- **Custom agents:** `.github/agents/<name>.agent.md`. Frontmatter: `description`, `name`,
  `tools`, `agents`, `model`, `argument-hint`, `handoffs`. Invoked `@<name>`; can be delegated
  to as a subagent with its own context window. These replaced `.chatmode.md` chat modes.
- **Instructions:** `.github/copilot-instructions.md` (repo-wide),
  `.github/instructions/*.instructions.md` (path-scoped via `applyTo` glob).
- Note: VS Code also scans `.claude/agents` for custom agents. If stage agents appear twice in
  your picker, restrict the scan via the `chat.agentFilesLocations` setting.

### Cursor

- **Commands:** `.cursor/commands/<name>.md`, **plain Markdown, no frontmatter**.
  Invoked `/<name>`.
- **Rules:** `.cursor/rules/<name>.mdc`, frontmatter `description`, `globs`, `alwaysApply`.

### Google Antigravity

- **Workflows:** `.agents/workflows/<name>.md`, frontmatter `description`.
  Invoked `/<name>`. Workflows may call other workflows.
- **Rules:** `.agents/rules/<name>.md`, 12,000-character limit, activation Always On /
  Model Decision / Glob / Manual.
- Older Antigravity builds use `.agent/` instead of `.agents/`. If the commands do not appear,
  copy the folder: `cp -r .agents .agent`.

## Capability differences that matter

**Subagents.** Claude Code and Copilot can run a stage in a dedicated agent with its own
context window. Cursor and Antigravity run the stage inline in the main thread. The workflow
does not depend on isolation — it depends on the artifacts on disk — so both work. Isolation
just keeps the main thread's context cleaner on long features.

**Asking the user.** Stages 0 and 1 have a question phase. Claude Code surfaces it through a
structured picker and resumes the same agent afterwards; the other tools ask in chat and wait
for a reply. Same conversation, different chrome.

**Tool restrictions.** Claude Code agents declare an explicit `tools` list, so Stage 4's
inability to edit source is mechanically enforced. The other adapters state the constraint in
prose instead, because tool-name vocabularies drift between versions and a wrong name silently
breaks the file. If you want hard enforcement in Copilot, add a `tools:` list to the
`.agent.md` files using your VS Code build's current tool names.

## Adding another tool

1. Find where it stores slash commands / reusable prompts and its always-on instructions.
2. Create five thin command files. Each should contain only: what the command is, its
   argument, an instruction to read `.spec-kit/stages/<stage>.md` and follow it exactly, and
   any tool-specific mechanics (how to ask the user, how to delegate).
3. Point its always-on instructions at `AGENTS.md`.
4. Add a row to the table above and to the root `AGENTS.md`.

Do not copy stage content into the new adapter. A duplicated playbook drifts within weeks, and
then the tools disagree about how the project works.
