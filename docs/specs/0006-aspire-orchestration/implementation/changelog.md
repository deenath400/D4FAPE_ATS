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
| `backend/Ats.sln` | New — the repository had no solution file before this checkpoint (`tech-stack.md`'s Commands never referenced one); created with `Api`, `Db`, `Service`, `Shared`, the three test projects, and `AppHost` |
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
