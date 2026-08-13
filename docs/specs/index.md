# Spec Index

One row per spec. This file is Tier 0 context — every stage agent reads it in full, every
time. Keep it cheap: one row, one sentence, no exceptions.

**Index-sync rule:** any stage that writes a spec's frontmatter updates that spec's row in
the same turn. A stale index degrades every future spec's context loading.

Status values: `specified` → `planned` → `implementing` → `implemented` → `validated`.

| Id | Title | Status | Components | Entities | Summary |
|---|---|---|---|---|---|
| 0001 | Project Scaffolding and Walking Skeleton | validated | infra/build, api/system, service/system, db/core, ui/bff, ui/portal | — | Stands up both deployables with tests and tooling, proving both routes to the backend through a shared server-side invoke function. |
| 0002 | User Authentication and Refresh Token Flow | validated | shared/auth, api/system, service/system, db/core, ui/bff, ui/portal | User, Role, RefreshToken | Implements backend ASP.NET Core Identity with JWT & refresh token rotation, and frontend NextAuth v5 session proxying. |
| 0003 | Requisition Management | validated | api/requisition, service/requisition, db/requisition, ui/staff, ui/portal | Requisition, Stage | Recruiters draft, publish, unpublish, and close requisitions behind role policies; candidates browse published ones anonymously on the portal. |
| 0004 | Application Submission and CV Upload | validated | api/application, service/application, db/application, shared/storage, ui/portal, ui/staff, ui/bff | Application, CvAttachment | Candidates submit a CV-backed Application to a published Requisition; Staff and the Candidate can each see a minimal list of what was submitted. |
| 0005 | Pipeline Progression | validated | api/pipeline, service/pipeline, db/pipeline, service/requisition, db/requisition, api/application, service/application, db/application, ui/staff, ui/portal | Stage, Application, StageTransition | Recruiters configure a Requisition's pipeline and advance or reject Applications through it, with an audited history and real candidate-visible status. |
| 0006 | Local Service Orchestration with Aspire | validated | infra/build, ui/bff, api/system, ui/portal | — | Developers start backend and frontend services together with a single Aspire orchestration command; independent run commands continue to work for backwards compatibility. |
| 0007 | Seed Sample User Accounts per Role | validated | db/core, shared/auth | User, Role | Seeds one sample login per existing role (Candidate, Recruiter, HiringManager) via EF Core migration, sharing the password `Temp@123`. |
| 0008 | Automated Candidate Screening | planned | api/application, service/application, service/pipeline, service/screening, db/application, db/pipeline, shared/storage, ui/staff | Application, CvAttachment, Requisition, Stage, StageTransition, ScreeningReport | Automatically evaluates candidate CVs against job listings using an AI screening agent, generates structured reports, and auto-advances qualified applicants in the pipeline. |

---

## Next id

`0009`

Update this after allocating an id, so `/specify` never has to scan the directory twice.

## Retired specs

| Id | Title | Retired | Reason |
|---|---|---|---|
