# Implementation Changelog — 0006 Local Service Orchestration with Aspire

What actually shipped, checkpoint by checkpoint. Append-only. This is the record `/validate`
and future specs consult to learn what is really in the code, as opposed to what was designed.

---

## CP-1 — Aspire AppHost Creation & Orchestration Wiring · 2026-08-14

**Tasks completed:** T-1, T-2, T-3, T-4

**Files created**

| Path | Purpose |
|---|---|
| `backend/src/AppHost/Ats.AppHost.csproj` | Aspire orchestration project, `Microsoft.NET.Sdk` (not Web) |
| `backend/src/AppHost/Program.cs` | Declares the `api` and `frontend` resources, port bindings, and `API_BASE_URL` service-discovery wiring |
| `backend/src/AppHost/Properties/launchSettings.json` | Launch profile the Aspire dashboard needs to bind its own web UI and OTLP endpoint (see Deviation D-3) |
| `backend/src/AppHost/packages.lock.json` | NuGet lockfile for the AppHost project |

**Files modified**

| Path | Change |
|---|---|
| `backend/Ats.slnx` | Extended with the `AppHost` project. **Correction (post-validation):** CP-1 originally created a second, duplicate `backend/Ats.sln` on the (false) premise that no solution file existed yet — `Ats.slnx` has existed since spec 0001. The duplicate broke every bare `dotnet` command (`MSB1011: more than one project or solution file`), which `/validate` caught as finding F-1. `Ats.sln` was deleted and `AppHost` was added to the pre-existing `Ats.slnx` instead. |
| `backend/src/Api/Properties/launchSettings.json` | New (post-validation fix for finding F-2/AC-11). `src/Api` had never had a launch profile at any point since spec 0001, so a bare `dotnet run --project src/Api` defaulted to `Production`, never loaded `appsettings.Development.json`, and failed on a missing `ConnectionStrings:Default`. Adds a `Development`-environment profile on port 5000, matching the port AppHost already assumed. |
| `backend/src/AppHost/Program.cs` | **Correction (post-validation, F-5).** Adding `src/Api`'s launch profile (above) caused Aspire's `AddProject` to auto-derive an `http` endpoint from its `applicationUrl` (port 5000). The backend declaration's pre-existing explicit `.WithHttpEndpoint(port: 5000, targetPort: 5000, isProxied: false)` call had no distinct `name:`, so it collided with the auto-derived one (`DistributedApplicationException: Endpoint with name 'http' already exists`) and the AppHost never left the resource-graph-construction phase. Fix: removed the explicit `.WithHttpEndpoint(...)` and `.WithEnvironment("ASPNETCORE_ENVIRONMENT", ...)` calls entirely — both are now redundant with what the launch profile already provides, and `backend.GetEndpoint("http")` (used to wire the frontend's `API_BASE_URL`) still resolves against the auto-derived endpoint under the same name. Re-verified live: AppHost starts cleanly, backend/frontend/dashboard all return 200/200/302, and shutdown leaves no orphaned processes or bound ports. |
| `backend/Directory.Build.props` | Added `NU1902` to `NoWarn` — see Decision I-3a below |
| `docs/specs/0006-aspire-orchestration/plan/lld.md` | Patched §2.1, §2.2, §6, §7.1 to match the actual implementation; added a Deviation Log (D-1 through D-6) |
| `docs/specs/meta/architecture.md` | `infra/build` row now mentions the AppHost and owning spec 0006; one Change Log row appended |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-1 | Used `AddProject("api", apiProjectPath)` (string-path overload) instead of the typed `AddProject<Projects.Ats_Api>("api")` | The typed generic requires the `Aspire.AppHost.Sdk`'s source generator, which the checkpoint's `Microsoft.NET.Sdk`-only constraint precludes; confirmed by a failing build attempt (`CS0234: The type or namespace name 'Ats_Api' does not exist in the namespace 'Projects'`) |
| I-2 | No `ProjectReference` from AppHost to `Api.csproj` | Not required by the string-path `AddProject` overload; keeps `dotnet build src/AppHost` decoupled from Api's own build, matching the LLD's original intent |
| I-3 | Added RID-specific `Aspire.Hosting.Orchestration.win-x64` / `Aspire.Dashboard.Sdk.win-x64` packages | Plain `Microsoft.NET.Sdk` does not pull in the DCP orchestrator or dashboard binaries the way `Aspire.AppHost.Sdk` would; without them `dotnet run` fails with `OptionsValidationException: … CliPath … DashboardPath …` before any resource starts |
| I-4 | Added `Properties/launchSettings.json` | The Aspire dashboard requires `ASPNETCORE_URLS` and `ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL` in the AppHost's environment to bind its own web UI/OTLP listener; every official Aspire AppHost template ships one for this reason |
| I-5 | Set `isProxied: false` on both HTTP endpoints | DCP rejects a proxied endpoint on a non-container resource whose `port` equals `targetPort`: `InvalidOperationException: … Non-container resources cannot be proxied when both TargetPort and Port are specified with the same value` |
| I-6 | Set `ASPNETCORE_ENVIRONMENT=Development` on the backend resource | `Api` ships no `Properties/launchSettings.json`; without an explicit environment Aspire runs it as `Production`, which skips `appsettings.Development.json` and leaves `ConnectionStrings:Default` empty, failing backend startup with `CRITICAL: Missing required configuration key 'ConnectionStrings:Default'` — reproduced independently confirming this is a standing gap in how `dotnet run --project src/Api` behaves without an environment variable set externally, not something this checkpoint introduced |
| I-7 | Routed the frontend command through `cmd.exe /c npm run dev` on Windows, and fixed the working-directory depth to three `..` segments | `npm` resolves to `npm.cmd`, which DCP cannot `exec` directly (`fork/exec …npm.cmd: The directory name is invalid`); separately, the original two-`..`-segment path pointed at a nonexistent `backend/frontend` directory instead of the repository-root `frontend` — both diagnosed from DCP's per-resource logs under `%TEMP%\aspire.*\resource-executable-*.log` |
| I-8 | Added `NU1902` to `backend/Directory.Build.props`'s `NoWarn` | `Aspire.Hosting` transitively pulls in `MessagePack` 2.5.192 (via `StreamJsonRpc`), which has several known moderate-severity advisories; with the repository's existing `TreatWarningsAsErrors=true`, `dotnet restore`/`build` fails outright (`NU1902: Warning As Error`) without this suppression. Confirmed by temporarily reverting the line and reproducing nine `NU1902` restore errors, all against `Ats.AppHost.csproj`. This line was already present, uncommitted, in the working tree when this checkpoint began (see Needs Your Attention in the final report) — it is recorded here because it is a real, necessary prerequisite for this checkpoint's build to succeed, not because this checkpoint authored the original edit |

