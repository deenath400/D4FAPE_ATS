---
id: 0005
slug: pipeline-progression
title: Pipeline Progression
status: implemented
components: [api/pipeline, service/pipeline, db/pipeline, service/requisition, db/requisition, api/application, service/application, db/application, ui/staff, ui/portal]
entities: [Stage, Application, StageTransition]
depends_on: [0003, 0004]
created: 2026-08-06
updated: 2026-08-13
---

# Pipeline Progression

## Problem & Context

Today `Stage` exists only as an empty table with a fixed ownership shape: `0003` created it,
fixed only that each Requisition owns an independent Stage set (FR-14), and explicitly deferred
"Pipeline/Stage management UI, ordering, and candidate advancement through stages" to this spec.
`Application` fares similarly — `0004` gave it nothing but a submission timestamp, and its own
Non-Goals name "pipeline/stage progression," "full candidate-visible status tracking," and "staff
decisioning" as work deferred here by function, three separate times. As a result, three of
`project.md`'s four success criteria remain unmet: S-2 (every active application has a current
pipeline stage in the database), S-3 (candidates self-serve status), and S-4 (a recruiter sees a
requisition's applicants grouped by stage). The Recruiter persona's stated pain — "applicant
state lives in inboxes and spreadsheets" — is unresolved for the one thing that actually moves
during a hire: where a candidate currently stands.

This is also the first spec that changes a previously shipped write path rather than only adding
new ones. `0003`'s Requisition-creation flow (`service/requisition`) must now also create a
default Stage set (FR-5), and `0004`'s Application-submission flow (`service/application`) must
now also place the new Application into that Requisition's first Stage (FR-7). Neither prior
spec's endpoints, request contracts, or authorization posture change — only their internal write
behaviour gains a step — but it is a genuine modification of shipped code, not a purely additive
feature, and is called out as such in Impacted Components and Related Specs below.

Finally, this spec has to answer, for the first time, who is allowed to move a candidate through
the pipeline. `project.md`'s HiringManager persona goal — "review shortlisted applicants... leave
a decision" — could plausibly widen write access beyond `0003`'s and `0004`'s established
`RecruiterOnly`-writes/staff-wide-reads precedent. It also has to decide how the transition audit
trail models its acting identity durably enough that a later, distinct spec (likely `0006`, an
AI/agent CV assessor) can be told apart from a human Recruiter without a SQLite table rebuild —
while adding no AI/agent behaviour itself. `architecture.md` records no `worker/*` component and
an empty Integration Points table; nothing about that future spec exists yet, and this one does
not get ahead of it.

## Goals

- **G-1** Every Application acquires and keeps a current Stage from the moment it is submitted,
  making S-2 a structural property of the schema rather than a convention.
- **G-2** A Recruiter can shape and adjust a Requisition's pipeline — add, rename, reorder, and
  remove Stages — without engineering involvement.
- **G-3** A Recruiter can advance or reject an Application, with every such action recorded
  permanently against who did it and when.
- **G-4** A Recruiter or HiringManager sees, at a glance, every applicant for a Requisition
  grouped by Stage (S-4).
- **G-5** A Candidate sees their own Application's real current status, replacing `0004`'s
  submission-only placeholder (S-3).
- **G-6** The transition audit trail's actor is modelled so a future non-human actor is
  representable without a later schema rebuild, while this spec itself adds no non-human
  behaviour.

## Non-Goals

- **AI/agent assessment of CVs and any automatic accept/reject** — no AI provider exists in
  `tech-stack.md`, the Integration Points table is empty, and `architecture.md` states no
  `worker/*` component exists; the audit trail is merely *shaped* to admit a non-human actor
  (FR-13), which is a column, not a behaviour.
- **Notifying a Candidate when their stage changes** — `project.md` lists the transactional email
  provider as "not yet chosen"; the Candidate pulls status from the portal instead.
- **Cross-requisition or multi-requisition pipeline views** — S-4 is scoped to one Requisition at
  a glance.
- **Reusable pipeline templates shared across Requisitions** — `0003` FR-14 makes each
  Requisition's Stage set structurally independent; a shared template is a second entity and a
  separate feature.
- **Interview scheduling, scorecards, ratings, or structured feedback on an Application** — not
  requested; the only note-taking surface is the single optional per-transition note (FR-23).
- **Reinstating a rejected Application, or Candidate-initiated withdrawal** — `0004` already
  deferred withdrawal; rejection is terminal, mirroring `0003`'s `closed`-is-terminal precedent.
- **Bulk moves** (selecting many Applications and advancing them together) — a volume affordance,
  additive once single moves exist.
- **HiringManager decisioning** — HiringManager remains read-only for stage configuration and
  Application transitions, consistent with `0003`'s precedent; only reading the board and
  transition history is in scope (FR-20).
- **A symmetric "Hired" terminal outcome** — not requested; reaching the last configured Stage
  carries no special terminal meaning in this spec.
- **A publish-readiness gate requiring Stages before a Requisition can publish** — unnecessary,
  since every Requisition receives a default Stage set at creation (FR-5); `0003`'s publish flow
  is unmodified.
- **Replacing or removing `0004`'s existing flat per-Requisition Applications list endpoint** —
  the new grouped pipeline board (FR-15) is additive, not a replacement.

## Users & Personas

| Persona | Need this feature serves |
|---|---|
| Recruiter | Configures each Requisition's pipeline, advances or rejects Applications, and sees where every Applicant stands. |
| HiringManager | Reads the pipeline board and any Application's transition history without depending on a Recruiter for status. |
| Candidate | Sees their own Application's real current status — a Stage name or a rejected indicator — without contacting anyone. |

## Functional Requirements

- **FR-1** — A Recruiter can add a Stage to a Requisition, specifying its name and its position
  in the pipeline order.
- **FR-2** — A Recruiter can rename an existing Stage without changing which Applications are
  currently assigned to it.
- **FR-3** — A Recruiter can reorder a Requisition's Stages.
- **FR-4** — A Recruiter can remove a Stage that holds no Applications; removing a Stage that
  holds at least one Application is rejected.
- **FR-5** — Every Requisition is created with a default Stage set (Applied, Screening, Interview,
  Offer, in that order), which the Recruiter may then edit freely via FR-1–FR-4.
- **FR-6** — Staff (Recruiter or HiringManager) can retrieve a Requisition's Stages in pipeline
  order.
- **FR-7** — Every Application is assigned its Requisition's first Stage (in current pipeline
  order) at the moment it is submitted.
