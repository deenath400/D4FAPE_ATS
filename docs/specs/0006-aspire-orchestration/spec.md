---
id: 0006
slug: aspire-orchestration
title: Local Service Orchestration with Aspire
status: validated
components: [infra/build, ui/bff, api/system, ui/portal]
entities: []
depends_on: [0001]
created: 2026-08-13
updated: 2026-08-14
---

# Local Service Orchestration with Aspire

## Problem & Context

Today, a developer must open two terminal windows and run two separate commands to work on the
system: `dotnet run --project src/Api` in the backend directory to start the ASP.NET Core API,
and `npm run dev` in the frontend directory to start the Next.js application. Orchestrating
startup, shutdown, and port binding across these two processes is manual and error-prone. A
developer who forgets to start one service, or starts them in the wrong order, gets obscure
networking errors. This friction increases during onboarding and during rapid iteration cycles
where the developer bounces between the services.

The project already runs both services together over HTTP — spec 0001 proved both routes to the
backend (`ui/bff`'s proxy handler for browser traffic and its shared invoke function for
server-side calls) work end to end. The HTTP boundary is stable. What is missing is a development
tool that manages the lifecycle of both services as one coordinated unit.

.NET Aspire is a stack designed to orchestrate local distributed applications. It starts and
stops multiple services, binds them to ports, exposes their addresses to each other, provides a
built-in dashboard for monitoring, and — when needed — can orchestrate the same services in
production. For a development workflow, Aspire on local processes provides a single entry point
and clear feedback on which services are running and healthy.

## Goals

- G-1 A developer can start both backend and frontend services with one documented command,
  eliminating the need for multiple terminal windows during development.
- G-2 Both services are visible in one place (the Aspire dashboard) with their status and
  configured ports, reducing debugging friction when something is not running.
- G-3 The existing independent run commands (`dotnet run`, `npm run dev`) continue to work
  unchanged, so developers who prefer separate processes or CI scripts that assume the old
  commands are not disrupted.
- G-4 No changes are required to the application code in backend or frontend; orchestration is
  purely a development-time concern and a configuration layer on top of the existing deployables.

## Non-Goals

- **Production deployment orchestration** — Aspire is for development iteration. Production
  deployment configuration is out of scope, deferred by the current "Infrastructure: TBD" in
  `tech-stack.md`, and will be decided when the deployment target is chosen.
- **Containerization or Docker configuration** — Aspire orchestrates local `.NET` and `Node.js`
  processes, not containers. This spec does not introduce Docker or container build steps.
- **Changes to authentication, storage, or data access** — All existing auth mechanisms, storage
  interfaces, and database connectivity continue to work as today. Aspire is transparent to
  these layers.
- **New monitoring or observability tooling beyond Aspire's built-in dashboard** — The Aspire
  dashboard is the provided visibility surface.
- **Performance tuning or benchmarking** — orchestration overhead is not measured or optimized.
- **Multi-environment (staging, production) orchestration configurations** — only development is
  in scope.

## Users & Personas

| Persona | Need this feature serves |
|---|---|
| Developer | A single entry point to start and monitor both deployables during local development and onboarding, without manual process management across terminals |
| Recruiter | None directly — unblocks faster developer iteration on features that serve recruiting workflows |
| HiringManager | None directly — unblocks faster developer iteration |
| Candidate | None directly — unblocks faster developer iteration |

## Functional Requirements

- **FR-1** — An Aspire AppHost project exists in the repository and declares the backend
  (ASP.NET Core) and frontend (Next.js) as managed services.
- **FR-2** — A developer can start both services with a single documented command that launches
  the Aspire orchestrator.
- **FR-3** — The Aspire orchestration assigns and exposes network addresses (host and port) for
  both services such that the frontend and backend can discover each other's locations at runtime.
- **FR-4** — The orchestrated backend service can access the SQLite database at the configured
  file path without any changes to database configuration or connection logic.
- **FR-5** — The `tech-stack.md` file documents the command to run both services through Aspire
  orchestration in the Commands table.
- **FR-6** — The documented independent run commands (`dotnet run --project src/Api` and
  `npm run dev`) continue to work without change, and a developer can still start services
  individually if desired.

## Non-Functional Requirements

- **NFR-1** — Aspire orchestration is development-only. It does not affect, constrain, or
  pre-empt the production deployment target, which remains TBD. Reason: The user explicitly
  clarified scope to development iteration only, and production deployment orchestration is a
  separate infrastructure decision.
- **NFR-2** — Starting services through Aspire produces the same behaviour as starting them
  independently in terms of logging, error output, and database state. Reason: Ensures debugging
  and troubleshooting are identical whether services run independently or via Aspire.

## Acceptance Criteria

Each criterion is independently testable and traces to one or more FRs. The actor throughout is
a developer on a machine with the documented runtimes and dependencies installed.

- **AC-1** *(FR-1)*
  - **Given** a fresh clone with backend and frontend dependencies installed
  - **When** the repository is searched for an Aspire AppHost project
  - **Then** exactly one Aspire AppHost project exists and can be identified and built

- **AC-2** *(FR-1)*
  - **Given** the Aspire AppHost project
  - **When** its source is inspected
  - **Then** it declares a service representing the backend and a service representing the frontend

- **AC-3** *(FR-2)*
  - **Given** a fresh clone with dependencies installed and no services running
  - **When** the developer runs the documented Aspire command in the working directory specified
    in `tech-stack.md`
  - **Then** the command starts successfully, both the backend and frontend services are
    reported as running, and a dashboard or status output is available