**Deviations from the LLD**

All recorded in `plan/lld.md` § Deviation Log as D-1 through D-6. Summary:

| LLD section | Designed | Actual | Reason | LLD patched? |
|---|---|---|---|---|
| §2.1 | `Aspire.Hosting` only, `10.0.0` | Four packages, all `13.0.0` | `10.0.0` isn't a real Aspire version; three extra packages are needed to replace what `Aspire.AppHost.Sdk` would otherwise supply | Yes |
| §2.2 | `AddProject<Projects.Ats_Api>`; `GetHttpEndpoint().Url` | `AddProject(name, path)`; `GetEndpoint("http")` passed directly | Typed generic requires a source generator unavailable under plain SDK; `GetHttpEndpoint()` doesn't exist on this API and `.Url` throws before the app starts | Yes |
| New | No config files beyond `Program.cs`/`.csproj` | Added `Properties/launchSettings.json` | Dashboard cannot bootstrap without it | Yes |
| §2.2 | `WithHttpEndpoint(port, targetPort)` | Same + `isProxied: false` | DCP rejects proxied non-container endpoints with equal port/targetPort | Yes |
| §2.2 | `AddExecutable("npm", …, two "..")` | `cmd.exe /c npm …`, three ".." | Windows `.cmd` exec limitation; original path depth was wrong | Yes |
| New | Backend needs no orchestration-level env beyond the endpoint | Added `ASPNETCORE_ENVIRONMENT=Development` | Backend has no launchSettings.json of its own and defaults to Production without it | Yes |

