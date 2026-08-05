---
id: 0001
slug: project-scaffolding
title: Project Scaffolding and Walking Skeleton
status: planned
components: [infra/build, api/system, service/system, db/core, ui/bff, ui/portal]
entities: []
depends_on: []
created: 2026-08-05
updated: 2026-08-05
---

# Project Scaffolding and Walking Skeleton

## Problem & Context

The repository contains no source code. It holds the spec-kit workflow definitions and the
blueprint written by `/initialize-project`, and nothing else — no `.gitignore`, no solution
file, no package manifest, no build output, no test project. Every command row in
`tech-stack.md` reads `not yet defined`, which means `/validate` currently has no way to
build, run, or test anything, and every subsequent spec would have to invent its own place to
put code.

Two decisions recorded in the blueprint are unenforceable until code exists. The five Layering
Rules in `architecture.md` and `coding-standards.md` describe a dependency direction
(`ui → api → service → db`) that today is a paragraph of prose; the first spec to write a
controller can violate it without anything noticing. Likewise the testing frameworks are
recorded as *assumptions* rather than decisions, so the first spec that needs a test would be
choosing the project's testing framework as a side effect of doing something else.

There is also an immediate data-protection exposure. SQLite stores the entire database in a
file inside the working tree. With no `.gitignore`, the first developer to run the application
produces a `*.db` file that git will offer to commit — and once candidate self-registration
ships, that file contains real candidate PII. This is not a hypothetical tidiness concern; it
is the single highest-risk item in this spec, and it is why FR-1 comes first.

Finally, the decision about how the frontend reaches the backend has a structural consequence
that gets much more expensive later. Browser traffic goes through a server-side proxy handler;
server-rendered code calls the backend directly server-to-server; and both construct that
outbound call through one shared server-side invoke function, which is the single place a token
will be attached. If the first few UI specs each construct their own calls and this structure
arrives afterwards, every call site written in between has to be rewritten. Establishing it now,
on an endpoint that needs no authentication, means spec `0002` fills one existing seam with
token attachment instead of restructuring the frontend.

## Goals

- G-1 A developer with a fresh clone and the documented runtimes can install, build, test, run
  and migrate both deployables using only commands published in `tech-stack.md`.
- G-2 Both routes to the backend are proven end to end against one status endpoint — browser to
  proxy handler to shared invoke function to API to service to database, and server-rendered
  code to that same invoke function to the API — exercising all five Layering Rules and the HTTP
  seam before any feature depends on them.
- G-3 A violation of Layering Rules 2 to 5 fails the build rather than surviving to code review.
- G-4 No database file, build artifact, installed dependency or secret value can be committed.
- G-5 Every runtime, framework and package version is pinned, so two clones resolve identically.

## Non-Goals

- **Authentication of any kind** — ASP.NET Core Identity, JWT issuance, NextAuth, roles, login,
  logout, refresh-token rotation. Spec `0002` owns all of it; see Out of Scope for why the proxy
  handler and the shared invoke function are nonetheless in scope here.
- **Any domain entity** — `Requisition`, `Application`, `Candidate`, `Stage`. `architecture.md`
  states the data model is empty by design and the first feature spec introduces the first
  entities.
- **A validation library, server-state library or forms library** — `tech-stack.md` assigns
  each to the first API or UI spec, and nothing in a scaffold needs them.
- **CI, containerisation, hosting or deployment configuration** — the deployment target is
  deferred by explicit user decision and no spec may pre-empt it.
- **CV or attachment storage (`shared/storage`)** — no upload surface exists yet.
- **A transactional email provider** — belongs to the spec that implements registration.
- **Any user-facing product feature** — this spec ships no capability a Recruiter,
  HiringManager or Candidate would recognise.

## Users & Personas

The direct actor here is a developer working on this repository, who is not a product persona.
The `project.md` personas are served only indirectly, by unblocking the specs that serve them.

| Persona | Need this feature serves |
|---|---|
| Recruiter | None directly — unblocks every spec that builds the staff workspace |
| HiringManager | None directly — unblocks every spec that builds the staff workspace |
| Candidate | None directly — but FR-1 prevents their PII being committed to version control from the first day the portal runs |

## Functional Requirements