- **FR-8** — A Recruiter can move an Application to any other Stage within its own Requisition's
  pipeline, forward or backward.
- **FR-9** — A move naming a Stage that belongs to a different Requisition than the Application's
  is rejected, and the Application's current Stage is unchanged.
- **FR-10** — A Recruiter can reject an Application. Rejection is a terminal outcome; the
  Application retains the Stage it was rejected from.
- **FR-11** — A rejected Application accepts no further stage move and cannot be rejected a
  second time.
- **FR-12** — Every stage move and every rejection is recorded permanently, capturing the
  originating Stage, the destination Stage or the rejected outcome, the acting identity, and the
  UTC time it occurred.
- **FR-13** — A transition's recorded actor is modelled as an actor kind (`User` or `System`), a
  nullable reference to the acting user, and a stored display label, so a future non-human actor
  is representable without a schema change; this spec's own code paths write only `User`-kind
  actors.
- **FR-14** — Transition records are append-only: no operation edits or deletes a previously
  recorded transition.
- **FR-15** — Staff (Recruiter or HiringManager) can view a Requisition's Applications grouped by
  Stage, in pipeline order, with a count of Applications per Stage; rejected Applications are
  shown in a separate group from the active pipeline.
- **FR-16** — Staff (Recruiter or HiringManager) can view the full transition history of a single
  Application, in chronological order.
- **FR-17** — A Candidate can see, for each of their own submitted Applications, its current Stage
  name, or that it was rejected.
- **FR-18** — A Candidate cannot see another Candidate's Application status, and cannot see any
  Application's transition history, acting identity, or staff-only note.
- **FR-19** — Stage configuration (FR-1–FR-4) and Application transitions (FR-8, FR-10) are
  restricted to the `Recruiter` policy; a HiringManager or Candidate request is rejected with
  HTTP 403.
