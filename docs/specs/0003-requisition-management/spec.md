---
id: 0003
slug: requisition-management
title: Requisition Management
status: implemented
components: [api/requisition, service/requisition, db/requisition, ui/staff, ui/portal]
entities: [Requisition, Stage]
depends_on: [0002]
created: 2026-08-05
updated: 2026-08-05
---

# Requisition Management

## Problem & Context

Today there is no `Requisition` entity anywhere in D4FAPE_ATS: `service/requisition` and
`api/requisition` do not exist, and `ui/staff` — the workspace `architecture.md` describes as
"authenticated recruiter and hiring-manager workspace: requisitions, pipeline, decisions" —
contains nothing but a placeholder route group. A Recruiter has no way to open a role, and a
HiringManager has no way to see one, which is exactly the pain project.md records for both
personas: applicant and role state living in inboxes and spreadsheets instead of the system,
and the HiringManager depending on the Recruiter for every status update.

Spec `0002` built the identity and policy layer this depends on — the `Recruiter` and
`HiringManager` roles, and the `CandidateOnly` / `StaffOnly` / `RecruiterOnly` /
`HiringManagerOnly` authorization policies — but nothing yet exercises them. Its edge case
`E-9` ("candidate hits `/staff` → redirect") describes intended behaviour for a route that
does not exist and middleware that was never written; `ui/staff` remains the only component
in `architecture.md` with no owning spec and no code. This spec is the first real consumer of
that authentication and authorization work.

It is also the first spec to touch the public-facing half of hiring: candidates today cannot
discover a role at all without a Recruiter emailing them a description, which contradicts
project.md's Candidate persona goal of applying without recruiter involvement (S-1). Finally,
the domain glossary leaves open "whether stages are global or per-requisition" — a decision
this spec settles at the schema level (not the feature level) so the future pipeline spec
lands on a fixed shape instead of a SQLite table rebuild.

## Goals

- **G-1** A Recruiter can open, edit, publish, unpublish, and close a Requisition — putting
  role state in the system instead of an inbox.
- **G-2** A HiringManager can see every Requisition and its current status without asking a
  Recruiter.
- **G-3** A Candidate can browse and read published roles anonymously, with no staff action
  required.
- **G-4** `ui/staff` exists as a real, role-gated route group, closing the gap `0002`'s `E-9`
  described but never implemented.
- **G-5** The Stage-ownership question (project.md:67) is fixed before any Stage data exists,
  so the pipeline spec inherits a schema instead of a migration.

## Non-Goals

- **Pipeline/Stage management UI, ordering, and candidate advancement through stages** — the
  requester explicitly deferred this to the spec that lands the pipeline on SQLite; this spec
  only fixes Stage's ownership shape (FR-14), not its behaviour.
- **The `Application` entity and the apply flow** — nothing exists yet for a candidate to
  apply against; that relationship is a separate spec.
- **A requisition approval workflow for HiringManager** — not requested; HiringManager access
  in this spec is read-only (see Clarifications C-2).
- **Reopening a closed Requisition** — only the draft ↔ published reversal was requested;
  closed remains terminal.
- **Deleting a Requisition** — closed is the terminal, retiring state; there is no delete
  endpoint.
- **Portal filtering beyond keyword search** (location, salary range, faceted filters) — only
  keyword search across published requisitions was requested.

## Users & Personas

| Persona | Need this feature serves |
|---|---|
| Recruiter | Opens, edits, publishes, unpublishes, and closes requisitions — owns them end to end. |
| HiringManager | Sees every requisition and its current status without depending on a Recruiter. |
| Candidate | Browses and reads published roles anonymously, without registering or contacting staff. |

## Functional Requirements

- **FR-1** — A Recruiter can create a Requisition, which is created in `draft` status.
- **FR-2** — A Recruiter can edit a Requisition's content fields while it is in `draft` or
  `published` status.
