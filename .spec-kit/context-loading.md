# Spec Kit — Context Loading Protocol

How a stage agent decides *which prior work to read*. Every stage agent runs this protocol
before producing anything.

The goal is not "read everything relevant" — it is **read the least that keeps this spec
consistent with the ones before it.** A spec that silently reinvents an existing entity name,
URL convention, or error shape has failed, and so has one that burned its whole context
window reading seven prior LLDs.

---

## Tier 0 — always read, in full

| File | Why |
|---|---|
| `docs/specs/meta/architecture.md` | Current component map, data model, cross-cutting decisions |
| `docs/specs/meta/tech-stack.md` | Frameworks, deps, and the run/test/build commands |
| `docs/specs/meta/coding-standards.md` | Naming, layering, error handling, test rules |
| `docs/specs/index.md` | One row per spec — the discovery surface for Tier 1 |

These four are deliberately budget-capped (see `meta-maintenance.md`) so reading all of them
always costs less than reading one full prior spec. Read them first, every time, without
exception.

If any is missing, stop and tell the user to run `/initialize-project`. Do not proceed on
guesses about the architecture.

## Tier 1 — selective, capped at 3 specs

**Selection.** Scan the `index.md` rows. Score each prior spec:

- **+2** for each `entity` it shares with the current feature
- **+2** for each `component` it shares with the current feature
- **+3** if the current spec's `depends_on` names it
- **+1** if its Summary describes an adjacent user-facing flow
- **−3** if its status is below `implemented` (its conventions are not yet proven in code)

Sort descending, drop anything scoring ≤ 1, **take at most 3**.

**What to read from each selected spec** — not the whole thing:

| Read | Skip | Reason |
|---|---|---|
| `spec.md` frontmatter + `## Acceptance Criteria` | the prose sections | ACs carry the behavioural contract compactly |
| `plan/api.md` | — | URL shapes, error envelope, pagination, auth headers |
| `plan/erd.md` | — | entity names, key/column conventions, relationships |

`api.md` and `erd.md` are mandatory reading at Tier 1 because they are where conventions live.
Everything else is prose you can infer from.

**Stage-specific narrowing:**

- `/specify` — usually needs only the frontmatter and ACs. Read `erd.md` too if the feature
  clearly touches shared entities. Skip `api.md` unless the feature is API-shaped.
- `/plan` — reads all three files of every selected spec. This is the stage where convention
  propagation actually matters; do not economise here.
- `/implement` — reads Tier 1 only for specs whose code it will modify; otherwise the LLD of
  the current spec is sufficient.
- `/validate` — reads Tier 1 to check the implementation did not contradict a prior spec's
  established conventions.

## Tier 2 — on demand only

Pull these only when a concrete question demands them, and name the question in your notes:

- A selected spec's `plan/lld.md` — when you are extending or refactoring its code.
- A selected spec's `implementation/changelog.md` — when you need to know what *actually*
  shipped versus what was designed (deviations are recorded there).
- A selected spec's `validation/report.md` — when you need its known outstanding findings.
- Actual source files — always allowed and often better than reading a spec about them.
  Prefer `Grep` over reading whole files.

Tier 2 has no hard cap, but every Tier 2 read should be justifiable in one sentence.

## 4. Recording what you loaded

Every artifact you write ends with:

```markdown
## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| 0001 Candidate Pipeline Management | 1 | Shares `Candidate` entity and `api/candidates`; reused its error envelope and pagination convention |
| 0003 Interview Scheduling | 2 | Read its LLD — this feature modifies its `SchedulerService` |

Considered and skipped: 0002 (no entity or component overlap).
Cap reached: no. 
```

If the 3-spec cap forced you to skip something that scored above the threshold, say so
explicitly (`Cap reached: yes — 0004 scored 4 and was omitted`). That is a signal to the
human that the feature may be too broad, and it makes the omission auditable rather than
invisible.

## 5. When there are no prior specs

First spec in the project: Tier 0 still applies (the blueprint exists even with zero specs).
Write `## Related Specs` as:

```markdown
## Related Specs

None — this is the first spec touching these components.
```

## 6. Budget discipline

Rough guidance, not a hard rule: if you have read more than ~1,500 lines of prior spec
artifacts before writing a single line of output, you are over-reading. Stop, write what you
have, and note the uncertainty under Open Questions. A spec that ships with two honest open
questions is more useful than one that consumed its context window achieving false certainty.