**Verification run**

```
$ dotnet build Ats.sln          # (from backend/)
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test tests/Ats.ArchitectureTests --no-build
Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 238 ms

$ dotnet run --project src/AppHost      # (from backend/)
Using launch settings from src\AppHost\Properties\launchSettings.json...
Building...
info: Aspire.Hosting.DistributedApplication[0]
      Aspire version: 13.0.0+7512c2944094a58904b6c803aa824c4a4ce42e11
info: Aspire.Hosting.DistributedApplication[0]
      Distributed application starting.
info: Aspire.Hosting.DistributedApplication[0]
      Application host directory is: C:\D_Drive\D4FAPE-_ATS\backend\src\AppHost
info: Aspire.Hosting.Dcp.DcpHost[0]
      Starting DCP with arguments: start-apiserver --monitor 25920 --detach --kubeconfig "...\kubeconfig"
info: Aspire.Hosting.DistributedApplication[0]
      Distributed application started. Press Ctrl+C to shut down.
info: Aspire.Hosting.DistributedApplication[0]
      Now listening on: http://localhost:17203
info: Aspire.Hosting.DistributedApplication[0]
      Login to the dashboard at http://localhost:17203/login?t=...

# Reachability checks while running:
$ curl http://localhost:5000/api/system/status
{"version":"1.0.0.0","database":{"reachable":true,"schemaCurrent":true}}   # HTTP 200

$ curl http://localhost:3000/api/bff/system-status
{"version":"1.0.0.0","database":{"reachable":true,"schemaCurrent":true}}   # HTTP 200 — proxied through the frontend, confirms AC-6/AC-7

$ curl -o /dev/null -w "%{http_code}" http://localhost:17203/
302   # dashboard redirects to /login — reachable and interactive

# Shutdown (CTRL_C_EVENT sent to the AppHost console process group):
[exited with code 0]

# Post-shutdown checks:
$ curl http://localhost:5000 / :3000 / :17203  -> all connection-refused (000)
$ Get-CimInstance Win32_Process -Filter "Name='dotnet.exe' or Name='node.exe' or Name='dcp.exe'"
  -> no api/frontend/dcp/dashboard processes remain (only unrelated pre-existing processes from
     another repository on the same machine, and this session's own dotnet build MSBuild
     node-reuse workers)
```

A first end-to-end pass returned `503`/`degraded` from `/api/system/status` (database
`schemaCurrent: false`) because no migration had ever been applied to this environment's SQLite
file. `dotnet ef database update --project src/Db --startup-project src/Api` was run once,
out of band, purely to obtain a conclusive T-4 proxy-connectivity result for this checkpoint;
formal migration-under-Aspire testing (apply/restart/persist/no-op-reapply) is CP-2's T-5 and is
not considered complete by this one-off run.

**Meta updates applied**

- `architecture.md`: `infra/build` row now names the AppHost and cites spec 0006; one Change
  Log row appended. No new component, no ER diagram change.
- `tech-stack.md`: not touched this checkpoint — the Commands table update is CP-2's T-10 (it
  depends on the command being proven, which this checkpoint did; documenting it is deferred).
- `coding-standards.md`: no change.

**Known gaps carried into the next checkpoint**

- CP-2's T-5 must formally verify migrate/restart/persist/no-op-reapply behaviour under
  Aspire; this checkpoint only proved that a migration can be applied while Aspire is running.
