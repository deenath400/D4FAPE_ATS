# Spec Index

One row per spec. This file is Tier 0 context — every stage agent reads it in full, every
time. Keep it cheap: one row, one sentence, no exceptions.

**Index-sync rule:** any stage that writes a spec's frontmatter updates that spec's row in
the same turn. A stale index degrades every future spec's context loading.

Status values: `specified` → `planned` → `implementing` → `implemented` → `validated`.

| Id | Title | Status | Components | Entities | Summary |
|---|---|---|---|---|---|

---

## Next id

`0001`

Update this after allocating an id, so `/specify` never has to scan the directory twice.

## Retired specs

| Id | Title | Retired | Reason |
|---|---|---|---|
