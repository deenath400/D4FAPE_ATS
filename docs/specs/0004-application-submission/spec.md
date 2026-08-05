---
id: 0004
slug: application-submission
title: Application Submission and CV Upload
status: specified
components: [api/application, service/application, db/application, shared/storage, ui/portal, ui/staff]
entities: [Application, CvAttachment]
depends_on: [0002, 0003]
created: 2026-08-06
updated: 2026-08-06
---

# Application Submission and CV Upload

## Problem & Context

`0003` gave the ATS a `Requisition` a Candidate can browse and read anonymously on `ui/portal`,
but explicitly stopped short of letting anyone apply to one — its own Non-Goals name "the
`Application` entity and the apply flow" as "a separate spec." Today a Candidate who finds a
published role has no way to act on it inside the system at all; per `project.md`'s Candidate
persona, they still have to "chase" a Recruiter outside the product, which directly contradicts
success criterion S-1 ("a candidate can apply without recruiter involvement"). The Recruiter and
HiringManager personas are equally blocked: `project.md` records their pain today as "applicant
state lives in inboxes and spreadsheets," and nothing yet writes an applicant's existence into
the database at all.

This spec gives the ATS its first write path from the public portal into staff-visible data: a
Candidate submits an Application, with a CV, against a specific `published` Requisition. It is
also the first spec to touch `shared/storage`, which `architecture.md` lists with its backing
store marked `TBD` — exactly the kind of unresolved architectural point `0003` settled for
`Stage` ownership at the schema level rather than leaving it for later. Settling it here, before
any CV file exists on disk, avoids a rebuild the same way `0003` avoided one for `Stage`.

Deliberately not addressed here: what happens to an Application after it is submitted beyond a
Candidate or Staff member being able to see that it exists. Pipeline progression — advancing or
rejecting an applicant through stages (`project.md` S-2, S-4) — depends on `Stage` behaviour
`0003` explicitly deferred to a future pipeline spec; this spec cannot and does not get ahead of
that.

## Goals

- **G-1** A Candidate can submit an Application, including a CV, to a `published` Requisition
  without any Recruiter or HiringManager action (`project.md` S-1).
- **G-2** An uploaded CV persists durably — surviving a backend process restart — and remains
  retrievable by the Candidate who submitted it and by Staff.
- **G-3** At most one Application exists per Candidate/Requisition pair, so submission data
  cannot silently duplicate.
- **G-4** A Candidate can see confirmation that their submission exists via a simple list of
  their own Applications.
- **G-5** A Recruiter or HiringManager can confirm Applications are arriving against a
  Requisition via a minimal list, without any pipeline/stage machinery existing yet.
- **G-6** `shared/storage` ships its first real implementation, resolving `tech-stack.md`'s
  "Object storage: TBD" line at the schema/interface level.

## Non-Goals

- **Pipeline/stage progression** — advancing, rejecting, or moving an Application through a
  Requisition's `Stage` set is deferred to the pipeline spec `0003` already carved out; no
  `Stage` row is read or written by this spec.
- **Full candidate-visible status tracking** — the Candidate's own list (FR-8) shows only that
  an Application was submitted and when, not a per-stage status, because no pipeline state exists
  yet to display.
- **Staff decisioning** — advancing, rejecting, or annotating an Application is pipeline-spec
  territory; this spec's staff endpoint is read-only (FR-10, FR-11).
- **Application withdrawal or cancellation** — "submission" was requested, not lifecycle
  management after the fact; there is no delete/cancel endpoint.
- **Malware/virus scanning of uploaded files** — `project.md` names untrusted file upload as a
  "day-one concern, not later hardening," and this spec accepts that risk rather than silently
  ignoring it: only file extension, declared content type, and size are validated (FR-3); content
  is not scanned for malicious payloads.
- **Cloud/object storage backend** — CV files persist to local disk behind the `shared/storage`
  interface (Clarification C-1); swapping the backing store later is an implementation change
  behind that interface, not a spec change.
- **CV formats other than PDF** — DOC/DOCX and other formats are rejected (Clarification C-5);
  widening accepted formats is additive.
- **Staff submitting an Application on a Candidate's behalf** — submission is Candidate-only,
  consistent with `0003`'s write-restriction pattern applied to a new entity.

## Users & Personas