- **FR-3** — A Recruiter can publish a `draft` Requisition, transitioning it to `published` and
  making it immediately visible on the public portal.
- **FR-4** — A Recruiter can revert a `published` Requisition back to `draft` (unpublish),
  immediately removing it from the public portal.
- **FR-5** — A Recruiter can close a `published` Requisition, transitioning it to `closed` and
  removing it from the public portal.
- **FR-6** — The system rejects any Requisition status transition other than
  `draft → published`, `published → draft`, or `published → closed`; `closed` accepts no
  further transitions.
- **FR-7** — Staff (Recruiter or HiringManager) can list and view Requisitions of any status
  via `api/requisition`.
- **FR-8** — Write operations on `api/requisition` (create, edit, publish, unpublish, close)
  are restricted to the `Recruiter` policy; a HiringManager or Candidate request is rejected
  with HTTP 403.
- **FR-9** — `ui/staff` routes are reachable only by a session holding the `Recruiter` or
  `HiringManager` role; any other session, including anonymous, is redirected away before the
  route renders.
- **FR-10** — An anonymous visitor can list `published` Requisitions on `ui/portal`, optionally
  filtered by a keyword that matches the title or description.
- **FR-11** — The public Requisition list is paginated, defaulting to 20 results per page with
  a maximum of 50 per page.
- **FR-12** — An anonymous visitor can view the detail of a single `published` Requisition on
  `ui/portal`.
- **FR-13** — Requisitions that are `draft` or `closed` — including a Requisition that has been
  unpublished back to `draft` — are not retrievable through any public portal endpoint (list
  or detail).
- **FR-14** — Each Requisition owns its own independent set of Stage rows; no Stage row is
  shared or reused across more than one Requisition.

## Non-Functional Requirements

- **NFR-1** — The public Requisition list endpoint enforces a hard maximum page size of 50,
  bounding response payload size and preventing a single anonymous request from retrieving the
  entire table at once.
- **NFR-2** — Public portal GET endpoints (list, detail) never open a write transaction,
  preserving SQLite's single-writer path for registration and application traffic per
  `architecture.md`'s documented one-writer constraint.

## Acceptance Criteria

Each criterion is independently testable and traces to one or more FRs.

- **AC-1** *(FR-1)*
  - **Given** an authenticated Recruiter submitting a valid requisition payload
  - **When** they call the create endpoint
  - **Then** the API returns HTTP 201 Created and a Requisition record exists with status
    `draft`.

- **AC-2** *(FR-1, FR-8)*
  - **Given** an authenticated HiringManager or Candidate
  - **When** they call the create endpoint
  - **Then** the API returns HTTP 403 Forbidden and no Requisition record is created.

- **AC-3** *(FR-2)*
  - **Given** a `draft` Requisition
  - **When** a Recruiter edits its content fields
  - **Then** the fields are updated and status remains `draft`.

- **AC-4** *(FR-2, FR-3)*
  - **Given** a `published` Requisition
  - **When** a Recruiter edits its content fields
  - **Then** the fields are updated, status remains `published`, and the next portal detail
    fetch reflects the new content.

- **AC-5** *(FR-2)*
  - **Given** a `closed` Requisition
  - **When** a Recruiter attempts to edit its content fields
  - **Then** the API returns HTTP 409 Conflict and no fields are changed.

- **AC-6** *(FR-3)*
  - **Given** a `draft` Requisition
  - **When** a Recruiter publishes it
  - **Then** status becomes `published` and it appears in the next public portal list and
    detail fetch.

- **AC-7** *(FR-4, FR-13)*
  - **Given** a `published` Requisition
  - **When** a Recruiter unpublishes it back to `draft`
  - **Then** status becomes `draft` and a subsequent public portal detail request for it
    returns HTTP 404 Not Found.

- **AC-8** *(FR-3, FR-4)*
  - **Given** a Requisition that was published, unpublished to `draft`, and edited while in
    `draft`
  - **When** a Recruiter re-publishes it
  - **Then** status becomes `published` and the portal detail reflects the edits made while it
    was in `draft`.