- **FR-1** — The repository excludes build output, installed dependencies, local environment
  files and SQLite database files (including write-ahead log and shared-memory sidecar files)
  from version control, such that no database file and no secret value can be committed.
- **FR-2** — A developer can install all dependencies for each deployable from a fresh clone
  using one documented command per deployable, with no manual preparatory steps.
- **FR-3** — The backend builds from a fresh clone with no manual steps and produces zero
  compiler warnings.
- **FR-4** — The frontend builds from a fresh clone with no manual steps and produces zero
  type errors.
- **FR-5** — The backend project structure makes Layering Rules 2 to 5 unsatisfiable by
  construction: a reference that violates any of them fails the build rather than being caught
  in review.
- **FR-6** — The backend runs locally and serves an unauthenticated system-status endpoint
  reporting its own version and whether the database is reachable and schema-current.
- **FR-7** — The frontend runs locally and renders a landing page that displays the backend's
  reported status as retrieved by **both** routes — once during server rendering and once from
  the browser — labelled so a reader can tell which result came by which route, and implementing
  the loading and error states required by `coding-standards.md` in addition to the success state.
- **FR-8** — Every browser-originated request to the backend passes through a server-side
  proxy handler in the frontend application. The browser never issues a request to the API's
  origin, and the backend's base URL is server-only configuration that is absent from any
  client bundle.
- **FR-9** — The database schema is created and updated by one documented migration command,
  producing a SQLite database at the configured path with write-ahead logging enabled.
- **FR-10** — The backend test suite runs with one documented command and contains at least one
  passing unit test and at least one passing integration test that exercises the running API
  over real HTTP in an in-process host (xUnit, per C-4).
- **FR-11** — The frontend test suite runs with one documented command and contains at least
  one passing component test (Vitest and Testing Library, per C-4).
- **FR-12** — Each deployable fails at startup with a message naming the missing configuration
  key when a required key is absent, rather than starting and failing at first use.
- **FR-13** — Every runtime, framework and package version is pinned such that two fresh clones
  resolve byte-identical dependency versions, and a manifest that disagrees with its lockfile
  fails the install rather than silently resolving something else.
- **FR-14** — Lint and format checks run for each deployable from one documented command each
  and report zero violations against the scaffolded code.
- **FR-15** — On completion, no step created by this spec remains `not yet defined` in the
  `tech-stack.md` Commands table, the Required Configuration table reflects the keys this
  spec actually requires, and the `coding-standards.md` rules now enforced by a linter or
  formatter are removed in favour of naming that tool.
- **FR-16** — A single shared server-side invoke function is the only place in the frontend
  where an outbound call to the backend API is constructed. Both the proxy handler and
  server-rendered code reach the backend through it, it is the sole reader of the backend base
  URL, it carries the point at which a credential will later be attached — inert in this spec,
  which sends none — and a backend call constructed anywhere else fails the frontend lint check.
- **FR-17** — Server-rendered code reaches the backend directly server-to-server through the
  shared invoke function. No request from the frontend process to its own origin occurs during
  server rendering.

## Non-Functional Requirements

- **NFR-1** — SQLite connections open with write-ahead logging enabled and a busy timeout of at
  least 5000 ms, so that a collision with the single writer retries rather than failing
  immediately. Reason: `architecture.md` records single-writer serialisation as an accepted
  constraint whose stated mitigation is WAL plus short transactions.
- **NFR-2** — The backend build treats warnings as errors; the accepted warning count is zero.
  Reason: warnings accumulate invisibly and a scaffold is the only moment the count is
  genuinely zero.
- **NFR-3** — The frontend compiles under TypeScript strict mode with zero errors. Reason: as
  NFR-2 — retrofitting strict mode after several UI specs is a repository-wide edit.

## Acceptance Criteria

Each criterion is independently testable and traces to one or more FRs. The actor throughout is
a developer on a machine with only the documented runtimes installed.

- **AC-1** *(FR-1)*
  - **Given** a fresh clone in which both deployables have been installed, built and run at
    least once, so that build output, installed dependencies and a SQLite database file all
    exist in the working tree
  - **When** the developer inspects version-control status
  - **Then** no build-output path, no dependency directory, no local environment file, and no
    database, write-ahead-log or shared-memory file appears as tracked, staged or untracked

