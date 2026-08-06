# Clarifications — 0005 Pipeline Progression

A verbatim log of ambiguities raised during `/specify` and how they were resolved. This is an
audit trail: when someone later asks "why does it work this way", the answer is here.

---

## Scope — one spec or a split

**Ambiguity.** At ~19 draft FRs this was already at the top of the range compared to `0003`
(14 FRs) and `0004` (13 FRs). The one clean cut identified was lifting the Candidate-facing
status upgrade (FR-17/FR-18, satisfying S-3) into its own follow-on spec, since it is the only
slice that reads pipeline state without writing any.

**Options presented.**
1. Keep it all in `0005` — one coherent feature; the parts are interdependent (stages without
   progression is what `0003` already half-shipped; progression without the candidate view
   leaves S-3 open for a third consecutive spec).
2. Split the candidate-status upgrade into its own spec — smaller specs, but the candidate view
   ships later and depends on this one anyway.

**Answer.** "Keep it all in 0005 (do not split candidate status into a follow-on spec)."

**Impact.** All of FR-1 through FR-25 remain in this spec. No split spec is created.

---

## Round 1 — 2026-08-06

### C-1 — Where a Requisition's initial pipeline comes from

**Ambiguity.** S-2 ("every active application has a current pipeline stage") only holds
structurally if a Stage exists before the first Application arrives at a Requisition. Whether
that Stage set is seeded automatically, gated behind manual configuration, or simply absent
until a Recruiter acts changes the submission path, `0003`'s publish flow, and every Stage AC.

**Options presented.**
1. Every Requisition is created with a default Stage set (e.g. Applied, Screening, Interview,
   Offer) that the Recruiter can then edit freely — S-2 holds with zero Recruiter effort, `0003`'s
   state machine untouched.
2. The Recruiter defines Stages manually and publishing is blocked until at least one exists —
   adds a publish-readiness gate, changing `0003`'s shipped behaviour (its E-11 explicitly has no
   such gate).
3. No requirement — an Application may sit with no Stage until a Recruiter creates one; S-2 is
   only conditionally true and every consumer must handle a null Stage.

**Answer.** "Default stage set on Requisition creation (e.g. Applied, Screening, Interview,
Offer), freely editable by the Recruiter afterward. 0003's publish flow is untouched — no new
publish-readiness gate."

**Impact.** FR-5 (default Stage set at creation), AC-7, AC-8, AC-33. `0003`'s publish/creation
state machine is confirmed unmodified except for the added default-Stage-set side effect
(Impacted Components: `service/requisition`).

---

### C-2 — Shape of the transition record's actor

**Ambiguity.** Flagged proactively (not by the user) because retrofitting a human-only `UserId`
foreign key to also carry a non-human actor is a table rebuild under SQLite, where EF Core
emulates most ALTERs by rebuilding the table. Doing it while the table is empty costs almost
nothing, but it must not be read as introducing any AI/agent behaviour.

**Options presented.**
1. Actor is a kind (`User` / `System`) plus a nullable user reference plus a stored display
   label — a system/agent actor is representable on day one, though nothing produces one in this
   spec.
2. Actor is a required user reference; a non-human actor needs a rebuild later.
3. Actor is a nullable user reference where null means "not a human" — cheap, but a null carries
   no information about *which* non-human actor and cannot be told apart from a data defect.

**Answer.** "Kind (User / System) + nullable user reference + stored display label. This spec
produces only `User`-kind actors; no AI/agent behaviour is added now — this is purely a
forward-compatible column shape, as previously discussed with the user."

**Impact.** FR-13, AC-16. Confirmed in Non-Goals and Out of Scope: no code path in this spec
writes a `System`-kind actor.

---

### C-3 — Reconfiguring a pipeline that already has Applications in it

**Ambiguity.** Determines whether S-2 can ever be violated by a configuration edit, and drives
the unhappy-path ACs for every Stage-write operation.

**Options presented.**
1. Rename and reorder always allowed; removing a Stage that holds Applications is rejected (409)
   — the Recruiter must move them out first.
2. Full CRUD always allowed; removing an occupied Stage automatically moves its Applications to
   the preceding Stage, generating audited transitions.
3. The pipeline is frozen once the Requisition is published or has its first Application.

**Answer.** "Rename and reorder always allowed. Removing a Stage that holds Applications is
rejected (409); the Recruiter must move them out first. An Application must never end up
pointing at a Stage that no longer exists."

**Impact.** FR-2, FR-3, FR-4, AC-3, AC-4, AC-5, AC-6, E-1.

---

### C-4 — How rejection is modelled relative to Stages

**Ambiguity.** Decides whether "rejected" is itself a Stage or an Application-level outcome,
which changes the data model, the grouped board, and the candidate status view.

**Options presented.**
1. Rejection is an Application-level terminal outcome separate from its Stage — the Application
   keeps the Stage it was rejected from.
