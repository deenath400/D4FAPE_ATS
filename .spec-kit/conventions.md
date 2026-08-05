# Spec Kit — Conventions

The shared contract for the four-stage spec-driven workflow. Every stage agent
(`spec-agent`, `planning-agent`, `implementation-agent`, `validation-agent`) and the
`blueprint-agent` must follow this file. When any rule here conflicts with an agent's own
instructions, **this file wins** — except for explicit user instructions, which always win.

---

## 1. Directory layout

All workflow artifacts live under `docs/specs/` at the repository root.

```
docs/specs/
  README.md                     # human-facing explanation of the workflow
  index.md                      # ONE table row per spec — the cheap context tier
  meta/
    project.md                  # purpose, scope, personas, domain glossary
    architecture.md             # the living architecture snapshot (budgeted, see §6)
    tech-stack.md               # languages, frameworks, deps, run/test/build commands
    coding-standards.md         # naming, layering, error handling, logging, test rules
  NNNN-slug/                    # one folder per spec
    spec.md                     # /specify
    clarifications.md           # /specify — Q&A log
    plan/
      hld.md                    # /plan — high-level design
      lld.md                    # /plan — low-level design
      api.md                    # /plan — API contract
      erd.md                    # /plan — data model + delta
      tasks.md                  # /plan — checkpointed task breakdown
    implementation/
      changelog.md              # /implement
    validation/
      report.md                 # /validate
```

`meta/` is **not** a spec. Never allocate an id to it, never give it an index row.

## 2. Spec identity

- **Id**: zero-padded four digits, allocated sequentially: `0001`, `0002`, …
  To allocate the next id, list the `docs/specs/NNNN-*` directories, take the highest id,
  add one. Never reuse an id, even for an abandoned spec.
- **Slug**: lowercase kebab-case, 2–4 words, derived from the feature title.
  `candidate-pipeline`, `bulk-candidate-email`, `interview-scheduling`.
- **Folder name**: `NNNN-slug` — e.g. `docs/specs/0001-candidate-pipeline/`.
- Referring to a spec anywhere in prose or frontmatter, use the bare id: `0001`.

## 3. Frontmatter — the source of truth

Every `spec.md` opens with exactly this frontmatter block:

```yaml
---
id: 0001
slug: candidate-pipeline
title: Candidate Pipeline Management
status: specified
components: [api/candidates, ui/pipeline, db/candidates]
entities: [Candidate, Application, Stage]
depends_on: [0000]
created: 2026-08-05
updated: 2026-08-05
---
```

| Field | Rule |
|---|---|
| `id` | Four-digit string, quoted-free, matches the folder |
| `slug` | Matches the folder |
| `title` | Human sentence, ≤ 60 chars, no trailing period |
| `status` | One of the five values in §4 |
| `components` | Logical component paths this spec touches — see §5. Empty list allowed at `specified` only if genuinely unknown; `/plan` must fill it in |
| `entities` | Domain entity names in PascalCase. These are the join keys for cross-spec discovery |
| `depends_on` | List of spec ids this spec builds on. `[]` if none |
| `created` | `YYYY-MM-DD`, set once by `/specify`, never changed |
| `updated` | `YYYY-MM-DD`, refreshed by every stage that writes |

`components` and `entities` are how `context-loading.md` finds relevant prior work.
Populating them accurately is not bookkeeping — it is the mechanism. Be specific and
consistent: reuse an existing component path or entity name from `index.md` rather than
inventing a near-synonym.

## 4. Status lifecycle

```
specified  →  planned  →  implementing  →  implemented  →  validated
```

| Status | Set by | Means |
|---|---|---|
| `specified` | `/specify` | spec.md + clarifications.md written; no design yet |
| `planned` | `/plan` | all five plan/ artifacts written; ready to build |
| `implementing` | `/implement` | at least one checkpoint done, more remain |
| `implemented` | `/implement` | every task in tasks.md is checked |
| `validated` | `/validate` | report verdict is PASS or PASS-WITH-FINDINGS |

