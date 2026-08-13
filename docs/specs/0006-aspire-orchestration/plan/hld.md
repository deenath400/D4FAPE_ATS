# High-Level Design — 0006 Local Service Orchestration with Aspire

**Spec:** `../spec.md` · **Status:** planned · **Updated:** 2026-08-13

The *what and why* of the design. Someone should be able to read this alone and understand
the shape of the solution and the reasoning behind it, without reading the LLD.

---

## 1. Solution Overview

A new .NET Aspire AppHost project (`src/AppHost/Ats.AppHost`) acts as the single orchestration
entry point during local development. It declares the backend (ASP.NET Core, running via
`dotnet run`) and frontend (Next.js, running via `npm run dev`) as managed services, assigns
them fixed network ports (5000 and 3000 respectively), wires environment variables so each
service knows how to reach the other, and exposes a built-in dashboard for monitoring and
debugging. The Aspire orchestrator starts and stops both services as a coordinated unit in
response to a single documented command, eliminating the friction of managing two separate
terminal windows. Existing independent run commands (`dotnet run --project src/Api` and
`npm run dev`) continue to work unchanged—Aspire is purely additive development tooling.

## 2. Architecture Decision: Local Process Orchestration, Not Containers

Aspire orchestrates local .NET and Node.js processes running directly on the developer's
machine, not Docker containers. This minimizes friction (no Docker daemon dependency during
iteration), keeps the local experience identical to the deployment model (both run the same
binaries), and aligns with spec clarification C-2. If production later chooses to containerize,
that is a separate orchestration decision outside this scope.

## 3. Components & Responsibilities

| Component | New/Modified | Responsibility |
|---|---|---|
| `src/AppHost` (new) | New | Aspire orchestration project: declares backend and frontend services, binds ports, injects environment variables, provides dashboard entry point. Development-only. |
| `src/Api` (backend) | No code change | Unchanged; orchestrated via existing `dotnet run` entry point. Receives `API_BASE_URL` binding from Aspire if needed. |
| `frontend` (Next.js) | No code change | Unchanged; orchestrated via existing `npm run dev` entry point. Receives `API_BASE_URL` environment variable from Aspire. |
| `docs/specs/meta/tech-stack.md` | Modify | Add Aspire launch command to Commands table; document Aspire as a development-time tool. |

## 4. Key Design Decisions

| # | Decision | Rationale |
|---|---|---|
| D-1 | AppHost lives in `src/AppHost/`, co-located with other .NET projects, using the same project naming convention (`Ats.AppHost.csproj`) | Aspire is a .NET project; placing it with the backend keeps related tooling together and follows the repository's existing structure |
| D-2 | Backend and frontend services use fixed ports (5000 and 3000) matching the documented defaults in `tech-stack.md` | Consistency with independent run commands; developers see the same ports whether running via Aspire or independently |
| D-3 | Environment variable binding for `API_BASE_URL` uses Aspire's `.WithEnvironment()` and resource-reference model, not manual configuration files | Aspire's built-in service-discovery pattern makes the binding declarative and maintainable; no separate env files for Aspire mode |
| D-4 | Port conflicts (when a service tries to start on an already-bound port) are detected and reported by Aspire or the OS at startup time, failing fast and clearly | Clearer UX than silent port remapping; developers immediately know what to fix (AC-13 option (a)) |
| D-5 | Aspire dashboard is enabled by default (runs at a separate port, e.g., `:17203`) and is the visibility surface for monitoring service health and logs | Satisfies G-2 (both services visible in one place); no additional monitoring tooling is introduced |

## 5. Non-Functional Approach

| NFR | How the design satisfies it |
|---|---|
| NFR-1 (development-only scope) | AppHost is a standalone orchestration project with no production configuration, multi-environment setup, or deployment artifact. It runs only during local iteration; production configuration remains TBD and is not pre-empted by this spec |
| NFR-2 (no changes to application code) | Backend `src/Api` and frontend `src/` code remain unmodified. Only orchestration configuration (the AppHost) and environment variable bindings change; application behavior is identical whether services run independently or via Aspire |

## 6. Risk Mitigation

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Port conflict when developer runs both Aspire and independent commands simultaneously | Medium | Low | AC-13 validates both scenarios; error message names the port and suggests stopping the conflicting process |
| Aspire version updates introduce breaking changes to the orchestration API | Low | Medium | Aspire versions are locked in NuGet lockfiles; any version bump is an explicit, reviewed diff (per spec 0001's lockfile-mismatch principle) |
| Developers unfamiliar with Aspire dashboard reporting may struggle to interpret service status | Low | Low | Dashboard UI is self-explanatory for the two-service case; tech-stack.md and README provide clear guidance |

## 7. Rollout Considerations

- **Migration order:** None — Aspire is a new development tool, not a code change. Existing migrations and schemas are unaffected.
- **Feature flag needed?** No — Aspire is purely opt-in; developers can continue using independent commands.
- **Backwards compatibility:** Full — independent commands remain unchanged and functional (FR-6, AC-11, AC-12, AC-13).

---

## Related Specs

- `0001` (Project Scaffolding and Walking Skeleton) — established the two-deployable architecture, defined independent run commands in tech-stack.md
