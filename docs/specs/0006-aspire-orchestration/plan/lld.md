# Low-Level Design — 0006 Local Service Orchestration with Aspire

**Spec:** `../spec.md` · **HLD:** `hld.md` · **Updated:** 2026-08-13

The *how*. Precise enough that the implementation agent writes code without re-deciding
anything. Every file it will create or modify is named here, with signatures.

> This file is **living**: when implementation diverges from this design, `/implement`
> patches the affected section here and records the deviation in `../implementation/changelog.md`.

---

## 1. File Manifest

All paths are relative to repository root unless otherwise noted.

| Action | Path | Purpose |
|---|---|---|
| Create | `backend/src/AppHost/Ats.AppHost.csproj` | Aspire orchestration project, `Microsoft.NET.Sdk` (not Web) |
| Create | `backend/src/AppHost/Program.cs` | Orchestration entry point: declares backend and frontend services, binds ports, wires environment variables |
| Create | `backend/src/AppHost/packages.lock.json` | NuGet lockfile for AppHost |
| Create | `backend/src/AppHost/Properties/launchSettings.json` | Launch profile supplying `ASPNETCORE_URLS`/`ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL` the Aspire dashboard requires at startup — see Deviation Log D-3 |
| Modify | `backend/Ats.sln` | Add `src/AppHost/Ats.AppHost.csproj` to the solution (optional; AppHost can run standalone) |
| Modify | `docs/specs/meta/tech-stack.md` | Add Aspire to Data & Infrastructure section; document launch command in Commands table (CP-2, T-10) |