- **FR-20** — A HiringManager can read the pipeline board (FR-15) and an Application's transition
  history (FR-16), consistent with `0003`'s HiringManager read-access precedent.
- **FR-21** — Stage configuration and Application transitions are rejected with HTTP 409 on a
  Requisition that is `closed`.
- **FR-22** — A move request states the Stage the requester believes the Application currently
  occupies; if the Application's actual current Stage no longer matches, the move is rejected with
  HTTP 409 and no change is made.
- **FR-23** — A move or rejection may carry an optional free-text note; the note is staff-visible
  only and is never included in any Candidate-facing response.
- **FR-24** — Stage names are unique within a single Requisition's pipeline.
- **FR-25** — Every Requisition and Application that existed before this feature shipped is
  backfilled so that every Requisition has the default Stage set and every Application has a
  current Stage, without generating a transition-log entry for the backfill itself.

## Non-Functional Requirements

- **NFR-1** — The staff pipeline board (FR-15) renders within 2 seconds at p95 for a Requisition
  with up to 500 Applications, with no pagination in this spec.
- **NFR-2** — Consistent with `architecture.md`'s documented SQLite single-writer constraint and
  `0004`'s NFR-3 precedent, a stage move or rejection's database transaction remains open only
  for the Application/StageTransition writes themselves.

## Acceptance Criteria

Each criterion is independently testable and traces to one or more FRs.

- **AC-1** *(FR-1)*
  - **Given** an authenticated Recruiter and an existing Requisition
  - **When** they add a Stage with a name and a pipeline position
  - **Then** the API returns HTTP 201 Created and the Stage appears in the Requisition's pipeline
    at that position.

- **AC-2** *(FR-1, FR-19)*
  - **Given** an authenticated HiringManager or Candidate
  - **When** they attempt to add a Stage to a Requisition
  - **Then** the API returns HTTP 403 Forbidden and no Stage is created.

- **AC-3** *(FR-2)*
  - **Given** a Stage with Applications currently assigned to it
  - **When** a Recruiter renames the Stage
  - **Then** the Stage's name updates and every Application previously assigned to it remains
    assigned to the same Stage.

- **AC-4** *(FR-3)*
  - **Given** a Requisition with four Stages
  - **When** a Recruiter reorders them
  - **Then** a subsequent request for the Stages (FR-6) returns them in the new order.

- **AC-5** *(FR-4)*
  - **Given** a Stage with zero Applications assigned
  - **When** a Recruiter removes it
  - **Then** the API returns a success response and the Stage no longer appears in the
    Requisition's pipeline.

- **AC-6** *(FR-4)*
  - **Given** a Stage with at least one Application assigned
  - **When** a Recruiter attempts to remove it
  - **Then** the API returns HTTP 409 Conflict, the Stage is not removed, and its Applications
    remain assigned to it.

- **AC-7** *(FR-5)*
  - **Given** a Recruiter creates a new Requisition
  - **When** creation completes
  - **Then** the Requisition has four Stages — Applied, Screening, Interview, Offer — in that
    pipeline order.

- **AC-8** *(FR-5)*
  - **Given** a newly created Requisition's default Stage set
  - **When** a Recruiter edits it (renames, reorders, adds, or removes a Stage per FR-1–FR-4)
  - **Then** the edit succeeds exactly as it would for any other Stage set — the default set
    carries no special protection.

- **AC-9** *(FR-6)*
  - **Given** a Requisition with configured Stages
  - **When** Staff request them
  - **Then** the API returns HTTP 200 OK with the Stages in pipeline order.

- **AC-10** *(FR-7)*
  - **Given** a Candidate submits an Application to a `published` Requisition (`0004` FR-1)
  - **When** the submission succeeds
  - **Then** the Application's current Stage is the Requisition's first Stage in pipeline order.

- **AC-11** *(FR-8, FR-12)*
  - **Given** an Application currently in Stage A
  - **When** a Recruiter moves it to Stage B in the same Requisition's pipeline
  - **Then** the Application's current Stage becomes B, and a transition record is written
    capturing origin Stage A, destination Stage B, the acting Recruiter, and the UTC time.

- **AC-12** *(FR-8)*
  - **Given** an Application currently in a later Stage
  - **When** a Recruiter moves it to an earlier Stage in the same pipeline
  - **Then** the move succeeds — moves are not restricted to forward order.

