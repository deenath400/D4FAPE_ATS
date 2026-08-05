# Spec Kit — Meta Maintenance

Rules for keeping `docs/specs/meta/` accurate and small. Primarily binding on the
`implementation-agent` (the only stage that ships code, and therefore the only stage that
changes reality), and on the `blueprint-agent` (which creates these files).

The architecture snapshot exists so a fresh session can orient in one short read. Its value
is inversely proportional to its length. A 600-line architecture.md is not a better snapshot,
it is a worse one — nobody reads it, and it stops being Tier 0.

---

## 1. Budgets

| File | Target | Hard ceiling | On overflow |
|---|---|---|---|
| `meta/architecture.md` | 150 lines | 200 lines | Compress oldest Change Log rows, then merge fine-grained component rows into their parent |
| `meta/tech-stack.md` | 80 lines | 120 lines | Drop transitive/minor deps; keep only what a developer must know |
| `meta/coding-standards.md` | 120 lines | 180 lines | Merge overlapping rules; delete rules the linter already enforces |
| `meta/project.md` | 100 lines | 150 lines | Trim the glossary to terms that are actually ambiguous |
| `docs/specs/index.md` | 1 row/spec | — | Never exceeds one row per spec; shorten Summary text instead |

Check the line count after editing. If over the ceiling, compress in the same turn — do not
leave it for later.

## 2. Structure of `architecture.md`

Fixed section order. Do not add, remove, or rename top-level sections.

1. `## Purpose` — 2–3 sentences. What the system does and for whom.
2. `## Tech Stack` — compact table: Layer | Technology | Notes. Mirrors `tech-stack.md`
   at one line per layer; the detail lives in that file.
3. `## Component Map` — a mermaid `graph` plus a table:
   `Component | Responsibility | Owning specs`. The component paths here are the
   authoritative list referenced by `conventions.md` §5.
4. `## Data Model` — a compact mermaid `erDiagram` showing entities and relationships only
   (no column lists — those live in each spec's `plan/erd.md`).
5. `## Cross-Cutting Concerns` — auth, authorization, logging, error handling, configuration,
   validation. One short bullet each, naming the mechanism and where it is implemented.
6. `## Integration Points` — external services/APIs: Name | Purpose | Direction | Owning spec.
7. `## Change Log` — table: Date | Spec | Change. Newest last.

## 3. Surgical update rule

When `/implement` finishes a checkpoint:

1. **Read** `architecture.md` in full first. Always.
2. **Diff mentally**: what did this checkpoint actually change about the *shape* of the
   system? Not "what did I write" — "what would a new developer now need to know that was
   not true before".
3. **Edit only the affected lines**, using `Edit`, not `Write`. Typical edits:
   - new component → one new node in the mermaid graph + one new table row
   - new entity → one new node + relationship in the ER diagram
   - new external dependency → one row in Integration Points
   - new cross-cutting mechanism (e.g. first introduction of caching) → one bullet
   - existing component gained a responsibility → amend that row's text
4. **Append exactly one Change Log row per checkpoint**:
   `| 2026-08-05 | 0001 | Added Candidate aggregate, /api/candidates CRUD, pipeline stage table |`
5. **Never regenerate the file.** If you find yourself rewriting more than ~20 lines, you are
   either doing it wrong or the architecture genuinely changed fundamentally — in the latter
   case, say so explicitly in your report to the user.

**Most checkpoints should change 0–5 lines.** A checkpoint that adds a repository method
changes nothing here. Resist the urge to record activity; record structure.

## 4. What does NOT belong in architecture.md

- Individual functions, classes, or file paths → those live in `plan/lld.md`
- Endpoint-level detail → `plan/api.md`
- Column definitions, indexes, constraints → `plan/erd.md`
- Task status or progress → `plan/tasks.md` and `index.md`
- Rationale and trade-offs → `plan/hld.md`
- Known bugs and findings → `validation/report.md`

If information is recoverable from a spec artifact, it does not go in the snapshot. The
snapshot answers "what exists and how does it fit together", nothing else.

## 5. Updating `tech-stack.md`

Update when, and only when, one of these changes:

- a runtime, framework, or major library is added, removed, or majorversion-bumped
- the run / build / test / lint / migrate command changes
- a new environment variable or config key becomes required to run the app
- the database engine or a piece of infrastructure changes

`tech-stack.md` must always contain a **Commands** section with the literal, copy-pasteable
commands, because `/validate` reads them to know what to run:

```markdown
## Commands

| Purpose | Command |
|---|---|
| Install | `npm ci` |
| Build | `npm run build` |
| Test (unit) | `npm test` |
| Lint | `npm run lint` |
| Migrate | `npm run db:migrate` |
| Run (dev) | `npm run dev` |
```

If a command does not exist yet, write `not yet defined` rather than guessing. `/validate`
must be able to distinguish "no lint step exists" from "lint step failed".

## 6. Updating `coding-standards.md`

`/implement` may append a standard only when it made a **project-wide** convention decision
that future work must follow (e.g. "all API errors use the ProblemDetails envelope",
"repositories return `Result<T>`, never throw for expected failures"). One-off local choices
belong in `implementation/changelog.md`.

When appending, cite the spec that established it:
`- API errors use the RFC 7807 ProblemDetails envelope. (est. 0001)`

## 7. Updating `project.md`

Rarely changes after `/initialize-project`. Update when a feature introduces a new persona,
a new domain term whose meaning is not obvious, or a change in project scope. Do not log
features here — that is what `index.md` is for.

## 8. Consistency check

Before finishing any `/implement` checkpoint, verify:

- [ ] Every component path in the current spec's frontmatter appears in the Component Map
- [ ] Every entity in the current spec's frontmatter appears in the ER diagram
- [ ] The Change Log has exactly one new row for this checkpoint
- [ ] `architecture.md` is within budget
- [ ] The spec's `index.md` row reflects its current status

If a check fails, fix it before reporting the checkpoint complete.