- **AC-9** *(FR-5, FR-13)*
  - **Given** a `published` Requisition
  - **When** a Recruiter closes it
  - **Then** status becomes `closed` and it no longer appears on the public portal list or
    detail.

- **AC-10** *(FR-6)*
  - **Given** a `draft` Requisition
  - **When** a Recruiter attempts to transition it directly to `closed`
  - **Then** the API returns HTTP 409 Conflict and status remains `draft`.

- **AC-11** *(FR-6)*
  - **Given** a `closed` Requisition
  - **When** a Recruiter attempts to transition it to `draft` or `published`
  - **Then** the API returns HTTP 409 Conflict and status remains `closed`.

- **AC-12** *(FR-7)*
  - **Given** an authenticated HiringManager
  - **When** they list requisitions via `api/requisition`
  - **Then** the API returns HTTP 200 OK including requisitions of every status.

- **AC-13** *(FR-7, FR-8)*
  - **Given** an authenticated Candidate
  - **When** they call any `api/requisition` endpoint
  - **Then** the API returns HTTP 403 Forbidden.

- **AC-14** *(FR-9)*
  - **Given** an anonymous visitor or an authenticated Candidate session
  - **When** they navigate directly to a `/staff` route
  - **Then** they are redirected away before the route renders.

- **AC-15** *(FR-9)*
  - **Given** an authenticated Recruiter or HiringManager session
  - **When** they navigate to a `/staff` route
  - **Then** the route renders normally.

- **AC-16** *(FR-10)*
  - **Given** at least one `published` Requisition whose title or description contains the
    keyword "engineer"
  - **When** an anonymous visitor lists requisitions with that keyword
  - **Then** only `published` requisitions matching the keyword are returned.

- **AC-17** *(FR-10)*
  - **Given** a keyword that matches no `published` Requisition
  - **When** an anonymous visitor searches with it
  - **Then** the API returns HTTP 200 OK with an empty result list, not an error.

- **AC-18** *(FR-11)*
  - **Given** more than 20 `published` Requisitions
  - **When** an anonymous visitor requests the list with no page parameter
  - **Then** the first page of 20 results is returned with pagination metadata (total count,
    page, page size).

- **AC-19** *(FR-11)*
  - **Given** a page parameter beyond the last available page
  - **When** an anonymous visitor requests it
  - **Then** the API returns HTTP 200 OK with an empty result list and pagination metadata
    reflecting the true total, not an error.

- **AC-20** *(FR-10, FR-11)*
  - **Given** a keyword search combined with a page parameter
  - **When** an anonymous visitor requests it
  - **Then** the API filters by the keyword first and paginates over the filtered result set.

- **AC-21** *(FR-12)*
  - **Given** a `published` Requisition
  - **When** an anonymous visitor requests its detail
  - **Then** the API returns HTTP 200 OK with its public content.

- **AC-22** *(FR-13)*
  - **Given** a Requisition that is `draft`, `closed`, or currently unpublished
  - **When** an anonymous visitor requests its detail by id
  - **Then** the API returns HTTP 404 Not Found.

- **AC-23** *(FR-14)*
  - **Given** two distinct Requisitions each with Stage rows
  - **When** their stage sets are inspected
  - **Then** no Stage row belongs to or is referenced by more than one Requisition.

- **AC-24** *(FR-11)*
  - **Given** a non-numeric or negative page parameter
  - **When** an anonymous visitor requests the list
  - **Then** the API returns HTTP 400 Bad Request ProblemDetails without executing the query.

## Edge Cases & Error States