- **AC-2** *(FR-1)*
  - **Given** the committed state of the repository at the end of this spec
  - **When** every committed configuration file is inspected
  - **Then** each required secret is present as a key name with no value, or absent entirely,
    and no file contains a credential, signing key or connection secret

- **AC-3** *(FR-2, FR-13)*
  - **Given** a fresh clone and no previously cached dependencies
  - **When** the developer runs the documented install command for each deployable
  - **Then** both commands exit successfully and resolve the exact versions recorded in the
    committed lockfiles

- **AC-4** *(FR-13)*
  - **Given** a frontend dependency manifest that has been edited so it no longer agrees with
    its committed lockfile
  - **When** the developer runs the documented install command
  - **Then** the command fails with a non-zero exit code naming the mismatch, rather than
    succeeding against a different resolved version

- **AC-5** *(FR-3, NFR-2)*
  - **Given** a fresh clone with dependencies installed
  - **When** the developer runs the documented backend build command
  - **Then** the build exits successfully and reports zero warnings and zero errors

- **AC-6** *(FR-4, NFR-3)*
  - **Given** a fresh clone with dependencies installed
  - **When** the developer runs the documented frontend build command
  - **Then** the build exits successfully and reports zero type errors under strict mode

- **AC-7** *(FR-5)*
  - **Given** the scaffolded backend
  - **When** a dependency is introduced from the API layer directly onto the database layer,
    bypassing the service layer
  - **Then** the backend build fails, and the same holds for a service-layer dependency on an
    HTTP type, a database-layer dependency on an HTTP or principal type, and a shared-layer
    dependency on any of the API, service or database layers

- **AC-8** *(FR-9, NFR-1)*
  - **Given** a fresh clone with no database file present
  - **When** the developer runs the documented migration command
  - **Then** the command exits successfully, a SQLite database exists at the configured path,
    its journal mode is write-ahead logging, and it contains the migration-history record

- **AC-9** *(FR-9)*
  - **Given** a database already at the current schema version
  - **When** the developer runs the documented migration command again
  - **Then** the command exits successfully, reports that no migration was applied, and leaves
    the database unchanged

- **AC-10** *(FR-6)*
  - **Given** the backend running locally against a migrated database
  - **When** an unauthenticated request is made to the system-status endpoint
  - **Then** the response is 200 and its body reports the backend version and that the database
    is reachable and schema-current

- **AC-11** *(FR-6)*
  - **Given** the backend running locally with the database file absent or unreadable
  - **When** an unauthenticated request is made to the system-status endpoint
  - **Then** the response reports the database as unreachable without leaking the file path or
    connection string, and the process does not crash

- **AC-12** *(FR-7, FR-8)*
  - **Given** both deployables running with the documented local defaults
  - **When** the developer loads the frontend landing page in a browser
  - **Then** the page displays the backend version and database status that originated from the
    system-status endpoint, retrieved by the browser through the frontend's own proxy handler

- **AC-13** *(FR-8)*
  - **Given** both deployables running and the landing page loaded
  - **When** every network request the browser issued is examined
  - **Then** none of them targets the API's origin; all backend data arrived via a request to
    the frontend's own origin

- **AC-14** *(FR-8)*
  - **Given** a production frontend build
  - **When** the emitted client bundle is searched for the backend base URL and for the name of
    the configuration key that holds it
  - **Then** neither appears, and the key is not exposed through a client-visible environment
    prefix

- **AC-15** *(FR-7)*
  - **Given** the frontend running while the backend is stopped
  - **When** the browser-retrieved portion of the landing page attempts to load
  - **Then** it renders its error state with a message that does not expose the backend URL or a
    stack trace, and no unhandled exception reaches the browser console

- **AC-16** *(FR-7)*
  - **Given** the frontend running and a backend response that has not yet arrived
  - **When** the landing page is rendering
  - **Then** a loading state is displayed rather than an empty region or a flash of error content

- **AC-17** *(FR-10)*
  - **Given** a fresh clone with dependencies installed
  - **When** the developer runs the documented backend unit-test command
  - **Then** the command exits successfully with at least one test executed and zero failures