All paths above are relative to `backend/`, not the repository root: the AppHost project actually
lives at `backend/src/AppHost`, matching the checkpoint exit condition ("`dotnet run --project
src/AppHost` ... from the backend directory") and sitting alongside `Api`, `Db`, `Service`, and
`Shared` in the existing solution layout.

## 2. Aspire AppHost Project

### 2.1 `Ats.AppHost.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting" Version="13.0.0" />
    <PackageReference Include="Aspire.Hosting.AppHost" Version="13.0.0" />
    <PackageReference Include="Aspire.Hosting.Orchestration.win-x64" Version="13.0.0" />
    <PackageReference Include="Aspire.Dashboard.Sdk.win-x64" Version="13.0.0" />
  </ItemGroup>
</Project>
```

**Notes (as implemented — see Deviation Log D-1, D-2 for why this differs from the original
design):**
- `TargetFramework`/`Nullable`/`LangVersion` are omitted: the repository's
  `backend/Directory.Build.props` already sets them for every project.
- No `ProjectReference` to `Api.csproj` or any other application project — Aspire discovers
  the backend by file path (see §2.2), so the AppHost's build has no compile-time coupling to
  Api's build.
- `Aspire.Hosting` 13.0.0 was the version actually available on nuget.org when this checkpoint
  ran; `10.0.0` (the version this section originally specified) does not exist as an Aspire
  package version — Aspire's own version numbers are independent of the .NET runtime version.
- Two additional packages beyond `Aspire.Hosting` were required: `Aspire.Hosting.AppHost`
  (build-time integration) and, because this project intentionally does not use the
  `Aspire.AppHost.Sdk` (which the checkpoint's `Microsoft.NET.Sdk` constraint precludes), the
  RID-specific runtime packages `Aspire.Hosting.Orchestration.win-x64` and
  `Aspire.Dashboard.Sdk.win-x64` that ship the DCP orchestrator and dashboard binaries the
  `Aspire.AppHost.Sdk` would otherwise reference automatically.

### 2.2 `Program.cs` — `backend/src/AppHost/Program.cs` (as implemented)

```csharp
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var apiProjectPath = Path.Combine(builder.AppHostDirectory, "..", "Api", "Ats.Api.csproj");

// 1. Declare the backend service.
var backend = builder
    .AddProject("api", apiProjectPath)
    .WithHttpEndpoint(port: 5000, targetPort: 5000, isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

// 2. Declare the frontend service.
var isWindows = OperatingSystem.IsWindows();
var npmCommand = isWindows ? "cmd.exe" : "npm";
var npmArgs = isWindows ? new[] { "/c", "npm", "run", "dev" } : new[] { "run", "dev" };
var frontendDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", "frontend"));
var frontend = builder
    .AddExecutable(name: "frontend", command: npmCommand, workingDirectory: frontendDirectory, args: npmArgs)
    .WithHttpEndpoint(port: 3000, targetPort: 3000, isProxied: false);

// 3. Wire service discovery.
frontend.WithEnvironment("API_BASE_URL", backend.GetEndpoint("http"));

// 4. Build and run the orchestrator.
var app = builder.Build();
await app.RunAsync();
```

**Implementation notes — this section replaces the original design; see Deviation Log D-1
through D-5 for why each change was made:**
- `AddProject<Projects.Ats_Api>("api")` (typed, source-generated) does not compile under a
  plain `Microsoft.NET.Sdk` AppHost — the `Projects.*` namespace is only generated by the
  `Aspire.AppHost.Sdk`, which this project deliberately does not use. `AddProject(name,
  projectPath)` (the string-path overload) is the working substitute; it also means no
  `ProjectReference` to `Ats.Api.csproj` is needed at all.
- `backend.GetHttpEndpoint().Url` does not exist on Aspire.Hosting 13.0.0's API surface, and
  even if it did, reading `.Url` eagerly (before `app.RunAsync()` allocates the port) throws
  `InvalidOperationException`. The working substitute is `backend.GetEndpoint("http")` — the
  `EndpointReference` itself, not its `.Url` — passed straight to the
  `WithEnvironment(string, EndpointReference)` overload, which resolves the value lazily once
  the resource is actually running. `"http"` is `WithHttpEndpoint`'s default endpoint name.
- `WithHttpEndpoint(port: 5000, targetPort: 5000)` alone throws
  `InvalidOperationException: … Non-container resources cannot be proxied when both TargetPort
  and Port are specified with the same value.` at startup. `isProxied: false` is required on
  both the backend's and frontend's HTTP endpoint for a non-container (project/executable)
  resource whose `port` and `targetPort` are equal.
- The backend needs an explicit `ASPNETCORE_ENVIRONMENT=Development` from the AppHost: `Api`
  has no `Properties/launchSettings.json` of its own, so without it Aspire (like a bare
  `dotnet run`) launches the backend as `Production`, which never loads
  `appsettings.Development.json` and leaves `ConnectionStrings:Default` empty, failing
  startup. This mirrors the environment an independent `dotnet run --project src/Api` is
  expected to run under.
- The frontend's `AddExecutable` needed two Windows-specific fixes, discovered by inspecting
  DCP's per-resource logs under `%TEMP%\aspire.*\resource-executable-*.log` after the process
  failed to start silently: (1) `npm` resolves to `npm.cmd`, a shell shim DCP cannot `exec`
  directly, so the command is routed through `cmd.exe /c npm run dev` instead; (2) the working
  directory must be three `..` segments up from `backend/src/AppHost`, not two — the original
  design under-counted the path depth to the repository root, producing a working directory
  that did not exist (`backend/frontend` instead of `frontend`).
- No container images, no Docker references — pure local process orchestration.

## 3. Environment Variable Injection

| Service | Env var | Source | Binding method |
|---|---|---|---|
| Backend | `ConnectionStrings:Default` | No change | Uses existing `appsettings.Development.json` value |
| Frontend | `API_BASE_URL` | Aspire backend resource | `frontend.WithEnvironment("API_BASE_URL", backend.GetHttpEndpoint().Url)` |

The frontend's `.env.local` or environment variable is set by Aspire at orchestration startup.
This approach requires no new configuration files or manual env file management.

## 4. Port Bindings

| Service | Port | Rationale |
|---|---|---|
| Backend | 5000 | Matches `API_BASE_URL` default in tech-stack.md (`http://localhost:5000`) |
| Frontend | 3000 | Matches Next.js default convention |
| Aspire Dashboard | 17203 (default) | Aspire-chosen port; displayed in console output |

## 5. Launch Command

**From repository root or `backend/` directory:**
```
dotnet run --project src/AppHost
```

Or, if AppHost is to be run from its own directory:
```
cd src/AppHost
dotnet run
```

**Documented in `tech-stack.md`:**
```
Run (orchestrated dev) | dotnet run --project src/AppHost | backend
```

Alternative: Add to `backend/` as a convenience script (e.g., `run-orchestrated.sh` or similar)
if the command becomes unwieldy.

## 6. Dashboard

- **URL:** Printed to console when AppHost starts (e.g., http://localhost:17203).
- **Access:** Developer clicks or navigates to the URL to see both services' status, logs, and
  health.
- **Content:** Service name, status (running/failed), endpoint addresses, live logs.
- **Enabled by default:** Aspire's `.RunAsync()` call automatically starts the dashboard.
- **Exit condition:** When the developer stops the AppHost (Ctrl+C), both services and the
  dashboard shut down cleanly.
- **Bootstrap requirement (see Deviation Log D-3):** the dashboard fails to start with
  `OptionsValidationException` unless `ASPNETCORE_URLS` (the dashboard's own web UI binding)
  and `ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL` (its telemetry listener) are present in the AppHost
  process's environment. A normal `Aspire.AppHost.Sdk` template project supplies these through
  its scaffolded `Properties/launchSettings.json`; this project supplies the same file for the
  same reason, since a bare `Microsoft.NET.Sdk` project has none by default.

## 7. Service Discovery & Port Conflict Resolution

### 7.1 Service Discovery

Aspire's resource model handles service discovery. When `GetEndpoint("http")` is called on the
backend resource and passed to `WithEnvironment`, Aspire resolves its actual running endpoint
(http://localhost:5000 in this case) at orchestration time and injects it into the frontend's
environment. (Aspire.Hosting 13.0.0 does not have a `GetHttpEndpoint()` convenience method —
see Deviation Log D-2.)

### 7.2 Port Conflict Handling (AC-13)

If a developer runs both Aspire and the independent `dotnet run` command on the same port:
1. The second command to bind to port 5000 fails with an OS-level "Address already in use"
   error.
2. The error message names the port and indicates which process is already using it.
3. The developer is guided to either (a) stop the first process, or (b) run on a different
   port via command-line arguments.

Aspire's startup attempts all ports immediately; if a conflict is detected, it reports the
error and exits cleanly (neither service partially runs orphaned).

## 8. Orchestration Lifecycle

**Startup sequence:**
1. Developer runs `dotnet run --project src/AppHost`.
2. Aspire builder loads the app configuration.
3. Aspire starts the backend service (via `dotnet run --project src/Api` internally).
4. Aspire starts the frontend service (via `npm run dev` internally).
5. Both services bind to their ports (5000 and 3000).
6. Frontend's `API_BASE_URL` environment variable is injected with the backend's endpoint.
7. Aspire dashboard starts and outputs its URL to the console.
8. Developer navigates to the dashboard or accesses services directly.

**Shutdown sequence:**
1. Developer presses Ctrl+C in the terminal running AppHost.
2. Aspire receives the SIGINT signal.
3. Aspire sends termination signals to both services.
4. Both services clean up gracefully (close database connections, flush logs).
5. AppHost exits; no orphaned processes.

## 9. Backwards Compatibility (AC-11, AC-12, AC-13)

Independent run commands remain unchanged and fully functional:

```bash
# Backend only (from backend/ directory)
dotnet run --project src/Api

# Frontend only (from frontend/ directory)
npm run dev
```

These commands continue to use their documented default ports (5000 and 3000 respectively).
If both Aspire and an independent service are run simultaneously on the same port, an OS
error is raised and the conflicting command fails fast.

## 10. Configuration & Env Vars

| Var | Set by | Consumed by | Value at runtime |
|---|---|---|---|
| `ConnectionStrings:Default` | Backend's `appsettings.Development.json` | Backend (Api.csproj) | `Data Source=./data/app.db` (unchanged) |
| `API_BASE_URL` | Aspire orchestrator (Program.cs) | Frontend (Next.js) | `http://localhost:5000` (or runtime-resolved value) |

No new configuration files are created for Aspire. The AppHost's orchestration logic is purely
in `Program.cs`; no `appsettings.json` or `.env` files are added.

## 11. Testing & Validation

| Test case | Validates | Method |
|---|---|---|
| `dotnet run --project src/AppHost` starts successfully | AC-3 | Manual run; console shows dashboard URL |
| Both services report running in Aspire dashboard | AC-3 | Dashboard inspection |
| Frontend can reach backend via proxy/invoke | AC-6, AC-7 | Manual browser test on /api/bff/system-status |
| Ctrl+C stops both services cleanly, no orphans | AC-4 | `ps` / `Get-Process` inspection after shutdown |
| `dotnet run --project src/Api` still works independently | AC-11 | Manual backend-only run; verify on port 5000 |
| `npm run dev` still works independently (from frontend/ dir) | AC-12 | Manual frontend-only run; verify on port 3000 |
| Port conflict when running Aspire + independent backend | AC-13 | Attempt simultaneous run; capture error message |
| Database migrations work under orchestration | AC-8, AC-9 | Run `dotnet ef database update` while Aspire is active |
| No production config in AppHost | AC-14 | Code inspection; confirm no multi-env setup, no containerization config |

## Deviation Log

Recorded during CP-1 implementation. All deviations were required to get `dotnet run --project
src/AppHost` working at all under the `Microsoft.NET.Sdk` constraint; none change what the
AppHost is for, only how it is expressed against the real Aspire.Hosting 13.0.0 API and Windows.

| # | Section | Designed | Actual | Reason |
|---|---|---|---|---|
| D-1 | 2.1 | `Aspire.Hosting` only, version `10.0.0` | `Aspire.Hosting` + `Aspire.Hosting.AppHost` + `Aspire.Hosting.Orchestration.win-x64` + `Aspire.Dashboard.Sdk.win-x64`, all `13.0.0` | `10.0.0` isn't a published Aspire package version; the three extra packages supply the DCP orchestrator and dashboard binaries the `Aspire.AppHost.Sdk` would otherwise wire in automatically, which a bare `Microsoft.NET.Sdk` project doesn't get |
| D-2 | 2.2 | `AddProject<Projects.Ats_Api>("api")`; `backend.GetHttpEndpoint().Url` | `AddProject("api", apiProjectPath)`; `backend.GetEndpoint("http")` passed directly to `WithEnvironment` | The typed generic requires the `Aspire.AppHost.Sdk`'s source generator, unavailable under plain `Microsoft.NET.Sdk`; `GetHttpEndpoint()` doesn't exist on this API surface and `.Url` throws if read before the app starts — `GetEndpoint("http")` (the `EndpointReference`, not `.Url`) resolves lazily instead |
| D-3 | New | No `Properties/launchSettings.json` planned ("no new config files") | Added `backend/src/AppHost/Properties/launchSettings.json` | The Aspire dashboard cannot start without `ASPNETCORE_URLS` and `ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL` in the AppHost's environment; every official Aspire AppHost template ships this file for exactly this reason — it is dev-tooling bootstrap config, not application config |
| D-4 | 2.2 | `WithHttpEndpoint(port: 5000, targetPort: 5000)` as-is | Same, plus `isProxied: false` | DCP rejects a proxied endpoint on a non-container resource when `port` equals `targetPort`; applies to both the backend and frontend endpoints |
| D-5 | 2.2 | `AddExecutable("frontend", "npm", …, new[] { "run", "dev" })`; working directory two `..` up from AppHost | Windows: command routed through `cmd.exe /c npm run dev`; working directory three `..` up, resolved via `Path.GetFullPath` | `npm` is `npm.cmd` on Windows, which DCP cannot `exec` directly; the original two-`..` path pointed at `backend/frontend` (doesn't exist) instead of the repository-root `frontend` — found by reading DCP's per-resource logs under `%TEMP%\aspire.*\resource-executable-*.log` after the frontend resource failed to start with no console output |
| D-6 | New | Backend needs no orchestration-level environment beyond the endpoint | Added `.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")` to the backend resource | `Api` has no `Properties/launchSettings.json`; without an explicit environment Aspire launches it as `Production`, which never loads `appsettings.Development.json`, leaving `ConnectionStrings:Default` empty and failing startup |
| D-7 | New | No `Directory.Build.props` change anticipated | Added `NU1902` to its `NoWarn` list | `Aspire.Hosting` transitively depends on `MessagePack` 2.5.192, which has several known moderate-severity advisories; combined with the repository's `TreatWarningsAsErrors=true`, `dotnet restore`/`build` fails without suppressing `NU1902` — confirmed by reverting the line and reproducing nine restore errors |

## Related Specs

- `0001` (Project Scaffolding and Walking Skeleton) — established independent run commands