2. Same as (1), plus a symmetric terminal `Hired` outcome.
3. Rejection is just a move into a terminal "Rejected" Stage row in the pipeline.

**Answer.** "Rejection is an Application-level terminal outcome, separate from the Stage list —
the Application retains the Stage it was rejected from. No symmetric 'Hired' terminal outcome;
not requested, do not add it."

**Impact.** FR-10, FR-11, AC-14, AC-15. Out of Scope explicitly excludes a "Hired" outcome.

---

### C-5 — Who can move and reject an Application

**Ambiguity.** `0003` set the precedent that staff writes are `RecruiterOnly` and HiringManager
is read-only, but `project.md`'s HiringManager persona goal is literally "review shortlisted
applicants, leave a decision." These conflict, and the answer changes roughly six permission ACs.

**Options presented.**
1. `Recruiter` only for stage configuration and transitions; HiringManager reads the board and
   history — consistent with `0003` C-2 and `0004`.
2. Any Staff (`Recruiter` or `HiringManager`) may move and reject; `Recruiter` alone configures
   Stages.
3. Recruiter-only for everything now, with HiringManager decisioning as its own later spec.

**Answer.** "Recruiter only, consistent with 0003's precedent. HiringManager may read the board
and transition history but cannot configure stages or move/reject Applications."

**Impact.** FR-19, FR-20, AC-2, AC-26, AC-27. Out of Scope explicitly names HiringManager
decisioning as excluded.

---

### C-6 — How much status detail the Candidate sees

**Ambiguity.** S-3 says candidates self-serve status; `project.md` says "honest status." Stage
names are internal Recruiter wording and could leak process detail. Changes the candidate view,
the API response shape, and possibly the `Stage` schema.

**Options presented.**
1. The Candidate sees the actual Stage name plus a rejected indicator — the Recruiter already
   controls the wording.
2. The Candidate sees a coarse fixed status (e.g. Submitted / In Review / Decision Made) mapped
   from the Stage, hiding internal names.
3. Each Stage carries a candidate-facing flag and/or a separate public label; the Candidate sees
   only what is marked visible.

**Answer.** "Candidate sees the actual Stage name plus a rejected indicator. No coarse status
mapping, no per-stage visibility flag."

**Impact.** FR-17, FR-18, AC-22, AC-23.

---

## Assumptions Made Without Asking

Carried forward from Phase 1; none were contradicted by the answers above.

| # | Assumption | Default chosen | Reversal cost if wrong |
|---|---|---|---|
| A-1 | Existing data is migrated forward: existing Requisitions receive the default Stage set and existing Applications are placed in their Requisition's first Stage, with no synthetic transition-log entry (an audited transition implies a Recruiter action, and none occurred). | Backfill migration, no transition record for the backfill itself | Low now (few dev-database rows), high once real data exists. |
| A-2 | A move request states the Stage the mover believed the Application to be in, and is rejected with a conflict if that no longer matches — the SQLite-single-writer-aware answer to concurrent moves. | Optimistic concurrency check on move | Low — relaxing to last-write-wins is deleting a check. |
| A-3 | A transition may carry an optional free-text note (e.g. a rejection reason); never required; staff-visible only, never shown to the Candidate. | Optional note, staff-only | Low — a nullable column and a form field. |
| A-4 | Rejection is terminal with no reinstate path, mirroring `0003`'s terminal `closed`. | No reinstate endpoint | Low — an additive endpoint later. |
| A-5 | The staff pipeline board returns every Application for one Requisition without pagination, bounded instead by a numeric NFR (renders within 2s at p95 with 500 Applications). | No pagination in this spec | Low — pagination is additive; the NFR number is cheap to change. |
| A-6 | The Candidate sees their current status only, never the transition history or who moved them. | Status only, no history | Low. |
| A-7 | The new audit entity is named `StageTransition` in frontmatter and prose, reusing `0003`/`0004`'s PascalCase entity-naming convention. | `StageTransition` | Trivial before Plan; a rename afterwards. |
| A-8 | Stage names must be unique within a Requisition; two Stages named "Interview" in one pipeline is a data-entry error, not a feature. | Unique per Requisition | Low. |
| A-9 | Ordinary Requisition content edits (`0003` FR-2) are untouched — this spec adds Stage operations and the default-Stage-set creation side effect alongside them, changing no other existing Requisition endpoint behaviour. | No change to `0003`'s edit/publish/unpublish/close endpoints | N/A — this is a non-change. |

## Deferred

| # | Question | Deferred to |
|---|---|---|
| D-1 | AI/agent assessment of CVs, with automatic accept/reject | A later spec (likely `0006`) — not this one. This spec only shapes the transition actor column (FR-13, C-2) to admit it without a future schema rebuild; it adds no AI/agent behaviour, provider, or `worker/*` component. |