- **AC-18** *(FR-10)*
  - **Given** a fresh clone with dependencies installed
  - **When** the developer runs the documented backend integration-test command
  - **Then** an in-process host starts, at least one test issues a real HTTP request to the
    system-status endpoint and asserts on the response, the suite exits successfully, and the
    test uses its own database rather than a shared file

- **AC-19** *(FR-11)*
  - **Given** a fresh clone with dependencies installed
  - **When** the developer runs the documented frontend test command
  - **Then** the command exits successfully with at least one component test executed against
    rendered output and zero failures

- **AC-20** *(FR-12)*
  - **Given** the backend's database connection-string configuration key is absent
  - **When** the backend is started
  - **Then** startup fails immediately with a non-zero exit and a message naming the missing
    key, and no HTTP port is opened

- **AC-21** *(FR-12)*
  - **Given** the frontend's backend-base-URL configuration key is absent
  - **When** the frontend is started
  - **Then** startup or the first proxied request fails with a message naming the missing key,
    rather than issuing a request to an undefined or default address

- **AC-22** *(FR-14)*
  - **Given** a fresh clone with dependencies installed
  - **When** the developer runs the documented lint command for each deployable
  - **Then** both exit successfully reporting zero violations

- **AC-23** *(FR-14)*
  - **Given** a fresh clone with dependencies installed
  - **When** the developer runs the documented format check for each deployable
  - **Then** both exit successfully reporting that no file would be reformatted

- **AC-24** *(FR-15)*
  - **Given** the completed spec
  - **When** the `tech-stack.md` Commands table is read
  - **Then** the install, build, run, unit-test, integration-test, lint, format and migrate rows
    each contain a literal command, and only the seed row remains `not yet defined`

- **AC-25** *(FR-15)*
  - **Given** a fresh clone
  - **When** each literal command in the `tech-stack.md` Commands table is executed in the
    stated working directory, in documented order
  - **Then** every one exits successfully

- **AC-26** *(FR-16)*
  - **Given** the scaffolded frontend
  - **When** a backend call is introduced that constructs its own request instead of going
    through the shared invoke function — including one added inside the proxy handler itself
  - **Then** the frontend lint check fails, and a search of the frontend source finds exactly
    one module that reads the backend base URL

- **AC-27** *(FR-16)*
  - **Given** both deployables running with the documented local defaults
  - **When** every request the frontend process sends to the backend is inspected
  - **Then** none carries an authorization header, cookie or other credential, and the
    system-status endpoint accepts them unauthenticated

- **AC-28** *(FR-17)*
  - **Given** both deployables running, and a browser with client-side scripting disabled
  - **When** the developer loads the landing page and the frontend process's outbound requests
    are observed
  - **Then** the server-rendered status value is present in the delivered HTML, at least one
    request was sent to the API origin during rendering, and no request was sent from the
    frontend process to its own origin

- **AC-29** *(FR-7, FR-17)*
  - **Given** both deployables running with the documented local defaults
  - **When** the developer loads the landing page in a browser with scripting enabled
  - **Then** both status results are displayed and each is labelled with the route that
    retrieved it, one server-rendered and one browser-retrieved

- **AC-30** *(FR-17)*
  - **Given** the frontend running while the backend is stopped
  - **When** the developer loads the landing page
  - **Then** the page still responds with rendered HTML showing an error state for the
    server-rendered portion, rather than a 5xx response or an unhandled server exception, and
    the backend URL does not appear in the response

## Edge Cases & Error States

| # | Situation | Expected behaviour |
|---|---|---|
| E-1 | A database file exists in the working tree when a developer commits | The file is ignored and never offered for staging (AC-1) — the PII exposure this spec exists to close |
| E-2 | Required configuration key missing at startup | Fail fast, name the key, open no port (AC-20, AC-21) — never start and fail at first request |
| E-3 | Backend unreachable when the frontend renders | Error state rendered with no backend URL or stack trace disclosed (AC-15) |
| E-4 | Migration command run against an already-current database | No-op, exit 0, database unchanged (AC-9) |
| E-5 | Dependency manifest and lockfile disagree | Install fails loudly rather than resolving a different version (AC-4) |
| E-6 | A developer writes a dependency that violates a Layering Rule | The build fails (AC-7) |
| E-7 | Database unreachable while the backend is already running | Status endpoint reports it, the process stays up, and no path or connection string is disclosed (AC-11) |
| E-8 | Two processes attempt to write concurrently | The busy timeout of at least 5000 ms causes a retry rather than an immediate failure (NFR-1) |
| E-9 | Backend unreachable while a page is being server-rendered | The page still returns rendered HTML with an error state, never a 5xx or an unhandled server exception (AC-30) — the failure mode the browser path does not exercise |
| E-10 | A developer constructs a backend call outside the shared invoke function | The frontend lint check fails (AC-26) — the frontend counterpart to the structural layering enforcement in AC-7 |

