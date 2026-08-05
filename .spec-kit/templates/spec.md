---
id: NNNN
slug: <kebab-case-slug>
title: <Title Case, <= 60 chars>
status: specified
components: [<layer/area>, ...]
entities: [<PascalCase>, ...]
depends_on: []
created: YYYY-MM-DD
updated: YYYY-MM-DD
---

# <Title>

## Problem & Context

<2-4 paragraphs. What is broken or missing today, who feels it, and why it matters now.
Ground this in the project's actual domain — reference meta/project.md personas by name.
No solution language here.>

## Goals

- G-1 <Outcome, not implementation. Measurable where possible.>
- G-2 ...

## Non-Goals

- <Explicitly excluded, with a one-clause reason. This section prevents scope creep during
  /plan and /implement, so be generous with it.>

## Users & Personas

| Persona | Need this feature serves |
|---|---|
| <from meta/project.md> | <one line> |

## Functional Requirements

- **FR-1** — <Single testable capability. "The system shall ..." or "A <persona> can ...">
- **FR-2** — ...

## Non-Functional Requirements

- **NFR-1** — <Performance, security, accessibility, auditability, scale. Include the number:
  "list endpoints respond in < 300 ms at p95 for 10k records", not "should be fast".>

## Acceptance Criteria

Each criterion is independently testable and traces to one or more FRs.

- **AC-1** *(FR-1)*
  - **Given** <initial state>
  - **When** <action>
  - **Then** <observable outcome>

- **AC-2** *(FR-1, FR-2)*
  - **Given** ...
  - **When** ...
  - **Then** ...

## Edge Cases & Error States

| # | Situation | Expected behaviour |
|---|---|---|
| E-1 | <e.g. concurrent edit by two recruiters> | <e.g. last-write-wins with a 409 warning banner> |

## Data Touched

| Entity | New / Existing | Notes |
|---|---|---|
| <Candidate> | Existing | <adds `stageId` foreign key> |

## Impacted Components

| Component | Change |
|---|---|
| `api/candidates` | <new endpoints> |

## Out of Scope

- <Things a reader might reasonably assume are included, but are not.>

## Open Questions

| # | Question | Blocking? | Owner |
|---|---|---|---|
| Q-1 | <unresolved> | Yes/No | <who decides> |

<If none: "None — all clarifications resolved, see clarifications.md.">

## Related Specs

<Per spec-kit/context-loading.md §4 — the table of what was loaded, at what tier, and why,
plus considered-and-skipped and cap-reached lines.>
