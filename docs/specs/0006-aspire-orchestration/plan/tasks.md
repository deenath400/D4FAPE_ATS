# Tasks — 0006 Local Service Orchestration with Aspire

**Spec:** `../spec.md` · **LLD:** `lld.md` · **Updated:** 2026-08-13

Execution order for `/implement`. Tasks are grouped into **checkpoints**; `/implement` runs
one checkpoint per invocation, then stops for review.

**Progress:** 10 / 10 tasks · checkpoint CP-2 of 2 complete — all tasks done

---

## How to read this file

- `- [ ]` pending · `- [x]` done · `- [~]` in progress (partial, see changelog)
- Each task names the files it touches and the `AC-n` it serves.
- A checkpoint ends at a state where the project **builds and its tests pass**. Never define
  a checkpoint that leaves the tree broken.
- `/implement` ticks boxes only after the code exists and the checkpoint's tests run green.

---

## CP-1 — Aspire AppHost Creation & Orchestration Wiring

*Exit condition: `dotnet run --project src/AppHost` starts successfully from the backend
directory, both backend and frontend services report running in the Aspire dashboard, the
dashboard is accessible and interactive, pressing Ctrl+C terminates both services cleanly
with no orphaned processes.*

- [x] **T-1** — Create AppHost project and declare services
  - Files: `src/AppHost/Ats.AppHost.csproj`, `src/AppHost/Program.cs` (part 1: service declarations and port bindings)
  - Covers: AC-1, AC-2, AC-5
  - Depends on: —
  - Details:
    - Create `Ats.AppHost.csproj` with `Microsoft.NET.Sdk`, no Web SDK
    - Add `Aspire.Hosting` NuGet package (pinned version per NuGet lockfile)
    - Create `Program.cs` with `DistributedApplication.CreateBuilder()`
    - Declare backend service via `AddProject<Projects.Ats_Api>("api")` with `.WithHttpEndpoint(port: 5000, targetPort: 5000)`
    - Declare frontend service via `AddExecutable()` for `npm run dev` with `.WithHttpEndpoint(port: 3000, targetPort: 3000)`

- [x] **T-2** — Wire environment variables (API_BASE_URL) for service discovery
  - Files: `src/AppHost/Program.cs` (part 2: environment binding)
  - Covers: AC-2, AC-5 (service discovery binding)
  - Depends on: T-1
  - Details:
    - Call `frontend.WithEnvironment("API_BASE_URL", backend.GetHttpEndpoint().Url)` to bind frontend's env var to backend's runtime endpoint
    - Verify Aspire's resource-reference model resolves the endpoint correctly (http://localhost:5000)
    - No new config files; binding is declarative in Program.cs only

