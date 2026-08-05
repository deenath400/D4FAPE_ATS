# Clarifications — 0003 Requisition Management

A verbatim log of ambiguities raised during `/specify` and how they were resolved. This is an
audit trail: when someone later asks "why does it work this way", the answer is here.

---

## Round 1 — 2026-08-05

### C-1 — Is `Stage` global or per-requisition?

**Ambiguity.** `project.md:67` explicitly leaves this open. It fixes the ERD shape `Requisition`
and the future pipeline spec both depend on; SQLite migrations are table rebuilds, so getting
this wrong is expensive to reverse once Stage data exists.

**Options presented.**
1. Per-requisition — each Requisition owns its own independent stage rows.
2. Global — one org-wide stage catalog reused unmodified by every requisition.
3. Global template + per-requisition override — a default catalog cloned into requisition-owned
   rows at creation, editable after.

**Answer.** "Per-requisition (Recommended)" — go with the recommended option: each Requisition
owns its own independent stage rows.

**Impact.** FR-14, AC-23, Data Touched (`Stage`).

---

### C-2 — Can HiringManager read Requisitions via `api/requisition`?

**Ambiguity.** Changes the authorization matrix and whether `ui/staff` requisition pages render
anything for a HiringManager session.

**Options presented.**
1. HiringManager gets read access (`StaffOnly` policy for GET, `RecruiterOnly` for writes).
2. HiringManager has no access to `api/requisition` at all.
3. HiringManager can also create/edit.

**Answer.** "Read access for HiringManager (Recommended)" — GET gated by `StaffOnly` (Recruiter
or HiringManager), writes stay `RecruiterOnly`.

**Impact.** FR-7, FR-8, AC-12, AC-13.

---

### C-3 — Are lifecycle transitions strictly forward, or can a requisition be unpublished / reopened?

**Ambiguity.** Determines how many transition paths the state machine must validate and whether
reversal edge cases need acceptance criteria.

**Options presented.**
1. Strictly forward, no reversal.
2. `published` can revert to `draft`.
3. `closed` can reopen to `published`.

**Answer.** "Published can revert to draft" — the user chose this over the recommended
strictly-forward option. The lifecycle is therefore not purely linear: in addition to
`draft → published` and `published → closed`, `published → draft` (unpublish) is a valid
transition. `closed` was not asked about and remains terminal — no reopen path.

**Impact.** FR-3, FR-4, FR-5, FR-6, AC-6 through AC-11, E-1, E-2, E-3, E-4. The moment a
`published` Requisition reverts to `draft`, it immediately stops being publicly readable,
consistent with FR-13's draft/closed exclusion (AC-7). Unpublishing has no side effects beyond
the status and visibility change — it does not delete or alter content, and requires no
confirmation step at the API level (assumption A-9). Re-publishing after unpublish restores
public visibility with whatever content was last saved, including edits made while in draft
(AC-8).

---

### C-4 — Can a `published` Requisition still be edited?

**Ambiguity.** Affects whether the edit requirement applies uniformly or needs a status-gated
variant, and whether the portal detail page can change under a candidate's feet.

**Options presented.**
1. Fully editable while published.
2. No edits once published, status-transition only.
3. Limited fields editable while published.

**Answer.** "Fully editable (Recommended)" — go with the recommended option: content fields stay
editable regardless of status (draft or published; closed is separately assumed read-only, see
A-7).

**Impact.** FR-2, AC-3, AC-4, AC-5.

---

### C-5 — Does the public portal listing need pagination/search, or is a flat list sufficient?

**Ambiguity.** Changes the listing endpoint's shape and the size of the portal work; affects
whether empty-result and out-of-range-page behaviour need acceptance criteria.

**Options presented.**
1. Flat list, no pagination or search.
2. Basic pagination only.
3. Pagination + keyword search.

**Answer.** "Pagination + keyword search" — the user chose this over the recommended flat-list
option. FR-9 (renumbered FR-10/FR-11 in the final spec) supports both keyword search and
pagination, combined: search filters first, pagination applies over the filtered set (AC-20).
Searchable fields are title and description (assumption A-8, the user's call was not specified
further). Default page size is 20, maximum 50 (assumption A-10). Empty search results and
out-of-range page requests both return HTTP 200 with an empty list, not an error (AC-17,
AC-19); an invalid page parameter returns HTTP 400 (AC-24, E-8).

**Impact.** FR-10, FR-11, NFR-1, AC-16 through AC-20, AC-24, E-6, E-7, E-8.

---

## Assumptions Made Without Asking

Ambiguities resolved by judgement rather than by asking, because a reasonable default existed
and the alternatives would not have changed the work materially. Listed so they can be
challenged.

| # | Assumption | Default chosen | Reversal cost if wrong |
|---|---|---|---|
| A-1 | Requisition's exact content fields are undetermined at spec level | Whatever `plan/erd.md` decides is sufficient for public display (e.g. title, description) | Low — no schema exists yet |
| A-2 | No approval workflow gates publishing | Recruiter publishes/unpublishes/closes unilaterally | Medium — adding a workflow later means a new status/actor model |
| A-3 | No soft- or hard-delete of Requisitions | `closed` is the terminal, retiring state; unaffected by C-3 since unpublish targets `draft`, not `closed` | Low — delete is additive later |
| A-4 | No optimistic concurrency control on requisition edits | Last write wins, acceptable given SQLite's single-writer path and low expected concurrent-edit volume | Medium — would need a version/row-token column added later |
| A-5 | Public detail responses for non-public requisitions don't distinguish "doesn't exist" from "not public" | Generic 404 in both cases, avoiding existence/status leakage | Low — a friendlier message is a later UI change |
| A-6 | This spec only fixes Stage's ownership shape, not its behaviour | No Stage CRUD, ordering, or pipeline-board UI ships here — deferred to the pipeline spec | N/A — explicitly deferred by the requester |
| A-7 | Whether `closed` Requisitions remain editable was not asked | `closed` is read-only; FR-2 (edit) applies only to `draft` and `published` | Low — relaxing this later is additive |
| A-8 | Which fields keyword search matches was not fully specified ("title is the obvious one — description too, your call") | Search matches `title` and `description` only | Low — adding more searchable fields later is additive |
| A-9 | Whether unpublish requires confirmation or has side effects was not specified | No confirmation step at the API level; no side effects beyond status and portal-visibility change — content is untouched | Low — a UI confirm dialog can be added without an API change |
| A-10 | Default/max portal page size was not specified beyond "pagination" | Default 20, maximum 50 | Low — a config-level change |

## Deferred

Questions raised but explicitly postponed, with where they were recorded.

| # | Question | Deferred to |
|---|---|---|
| D-1 | Reopening a closed Requisition | Not asked about in C-3; revisit in a future spec if a real need arises |
| D-2 | Pipeline/Stage CRUD, ordering, and board UI | The pipeline spec — explicitly out of scope here per the original request |
| D-3 | A requisition approval workflow for HiringManager | Not requested; revisit only if HiringManager needs authorship, not just read access |
