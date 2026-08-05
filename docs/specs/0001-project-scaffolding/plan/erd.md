# Data Model — 0001 Project Scaffolding and Walking Skeleton

**Spec:** `../spec.md` · **Updated:** 2026-08-05

> **Model inheritance.** This is the first spec to touch the data model. There is nothing
> prior to inherit from. `entities: []` in this spec's frontmatter is deliberate, not
> incomplete — see C-3 in `../clarifications.md`.

---

## 1. Diagram

No domain entity exists. `architecture.md`'s ER diagram stays empty, exactly as it is today:

```mermaid
erDiagram
```

The one physical table this spec creates is EF Core's own migration ledger, not a domain
entity — see §3.

## 2. Delta Summary

| Change | Entity | Detail |
|---|---|---|
| New table (framework-owned, not domain) | `__EFMigrationsHistory` | Created automatically by `dotnet ef database update` the first time any migration runs. Records applied migration ids. Not a `db/*` component concept — EF Core creates and owns it |
| New table | none | No domain table is created. `AppDbContext` (`db/core`) declares zero `DbSet<T>` members |
| Altered table | none | Nothing pre-existed to alter |
| Referenced-but-unchanged | none | No prior spec, no prior entity |

This is the direct, intended result of C-3 in `clarifications.md`: an empty initial migration
proves the migration toolchain (EF Core, SQLite, WAL) without placing a non-domain
placeholder table in the schema that a later spec would have to drop — which, under SQLite,
is a full table rebuild rather than a simple `DROP TABLE` (see `architecture.md`'s Known
Constraints).

## 3. Table Definitions

### 3.1 `__EFMigrationsHistory` *(new, framework-owned)*

Standard EF Core shape — documented here for completeness since `/validate` inspects it
directly (AC-8), not because `db/core` defines it explicitly.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| `MigrationId` | TEXT | No | | PK. Format `<timestamp>_InitialCreate` for this spec's one row |
| `ProductVersion` | TEXT | No | | EF Core version that generated the migration |

**Indexes.** None beyond the primary key — EF Core manages this table; `db/core` never
queries it directly.

**Constraints.** Primary key on `MigrationId`.

**Retention.** Grows by exactly one row per migration ever applied, for the life of the
database. Never pruned.

No other table exists after this spec ships. There is no `application`, `candidate`,
`requisition`, or `stage` table — those arrive with the first entity-bearing spec.

## 4. Relationships

None — a single, unrelated table.

## 5. Migrations

| # | Operation | Reversible | Backfill | Downtime |
|---|---|---|---|---|
| 1 | `dotnet ef migrations add InitialCreate --project src/Db` generates a migration with an empty `Up()`/`Down()` | Yes — `Down()` is a no-op | None | None |
| 2 | `dotnet ef database update --project src/Db` creates the SQLite file at the configured path (if absent), applies the (empty) migration, and EF Core creates `__EFMigrationsHistory` with one row | Yes — deleting the SQLite file fully reverses this, since no domain data exists at this schema version | None | None — the database does not exist yet when this runs |

**Rollback plan.** Delete the SQLite file (and its `-wal`/`-shm` sidecars if present) and
re-run the migrate command. There is no production data at this schema version to lose.

## 6. Data Volume & Growth

| Table | Initial rows | Growth | Notes affecting indexing |
|---|---|---|---|
| `__EFMigrationsHistory` | 1 (after first migrate) | +1 per future migration | Trivially small forever; no index beyond the PK is ever warranted |

## 7. Seed / Reference Data

None. The `Seed` command row in `tech-stack.md` stays `not yet defined` — there is nothing
to seed (A-7 in `clarifications.md`).

## 8. PII & Retention

| Column | Classification | Retention | Deletion path |
|---|---|---|---|
| — | — | — | No column in this spec holds personal data. `__EFMigrationsHistory` holds only migration ids and an EF Core version string |

## Related Specs

None — this is the first spec touching these components.