Rules:
- Never skip forward. `/plan` refuses a spec that is not `specified` (or `planned`, when
  explicitly re-planning). `/implement` refuses anything below `planned`.
- Status may move **backward** when work is revised — e.g. re-running `/plan` on an
  `implemented` spec returns it to `planned`. Say so loudly in the report when this happens.
- `/validate` on a FAIL verdict leaves the status at `implemented`. It never regresses status.

## 5. Component paths

A component path is a stable, coarse logical address — not a filesystem path.
Two segments, lowercase, slash-separated: `<layer>/<area>`.

Layers: `api`, `ui`, `db`, `service`, `worker`, `infra`, `shared`.

Examples: `api/candidates`, `ui/pipeline`, `db/candidates`, `service/notifications`,
`worker/email`, `shared/auth`.

The authoritative list of component paths in use is the Component Map section of
`docs/specs/meta/architecture.md`. Before inventing a new path, check there and in
`index.md`. Adding a genuinely new component is fine — it just must also appear in the
architecture snapshot once `/implement` ships it.

## 6. The index

`docs/specs/index.md` holds one row per spec:

```markdown
| Id | Title | Status | Components | Entities | Summary |
|---|---|---|---|---|---|
| 0001 | Candidate Pipeline Management | validated | api/candidates, ui/pipeline, db/candidates | Candidate, Application, Stage | Recruiters create requisitions and move candidates through configurable pipeline stages. |
```

**The index-sync rule: any stage that writes spec frontmatter updates that spec's index row
in the same turn.** A stale index silently degrades every future spec's context loading, so
this is not optional.

Keep `Summary` to a single sentence under ~140 characters. The index must stay cheap enough
to read in full, forever — that is its entire purpose.

## 7. Requirement and task identifiers

| Prefix | Lives in | Example |
|---|---|---|
| `FR-n` | spec.md — functional requirement | `FR-3` |
| `NFR-n` | spec.md — non-functional requirement | `NFR-1` |
| `AC-n` | spec.md — acceptance criterion, Given/When/Then | `AC-7` |
| `T-nn` | plan/tasks.md — implementation task | `T-04` |
| `CP-n` | plan/tasks.md — checkpoint group | `CP-2` |
| `F-n` | validation/report.md — finding | `F-2` |

Ids are stable once assigned. If a requirement is dropped, keep the id and mark it
`~~AC-4~~ (dropped: reason)` rather than renumbering — downstream artifacts cite these ids.

Every `AC-n` must be traceable: at least one `T-nn` should reference it, and `/validate`
builds a matrix over all of them.

## 8. Writing rules for all artifacts

- **Markdown, ATX headings** (`##`), no HTML.
- **Mermaid for all diagrams**, in fenced ```mermaid blocks. No ASCII art. Verify the syntax
  is valid mermaid before writing — a broken diagram is worse than no diagram.
- **Tables over prose** for anything enumerable (endpoints, columns, tasks, findings).
- **No invented facts.** If a decision has not been made, write it under Open Questions;
  do not fabricate a plausible answer and present it as settled.
- **State uncertainty inline** with `> **Assumption:** …` blockquotes, so the reader can
  audit what was guessed.
- **Every artifact ends with a `## Related Specs` section** — see `context-loading.md` §4.
  If nothing prior was relevant, write `None — this is the first spec touching these
  components.` Do not omit the section.
- **Absolute dates.** `2026-08-05`, never "today" or "last week".
- **No emoji** in artifacts.

## 9. Never do

- Never write outside `docs/specs/` from `/specify`, `/plan`, or `/validate`.
  Only `/implement` touches application source.
- Never overwrite an existing artifact without reading it first.
- Never renumber existing ids.
- Never delete a spec folder. To retire a spec, set its summary to
  `RETIRED: <reason>` and leave the artifacts in place.
- Never regenerate `meta/architecture.md` wholesale — see `meta-maintenance.md`.
- Never claim tests pass without having run them and captured real output.