- **AC-4** *(FR-2)*
  - **Given** both services running under Aspire orchestration
  - **When** the developer terminates the Aspire process (e.g. Ctrl+C)
  - **Then** both services shut down cleanly and no child processes remain orphaned

- **AC-5** *(FR-3)*
  - **Given** the Aspire AppHost declaring both services
  - **When** the AppHost is inspected
  - **Then** it configures network port bindings for both services and exposes them for
    inter-service communication (e.g. the frontend knows the backend's address and port)

- **AC-6** *(FR-3)*
  - **Given** both services running under Aspire
  - **When** the frontend makes an HTTP request to the backend through the BFF proxy handler
  - **Then** the request succeeds and returns the response (exercised via the system-status
    endpoint from 0001)

- **AC-7** *(FR-3)*
  - **Given** both services running under Aspire
  - **When** the backend receives a request from the frontend through the configured Aspire
    network topology
  - **Then** the request is accepted and processed (exercised via the system-status endpoint
    from 0001)

- **AC-8** *(FR-4)*
  - **Given** the backend running under Aspire orchestration
  - **When** the `dotnet ef database update` migration command is run for the orchestrated
    backend
  - **Then** the command succeeds and the SQLite database file is created or updated at the
    configured path

- **AC-9** *(FR-4)*
  - **Given** the backend running under Aspire and a migration already applied
  - **When** the backend is stopped and restarted under Aspire
  - **Then** the database file persists at the same location and the migration history is
    retained

- **AC-10** *(FR-5)*
  - **Given** the completed spec
  - **When** the `tech-stack.md` Commands table is read
  - **Then** the "Run (dev)" row contains a documented command that starts both services
    through Aspire (in addition to or alongside the existing independent commands)

- **AC-11** *(FR-6)*
  - **Given** the Aspire orchestration added to the project
  - **When** the developer runs `dotnet run --project src/Api` in the `backend` directory
  - **Then** the backend starts on its documented port and serves requests independently of
    Aspire, exactly as before this spec

- **AC-12** *(FR-6)*
  - **Given** the Aspire orchestration added to the project
  - **When** the developer runs `npm run dev` in the `frontend` directory
  - **Then** the frontend starts on its documented port and serves requests independently of
    Aspire, exactly as before this spec

- **AC-13** *(FR-6)*
  - **Given** both Aspire orchestration and independent run commands available
  - **When** one service is running independently and the developer starts that same service
    through Aspire
  - **Then** one of the following occurs: (a) a clear error is raised naming the port conflict,
    or (b) Aspire is configured to use a different port than the independent command such that
    no conflict exists

- **AC-14** *(NFR-1)*
  - **Given** the Aspire-orchestrated services running locally
  - **When** the backend and frontend are inspected for production-deployment configuration or
    multi-environment orchestration configuration
  - **Then** none exists — Aspire orchestration is present only as a development-time tool, and
    production configuration remains TBD

## Edge Cases & Error States

| # | Situation | Expected behaviour |
|---|---|---|
| E-1 | Backend service fails to start within Aspire | Aspire reports the failure in the dashboard or console output, names the service, and provides logs; the frontend and Aspire remain running |
| E-2 | Frontend service fails to start within Aspire | Aspire reports the failure in the dashboard or console output, names the service, and provides logs; the backend and Aspire remain running |
| E-3 | A required configuration key (e.g. `ConnectionStrings:Default`) is missing when orchestrated backend starts | Backend fails at startup with a message naming the key, and Aspire reports the service as failed (applies whether running independently or via Aspire) |
| E-4 | Database file is deleted while the backend is running under Aspire | Backend's health check detects it and reports the database as unreachable in the system-status endpoint; Aspire service remains running (applies whether via Aspire or independent) |
| E-5 | Two developers clone the repository and start Aspire on machines with the same network — port binding conflict at the OS level | Aspire or the OS reports a port conflict; the second instance fails to bind to the port and reports the error clearly |
| E-6 | Developer uses both Aspire orchestration and independent `dotnet run` on the same machine | Port conflict is detected and reported clearly, or port assignment avoids conflict (tested by AC-13) |

## Data Touched

| Entity | New / Existing | Notes |
|---|---|---|
| — | — | No data model changes. The SQLite database and its schema remain unchanged; Aspire is transparent to data access. |

## Impacted Components

| Component | Change |
|---|---|
| `infra/build` | Adds Aspire AppHost project, orchestration configuration, and updates documented run commands |
| `ui/bff` | No code change; the proxy handler and shared invoke function continue to work. Environment variable binding for backend address may be updated to work with Aspire-assigned ports |
| `api/system` | No code change; the system-status endpoint continues to work. Configuration binding for ports and addresses may be read from Aspire-provided environment variables |

## Out of Scope

- **Containerization** — this spec uses local process orchestration only, not Docker or container images.
- **Production deployment** — Aspire is for development; production orchestration is TBD and a separate decision.
- **Modifying application code** — only orchestration configuration and dev-tooling configuration change.
- **Observability beyond the Aspire dashboard** — no custom monitoring or alerting is added.
- **Aspire extensions or custom instrumentation** — the orchestration uses Aspire in its default configuration.
- **Performance optimisation or resource constraints** — no CPU, memory, or timeout configurations are specified.

## Open Questions

None — all clarifications resolved, see `clarifications.md`.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| 0001 - Project Scaffolding and Walking Skeleton | 1 | Established the two-deployable architecture, defined the independent run commands in `tech-stack.md`, and proved both services communicate over HTTP. This spec extends the dev-command set without breaking the independent commands. |

Considered and skipped: 0002–0005 (all business logic; no component or entity overlap with orchestration infrastructure).
Cap reached: no.