- **AC-13** *(FR-9)*
  - **Given** a Stage that belongs to a different Requisition than the Application's
  - **When** a Recruiter attempts to move the Application to that Stage
  - **Then** the API rejects the request and the Application's current Stage is unchanged.

- **AC-14** *(FR-10, FR-12)*
  - **Given** an Application in an active Stage
  - **When** a Recruiter rejects it
  - **Then** the Application's outcome becomes rejected, it retains the Stage it was rejected
    from, and a transition record is written capturing that Stage, the rejected outcome, the
    acting Recruiter, and the UTC time.

- **AC-15** *(FR-11)*
  - **Given** a rejected Application
  - **When** a Recruiter attempts to move it or reject it again
  - **Then** the API returns HTTP 409 Conflict and no change is made.

- **AC-16** *(FR-13)*
  - **Given** a transition created by this spec's move or reject endpoints
  - **When** the transition's actor is inspected
  - **Then** its actor kind is `User`, its user reference is the acting Recruiter, and its display
    label is populated.

- **AC-17** *(FR-14)*
  - **Given** a transition record already written for an Application
  - **When** a further move or rejection occurs on that Application
  - **Then** the earlier transition record is still present, unmodified, and the new transition
    is appended alongside it.

- **AC-18** *(FR-15)*
  - **Given** a Requisition with Applications spread across several Stages and at least one
    rejected Application
  - **When** Staff request the pipeline board
  - **Then** the API returns HTTP 200 OK with Applications grouped by Stage in pipeline order,
    each group showing its count, and rejected Applications shown in a separate group.

- **AC-19** *(FR-15)*
  - **Given** a Requisition with zero submitted Applications
  - **When** Staff request the pipeline board
  - **Then** the API returns HTTP 200 OK showing every configured Stage with a zero count, not an
    error.

- **AC-20** *(FR-16)*
  - **Given** an Application with two recorded transitions
  - **When** Staff request its transition history
  - **Then** the API returns HTTP 200 OK with both transitions in chronological order.

- **AC-21** *(FR-16)*
  - **Given** an Application with no Recruiter-initiated transitions yet
  - **When** Staff request its transition history
  - **Then** the API returns HTTP 200 OK with an empty list, not an error.

- **AC-22** *(FR-17)*
  - **Given** a Candidate with an Application currently in Stage "Interview"
  - **When** they view their own Applications
  - **Then** they see "Interview" as that Application's current status.

- **AC-23** *(FR-17)*
  - **Given** a Candidate with a rejected Application
  - **When** they view their own Applications
  - **Then** they see a rejected indicator, not a Stage name.

- **AC-24** *(FR-18)*
  - **Given** Candidate A and Candidate B, each with their own Applications
  - **When** Candidate A requests status for one of Candidate B's Applications
  - **Then** the API returns HTTP 403 Forbidden (or 404), never Candidate B's data.

- **AC-25** *(FR-18)*
  - **Given** an authenticated Candidate session
  - **When** it calls the staff pipeline board or transition-history endpoint
  - **Then** the API returns HTTP 403 Forbidden.

- **AC-26** *(FR-19)*
  - **Given** an authenticated HiringManager
  - **When** they attempt to add, rename, reorder, or remove a Stage, or to move or reject an
    Application
  - **Then** the API returns HTTP 403 Forbidden and no change is made.

- **AC-27** *(FR-20)*
  - **Given** an authenticated HiringManager
  - **When** they request the pipeline board or an Application's transition history
  - **Then** the API returns HTTP 200 OK.

- **AC-28** *(FR-21)*
  - **Given** a `closed` Requisition
  - **When** a Recruiter attempts any Stage configuration operation or Application transition on
    it
  - **Then** the API returns HTTP 409 Conflict and no change is made.

- **AC-29** *(FR-22)*
  - **Given** a Recruiter's move request states Stage A as the Application's believed current
    Stage, but the Application's actual current Stage is Stage B because another move already
    occurred
  - **When** the move is submitted
  - **Then** the API returns HTTP 409 Conflict, no change is made, and the response indicates the
    Application's actual current Stage.