| Persona | Need this feature serves |
|---|---|
| Candidate | Applies with a CV to a role they found on the portal, and can see that the submission went through, without contacting a recruiter. |
| Recruiter | Sees that Applications are arriving against a Requisition they opened, without asking the candidate directly. |
| HiringManager | Same read visibility into arriving Applications as the Recruiter (consistent with `0003`'s `StaffOnly` read-access precedent, C-2). |

## Functional Requirements

- **FR-1** — A Candidate can submit an Application to a Requisition that is currently
  `published`, providing a CV file.
- **FR-2** — Application submission requires exactly one CV file; a submission request with no
  file attached is rejected and no Application record is created.
- **FR-3** — The system accepts only PDF-format CV files up to 5 MB; any other file type or a
  file exceeding 5 MB is rejected and no Application record is created.
- **FR-4** — The system rejects an Application submission whose target Requisition is not
  currently `published` (whether `draft`, `closed`, or non-existent), returning the same
  not-found response the public portal already returns for non-public requisitions (`0003`
  FR-13).
- **FR-5** — The system permits at most one Application per Candidate per Requisition; a second
  submission attempt by the same Candidate against the same Requisition is rejected and no
  second Application record is created.
- **FR-6** — Only an authenticated session holding the `Candidate` role can submit an
  Application; anonymous and Staff (`Recruiter`/`HiringManager`) sessions are rejected.
- **FR-7** — Every submitted Application's CV file is persisted to durable storage that survives
  a backend process restart, addressable by the Application it belongs to.
- **FR-8** — A Candidate can list their own submitted Applications, seeing at minimum the
  Requisition title and the submission date for each.
- **FR-9** — A Candidate can retrieve/download the CV file of their own submitted Application.
- **FR-10** — Staff (Recruiter or HiringManager) can list the Applications submitted against a
  given Requisition, seeing at minimum the applying Candidate's identity, the submission date,
  and a CV download link for each.
- **FR-11** — Staff (Recruiter or HiringManager) can download the CV file of any Application
  submitted against a Requisition.
- **FR-12** — A Candidate cannot list or retrieve another Candidate's Applications or CV files.
- **FR-13** — Every Application records which Requisition and which Candidate it belongs to, and
  the UTC timestamp it was submitted.

## Non-Functional Requirements

- **NFR-1** — Application submission is atomic with respect to CV validation: if file-type or
  file-size validation fails, no Application row is written — an Application never exists
  without a valid, persisted CV.
- **NFR-2** — CV download endpoints (Candidate-own and Staff) authorize every request against the
  requester's identity and role; no CV is reachable by a guessable or sequential identifier alone
  (FR-12).
- **NFR-3** — Given `architecture.md`'s documented SQLite single-writer constraint, and that
  candidate-portal writes "concentrate exactly when traffic spikes," the Application submission
  database transaction remains open only for the row insert itself — the CV file write does not
  extend how long the SQLite write lock is held.

## Acceptance Criteria

Each criterion is independently testable and traces to one or more FRs.

- **AC-1** *(FR-1, FR-7, FR-13)*
  - **Given** an authenticated Candidate and a `published` Requisition
  - **When** they submit an Application with a valid PDF CV (≤ 5 MB)
  - **Then** the API returns HTTP 201 Created, an Application record exists referencing the
    Candidate, the Requisition, and a UTC submission timestamp, and the CV file is persisted.

- **AC-2** *(FR-2)*
  - **Given** an authenticated Candidate and a `published` Requisition
  - **When** they submit an Application with no CV file attached
  - **Then** the API returns HTTP 400 Bad Request and no Application record is created.

- **AC-3** *(FR-3)*
  - **Given** an authenticated Candidate and a `published` Requisition
  - **When** they submit an Application with a non-PDF file (e.g. `.docx`)
  - **Then** the API returns HTTP 400 Bad Request (or 415 Unsupported Media Type) and no
    Application record is created.

- **AC-4** *(FR-3)*
  - **Given** an authenticated Candidate and a `published` Requisition
  - **When** they submit an Application with a PDF file exceeding 5 MB
  - **Then** the API returns HTTP 400 Bad Request (or 413 Payload Too Large) and no Application
    record is created.

- **AC-5** *(FR-4)*
  - **Given** a Requisition in `draft` status
  - **When** an authenticated Candidate submits an Application against it
  - **Then** the API returns HTTP 404 Not Found and no Application record is created.

- **AC-6** *(FR-4)*
  - **Given** a Requisition in `closed` status
  - **When** an authenticated Candidate submits an Application against it
  - **Then** the API returns HTTP 404 Not Found and no Application record is created.

- **AC-7** *(FR-4)*
  - **Given** a Requisition id that does not exist
  - **When** an authenticated Candidate submits an Application against it
  - **Then** the API returns HTTP 404 Not Found, identical to AC-5/AC-6 — no existence leak.

- **AC-8** *(FR-5)*
  - **Given** a Candidate who already has an Application against Requisition X
  - **When** they submit a second Application against Requisition X
  - **Then** the API returns HTTP 409 Conflict and no second Application record is created.

- **AC-9** *(FR-5)*
  - **Given** two distinct Candidates each submitting an Application against the same
    Requisition
  - **When** both submissions are made
  - **Then** both succeed independently with their own Application records — the one-per-pair
    rule is scoped per Candidate, not per Requisition.

- **AC-10** *(FR-6)*
  - **Given** an unauthenticated (anonymous) request
  - **When** it calls the Application submission endpoint
  - **Then** the API returns HTTP 401 Unauthorized and no Application record is created.

- **AC-11** *(FR-6)*
  - **Given** an authenticated Recruiter or HiringManager session
  - **When** it calls the Application submission endpoint
  - **Then** the API returns HTTP 403 Forbidden and no Application record is created.

- **AC-12** *(FR-8)*
  - **Given** a Candidate with two submitted Applications
  - **When** they list their own Applications
  - **Then** the API returns HTTP 200 OK with both, each showing its Requisition title and
    submission date.

- **AC-13** *(FR-8)*
  - **Given** a Candidate with zero submitted Applications
  - **When** they list their own Applications
  - **Then** the API returns HTTP 200 OK with an empty list, not an error.

- **AC-14** *(FR-9)*
  - **Given** a Candidate who owns Application A
  - **When** they request Application A's CV download
  - **Then** the API returns HTTP 200 OK with the CV file bytes.

- **AC-15** *(FR-9, FR-12)*
  - **Given** a Candidate who does not own Application A
  - **When** they request Application A's CV download
  - **Then** the API returns HTTP 403 Forbidden and the file is not returned.

- **AC-16** *(FR-10)*
  - **Given** a Requisition with two submitted Applications
  - **When** a Recruiter lists Applications for that Requisition
  - **Then** the API returns HTTP 200 OK with both, each showing the applying Candidate's
    identity, submission date, and a CV download link.

- **AC-17** *(FR-10)*
  - **Given** an authenticated HiringManager
  - **When** they list Applications for a Requisition
  - **Then** the API returns HTTP 200 OK, consistent with `0003`'s `StaffOnly` read-access
    precedent (C-2) applied to this new endpoint.

- **AC-18** *(FR-10)*
  - **Given** a Requisition with zero submitted Applications
  - **When** Staff lists Applications for it
  - **Then** the API returns HTTP 200 OK with an empty list, not an error.

- **AC-19** *(FR-10, FR-6)*
  - **Given** an authenticated Candidate session
  - **When** it calls the staff Applications-list endpoint for a Requisition
  - **Then** the API returns HTTP 403 Forbidden.

- **AC-20** *(FR-11)*
  - **Given** an authenticated Recruiter
  - **When** they request the CV download for an Application on a Requisition
  - **Then** the API returns HTTP 200 OK with the CV file bytes.

- **AC-21** *(FR-12)*
  - **Given** Candidate A and Candidate B, each with their own Applications
  - **When** Candidate A calls any candidate-facing Application or CV endpoint referencing
    Candidate B's data
  - **Then** the API returns HTTP 403 Forbidden (or 404), never Candidate B's data.

- **AC-22** *(FR-13)*
  - **Given** a successfully submitted Application
  - **When** its record is inspected
  - **Then** it references the correct Requisition id, the correct Candidate id, and a UTC
    submission timestamp.

## Edge Cases & Error States

| # | Situation | Expected behaviour |
|---|---|---|
| E-1 | Same Candidate fires two near-simultaneous submissions against the same Requisition (race on the duplicate check) | Only one Application record survives; the losing request receives HTTP 409, enforced structurally (e.g. a uniqueness constraint), not by application-level check timing alone (AC-8). |
| E-2 | A Recruiter unpublishes or closes a Requisition between the Candidate loading the apply form and submitting it | Submission is rejected with HTTP 404, identical to AC-5/AC-6 — no stale-form success against a no-longer-public Requisition. |
| E-3 | CV upload is interrupted mid-transfer (network drop) | No Application record is created; a partially received file is not referenced by any record and is not retrievable. |
| E-4 | The storage write for a CV fails (e.g. disk full) during an otherwise valid submission | The Application creation fails atomically — no orphaned Application record without a persisted CV (NFR-1) — and the API returns HTTP 500 ProblemDetails; the Candidate can retry. |
| E-5 | Staff requests the Applications list for a Requisition id that does not exist | HTTP 404 Not Found. |
| E-6 | A Candidate or Staff member requests a CV download for an Application id that does not exist | HTTP 404 Not Found. |
| E-7 | The same Candidate applies to two different Requisitions | Both succeed independently — the one-per-pair rule (FR-5) is scoped to a single Requisition, not the Candidate globally. |

## Data Touched

| Entity | New / Existing | Notes |
|---|---|---|
| `Application` | New | Links a Candidate and a Requisition, records submission timestamp; exact columns decided in `plan/erd.md`. |
| `CvAttachment` | New | Metadata about the persisted CV file (e.g. storage reference, original filename, size, content type) behind the `shared/storage` interface; exact shape decided in `plan/erd.md`. |
| `Requisition` | Existing | Referenced, not modified — Application's target; the existing `published`-only gate (`0003` FR-13) is reused unchanged (FR-4). |
| `User` (`AspNetUsers`) | Existing | Referenced, not modified — the Candidate identity an Application belongs to, reused unchanged from `0002`. |

## Impacted Components

| Component | Change |
|---|---|
| `api/application` | New. Candidate submission endpoint, Candidate own-Applications-list endpoint, Candidate CV download endpoint, Staff Requisition-scoped Applications-list endpoint, Staff CV download endpoint. |
| `service/application` | New. Enforces submission eligibility (Requisition must be `published`), the one-Application-per-Candidate-per-Requisition rule, CV file validation orchestration, and authorization checks for CV access. |
| `db/application` | New. EF Core entities, configuration, and migration for `Application` and `CvAttachment`. |
| `shared/storage` | New. CV file persistence interface plus a local-disk-backed implementation under the backend's app-data directory (Clarification C-1) — the first owning spec, resolving `tech-stack.md`'s "Object storage: TBD" line and `architecture.md`'s "backing store TBD" note. |
| `ui/portal` | Modified. Adds an apply flow (application form with CV file input) reachable from the Requisition detail page, and a "My Applications" list page for the authenticated Candidate. |
| `ui/staff` | Modified. Adds a per-Requisition Applications list view (Candidate identity, submitted date, CV download link) — no stage grouping or decisioning UI. |

## Out of Scope

- Pipeline/stage progression — advancing or rejecting an Application through a Requisition's
  `Stage` set — deferred to the pipeline spec.
- Full candidate-visible per-stage status tracking — this spec's Candidate list shows submission
  confirmation only.
- Staff decisioning (advance, reject, annotate) on an Application.
- Application withdrawal or cancellation by the Candidate.
- Malware/virus scanning of uploaded CV files.
- Cloud/object storage backend for CVs — local disk only in this spec.
- CV formats other than PDF.
- Staff submitting an Application on a Candidate's behalf.

## Open Questions

None — all clarifications resolved, see `clarifications.md`.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0003` (Requisition Management) | 1 | Owns the `Requisition` entity this spec's Application targets; reused its `published`-only visibility gate (FR-13) and 404 no-existence-leak pattern (AC-22) for FR-4/AC-5..AC-7. Its own Non-Goals explicitly named this spec as the deferred "apply flow." |
| `0002` (User Authentication and Refresh Token Flow) | 1 | Owns the `Candidate`/`Recruiter`/`HiringManager` roles and JWT/policy conventions this spec's authorization (FR-6, FR-10-FR-12) consumes unchanged; reused its RFC 7807 ProblemDetails error convention. |
| `0001` (Project Scaffolding and Walking Skeleton) | 1 | Established the `ui/portal` route-group pattern and `ui/bff` proxy/invoke seam this spec's apply flow and "My Applications" page reuse without modification. |

Tier 0 was read in full: `meta/architecture.md`, `meta/tech-stack.md`, `meta/coding-standards.md`, `index.md`.
Considered and skipped: none — all three prior specs scored above threshold and all three were relevant.
Cap reached: no (3 prior specs loaded, at the cap but not exceeding it).
