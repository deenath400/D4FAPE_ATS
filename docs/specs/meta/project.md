# Project

**Updated:** 2026-08-05 · **Budget:** 100 lines target / 150 hard ceiling

What the product is and who it is for. Changes rarely. Features are **not** logged here —
that is what `index.md` is for.

---

## Overview

D4FAPE_ATS is an applicant tracking system built and operated by Digital400 for a single
organisation's own hiring. Recruiters and hiring managers publish requisitions and move
applicants through a hiring pipeline; candidates register themselves through a public
self-service portal, submit applications with a CV, and track their own progress without
contacting a recruiter. It is single-tenant by decision — one organisation's data, with no
tenant partitioning anywhere in the model.

The build exists rather than buying an off-the-shelf ATS because the hiring pipeline and the
candidate-facing experience are meant to be shaped to this organisation's process instead of
the reverse.

## Primary Users

| Persona | Role | Primary goals | Pain today |
|---|---|---|---|
| Recruiter | Owns requisitions end to end | Publish a role, screen applicants, advance or reject them, keep the pipeline current | Applicant state lives in inboxes and spreadsheets |
| HiringManager | Requests and decides on hires | Review shortlisted applicants, leave a decision, see where a role stands | Depends on the recruiter for every status update |
| Candidate | Applies for roles | Register, apply with a CV, see honest status without chasing anyone | No visibility after applying; re-enters the same details per application |

Specs reference these personas by name, so keep the names short and stable.

## Scope

**In scope**

- Requisition management — create, publish, close
- Candidate self-registration and authentication on a public portal
- Application submission, including CV/attachment upload
- Pipeline progression — advancing and rejecting applicants through stages
- Candidate-visible application status
- Staff workspace for recruiters and hiring managers

**Out of scope**

- Multi-tenancy or client-partitioned data — single-tenant was chosen explicitly
- Job-board syndication and external sourcing integrations — no integration is committed yet
- Offer letters, contracts, and onboarding — downstream of hiring, separate concern
- Payroll and HRIS functionality — not a hiring concern

## Success Criteria

| # | Criterion | Measure |
|---|---|---|
| S-1 | A candidate can apply without recruiter involvement | Self-registration to submitted application with no staff action |
| S-2 | Applicant state lives in the system, not in inboxes | Every active application has a current pipeline stage in the database |
| S-3 | Candidates can self-serve status | Candidate sees current status of each of their applications in the portal |
| S-4 | A requisition's pipeline is visible at a glance | Recruiter sees all applicants for a requisition grouped by stage |

## Domain Glossary

| Term | Meaning in this project |
|---|---|
| Requisition | An approved open role that candidates apply to. Not the public advert text. |
| Application | One candidate's submission against one requisition. The unit that moves through the pipeline. |
| Pipeline | The ordered set of stages an application passes through for a requisition. |
| Stage | One step in a pipeline (e.g. screening, interview). Whether stages are global or per-requisition is not yet decided. |
| Candidate | A person with a portal account. Exists independently of any single application. |
| Staff | Collective term for Recruiter and HiringManager identities, as distinct from Candidate identities. |
| Portal | The public, candidate-facing surface. Distinct from the staff workspace. |

> **Assumption:** These terms are the working vocabulary, not a committed data model. Entity
> names in spec frontmatter must match this table, and the first spec confirms or amends it.

## Key Constraints

| Constraint | Type | Implication |
|---|---|---|
| SQLite is the datastore | Technical | Single-writer file database; bounds concurrent write throughput from the public portal and limits in-place schema changes |
| Two deployables (Next.js UI + ASP.NET Core API) | Technical | All UI/API communication is HTTP across a process boundary; no shared in-process state |
| Self-hosted identity, no external IdP | Technical | The project owns password reset, lockout, and any MFA it later needs |
| Public candidate registration | Business | Anonymous traffic and untrusted file upload are day-one concerns, not later hardening |
| Single-tenant | Business | No tenant discriminator; adding multi-tenancy later is a migration of every table |

## External Dependencies

| Dependency | Owner | Risk if unavailable |
|---|---|---|
| Transactional email provider | Not yet chosen | Candidate registration confirmation and password reset cannot complete |
| Deployment/hosting target | Deferred by the user | No production environment; local development is unaffected |

## Stakeholders

| Name / Role | Interest | Decides |
|---|---|---|
| Digital400 (deenathg@digital400.com) | Owns the product and the repository | Scope, stack, and all blueprint decisions |

## Related Specs

None — this is the first artifact in the repository.