- [x] **T-3** — Test orchestration startup and shutdown
  - Files: — (manual validation)
  - Covers: AC-3, AC-4
  - Depends on: T-2
  - Details:
    - Run `dotnet run --project src/AppHost` from repository root or `backend/` directory
    - Verify both services report "running" in Aspire dashboard
    - Verify dashboard URL is printed to console (e.g., http://localhost:17203)
    - Verify backend is reachable at http://localhost:5000
    - Verify frontend is reachable at http://localhost:3000
    - Press Ctrl+C and verify both services shut down gracefully
    - Verify `ps` / `Get-Process` shows no orphaned processes (e.g., stray `npm` or `dotnet` processes)

- [x] **T-4** — Test backend-frontend connectivity through proxy (AC-6, AC-7)
  - Files: — (manual validation)
  - Covers: AC-6, AC-7
  - Depends on: T-3
  - Details:
    - With Aspire running and both services healthy, navigate to `http://localhost:3000` in a browser
    - Trigger the frontend's proxy call to backend (via the system-status endpoint from spec 0001)
    - Verify the request succeeds and returns the backend's response
    - Verify the frontend can render the backend status without errors
    - Confirm the flow: browser → frontend (port 3000) → proxy handler → invoke function → backend (port 5000) → response

## CP-2 — Backwards Compatibility, Database Operations & Documentation

*Exit condition: independent `dotnet run --project src/Api` and `npm run dev` commands both
work on their documented ports; database migrations succeed under Aspire; port conflicts are
handled cleanly; `tech-stack.md` is updated with the Aspire command; no production
configuration or multi-environment setup is present in the AppHost.*

- [x] **T-5** — Test database operations under Aspire orchestration
  - Files: — (manual validation)
  - Covers: AC-8, AC-9
  - Depends on: T-3
  - Details:
    - Start Aspire with both services running
    - In a separate terminal, run `dotnet ef database update --project backend/src/Db` while Aspire is active
    - Verify the migration succeeds and SQLite file is created at the configured path
    - Stop and restart Aspire
    - Verify the database file persists at the same location
    - Verify the migration history in `__EFMigrationsHistory` table is retained
    - Run the migration again; verify it is a no-op (no new migrations applied)

- [x] **T-6** — Verify independent backend command continues to work
  - Files: — (manual validation)
  - Covers: AC-11
  - Depends on: —
  - Details:
    - Without Aspire running, open a terminal in `backend/` directory
    - Run `dotnet run --project src/Api`
    - Verify the backend starts on port 5000 (or its configured port)
    - Verify `GET /api/system/status` responds successfully
    - Verify the behavior is identical to running the backend independently before this spec
    - Stop the backend with Ctrl+C

- [x] **T-7** — Verify independent frontend command continues to work
  - Files: — (manual validation)
  - Covers: AC-12
  - Depends on: —
  - Details:
    - Without Aspire running, open a terminal in `frontend/` directory
    - Run `npm run dev`
    - Verify the frontend starts on port 3000 (or its configured port)
    - Verify the landing page is reachable at `http://localhost:3000`
    - Verify the behavior is identical to running the frontend independently before this spec
    - Stop the frontend with Ctrl+C

- [x] **T-8** — Test port conflict detection (AC-13)
  - Files: — (manual validation)
  - Covers: AC-13
  - Depends on: T-6, T-7 (or T-3)
  - Details:
    - Start Aspire with `dotnet run --project src/AppHost` (occupies ports 5000 and 3000)
    - In a separate terminal, attempt to run `dotnet run --project backend/src/Api` (tries to bind port 5000)
    - Capture the error output; verify it names the port (5000) and indicates "address already in use" or similar
    - Verify Aspire continues running unaffected
    - Stop the independent backend attempt
    - Verify Aspire is still running with both services healthy
    - Alternative test: start the independent backend first, then Aspire; verify Aspire fails fast with a clear port-conflict error

- [x] **T-9** — Verify development-only scope (AC-14)
  - Files: `src/AppHost/Program.cs`, `src/AppHost/Ats.AppHost.csproj`
  - Covers: AC-14
  - Depends on: T-1
  - Details:
    - Code review: inspect `src/AppHost/Program.cs` for any production deployment configuration (e.g., multi-environment appsettings, containerization directives, cloud resource declarations)
    - Verify no `appsettings.json` or multi-environment config exists in AppHost
    - Verify no Docker or container build instructions are referenced
    - Confirm the AppHost is purely a local orchestration tool with no production artifact
    - Verify tech-stack.md does not list Aspire as a production infrastructure component (Infrastructure row remains "TBD")

- [x] **T-10** — Update tech-stack.md with Aspire command
  - Files: `docs/specs/meta/tech-stack.md`
  - Covers: AC-10
  - Depends on: T-3 (command must exist and work first)
  - Details:
    - Add "Aspire" entry to Data & Infrastructure section (under "Orchestration / service management"), noting it is development-only
    - In Commands table, add a row for "Run (orchestrated dev)": `dotnet run --project src/AppHost` from `backend/` working directory
    - Update the "Run (dev)" row to clarify that independent commands (`dotnet run --project src/Api` and `npm run dev`) remain the primary methods
    - Alternatively, document both as co-equal options, with a note that developers can choose either based on preference
    - Verify all commands in the Commands table are still literal and executable

---

## Coverage Check

Every acceptance criterion is covered by at least one task. No AC is covered more than once
(unless it serves as a prerequisite or dependency check).

| AC | Covered by |
|---|---|
| AC-1 | T-1 |
| AC-2 | T-1, T-2 |
| AC-3 | T-3 |
| AC-4 | T-3 |
| AC-5 | T-1, T-2 |
| AC-6 | T-4 |
| AC-7 | T-4 |
| AC-8 | T-5 |
| AC-9 | T-5 |
| AC-10 | T-10 |
| AC-11 | T-6 |
| AC-12 | T-7 |
| AC-13 | T-8 |
| AC-14 | T-9 |

All 14 ACs are covered. No gaps detected.

## Parallelisable

- T-6, T-7 can run independently in parallel with T-3–T-4 (they test backwards compatibility
  without Aspire, which does not depend on Aspire existing yet).
- T-9 (code review for development-only scope) can run alongside T-1–T-2 once the AppHost
  code is written.
- T-10 (documentation) has a weak dependency on T-3 (the command must work), but can be
  drafted in parallel once the command is known.

---

## Checkpointing Notes

**CP-1 exits when:**
- Aspire AppHost builds successfully (`dotnet build src/AppHost`)
- Both services start cleanly via `dotnet run --project src/AppHost`
- Dashboard displays both services as running
- Shutdown is clean and graceful (no orphaned processes)
- Frontend can communicate with backend through the proxy (AC-6, AC-7 are tested)

**CP-2 exits when:**
- Database operations work under orchestration
- All backwards-compatibility tests pass
- Port conflicts are handled with clear error messages
- Documentation is updated and accurate
- Code is reviewed to confirm development-only scope

---

## Related Specs

- `0001` (Project Scaffolding and Walking Skeleton) — established independent run commands
