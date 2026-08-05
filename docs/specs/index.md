# Spec Index

One row per spec. This file is Tier 0 context — every stage agent reads it in full, every
time. Keep it cheap: one row, one sentence, no exceptions.

**Index-sync rule:** any stage that writes a spec's frontmatter updates that spec's row in
the same turn. A stale index degrades every future spec's context loading.

Status values: `specified` → `planned` → `implementing` → `implemented` → `validated`.

| Id | Title | Status | Components | Entities | Summary |
|---|---|---|---|---|---|
| 0001 | Project Scaffolding and Walking Skeleton | implementing | infra/build, api/system, service/system, db/core, ui/bff, ui/portal | — | Stands up both deployables with tests and tooling, proving both routes to the backend through a shared server-side invoke function. |

---

## Next id

`0002`

Update this after allocating an id, so `/specify` never has to scan the directory twice.

## Retired specs

| Id | Title | Retired | Reason |
|---|---|---|---|
