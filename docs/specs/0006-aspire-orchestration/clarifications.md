# Clarifications — 0006 Local Service Orchestration with Aspire

A verbatim log of ambiguities raised during `/specify` and how they were resolved. This is an
audit trail: when someone later asks "why does it work this way", the answer is here.

---

## Round 1 — 2026-08-13

### C-1 — Development-only vs. production scope

**Ambiguity.** The feature request mentioned "create a separate aspire solution", but did not
specify whether Aspire should handle orchestration only during local development, or whether it
should also become the orchestration path for production deployment. The `tech-stack.md` explicitly
records Infrastructure as TBD, meaning the production deployment model and orchestration strategy
are not yet decided. The two readings produce very different work: development-only is a
dev-tooling addition; production-inclusive commits the project to using Aspire in the deployment
pipeline.

**Options presented.**
1. Development only (Aspire for local iteration, production deployment orchestration remains TBD and separate) — focuses the feature on dev-time iteration and does not pre-empt infrastructure decisions
2. Development and production (Aspire handles both) — ties the project's future deployment strategy to Aspire, requires additional planning around containerization and cloud deployment
3. Production only (dev stays unchanged) — does not solve the immediate dev-experience pain

**Answer.** Development only (user confirmed the Recommended option).

**Impact.** Determines scope boundary: Aspire is a development-time tool. NFR-1 in the spec
codifies this as a non-functional requirement. The spec does not introduce production orchestration
configuration, multi-environment setup, or containerization. Production deployment decisions
remain deferred.

---

### C-2 — Runtime model (local processes vs. Docker containers)

**Ambiguity.** Aspire is commonly used to orchestrate Docker containers, but can also orchestrate
local .NET and Node.js processes running directly. The two readings differ in infrastructure
requirements: process orchestration adds no new dev dependencies, while container orchestration
requires Docker (or similar) to be installed and running locally. This affects tooling, build
steps, and whether developers experience containers as part of their day-to-day workflow.

**Options presented.**
1. Local processes (Aspire manages process startup/shutdown of `dotnet run` and `npm run dev`) — simpler for local iteration, no Docker infrastructure required, less friction
2. Docker containers (services packaged as containers, Aspire orchestrates them) — aligns with production patterns if containers are used later, but adds local Docker dependency
3. Hybrid (e.g. backend in process, frontend in container) — middle ground, but complexity and inconsistency in local development vs. production

**Answer.** Local processes (user confirmed the Recommended option).

**Impact.** Aspire in this spec orchestrates local .NET and Node.js processes, not containers.
The spec does not introduce Docker or container-build configuration. Process orchestration is
simpler and aligns with the current development workflow. No changes to Dockerfile or container
image build steps. If production later chooses to containerize, that is a separate orchestration
decision.

---

### C-3 — Backwards compatibility of existing run commands

**Ambiguity.** Spec 0001 defined independent run commands: `dotnet run --project src/Api` and
`npm run dev`. Aspire is an alternative orchestration path. The ambiguity is whether the old
commands should be preserved for backwards compatibility (allowing developers to choose), or
whether Aspire should become the only documented path, possibly breaking existing workflows, CI
scripts, or developer habits. Preserved commands increase flexibility; removing them simplifies
documentation but breaks existing setups.

**Options presented.**
1. Both always work (independent and Aspire both available) — Aspire is additive, developers choose their entry point, no breaking changes
2. Aspire becomes primary / exclusive path (existing commands deprecated or removed) — simplifies documentation but breaks existing workflows and CI scripts
3. Aspire and independent commands coexist as documented alternatives — same as option 1

**Answer.** Both always work (user confirmed option 1 as Recommended).

**Impact.** Spec requires that `dotnet run --project src/Api` and `npm run dev` continue to
work independently (AC-11, AC-12). The Aspire command is added as an *additional* documented
option in `tech-stack.md`, not a replacement. This preserves backwards compatibility and allows
developers who prefer separate processes or existing CI scripts to continue unchanged. AC-13
addresses potential port conflicts if both are run simultaneously.

---

## Assumptions Made Without Asking

Ambiguities resolved by judgement rather than by asking, because a reasonable default existed
and the alternatives would not have changed the work materially. Listed so they can be
challenged.

| # | Assumption | Default chosen | Reversal cost if wrong |
|---|---|---|---|
| A-1 | Database file location and persistence should remain unchanged | Database persists at configured location (default `./data/app.db`) during Aspire orchestration | Low — purely a configuration detail; can be changed after the fact |
| A-2 | Aspire AppHost project lives in a new directory within the repository (e.g. `infra/aspire/` or similar) | New top-level project structure for Aspire | Medium — project structure can be reorganized, but impacts where developers expect to find the orchestration code |
| A-3 | Application code in backend and frontend needs no changes to support Aspire orchestration | Backend and frontend code remain unmodified; only orchestration configuration and environment variable binding change | Low — if code changes are needed, they are caught during `/plan` |
| A-4 | Port assignment and environment variable binding for the frontend's `API_BASE_URL` can be handled by Aspire's configuration model | Standard Aspire patterns for service discovery and environment binding | Medium — if Aspire's model does not support the pattern, workarounds or additional tooling may be needed |
| A-5 | Aspire AppHost runs in the backend directory or a top-level `infra/` directory, not inside the frontend repository | Aspire is a .NET project, naturally co-located with backend | Low — directory structure is an implementation detail for `/plan` to decide |

## Deferred

Questions raised but explicitly postponed, with where they were recorded.

| # | Question | Deferred to |
|---|---|---|
| D-1 | Should Aspire AppHost also expose a health-check or readiness-probe endpoint that `/plan` can test against? | Plan stage — not a specification question; testing strategy is implementation detail |
| D-2 | Should the Aspire dashboard be mandatory or optional for developers? | Plan stage — usability and developer-experience convention for this project |
| D-3 | How should log output from both services be unified or presented to the developer when running under Aspire? | Plan stage — logging aggregation is implementation detail, not a contract |
