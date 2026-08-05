# Implementation Changelog — NNNN <Title>

What actually shipped, checkpoint by checkpoint. Append-only. This is the record `/validate`
and future specs consult to learn what is really in the code, as opposed to what was designed.

---

## CP-1 — Data layer · YYYY-MM-DD

**Tasks completed:** T-01, T-02, T-03

**Files created**

| Path | Purpose |
|---|---|
| `src/Domain/Pipeline/StageHistory.cs` | Append-only transition record |

**Files modified**

| Path | Change |
|---|---|
| `src/Domain/Pipeline/Application.cs` | Added `MoveToStage`, `RowVersion` |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-1 | Used `bytea` row_version rather than a timestamp column | Matches EF Core's native concurrency token support; avoids clock skew |

**Deviations from the LLD**

| LLD section | Designed | Actual | Reason | LLD patched? |
|---|---|---|---|---|
| §3.1 step 6 | Publish event inline | Publish via outbox row | Event must not fire if the transaction rolls back | Yes |

Every deviation must also be patched into `plan/lld.md` §Deviation Log. If the "LLD patched?"
column says No, that is an open defect.

**Verification run**

```
$ dotnet build
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test --filter Category=Unit
Passed!  - Failed: 0, Passed: 34, Skipped: 0, Duration: 4.1s
```

Paste real output. Never summarise a run you did not perform.

**Meta updates applied**

- `architecture.md`: added `STAGE_HISTORY` to ER diagram; Change Log row appended.
- `tech-stack.md`: no change.
- `coding-standards.md`: no change.

**Known gaps carried into the next checkpoint**

- <e.g. backfill migration written but not yet run against a seeded database>

---

## CP-2 — API · YYYY-MM-DD

<same structure>