## Data Touched

| Entity | New / Existing | Notes |
|---|---|---|
| — | — | No entity is introduced. The data model in `architecture.md` stays empty by design |

The database itself is created: a SQLite file at the configured path containing an empty
initial migration and the migration-history record, and nothing else. This proves the migration
toolchain without placing a non-domain table in the schema that a later spec would have to drop
— which, under SQLite, would be a full table rebuild.

## Impacted Components

| Component | Change |
|---|---|
| `infra/build` | New. Repository-level tooling: ignore rules, runtime and package version pinning, lockfiles, lint and format configuration, and the documented command set for both deployables |
| `api/system` | New. The backend host and its HTTP boundary: the unauthenticated system-status endpoint, the ProblemDetails error handler, structured logging and typed configuration binding |
| `service/system` | New. The version and database-reachability check. Sole caller of `db/core`, and the layer that makes the walking skeleton traverse `api → service → db` rather than shortcut it |
| `db/core` | New. The EF Core context, SQLite connection configuration including WAL and busy timeout, and the initial empty migration. The foundation every later `db/<area>` builds on |
| `ui/bff` | New, and deliberately coarse: the frontend's entire server-side backend-access layer, comprising **both** the proxy handler through which all browser-originated traffic passes **and** the shared invoke function that is the sole constructor of outbound backend calls, sole reader of the backend base URL, and the seam spec `0002` fills with JWT attachment. The handler is one of that function's two callers, not its owner |
| `ui/portal` | Existing path from `architecture.md`. The anonymous landing page that displays backend status retrieved by both routes — server-rendered via `ui/bff`'s invoke function, and browser-retrieved via `ui/bff`'s proxy handler — with loading, error and success states |

`ui/staff` is deliberately not listed. The scaffold creates its route group as an empty
directory with no surface, and listing it would draw future staff specs into loading this one
for nothing. Five of these six paths are new and must be added to the `architecture.md`
Component Map — by `/implement`, not by this stage.

## Out of Scope

- **Authentication, and everything that follows from it.** Identity, JWT issuance and
  validation, NextAuth, role policies, login and logout, refresh-token rotation, and the
  `Jwt__Issuer` / `Jwt__Audience` / `Jwt__SigningKey` configuration keys currently marked
  required in `tech-stack.md`. Spec `0002` owns all of it, and those three keys become required
  then, not now.
- **What the proxy handler and invoke function carry.** Both are established here as structural
  patterns on an endpoint that needs no credentials. The invoke function attaches no token and
  reads no session, the proxy handler enforces no authorization, and neither has any session
  dependency in this spec. Spec `0002` fills the attachment point that FR-16 puts in place; it
  should not need to move a call site to do so.
- **Any endpoint beyond system status.** No feature endpoint, no CRUD, no anonymous portal
  surface other than the landing page.
- **Styling beyond making Tailwind CSS available and demonstrably applied.** No design system,
  component library, theme or layout shell.
- **Seed data.** The seed command row stays `not yet defined` — there is nothing to seed.
- **A CORS policy on the backend.** FR-8 makes the browser incapable of reaching the API origin,
  so no cross-origin browser request exists to permit. If a later spec introduces one, that spec
  owns the policy.

## Open Questions

None — all clarifications resolved, see `clarifications.md`. Q-1, raised in the first round and
concerning server-to-server calls, was answered by the user and is recorded as C-7.

## Related Specs

None — this is the first spec touching these components.

| Spec | Tier | Why loaded |
|---|---|---|
| — | — | — |

Tier 0 was read in full: `meta/project.md`, `meta/architecture.md`, `meta/tech-stack.md`,
`meta/coding-standards.md`, `index.md`.
Considered and skipped: none — `index.md` contains zero rows.
Cap reached: no.