| # | Situation | Expected behaviour |
|---|---|---|
| E-1 | A candidate has a published requisition's detail page open in another tab when a Recruiter unpublishes it | The next request for that detail (refresh, deep link) returns 404; the already-rendered tab is not proactively invalidated — no push mechanism exists (AC-7). |
| E-2 | Recruiter re-publishes a requisition that was unpublished and edited while in draft | Portal shows the edited content immediately on republish (AC-8). |
| E-3 | Recruiter attempts `draft → closed` directly | Rejected as an invalid transition (AC-10). |
| E-4 | Recruiter attempts any transition out of `closed` | Rejected — closed is terminal, no reopen path in this spec (AC-11). |
| E-5 | Recruiter attempts to edit a `closed` requisition | Rejected with 409 (AC-5). |
| E-6 | Anonymous keyword search matches zero requisitions | 200 with an empty list, not an error (AC-17). |
| E-7 | Anonymous list request with a page beyond the last page | 200 with an empty list and the correct total (AC-19). |
| E-8 | Anonymous list request with an invalid (non-numeric or negative) page parameter | 400 Bad Request (AC-24). |
| E-9 | Unauthenticated or Candidate session navigates directly to `/staff` or a staff subpath | Redirected before render, implementing `0002`'s `E-9` (AC-14). |
| E-10 | A candidate guesses a `draft` or `closed` requisition's id and requests its portal detail directly | 404, identical response whether the id doesn't exist or merely isn't public — no existence leak (AC-22). |
| E-11 | Recruiter publishes a requisition missing required content (e.g. empty title) | Rejected by the same field-level validation applied on create/edit; there is no separate publish-readiness gate beyond normal validation (AC-1, AC-6). |

## Data Touched

| Entity | New / Existing | Notes |
|---|---|---|
| `Requisition` | New | Content fields, status (`draft`/`published`/`closed`), timestamps; exact columns decided in `plan/erd.md`. |
| `Stage` | New | One independent set of rows per Requisition per FR-14. Full CRUD, ordering, and pipeline behaviour are deferred to the pipeline spec — this spec only fixes the ownership shape. |

## Impacted Components

| Component | Change |
|---|---|
| `api/requisition` | New. Staff CRUD and lifecycle-transition endpoints (create, edit, publish, unpublish, close, list, get) plus anonymous public list/detail endpoints. |
| `service/requisition` | New. Enforces the Requisition state machine, the transaction boundary, and the Stage ownership rule (FR-14). |
| `db/requisition` | New. EF Core entities, configuration, and migration for `Requisition` and `Stage`. |
| `ui/staff` | New — first code behind this previously-unowned component. Route group plus role-gating middleware implementing `0002`'s `E-9`. |
| `ui/portal` | Modified. Adds public requisition list (with keyword search and pagination) and detail pages. |

## Out of Scope

- Pipeline/Stage management UI, ordering, and candidate advancement through stages — deferred
  to the pipeline spec.
- The `Application` entity and the apply flow — no requisition-application relationship exists
  yet.
- Any requisition approval workflow for HiringManager — access in this spec is read-only.
- Reopening a closed Requisition — only draft ↔ published reversal was requested.
- Deleting a Requisition — closed is the terminal, retiring state.
- Portal filtering beyond keyword search (location, salary range, faceted filters).

## Open Questions

None — all clarifications resolved, see `clarifications.md`.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0002` (User Authentication and Refresh Token Flow) | 1 | Defines the `Recruiter`/`HiringManager`/`Candidate` roles and `RecruiterOnly`/`StaffOnly`/`HiringManagerOnly`/`CandidateOnly` policies this spec's authorization (FR-7, FR-8, FR-9) consumes unchanged; its `E-9` is the edge case this spec finally implements. |
| `0001` (Project Scaffolding and Walking Skeleton) | 1 | Established the `ui/portal` route-group pattern and the `ui/bff` proxy/invoke seam this spec's portal pages and API calls reuse without modification. |

Tier 0 was read in full: `meta/architecture.md`, `meta/tech-stack.md`, `meta/coding-standards.md`, `index.md`.
Considered and skipped: none — only two prior specs exist and both were relevant.
Cap reached: no (2 prior specs loaded).