- **AC-30** *(FR-23)*
  - **Given** a Recruiter rejects an Application with an accompanying note
  - **When** Staff view the Application's transition history
  - **Then** the note is visible; **When** the same Candidate views their own Application status,
    **Then** the note does not appear anywhere in the response.

- **AC-31** *(FR-24)*
  - **Given** a Requisition with a Stage already named "Screening"
  - **When** a Recruiter attempts to add or rename another Stage to "Screening" within the same
    Requisition
  - **Then** the API returns HTTP 409 Conflict and no change is made.

- **AC-32** *(FR-25)*
  - **Given** a Requisition and Application that existed before this feature's migration
  - **When** the migration runs
  - **Then** the Requisition has the default Stage set and the Application has a current Stage
    equal to that Requisition's first Stage, and no transition record is created for this
    backfill.

- **AC-33** *(FR-5)*
  - **Given** a Requisition being created
  - **When** creation completes
  - **Then** its status is still `draft` exactly as `0003` FR-1 specifies — the default Stage set
    is additive to, not a replacement of, `0003`'s creation behaviour.

## Edge Cases & Error States

| # | Situation | Expected behaviour |
|---|---|---|
| E-1 | Recruiter attempts to remove a Stage holding Applications | Rejected with 409; Stage and its Applications untouched (AC-6). |
| E-2 | Two Recruiters attempt to move the same Application concurrently, each believing a different current Stage | The losing request is rejected with 409 and told the Application's actual current Stage; no silent overwrite (AC-29). |
| E-3 | Recruiter attempts to move an Application to a Stage belonging to a different Requisition | Rejected; the Application's current Stage is unchanged (AC-13). |
| E-4 | Recruiter attempts to reject an already-rejected Application | Rejected with 409; no second transition record is written (AC-15). |
| E-5 | Recruiter attempts any Stage configuration or Application transition on a `closed` Requisition | Rejected with 409 (AC-28). |
| E-6 | Recruiter attempts to add or rename a Stage to a name already used in the same Requisition | Rejected with 409 (AC-31). |
| E-7 | HiringManager attempts to configure Stages or move/reject an Application | Rejected with 403 (AC-26). |
| E-8 | Candidate requests another Candidate's status, or calls any staff pipeline endpoint | Rejected with 403/404, never the other Candidate's data (AC-24, AC-25). |
| E-9 | Staff request the pipeline board for a Requisition with zero Applications | 200 OK with every configured Stage shown at a zero count (AC-19). |
| E-10 | A Recruiter reorders a Requisition's Stages moments before a Candidate submits an Application against it | The Application lands in whichever Stage is currently first per the latest order at the moment of submission, not a stale one (FR-7, FR-3). |
| E-11 | A Requisition and its Applications that existed before this spec shipped (created under `0003`/`0004`) | Backfilled by migration with the default Stage set and a current Stage on every Application, with no transition-log entry generated for the backfill (AC-32). |
| E-12 | Move request references a Stage id that does not exist at all | HTTP 404 Not Found; no change is made. |

## Data Touched

| Entity | New / Existing | Notes |
|---|---|---|
| `Stage` | Existing, modified | Gains an ordering position within its owning Requisition (FR-3); name uniqueness enforced within a Requisition (FR-24). Ownership shape (`0003` FR-14) is unchanged. |
| `Application` | Existing, modified | Gains a reference to its current Stage (FR-7) and a terminal rejected-outcome marker (FR-10, FR-11). `0004`'s existing columns are unchanged. |
| `StageTransition` | New | Append-only audit record of every move and rejection: originating Stage, destination Stage or rejected outcome, actor (kind/user-reference/display-label per FR-13), an optional staff-only note (FR-23), and a UTC timestamp (FR-12). |
| `Requisition` | Existing, referenced not modified | No new column; its creation flow (`service/requisition`) gains a side effect — creating the default Stage set (FR-5) — but the entity's own shape is untouched. |

## Impacted Components

