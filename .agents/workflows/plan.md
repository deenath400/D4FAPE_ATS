---
description: Stage 2 — turn an approved spec into an HLD, LLD, API contract, ER model, and a checkpointed task breakdown under docs/specs/NNNN-slug/plan/.
---

# Plan

**Read `@.spec-kit/stages/plan.md` and follow it exactly.** It is the authoritative definition
of this stage, shared with the project's other AI tools. The steps below are the sequence;
that file is the detail.

**Target spec:** the id the user typed after the command. With no argument, take the newest
spec with `status: specified`. If more than one qualifies, list them and ask which.

You turn a specification into a design precise enough that `/implement` writes code without
re-deciding anything. Every ambiguity you leave becomes an improvised decision made later with
less context than you have now.

## Steps

1. **Check preconditions.** Blueprint present? Spec found? Status `specified`? If the status
   is `planned` or beyond this is a **re-plan** — confirm with the user, since it regresses
   status and may invalidate completed work. If the spec has **blocking** open questions,
   stop and ask the user to resolve them.

2. **Load context.** The spec and its `clarifications.md` in full — the log tells you *why*
   the spec says what it says. Then Tier 0, then Tier 1: at most three prior specs, reading
   their acceptance criteria, `plan/api.md`, and `plan/erd.md` **in full**. This is the step
   where project conventions propagate; do not economise here. For brownfield changes, read
   the real source too.

3. **Write `plan/hld.md`** — solution overview, mermaid context diagram, component table,
   a sequence diagram per key flow including **at least one failure flow**, design decisions
   with alternatives and rationale, NFR approach, security, risks.

4. **Write `plan/lld.md`** — file manifest with exact paths marked Create or Modify, domain
   types with real signatures, service methods with numbered behaviour and outcome tables,
   frontend components and hooks with all four async states, DTOs, validation rules, error
   handling, config keys, ordered reversible migrations, and a Test Plan where every `AC-n`
   appears at least once.

5. **Write `plan/api.md`** — restate the inherited conventions with the spec that established
   each, then endpoint detail: parameters, request schema, **every** response status, worked
   examples, side effects, idempotency, authorization matrix, events published.

6. **Write `plan/erd.md`** — mermaid `erDiagram`, then the **Delta Summary** (new / altered /
   referenced-unchanged), table definitions for new and altered tables only, indexes with
   rationale, migrations with rollback plan, PII classification. Never redefine an existing
   entity — reference it and describe the delta.

7. **Write `plan/tasks.md`** — ordered `T-nn` tasks grouped into `CP-n` checkpoints. Every
   checkpoint must end in a state where the project builds and its tests pass; state the exit
   condition. Include a Coverage Check table mapping every AC to its tasks.

8. **Update frontmatter and index** — `status: planned`, fill in `components` and `entities`
   precisely, refresh the index row. Add a `## Related Specs` section to all five files.

9. **Report:** the five files with line counts; N tasks across M checkpoints; whether all ACs
   are covered; conventions inherited and from where; **decisions that deserve pushback**; top
   risks; next step: `/implement`, which runs CP-1 only.

## Constraints

- Write only inside `docs/specs/`. You design; you do not implement.
- Never contradict the Layering Rules in `architecture.md`. If the feature genuinely requires
  an exception, make it an explicit Design Decision and flag it — never quietly.
- Never introduce a technology absent from `tech-stack.md` without raising it for approval.
- An uncovered acceptance criterion is a defect to fix before finishing, not to report.
- Prefer the project's existing patterns to better patterns.
