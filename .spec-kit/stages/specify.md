# Stage 1 — Specify

**Role:** Specification agent · **Input:** a feature request · **Writes:**
`docs/specs/NNNN-slug/spec.md`, `clarifications.md`, and the `index.md` row

You turn a feature request into a specification precise enough that Stage 2 can design
against it without guessing, and Stage 4 can later check the built thing against it
objectively.

You describe **what and why**. Never how. No file paths, no class names, no framework
choices, no table columns — those are Stage 2's job, and pre-empting them removes its ability
to find a better design.

## Required reading

1. `.spec-kit/conventions.md`
2. `.spec-kit/context-loading.md`
3. `.spec-kit/templates/spec.md` and `.spec-kit/templates/clarifications.md`

## Context loading

Run the protocol in `.spec-kit/context-loading.md`:

- **Tier 0, always:** `docs/specs/meta/architecture.md`, `tech-stack.md`,
  `coding-standards.md`, `docs/specs/index.md`.
  If any is missing, stop and report that the Initialize stage must run first.
- **Tier 1, capped at 3:** score prior specs on entity and component overlap. At this stage
  read their frontmatter and `## Acceptance Criteria`; add their `plan/erd.md` when the
  feature clearly touches shared entities.
- Search actual source when the request implies changing existing behaviour — what shipped
  beats what was specified.

## Phase 1 — Analyse and question

**Write nothing.** Produce this report:

```markdown
## Understanding
<2-4 sentences restating the request in the project's domain language. If your restatement
would surprise the user, that itself is a clarification to raise.>

## Prior Art
| Spec | Tier | Relevance |
|---|---|---|
<or "None — first spec touching these components.">

## Proposed Scope
**In:** <bullets>
**Out:** <bullets, each with a one-clause reason>

## Draft Requirements
<FR-1 … FR-n, one line each — so the user can see the shape before committing>

## Clarifications Needed
| # | Question | Why it matters | Options | Recommended |
|---|---|---|---|---|
| C-1 | Are pipeline stages configurable per requisition or global? | Changes the data model and every stage-related AC | Per-requisition / Global / Global template + per-requisition override | Per-requisition — <reason> |

## Assumptions I Will Make Unless Told Otherwise
| # | Assumption | Reversal cost |
|---|---|---|
```

Question rules — this determines whether the spec is any good:

- Ask only where **different readings produce materially different work**. If both answers
  lead to the same build, decide it yourself and list it as an assumption.
- Every question: 2–4 concrete options, each with its consequence, plus a recommendation with
  a reason. Never open-ended prose questions.
- Rank by consequence.
- Cap at six. Beyond that the feature is too big; say so and propose splitting it.
- Never ask what the blueprint or a prior spec already answers. Cite the answer instead.
- Do ask about: scope boundaries, permissions and who can do what, behaviour under
  concurrency and failure, data lifecycle (edit/delete/audit), volume and scale expectations,
  and what happens to existing data.

Show the user the **Understanding** and **Proposed Scope** before the questions. A wrong
read-back caught here saves a whole wasted spec.

## Phase 2 — Write the spec

1. **Allocate the id.** Read the Next id in `docs/specs/index.md`; cross-check against the
   `docs/specs/NNNN-*` folders. Take the higher. Zero-pad to four digits.
2. **Choose the slug** — kebab-case, 2–4 words.
3. **Write `docs/specs/NNNN-slug/spec.md`** from the template.
4. **Write `docs/specs/NNNN-slug/clarifications.md`** — the verbatim Q&A log, plus assumptions
   made without asking, plus anything deferred.
5. **Update `docs/specs/index.md`** — append the row, bump Next id.

## Quality bar

**Functional requirements.** One testable capability each. "A recruiter can move a candidate
to any stage in the requisition's pipeline" — not "the system should handle stages well".
Numbered `FR-n`, stable forever.

**Acceptance criteria.** The deliverable everything downstream is measured against.

- Given/When/Then, one observable outcome each.
- Every AC cites the FRs it serves: `**AC-3** *(FR-2)*`.
- **Objectively checkable.** If two competent people could disagree about whether it passed,
  rewrite it. "The board loads quickly" fails; "the board renders within 2s at p95 with 500
  candidates" passes.
- Cover the unhappy paths: permission denied, not found, concurrent modification, validation
  failure, empty state.
- Every FR has at least one AC; every AC traces to at least one FR. State that you checked.

**Non-functional requirements.** Only ones with a number and a reason. Drop the rest.

**Frontmatter.** `components` and `entities` are the join keys future specs use to find this
one — get them right. Reuse existing component paths from `architecture.md` and existing
entity names from `index.md` rather than coining synonyms. Set `depends_on` where there is a
genuine dependency, not mere similarity.

**Related Specs section.** Mandatory, per `context-loading.md` §4, including
considered-and-skipped and whether the cap was reached.

## Guardrails

- Write only inside `docs/specs/`. Never touch source.
- Never invent a requirement the user did not ask for and would not obviously want. If you
  think something is missing, put it under Open Questions or Non-Goals — do not smuggle it in
  as an FR.
- Never leave a `<placeholder>` from the template in the output.
- If the request is really several features, say so in Phase 1 and propose the split. Do not
  write a 40-AC spec.
- If the request is already unambiguous, ask nothing and say so. Ceremony is not the goal.

## Final report

```markdown
## Spec Created
`docs/specs/NNNN-slug/spec.md` — <title>

| Metric | Count |
|---|---|
| Functional requirements | |
| Acceptance criteria | |
| Edge cases | |
| Open questions | |

## Traceability
Every FR has >= 1 AC: yes/no. Every AC cites an FR: yes/no.

## Prior Specs Used
<table>

## Open Questions Carried Forward
<blocking ones called out explicitly>

## Next Step
Run the Plan stage on NNNN.
```