- The independent `dotnet run --project src/Api` command (documented in `tech-stack.md` today)
  fails in a machine with no `ASPNETCORE_ENVIRONMENT` set anywhere (system, user, or a backend
  `launchSettings.json`) — confirmed by running it directly in this environment. CP-1's AppHost
  papers over this for the orchestrated path via `.WithEnvironment("ASPNETCORE_ENVIRONMENT",
  "Development")`, but the *independent* command has no equivalent fix, and none was made here
  (out of this checkpoint's "no code changes to backend" scope). CP-2's T-6, which is supposed
  to verify the independent command "works... exactly as before this spec", will hit this same
  failure unless the developer's shell already exports `ASPNETCORE_ENVIRONMENT=Development` —
  flagged for the CP-2 implementer/validator to confirm and, if genuinely pre-existing, log as
  a defect against spec 0001 rather than 0006.
- Spec 0006's frontmatter was still `status: specified` when this checkpoint began, despite a
  complete `plan/` (hld.md, lld.md, api.md, erd.md, tasks.md with all 10 tasks defined and
  checkpointed). This looks like a bookkeeping gap left by `/plan` rather than a genuine
  planning gap — the plan itself was complete and internally consistent, so CP-1 proceeded and
  this checkpoint corrects the status to `implementing`.

---

## CP-2 — Backwards Compatibility, Database Operations & Documentation · 2026-08-14

**Tasks completed:** T-5, T-6, T-7, T-8, T-9, T-10

**Files created**

None.

**Files modified**

| Path | Change |
|---|---|
| `docs/specs/meta/tech-stack.md` | Added an "Orchestration" row to Data & Infrastructure (Aspire, development-only); added a "Run (orchestrated dev)" row to Commands; annotated the existing "Run (dev)" row to state the independent commands remain primary and fully supported; added `0006` to Related Specs; refreshed `Updated` date |
| `docs/specs/meta/architecture.md` | One Change Log row appended for CP-2. No component/entity change — the `infra/build` row was already accurate from CP-1 |
| `docs/specs/0006-aspire-orchestration/plan/tasks.md` | Ticked T-5–T-10; progress header updated to 10/10, CP-2 complete |
| `docs/specs/0006-aspire-orchestration/spec.md` | `status: implementing` → `implemented` |
| `docs/specs/index.md` | 0006 row status → `implemented` |

**Decisions made during implementation**

| # | Decision | Why |
|---|---|---|
| I-9 | Stopped the AppHost between T-5's migrate/restart/persist cycle and after T-8 with `taskkill /F` on the AppHost's own `dotnet.exe` PID, rather than an interactive Ctrl+C | This checkpoint's shell has no interactive console attached to the AppHost's process group, so `CTRL_C_EVENT` cannot be delivered the way a developer's terminal would; killing the AppHost's own PID (not its children) was sufficient to observe DCP and all child processes (frontend `node`, `dcp.exe`, dashboard) exit on their own within ~3s, which is evidence of cascading cleanup even under this non-graceful trigger. Graceful Ctrl+C shutdown itself was already fully verified interactively in CP-1's T-3 (see that checkpoint's verification log); this checkpoint did not need to re-prove it |
| I-10 | For T-8's port-conflict test, ran the independent backend with `ASPNETCORE_ENVIRONMENT=Development` set explicitly for that one invocation | Without it, the independent command fails at configuration validation (`CRITICAL: Missing required configuration key 'ConnectionStrings:Default'`) before Kestrel ever attempts to bind a port — the pre-existing gap described below. Setting the variable for this one test isolates the port-bind behaviour AC-13 actually asks about from that separate, already-flagged issue; the variable was not set for T-6, which tests the command exactly as documented |

**Deviations from the LLD**

None. All CP-2 tasks were manual verification against the LLD's §11 Testing & Validation table; no design change was required.

**Verification run**

Build (tree confirmed to build before starting manual verification):
```
$ dotnet build Ats.sln          # (from backend/)
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:04.19
```

