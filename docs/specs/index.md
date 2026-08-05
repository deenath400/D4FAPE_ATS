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
| 0004 | Application Submission and CV Upload | planned | api/application, service/application, db/application, shared/storage, ui/portal, ui/staff, ui/bff | Application, CvAttachment | Candidates submit a CV-backed Application to a published Requisition; Staff and the Candidate can each see a minimal list of what was submitted. |

---

## Next id

`0005`

Update this after allocating an id, so `/specify` never has to scan the directory twice.

## Retired specs

| Id | Title | Retired | Reason |
|---|---|---|---|
