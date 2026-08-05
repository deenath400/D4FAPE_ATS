# Low-Level Design — NNNN <Title>

**Spec:** `../spec.md` · **HLD:** `hld.md` · **Updated:** YYYY-MM-DD

The *how*. Precise enough that the implementation agent writes code without re-deciding
anything. Every file it will create or modify is named here, with signatures.

> This file is **living**: when implementation diverges from this design, `/implement`
> patches the affected section here and records the deviation in
> `../implementation/changelog.md`. Silent drift is a defect.

---

## 1. File Manifest

Exact paths, following the project's structure in `meta/architecture.md`.

| Action | Path | Purpose |
|---|---|---|
| Create | `src/Domain/Candidates/Candidate.cs` | Aggregate root |
| Modify | `src/Api/Controllers/CandidatesController.cs` | Add stage-move endpoint |
| Create | `tests/Unit/Candidates/MoveToStageTests.cs` | AC-1, AC-2 |

## 2. Domain / Data Layer

### 2.1 `<Entity>` — `<path>`

```csharp
public sealed class Candidate
{
    public CandidateId Id { get; private set; }
    public StageId StageId { get; private set; }
    public byte[] RowVersion { get; private set; }

    // Invariant: a candidate may only move to a stage belonging to its requisition.
    public Result MoveToStage(StageId target, IClock clock);
}
```

**Invariants.** <bulleted rules the type must never violate>

**Persistence notes.** <mapping, owned types, concurrency token>

## 3. Service / Application Layer

### 3.1 `<Service>.<Method>` — `<path>`

**Signature**

```csharp
Task<Result<CandidateDto>> MoveToStageAsync(
    CandidateId id, StageId target, CancellationToken ct);
```

**Behaviour**

1. Load candidate; `NotFound` if absent.
2. Authorize: caller must own the requisition (`shared/auth`).
3. Validate target stage belongs to the requisition; else `Validation`.
4. Mutate + append `StageHistory` row.
5. Persist; on `DbUpdateConcurrencyException` return `Conflict`.
6. Publish `CandidateStageChanged` to `worker/email`.

**Returns**

| Outcome | Result | Maps to HTTP |
|---|---|---|
| Success | `Result.Ok(dto)` | 200 |
| Missing | `Result.NotFound` | 404 |
| Bad stage | `Result.Validation` | 422 |
| Concurrency | `Result.Conflict` | 409 |

## 4. API Layer

Endpoint shapes are specified in `api.md`. Here, only the wiring:

| Route | Handler | Auth policy | Maps service result via |
|---|---|---|---|
| `PATCH /api/candidates/{id}/stage` | `CandidatesController.MoveStage` | `RequisitionOwner` | `ResultToActionResult` |

## 5. Frontend

### 5.1 Components

| Component | Path | Props | State | Notes |
|---|---|---|---|---|
| `PipelineBoard` | `src/features/pipeline/PipelineBoard.tsx` | `requisitionId` | server state via query cache | drag-and-drop container |

### 5.2 Data Access

| Hook | Endpoint | Cache key | Invalidates |
|---|---|---|---|
| `useMoveStage` | `PATCH /api/candidates/{id}/stage` | — | `['pipeline', requisitionId]` |

### 5.3 UI States

Every async surface defines all four: loading, empty, error, success.

| State | Treatment |
|---|---|
| Loading | skeleton columns |
| Empty | "No candidates in this requisition yet" + CTA |
| Error | inline retry, preserves optimistic position rollback |

## 6. DTOs & Contracts

```ts
type CandidateDto = {
  id: string;
  fullName: string;
  stageId: string;
  updatedAt: string;   // ISO-8601 UTC
  rowVersion: string;  // base64
};
```

## 7. Validation Rules

| Field | Rule | Message | AC |
|---|---|---|---|
| `stageId` | required, must exist in requisition | "Stage does not belong to this requisition." | AC-3 |

## 8. Error Handling

Follow the project envelope from `meta/coding-standards.md`. Per-case:

| Condition | Code | Logged at | User-facing message |
|---|---|---|---|
| Concurrency conflict | `candidate.stage.conflict` | Information | "This candidate was moved by someone else. Refreshing." |

## 9. Configuration

| Key | Type | Default | Required | Where consumed |
|---|---|---|---|---|
| `Pipeline:MaxStages` | int | 12 | No | `PipelineService` |

## 10. Database Migration

| Step | Change | Reversible |
|---|---|---|
| 1 | Add `StageId` to `Candidates`, nullable | Yes |
| 2 | Backfill from `Applications.CurrentStage` | Yes |
| 3 | Set NOT NULL, add index `IX_Candidates_RequisitionId_StageId` | Yes |

## 11. Test Plan

| Test | Type | Covers | Path |
|---|---|---|---|
| `MoveToStage_WhenStageForeign_ReturnsValidation` | Unit | AC-3, E-1 | `tests/Unit/...` |

Every `AC-n` in the spec must appear at least once in this table.

## 12. Implementation Notes

<Ordering constraints, gotchas, things that look wrong but are intentional.>

## Related Specs

<Per spec-kit/context-loading.md §4.>

## Deviation Log

Appended by `/implement` when reality diverged from this design.

| Date | Task | Section | Designed | Actual | Reason |
|---|---|---|---|---|---|