T-5 — Database operations under Aspire orchestration (AC-8, AC-9):
```
$ dotnet run --project src/AppHost      # (from backend/)
Using launch settings from src\AppHost\Properties\launchSettings.json...
...
info: Aspire.Hosting.DistributedApplication[0]
      Distributed application started. Press Ctrl+C to shut down.
info: Aspire.Hosting.DistributedApplication[0]
      Now listening on: http://localhost:17203

$ curl http://localhost:5000/api/system/status
{"version":"1.0.0.0","database":{"reachable":true,"schemaCurrent":true}}   # HTTP 200 (migration already applied from CP-1's one-off run)

# db file location (relative to Api's working directory under Aspire): src/Api/data/app.db
# size before: 225280 bytes, mtime 2026-08-14 00:16:01

$ dotnet ef database update --project src/Db --startup-project src/Api    # while Aspire is running
Build started...
Build succeeded.
Acquiring an exclusive lock for migration application. See https://aka.ms/efcore-docs-migrations-lock for more information if this takes too long.
No migrations were applied. The database is already up to date.
Done.

$ dotnet ef migrations list --project src/Db --startup-project src/Api
20260805133328_InitialCreate
20260805141657_AddAuthenticationAndRefreshTokens
20260805171525_AddRequisitionsAndStages
20260805191845_AddApplicationsAndCvAttachments
20260806080934_AddPipelineProgression
# All five listed with no "(Pending)" marker -> full migration history intact in __EFMigrationsHistory

# --- stop AppHost (taskkill /F on the AppHost PID; see Decision I-9), confirm persistence ---
$ powershell -Command "Test-Path 'backend/src/Api/data/app.db'; (Get-Item 'backend/src/Api/data/app.db').Length"
True
225280        # same size as before stop -> file persists at the same path across a stop

# --- restart AppHost ---
$ dotnet run --project src/AppHost
...
info: Aspire.Hosting.DistributedApplication[0]
      Distributed application started. Press Ctrl+C to shut down.
      Now listening on: http://localhost:17203

$ curl http://localhost:5000/api/system/status
{"version":"1.0.0.0","database":{"reachable":true,"schemaCurrent":true}}   # HTTP 200 -> schema still current after restart, no re-migration needed

$ dotnet ef database update --project src/Db --startup-project src/Api    # re-run after restart
Build started...
Build succeeded.
Acquiring an exclusive lock for migration application. See https://aka.ms/efcore-docs-migrations-lock for more information if this takes too long.
No migrations were applied. The database is already up to date.
Done.
```
AC-8 and AC-9 both hold: migration succeeds while Aspire is active, the SQLite file persists at
the same path across a stop/restart, migration history in `__EFMigrationsHistory` is retained,
and a repeat migration run is a confirmed no-op both before and after the restart.

T-6 — Independent backend command (AC-11):
```
$ cd backend && dotnet run --project src/Api     # Aspire NOT running; no env var overrides — exactly the documented command
CRITICAL: Missing required configuration key 'ConnectionStrings:Default'.
EXIT_CODE:1
```
See "Pre-existing issue surfaced" below — this is not a regression caused by 0006.

