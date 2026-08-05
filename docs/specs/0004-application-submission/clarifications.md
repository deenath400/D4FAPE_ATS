# Clarifications — 0004 Application Submission and CV Upload

A verbatim log of ambiguities raised during `/specify` and how they were resolved. This is an
audit trail: when someone later asks "why does it work this way", the answer is here.

---

## Round 1 — 2026-08-06

### C-1 — Where must uploaded CV files durably persist?

**Ambiguity.** `tech-stack.md` lists "Object storage: TBD" and `architecture.md` describes
`shared/storage` as "CV and attachment persistence behind an interface; backing store TBD." This
spec is the first to actually write a CV file, so the backing store had to be settled — it
changes the durability guarantees (NFR) and what a restart or redeploy can lose.

**Options presented.**
1. Local disk under the backend's app-data directory, behind the `shared/storage` interface —
   matches the project's "defer infra, keep an interface" pattern; cheap to swap for cloud
   storage later.
2. SQLite BLOB column — couples file storage to the database file, growing it and working against
   SQLite's single-writer constraint on every upload.
3. Cloud/object storage (a specific provider would need choosing) — commits to a vendor before
   any hosting/infra decision exists (`architecture.md` still lists Infrastructure as "TBD").

**Answer.** "Local disk under the backend's app-data directory, behind the shared/storage
interface (Recommended option accepted.)"

**Impact.** FR-7, NFR-1, Impacted Components (`shared/storage`), Out of Scope (cloud storage
explicitly excluded).

---

### C-2 — Does this spec give Staff any read access to submitted Applications?

**Ambiguity.** Determines whether `api/application`/`service/application` need any staff-facing
endpoint in this spec, or whether all staff visibility waits for the pipeline spec.

**Options presented.**
1. A minimal endpoint: list Applications for a Requisition with candidate identity, submitted
   date, and a CV download link — no stage grouping.
2. None — staff gets zero visibility until the pipeline spec.
3. A full review UI with stage assignment — pulls the pipeline spec's scope forward.

**Answer.** "Minimal endpoint — Staff can list Applications for a Requisition with candidate
identity, submitted date, and CV download link. No stage grouping. (Recommended option
accepted.)"

**Impact.** FR-10, FR-11, AC-16 through AC-20, Impacted Components (`ui/staff`).

---

### C-3 — Does a Candidate get any view of their own submitted Applications?

**Ambiguity.** Determines whether `ui/portal` needs a "my applications" list page in this spec,
or whether submission is a fire-and-forget action with confirmation only.

**Options presented.**
1. A simple list (Requisition title, submitted date) — no per-stage status detail, since no
   pipeline state exists yet.
2. Submission confirmation only, no list view.
3. Full status detail page — not buildable yet, since no `Stage` data is ever written by any
   existing spec.

**Answer.** "Simple list of the Candidate's own submitted Applications — Requisition title +
submitted date, no per-stage status detail. (Recommended option accepted.)"

**Impact.** FR-8, AC-12, AC-13, Non-Goals (full status tracking explicitly excluded), Impacted
Components (`ui/portal`).

---

### C-4 — Can a Candidate submit more than one Application to the same Requisition?

**Ambiguity.** Changes the data model (whether a uniqueness constraint exists) and the
acceptance criteria for duplicate-submission behaviour.

**Options presented.**
1. One Application per Candidate per Requisition; a second attempt is rejected (409 Conflict).
2. Unlimited — each submission creates a new row.
3. Allow a new submission only after the previous one reaches a terminal pipeline state — not
   enforceable yet, since no pipeline exists.

**Answer.** "One Application per Candidate per Requisition; a second submission attempt is
rejected with 409 Conflict. (Recommended option accepted.)"

**Impact.** FR-5, AC-8, AC-9, E-1.

---

### C-5 — What CV file types and maximum size are accepted?

**Ambiguity.** Directly shapes FR-3's validation rule and its acceptance criteria; also a
security-relevant boundary given `project.md`'s note that untrusted file upload is a day-one
concern.

**Options presented.**
1. PDF only, max 5 MB.
2. PDF + DOC/DOCX, max 10 MB.
3. Configurable, decided at Plan stage.

**Answer.** "PDF only, max 5MB. (Recommended option accepted.)"

**Impact.** FR-3, AC-3, AC-4, Non-Goals (other CV formats explicitly excluded).

---

## Assumptions Made Without Asking

Ambiguities resolved by judgement rather than by asking, because a reasonable default existed
and the alternatives would not have changed the work materially. Listed so they can be
challenged.

| # | Assumption | Default chosen | Reversal cost if wrong |
|---|---|---|---|
| A-1 | Whether an anonymous/guest could apply without registering was not asked | Application submission requires authentication as `Candidate`; no guest-apply path — consistent with `0002`/`project.md` framing candidates as registered portal users | Low — adding guest-apply later is additive, not a rewrite of the submission flow |
| A-2 | Whether a Candidate can withdraw/cancel a submitted Application was not asked | No withdrawal/cancellation endpoint in this spec — "submission" was requested, not post-submission lifecycle management | Low — a future spec can add a withdraw endpoint without touching submission |
| A-3 | Whether Staff can submit an Application on a Candidate's behalf was not asked | No — submission is Candidate-only, matching `0003`'s write-restriction pattern (FR-8) applied to this new entity | Low — proxy-submission is a distinct, separable capability if ever requested |
| A-4 | What "status" an Application has after submission was not asked | A single fixed state exists implicitly (the record's mere existence signals "submitted"); no status column with multiple values is introduced, since no pipeline exists yet to assign one elsewhere | Low — the pipeline spec extends this by adding stage/status fields, it does not need to restructure `Application` itself |
| A-5 | Whether uploaded CVs are scanned for malicious content was not asked | No malware/virus scanning is performed in this spec — flagged explicitly as a Non-Goal with a stated reason (accepted risk, not a silent omission), per `project.md`'s own framing of untrusted upload as a day-one concern | Medium — scanning can be added later behind the `shared/storage` interface without changing the Application API shape |
| A-6 | Exact `Application`/`CvAttachment` columns were not specified | Left to `plan/erd.md`, sufficient to satisfy FR-1 through FR-13 (Requisition/Candidate references, submission timestamp, file metadata) | Low — no schema exists yet |
| A-7 | Whether the CV file's original filename or content-type is preserved/displayed was not specified | Assumed necessary at minimum to support a meaningful download (browsers need a filename); exact display treatment left to `plan/erd.md` and `plan/api.md` | Low — additive metadata |
| A-8 | Whether basic file-content verification (e.g. magic-byte check) beyond extension/declared MIME type is required was not specified | FR-3's "PDF-format" validation is assumed to include a basic content check, not extension-only trust, without constituting the malware scanning excluded by A-5 | Low — a validation-layer detail, not a schema or API shape change |

## Deferred

Questions raised but explicitly postponed, with where they were recorded.

| # | Question | Deferred to |
|---|---|---|
| D-1 | Pipeline/stage progression and staff decisioning on an Application | The pipeline spec — explicitly out of scope here, inherited from `0003`'s own deferral |
| D-2 | Full candidate-visible per-stage status tracking (`project.md` S-3 beyond simple submission confirmation) | A future spec, once pipeline/stage state actually exists to display |
| D-3 | Application withdrawal/cancellation | Not requested; revisit only if a real need arises |
| D-4 | Cloud/object storage backend for CVs | Not requested; `shared/storage`'s interface (C-1) allows swapping the local-disk implementation later without an API change |
