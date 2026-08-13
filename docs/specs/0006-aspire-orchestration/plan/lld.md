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
| Create | `src/AppHost/Ats.AppHost.csproj` | Aspire orchestration project, `Microsoft.NET.Sdk` (not Web) |
| Create | `src/AppHost/Program.cs` | Orchestration entry point: declares backend and frontend services, binds ports, wires environment variables |
| Create | `backend/src/AppHost/packages.lock.json` | NuGet lockfile for AppHost |
| Modify | `backend/Ats.sln` | Add `src/AppHost/Ats.AppHost.csproj` to the solution (optional; AppHost can run standalone) |
| Modify | `docs/specs/meta/tech-stack.md` | Add Aspire to Data & Infrastructure section; document launch command in Commands table |

## 2. Aspire AppHost Project

### 2.1 `Ats.AppHost.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting" Version="10.0.0" />
  </ItemGroup>

  <ItemGroup>
    <!-- AppHost orchestrates projects; it does not reference them as ProjectReference.
         Instead, it uses reflection or naming conventions to discover them. -->
  </ItemGroup>
</Project>
```

**Notes:**
- No `ProjectReference` to `Api.csproj` or any other application project — Aspire discovers
  services by name and file path.
- `Aspire.Hosting` is the only new NuGet dependency introduced.
- SDK is `Microsoft.NET.Sdk` (not `Microsoft.NET.Sdk.Web`), since the AppHost is not an
  ASP.NET Core application; it is a process orchestrator.

### 2.2 `Program.cs` — `src/AppHost/Program.cs`

```csharp
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// 1. Declare the backend service.
//    - Project name: "api" (must match the project file or resource name Aspire expects)
//    - Working directory: ../Api (relative to AppHost project, pointing to the Api.csproj)
//    - Port: 5000 (matches `API_BASE_URL` default in tech-stack.md)
//    - No code changes to Api.csproj; runs via standard `dotnet run` internally.
var backend = builder
    .AddProject<Projects.Ats_Api>("api")
    .WithHttpEndpoint(port: 5000, targetPort: 5000);

// 2. Declare the frontend service.
//    - Working directory: ../../frontend (Node.js project)
//    - Port: 3000 (matches independent `npm run dev` convention)
//    - Uses AddExecutable to run the npm script (not a .NET project)
//    - Command: npm; Args: run dev
var frontend = builder
    .AddExecutable(
        name: "frontend",
        command: "npm",
        workingDirectory: Path.Combine(builder.AppHostDirectory, "..", "..", "frontend"),
        args: new[] { "run", "dev" })
    .WithHttpEndpoint(port: 3000, targetPort: 3000);

// 3. Wire environment variables.
//    - Backend: receives API_BASE_URL if needed (optional, only if backend needs to call itself).
//    - Frontend: receives API_BASE_URL pointing to the backend's resource endpoint.
//      Aspire's resource model automatically resolves the backend's http endpoint.
frontend
    .WithEnvironment("API_BASE_URL", backend.GetHttpEndpoint().Url);

// 4. Build and run the orchestrator.
//    - Aspire automatically starts/stops both services.
//    - Dashboard is enabled by default.
var app = builder.Build();
await app.RunAsync();
```

**Implementation notes:**
- The `.AddProject<Projects.Ats_Api>()` syntax requires Aspire's project discovery (may need
  a special attribute on Api.csproj or a naming convention adjustment; `/implement` refines).
- `GetHttpEndpoint()` is Aspire's resource-discovery method; it resolves the backend service's
  HTTP endpoint at runtime and interpolates it into the frontend's environment.
- `WithExecutable()` is used for the Node.js frontend since it is not a .NET project.
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

## 7. Service Discovery & Port Conflict Resolution

### 7.1 Service Discovery

Aspire's resource model handles service discovery. When `GetHttpEndpoint()` is called on the
backend resource, Aspire resolves its actual running endpoint (http://localhost:5000 in this
case) at orchestration time and injects it into the frontend's environment.

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

## Related Specs

- `0001` (Project Scaffolding and Walking Skeleton) — established independent run commands