| Component | Change |
|---|---|
| `api/pipeline` | New. Stage CRUD/reorder/remove endpoints, move and reject endpoints, staff pipeline board endpoint, and Application transition-history endpoint. |
| `service/pipeline` | New. Stage configuration rules (uniqueness, occupied-Stage removal guard), move/reject transition logic (terminal-rejection guard, optimistic-concurrency check per FR-22), the closed-Requisition guard (FR-21), and the transaction boundary for every transition write. |
| `db/pipeline` | New. `StageTransition` entity, EF Core configuration, and the migration that also adds the ordering column to `Stages` and the current-Stage/rejected-outcome columns to `Applications`. |
| `service/requisition` | Modified. Requisition creation (`0003` FR-1) now also creates the Requisition's default Stage set (FR-5) — the first modification of `0003`'s shipped write path. |
| `db/requisition` | Modified. `Stage` entity configuration gains the ordering column (FR-3); `0003`'s ownership-shape FK is unchanged. |
| `api/application` | Modified. The Candidate's own-Applications-list response (`0004` FR-8) gains the current Stage name / rejected indicator (FR-17) as an additive field. |
| `service/application` | Modified. Application submission (`0004` FR-1) now also assigns the new Application to the Requisition's first Stage (FR-7) — the first modification of `0004`'s shipped write path. |
| `db/application` | Modified. `Application` entity gains a current-Stage reference and a terminal rejected-outcome marker; `0004`'s existing columns and constraints are unchanged. |
| `ui/staff` | Modified. Adds a Stage-configuration screen, move/reject controls on an Application, the pipeline board grouped by Stage (FR-15), and a per-Application transition-history view (FR-16). |
| `ui/portal` | Modified. The "My Applications" list (`0004`) is upgraded from submission-confirmation-only to a real current status (Stage name or rejected indicator, FR-17). |

## Out of Scope

- AI/agent assessment of CVs and any automatic accept/reject — no provider, integration, or
  `worker/*` component exists; only the actor-shape column (FR-13) anticipates it.
- Notifying a Candidate when their stage changes — no transactional email provider is chosen.
- Cross-requisition or multi-requisition pipeline views.
- Reusable pipeline templates shared across Requisitions.
- Interview scheduling, scorecards, ratings, or structured feedback beyond the single optional
  per-transition note.
- Reinstating a rejected Application, or Candidate-initiated withdrawal.
- Bulk moves across multiple Applications at once.
- HiringManager decisioning (stage configuration or moving/rejecting an Application) — read-only.
- A symmetric "Hired" terminal outcome.
- A publish-readiness gate requiring Stages before a Requisition can publish.
- Replacing or removing `0004`'s existing flat per-Requisition Applications list endpoint.

## Open Questions

None — all clarifications resolved, see `clarifications.md`.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0003` (Requisition Management) | 1 | Owns `Stage` and `Requisition`. This spec gives `Stage` its first behaviour (ordering, CRUD) on top of `0003`'s ownership-shape-only FK (FR-14), and modifies `0003`'s Requisition-creation write path to also create the default Stage set (FR-5) — the first spec to change `0003`'s shipped behaviour, not merely read it. Reused `0003`'s `RecruiterOnly`-writes/`StaffOnly`-reads permission precedent (FR-19, FR-20) and its `closed`-is-terminal pattern (FR-21). |
| `0004` (Application Submission and CV Upload) | 1 | Owns `Application`. This spec modifies `0004`'s submission write path to assign a current Stage at creation (FR-7) — the first spec to change `0004`'s shipped behaviour — and upgrades the Candidate status view `0004` explicitly left as a submission-only placeholder (FR-17). Reused `0004`'s RFC 7807 ProblemDetails and no-existence-leak conventions for the unhappy paths. |
| `0002` (User Authentication and Refresh Token Flow) | 1 | Owns the `Recruiter`/`HiringManager`/`Candidate` roles and `RecruiterOnly`/`StaffOnly`/`CandidateOnly` policies this spec's authorization (FR-19, FR-20) consumes unchanged, and `AspNetUsers.Id` — the column the transition actor's user reference (FR-13) points to. |

Tier 0 was read in full: `meta/architecture.md`, `meta/tech-stack.md`, `meta/coding-standards.md`,
`index.md`.
Considered and skipped: `0001` (Project Scaffolding and Walking Skeleton) — scored above
threshold on shared `ui/portal`/`ui/bff` components, but its conventions reach this spec
unchanged via `0003` and `0004`, both of which were already at the cap.
Cap reached: yes — 3 prior specs loaded (`0003`, `0004`, `0002`).