T-7 — Independent frontend command (AC-12):
```
$ cd frontend && npm run dev

> ats-frontend@0.1.0 dev
> npx --no-install next dev

   ▲ Next.js 15.1.7
   - Local:        http://localhost:3000
   - Network:      http://192.168.1.7:3000

 ✓ Starting...
 ✓ Ready in 1959ms

$ curl -o /dev/null -w "HTTP_CODE:%{http_code}\n" http://localhost:3000/
HTTP_CODE:200
```
Frontend starts on port 3000 and serves the landing page independently of Aspire, unchanged.
Stopped afterward (`taskkill /F` on the `next dev`/`node` process tree — no interactive console
available in this environment for Ctrl+C, see Decision I-9's rationale).

T-8 — Port conflict detection (AC-13):
```
# Aspire running, occupying 5000 and 3000.
$ cd backend && dotnet run --project src/Api                     # no env override: reproduces the pre-existing gap first
CRITICAL: Missing required configuration key 'ConnectionStrings:Default'.
EXIT_CODE:1

$ cd backend && ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Api   # isolates the port-bind path (see Decision I-10)
fail: Microsoft.Extensions.Hosting.Internal.Host[11]
      Hosting failed to start
      System.IO.IOException: Failed to bind to address http://127.0.0.1:5000: address already in use.
       ---> Microsoft.AspNetCore.Connections.AddressInUseException: Only one usage of each socket address (protocol/network address/port) is normally permitted.
       ---> System.Net.Sockets.SocketException (10048): Only one usage of each socket address (protocol/network address/port) is normally permitted.
...
Unhandled exception. System.IO.IOException: Failed to bind to address http://127.0.0.1:5000: address already in use.
EXIT_CODE:127

$ curl http://localhost:5000/api/system/status          # Aspire-managed backend, checked immediately after the conflict attempt
{"version":"1.0.0.0","database":{"reachable":true,"schemaCurrent":true}}   # HTTP 200 -> Aspire's instance is unaffected
```
The error names the port (`http://127.0.0.1:5000`) and states "address already in use" —
satisfies AC-13. Aspire's own instance continued serving requests throughout. The "alternative"
ordering (independent backend first, then start Aspire) was not additionally exercised — the
primary direction above is sufficient evidence for AC-13 and the LLD marks it optional
("Alternative:").

T-9 — Development-only scope (AC-14), code review narrative:
- `src/AppHost/Program.cs` (67 lines, read in full) declares exactly two resources — `api`
  (`AddProject`, string-path) and `frontend` (`AddExecutable`, `npm run dev`) — with port
  bindings and one `WithEnvironment` call each. No `appsettings.json`, no multi-environment
  profile beyond the single hardcoded `ASPNETCORE_ENVIRONMENT=Development` on the backend
  resource, no `AddAzureX`/`AddAWSX`/cloud-resource builder calls, no image/container-build
  API (`WithDockerfile`, `AddContainer`, etc.) anywhere in the file.
- `src/AppHost/Ats.AppHost.csproj` (15 lines, read in full) references only
  `Aspire.Hosting`/`Aspire.Hosting.AppHost`/`Aspire.Hosting.Orchestration.win-x64`/
  `Aspire.Dashboard.Sdk.win-x64`, all `13.0.0` — local orchestrator and dashboard binaries,
  not container or cloud SDKs.
- Directory listing of `src/AppHost` (source files only — `bin`/`obj` excluded) confirms the
  only checked-in files are `Ats.AppHost.csproj`, `Program.cs`, `packages.lock.json`, and
  `Properties/launchSettings.json` — no `Dockerfile`, no `docker-compose.yml`, no
  `appsettings.Production.json`.
- `Properties/launchSettings.json` (read in full) has a single `"http"` profile carrying
  `ASPNETCORE_ENVIRONMENT`/`DOTNET_ENVIRONMENT` both hardcoded to `Development`, plus the
  dashboard's own bootstrap URLs (`ASPNETCORE_URLS`, `ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL`) —
  no second (e.g. `Production`) profile exists.
- `grep -i "docker|container|azure|aws|kubernetes|k8s"` across `src/AppHost` (excluding
  `bin`/`obj`) returns one hit, a code comment ("non-container resource") explaining why
  `isProxied: false` is required for DCP — not a container/cloud feature this project uses.
  `packages.lock.json` transitively pins `KubernetesClient` (a dependency of
  `Aspire.Hosting.Orchestration.win-x64`): this is DCP's own local resource-state API, an
  internal implementation detail of Aspire's dev-time orchestrator, not a real Kubernetes
  cluster or deployment target this project talks to.
- `docs/specs/meta/tech-stack.md`'s Data & Infrastructure "Hosting" row still reads `TBD`
  (verified after T-10's edit, see diff below) — the new "Orchestration" row added by T-10 is
  explicitly annotated "does not pre-empt Hosting, which remains TBD".

Conclusion: AC-14 holds. The AppHost is pure local dev-process orchestration with no production
or multi-environment surface.

T-10 — `tech-stack.md` diff:
```diff
--- a/docs/specs/meta/tech-stack.md
+++ b/docs/specs/meta/tech-stack.md
@@ Data & Infrastructure
 | Hosting | TBD | Deployment target deliberately deferred |
+| Orchestration | .NET Aspire (`src/AppHost`) | Development-only local process orchestration for both deployables (est. 0006); does not pre-empt Hosting, which remains TBD |
 | CI | None | No `.github/workflows/` exists |
@@ Commands
-| Run (dev) | `dotnet run --project src/Api` / `npm run dev` | `backend` / `frontend` |
+| Run (dev) | `dotnet run --project src/Api` / `npm run dev` | `backend` / `frontend` (independent, primary; remain fully supported alongside Aspire — est. 0006) |
+| Run (orchestrated dev) | `dotnet run --project src/AppHost` | `backend` (starts both services + dashboard; development-only — est. 0006) |
@@ Related Specs
 - `0001` (Project Scaffolding and Walking Skeleton)
+- `0006` (Local Service Orchestration with Aspire)
```
Also updated the `**Updated:**` date to 2026-08-14. File is now 92 lines — over the 80-line
target, under the 120-line hard ceiling; no compression required per `meta-maintenance.md` §1.
Every command in the table remains literal and directly executable.

**Pre-existing issue surfaced (not owned by this spec)**

T-6 reproduces exactly the failure CP-1 flagged: `dotnet run --project src/Api`, run
independently (no Aspire, no environment variable set anywhere on this machine — confirmed
`ASPNETCORE_ENVIRONMENT` is unset at shell, user, and machine scope), fails immediately with
`CRITICAL: Missing required configuration key 'ConnectionStrings:Default'.` and exits before
Kestrel attempts to bind a port. Root cause: `backend/src/Api` ships no
`Properties/launchSettings.json`, so a bare `dotnet run` defaults `ASPNETCORE_ENVIRONMENT` to
`Production`, which never loads `appsettings.Development.json` (the only file that supplies a
non-empty `ConnectionStrings:Default`).

This is a **pre-existing gap that predates spec 0006**, not a regression it introduced:
- Spec 0006 makes zero changes to any file under `backend/src/Api` (confirmed against the LLD's
  File Manifest and this checkpoint's own file list above — only `src/AppHost` and
  `docs/specs/meta/tech-stack.md` are touched across both checkpoints).
- Spec 0001's own implementation changelog documents a "fail-fast configuration check on
  `ConnectionStrings:Default`" but no `launchSettings.json` and no record of setting
  `ASPNETCORE_ENVIRONMENT` externally.
- This machine has no `ASPNETCORE_ENVIRONMENT` set at any scope (verified via
  `[System.Environment]::GetEnvironmentVariable` for User and Machine, and an empty shell
  variable), so the documented `dotnet run --project src/Api` command in `tech-stack.md`, taken
  completely literally on a fresh clone, has been broken since spec 0001 shipped it.

AC-11's actual wording ("...exactly as before this spec") is satisfied: Aspire's presence does
not change this behaviour at all — the independent command fails identically whether or not the
AppHost exists in the repository. T-6 is therefore ticked as *investigated and confirmed
unchanged*, not as *the independent command works standalone*. Per this checkpoint's brief,
no fix was applied to `backend/src/Api` (out of spec 0006's scope — it owns only
`src/AppHost` and `tech-stack.md`). **This should be logged as a defect against spec 0001**
(missing `Properties/launchSettings.json` for `Ats.Api`) by whichever process tracks such
things next; spec 0006 surfaces it precisely but does not fix it.

**Meta updates applied**

- `architecture.md`: one Change Log row appended for CP-2. No component, entity, or ER diagram
  change — CP-1 already recorded the AppHost's existence and role.
- `tech-stack.md`: Orchestration row added to Data & Infrastructure; Commands table gained
  "Run (orchestrated dev)" and an annotation on "Run (dev)"; Related Specs gained `0006`. See
  T-10's diff above.
- `coding-standards.md`: no change — nothing in CP-2 established a new project-wide convention.

**Known gaps carried forward**

- The spec-0001 `ASPNETCORE_ENVIRONMENT`/`launchSettings.json` gap described above remains
  unfixed, by design (out of this spec's scope). It affects every fresh clone of the repository
  today, independent of Aspire.
- T-8's "alternative" ordering (start the independent backend first, then Aspire, and confirm
  Aspire itself fails fast) was not additionally exercised; the primary direction tested is
  sufficient evidence for AC-13 and the task explicitly marks the alternative optional.

---
