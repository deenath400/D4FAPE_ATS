# Low-Level Design — 0001 Project Scaffolding and Walking Skeleton

**Spec:** `../spec.md` · **HLD:** `hld.md` · **Updated:** 2026-08-05

The *how*. Precise enough that the implementation agent writes code without re-deciding
anything. Every file it will create or modify is named here, with signatures.

> This file is **living**: when implementation diverges from this design, `/implement`
> patches the affected section here and records the deviation in
> `../implementation/changelog.md`. Silent drift is a defect.

---

## 1. File Manifest

Project/namespace prefix chosen: `Ats` (short for the repository's product, D4FAPE ATS).
All backend paths are relative to `backend/`, all frontend paths relative to `frontend/`.

| Action | Path | Purpose |
|---|---|---|
| Create | `.gitignore` | Excludes build output, dependencies, env files, SQLite DB + sidecars (FR-1) |
| Create | `.editorconfig` | Shared indentation/style rules for both languages |
| Create | `backend/global.json` | Pins the .NET SDK version (G-5) |
| Create | `backend/Ats.sln` | Solution file referencing all projects below |
| Create | `backend/Directory.Build.props` | Shared MSBuild props: nullable, warnings-as-errors, analysis level, lockfile mode |
| Create | `backend/.config/dotnet-tools.json` | Local tool manifest pinning `dotnet-ef` |
| Create | `backend/src/Db/Ats.Db.csproj` | `db/core` — plain `Microsoft.NET.Sdk`, no `ProjectReference` to anything |
| Create | `backend/src/Db/AppDbContext.cs` | EF Core context, no `DbSet<T>` (schema empty by design) |
| Create | `backend/src/Db/SqlitePragmaConnectionInterceptor.cs` | WAL + busy-timeout on every connection open (NFR-1) |
| Create | `backend/src/Db/DatabaseHealth.cs` | `DatabaseHealth` record + `IDatabaseHealthCheck` interface |
| Create | `backend/src/Db/EfDatabaseHealthCheck.cs` | `IDatabaseHealthCheck` implementation |
| Create | `backend/src/Db/DbServiceCollectionExtensions.cs` | `AddDbCore(IServiceCollection, IConfiguration)` — fail-fast on missing connection string |
| Create | `backend/src/Db/AppDbContextFactory.cs` | `IDesignTimeDbContextFactory<AppDbContext>` for `dotnet ef` (D-5) |
| Create | `backend/src/Db/Migrations/<ts>_InitialCreate.cs` | Empty migration (C-3) |
| Create | `backend/src/Db/Migrations/AppDbContextModelSnapshot.cs` | EF Core model snapshot |
| Create | `backend/src/Db/packages.lock.json` | NuGet lockfile (G-5) |
| Create | `backend/src/Service/Ats.Service.csproj` | `service/system` — plain SDK, `ProjectReference` to `Ats.Db` only |
| Create | `backend/src/Service/SystemStatusResult.cs` | Result record returned to the API layer |
| Create | `backend/src/Service/ISystemStatusService.cs` | Service contract |
| Create | `backend/src/Service/SystemStatusService.cs` | Implementation — sole caller of `db/core` |
| Create | `backend/src/Service/IVersionProvider.cs` | Contract for reporting backend version |
| Create | `backend/src/Service/ServiceCollectionExtensions.cs` | `AddSystemService(IServiceCollection, IConfiguration)`, calls `AddDbCore` internally (D-2) |
| Create | `backend/src/Service/packages.lock.json` | NuGet lockfile |
| Create | `backend/src/Api/Ats.Api.csproj` | `api/system` — `Microsoft.NET.Sdk.Web`, `ProjectReference` to `Ats.Service` only |
| Create | `backend/src/Api/Program.cs` | Composition root, fail-fast config check, ProblemDetails, endpoint mapping |
| Create | `backend/src/Api/AssemblyVersionProvider.cs` | `IVersionProvider` implementation reading assembly metadata |
| Create | `backend/src/Api/SystemStatusEndpoints.cs` | `GET /api/system/status` minimal API |
| Create | `backend/src/Api/SystemStatusDto.cs` | `SystemStatusDto`, `DatabaseStatusDto` |
| Create | `backend/src/Api/appsettings.json` | Base config; `ConnectionStrings:Default` present with empty value |
| Create | `backend/src/Api/appsettings.Development.json` | Local dev default connection string |
| Create | `backend/src/Api/packages.lock.json` | NuGet lockfile |
| Create | `backend/src/Shared/Ats.Shared.csproj` | Empty scaffold proving Rule 5 is testable (D-6) — no `ProjectReference` to anything |
| Create | `backend/tests/Ats.UnitTests/Ats.UnitTests.csproj` | xUnit project, references `Ats.Service` |
| Create | `backend/tests/Ats.UnitTests/SystemStatusServiceTests.cs` | Unit tests with a fake `IDatabaseHealthCheck` |
| Create | `backend/tests/Ats.IntegrationTests/Ats.IntegrationTests.csproj` | xUnit project, references `Ats.Api` |
| Create | `backend/tests/Ats.IntegrationTests/CustomWebApplicationFactory.cs` | In-process host, unique temp SQLite file per instance |
| Create | `backend/tests/Ats.IntegrationTests/SystemStatusEndpointTests.cs` | Real HTTP assertions for 200 and 503 |
| Create | `backend/tests/Ats.ArchitectureTests/Ats.ArchitectureTests.csproj` | xUnit project referencing all four `src/*` projects |
| Create | `backend/tests/Ats.ArchitectureTests/LayeringRuleTests.cs` | The four AC-7 checks (D-1) |
| Create | `frontend/package.json` | Pinned dependency versions, npm scripts (G-5) |
| Create | `frontend/package-lock.json` | npm lockfile |
| Create | `frontend/tsconfig.json` | `"strict": true` (NFR-3) |
| Create | `frontend/next.config.ts` | Next.js config |
| Create | `frontend/eslint.config.mjs` | Flat ESLint config incl. the two FR-16 rules (D-4) |
| Create | `frontend/.prettierrc.json` | Prettier config |
| Create | `frontend/tailwind.config.ts` | Tailwind CSS config (C-6) |
| Create | `frontend/postcss.config.mjs` | PostCSS config for Tailwind |
| Create | `frontend/vitest.config.ts` | Vitest + Testing Library config |
| Create | `frontend/.env.example` | `API_BASE_URL=` — key name, no value (AC-2) |
| Create | `frontend/src/app/layout.tsx` | Root layout, imports Tailwind base styles |
| Create | `frontend/src/app/globals.css` | Tailwind directives |
| Create | `frontend/src/app/(portal)/page.tsx` | Landing page — composes both status sections |
| Create | `frontend/src/app/(staff)/.gitkeep` | Empty route group placeholder, no surface |
| Create | `frontend/src/app/api/bff/system-status/route.ts` | Proxy route handler (`ui/bff`) |
| Create | `frontend/src/lib/server/backend-invoke.ts` | Shared invoke function (`ui/bff`) — sole reader of `API_BASE_URL` |
| Create | `frontend/src/lib/types/system-status.ts` | `SystemStatusDto` TypeScript type |
| Create | `frontend/src/components/StatusSkeleton.tsx` | Shared loading UI |
| Create | `frontend/src/components/ServerStatusSection.tsx` | Async Server Component, direct invoke call |
| Create | `frontend/src/components/ClientStatusPanel.tsx` | `"use client"`, calls the proxy route |
| Create | `frontend/tests/client-status-panel.test.tsx` | Vitest + Testing Library component test |
| Modify | `docs/specs/meta/tech-stack.md` | Fill Commands + Required Configuration tables (FR-15) |
| Modify | `docs/specs/meta/coding-standards.md` | Remove superseded prose rules, add Project-Specific Rules (FR-15) |
| Modify | `docs/specs/meta/architecture.md` | Add 5 components to Component Map, Change Log entry |

## 2. Domain / Data Layer

### 2.1 `AppDbContext` — `backend/src/Db/AppDbContext.cs`

```csharp
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Intentionally no DbSet<T> members. entities: [] per spec frontmatter and C-3 —
    // the first entity-bearing spec adds the first DbSet and a real migration.
}
```

**Invariants.** Never gains a `DbSet<T>` in this spec. Any migration this spec produces must
have an empty `Up`/`Down` body.

**Persistence notes.** Registered via `AddDbCore`, using SQLite through
`Microsoft.EntityFrameworkCore.Sqlite`. Connection string comes from
`ConnectionStrings:Default`.

### 2.2 `DatabaseHealth` / `IDatabaseHealthCheck` — `backend/src/Db/DatabaseHealth.cs`

```csharp
public sealed record DatabaseHealth(bool Reachable, bool SchemaCurrent);

public interface IDatabaseHealthCheck
{
    Task<DatabaseHealth> CheckAsync(CancellationToken ct);
}
```

### 2.3 `EfDatabaseHealthCheck` — `backend/src/Db/EfDatabaseHealthCheck.cs`

```csharp
public sealed class EfDatabaseHealthCheck : IDatabaseHealthCheck
{
    public EfDatabaseHealthCheck(AppDbContext db, ILogger<EfDatabaseHealthCheck> logger);

    public async Task<DatabaseHealth> CheckAsync(CancellationToken ct)
    {
        // 1. Try Database.CanConnectAsync(ct). Any exception -> log Warning, return (false, false).
        // 2. If not reachable, return (false, false).
        // 3. If reachable, call Database.GetPendingMigrationsAsync(ct);
        //    SchemaCurrent = pending migrations is empty.
        // 4. Never let a connection-string or file-path value leave this method in the
        //    returned DatabaseHealth — only booleans cross the boundary (AC-11).
    }
}
```

### 2.4 `SqlitePragmaConnectionInterceptor` — `backend/src/Db/SqlitePragmaConnectionInterceptor.cs`

```csharp
public sealed class SqlitePragmaConnectionInterceptor : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken ct = default)
    {
        // Execute "PRAGMA journal_mode=WAL;" then "PRAGMA busy_timeout=5000;" on `connection`.
        // Also override the synchronous ConnectionOpened for non-async callers.
    }
}
```

**Invariant.** Busy timeout is set to at least 5000 ms on every connection open, per NFR-1 —
never only once at startup, since SQLite's `busy_timeout` is a per-connection setting.

### 2.5 `AddDbCore` — `backend/src/Db/DbServiceCollectionExtensions.cs`

```csharp
public static class DbServiceCollectionExtensions
{
    public static IServiceCollection AddDbCore(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. connectionString = configuration.GetConnectionString("Default")
        // 2. if string.IsNullOrWhiteSpace(connectionString): throw new InvalidOperationException(
        //      "Missing required configuration key 'ConnectionStrings:Default'.")
        //    — caught and turned into fail-fast exit by Program.cs (see §4).
        // 3. services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString)
        //      .AddInterceptors(new SqlitePragmaConnectionInterceptor()));
        // 4. services.AddScoped<IDatabaseHealthCheck, EfDatabaseHealthCheck>();
        // 5. return services;
    }
}
```

### 2.6 `AppDbContextFactory` — `backend/src/Db/AppDbContextFactory.cs`

```csharp
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        //   ?? "Data Source=./data/app.db";
        // Build DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString) and
        // return new AppDbContext(options). No DI container involved — this is why
        // `dotnet ef` needs no startup project reference to Api (D-5).
    }
}
```

### 2.7 Migration — `backend/src/Db/Migrations/<timestamp>_InitialCreate.cs`

Generated by `dotnet ef migrations add InitialCreate --project src/Db`. `Up(MigrationBuilder)`
and `Down(MigrationBuilder)` bodies are both empty — no `CreateTable` calls. Applying it
creates only EF Core's own `__EFMigrationsHistory` table (see `erd.md`).

## 3. Service / Application Layer

### 3.1 `SystemStatusService.GetStatusAsync` — `backend/src/Service/SystemStatusService.cs`

**Signature**

```csharp
public interface ISystemStatusService
{
    Task<SystemStatusResult> GetStatusAsync(CancellationToken ct);
}

public sealed record SystemStatusResult(string Version, bool DatabaseReachable, bool DatabaseSchemaCurrent);

public sealed class SystemStatusService : ISystemStatusService
{
    public SystemStatusService(IDatabaseHealthCheck health, IVersionProvider version);
    public Task<SystemStatusResult> GetStatusAsync(CancellationToken ct);
}
```

**Behaviour**

1. `var health = await _health.CheckAsync(ct);`
2. `var version = _version.GetVersion();`
3. Return `new SystemStatusResult(version, health.Reachable, health.SchemaCurrent)`.
4. No exception is thrown for a degraded database — `IDatabaseHealthCheck` already converts
   failure into `(false, false)`. Only a genuinely unexpected failure (e.g. `_version` itself
   throwing) is allowed to bubble, per `coding-standards.md`'s "unexpected failures bubble"
   rule.

**Returns**

| Outcome | Result | Maps to HTTP |
|---|---|---|
| DB reachable and schema current | `SystemStatusResult(version, true, true)` | 200 |
| DB unreachable or schema not current | `SystemStatusResult(version, false/true, false)` | 503 |

### 3.2 `AddSystemService` — `backend/src/Service/ServiceCollectionExtensions.cs`

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSystemService(this IServiceCollection services, IConfiguration configuration)
    {
        // services.AddDbCore(configuration);          // D-2 — keeps Api -> Db reference absent
        // services.AddScoped<ISystemStatusService, SystemStatusService>();
        // return services;
    }
}
```

## 4. API Layer

Endpoint shapes are specified in `api.md`. Here, only the wiring.

| Route | Handler | Auth policy | Maps result via |
|---|---|---|---|
| `GET /api/system/status` | `SystemStatusEndpoints.MapSystemStatus` (minimal API) | `.AllowAnonymous()` | inline mapping in the handler (D-7) |

**`Program.cs` behaviour steps**

1. `var builder = WebApplication.CreateBuilder(args);`
2. Read `builder.Configuration.GetConnectionString("Default")`. If null/whitespace:
   `builder.Logging` critical-log the missing key by name, then `Environment.Exit(1)` —
   **before** `builder.Build()`/`app.Run()` so no Kestrel port ever opens (AC-20, FR-12).
3. `builder.Services.AddSystemService(builder.Configuration);`
4. `builder.Services.AddSingleton<IVersionProvider, AssemblyVersionProvider>();`
5. `builder.Services.AddProblemDetails();`
6. `var app = builder.Build();`
7. `app.UseExceptionHandler();` — built-in ASP.NET Core ProblemDetails exception middleware.
8. `app.MapSystemStatus();`
9. `app.Run();`
10. No `dbContext.Database.Migrate()` call anywhere in this file (D-9) — schema changes are
    exclusively the documented `Migrate` command's job.

**`AssemblyVersionProvider`** — `backend/src/Api/AssemblyVersionProvider.cs`

```csharp
public interface IVersionProvider { string GetVersion(); }

public sealed class AssemblyVersionProvider : IVersionProvider
{
    public string GetVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
}
```

**`SystemStatusEndpoints`** — `backend/src/Api/SystemStatusEndpoints.cs`

```csharp
public static class SystemStatusEndpoints
{
    public static IEndpointRouteBuilder MapSystemStatus(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/system/status", async (ISystemStatusService svc, CancellationToken ct) =>
        {
            var result = await svc.GetStatusAsync(ct);
            var dto = new SystemStatusDto(result.Version,
                new DatabaseStatusDto(result.DatabaseReachable, result.DatabaseSchemaCurrent));

            if (result.DatabaseReachable && result.DatabaseSchemaCurrent)
                return Results.Ok(dto);

            var problem = new ProblemDetails
            {
                Type = "https://d4fape.ats/errors/system-status-unavailable",
                Title = "System status degraded",
                Status = StatusCodes.Status503ServiceUnavailable,
            };
            problem.Extensions["code"] = "system.status.database-unavailable";
            problem.Extensions["version"] = dto.Version;
            problem.Extensions["database"] = dto.Database;
            return Results.Json(problem, statusCode: StatusCodes.Status503ServiceUnavailable,
                contentType: "application/problem+json");
        }).AllowAnonymous();

        return app;
    }
}
```

## 5. Frontend

### 5.1 Components

| Component | Path | Props | State | Notes |
|---|---|---|---|---|
| `LandingPage` | `src/app/(portal)/page.tsx` | — (Server Component) | none — awaits inline (D-3) | composes `ServerStatusSection` and `ClientStatusPanel`, each visibly labelled |
| `ServerStatusSection` | `src/components/ServerStatusSection.tsx` | — (async Server Component) | none | `await`s `invokeBackend` directly, try/catch, no Suspense (D-3) |
| `ClientStatusPanel` | `src/components/ClientStatusPanel.tsx` | — | `status: SystemStatusDto \| null`, `error: string \| null`, `loading: boolean` | `"use client"`; fetches `/api/bff/system-status` in `useEffect` |
| `StatusSkeleton` | `src/components/StatusSkeleton.tsx` | `label: string` | none | shared loading treatment for both sections |

### 5.2 Data Access

| Function | Target | Cache key | Notes |
|---|---|---|---|
| `invokeBackend<T>({ path })` | `src/lib/server/backend-invoke.ts` | — (no server-state library, per Non-Goals) | `fetch(baseUrl + path, { cache: "no-store" })`; throws `BackendInvokeError` on non-2xx or network failure; sole reader of `API_BASE_URL` |
| Proxy route `GET` handler | `src/app/api/bff/system-status/route.ts` | — | calls `invokeBackend`, translates a thrown `BackendInvokeError` into a `502` JSON body with a generic message |
| `ClientStatusPanel`'s `useEffect` fetch | `fetch("/api/bff/system-status")` (same origin) | — | no server-state library, per Non-Goals — plain `useState`/`useEffect` |

**`invokeBackend` signature**

```ts
export type InvokeBackendOptions = { path: string; init?: RequestInit };

export class BackendInvokeError extends Error {
  constructor(public status: number, public path: string);
}

// TOKEN-ATTACHMENT-POINT (0002): attach an Authorization header here once a NextAuth
// session is available. This spec sends none (FR-16, AC-27).
export async function invokeBackend<T>(options: InvokeBackendOptions): Promise<T>;
```

**Behaviour**

1. Read `process.env.API_BASE_URL`. If falsy, throw
   `new Error("Missing required configuration key 'API_BASE_URL'.")` (AC-21).
2. `fetch(`${baseUrl}${options.path}`, { ...options.init, cache: "no-store" })`.
3. If `!response.ok`, throw `new BackendInvokeError(response.status, options.path)` — message
   never includes `baseUrl` (AC-15, AC-30 — no URL disclosure).
4. Otherwise `return (await response.json()) as T`.
5. This is the **only** module in the frontend that reads `API_BASE_URL` or calls `fetch`
   against the backend — enforced by the ESLint rules in `eslint.config.mjs` (D-4, FR-16).

### 5.3 UI States

Every async surface defines all four: loading, empty, error, success. "Empty" does not apply
to a status panel (there is no empty collection) — it collapses into success with default
field values, noted below.

| Surface | Loading | Empty | Error | Success |
|---|---|---|---|---|
| `ServerStatusSection` (SSR) | N/A — awaited inline before any HTML is sent (D-3); no client-visible loading moment | N/A | Caught `BackendInvokeError`; renders "Unable to reach the backend service." labelled "Server-rendered", page still 200 (AC-30) | Renders version + reachable/schemaCurrent labelled "Server-rendered" (AC-28, AC-29) |
| `ClientStatusPanel` (browser) | `<StatusSkeleton label="Browser-retrieved" />` while `loading === true` (AC-16) | N/A | Catches fetch/parse failure, sets `error`, renders inline message, never rethrows to an unhandled rejection (AC-15) | Renders version + reachable/schemaCurrent labelled "Browser-retrieved" (AC-12, AC-29) |

## 6. DTOs & Contracts

```ts
// src/lib/types/system-status.ts
export type SystemStatusDto = {
  version: string;
  database: {
    reachable: boolean;
    schemaCurrent: boolean;
  };
};
```

```csharp
// backend/src/Api/SystemStatusDto.cs
public sealed record SystemStatusDto(string Version, DatabaseStatusDto Database);
public sealed record DatabaseStatusDto(bool Reachable, bool SchemaCurrent);
```

## 7. Validation Rules

`GET /api/system/status` accepts no parameters and no body — there is nothing to validate at
the request boundary. The closest analogue is configuration validation, covered in §9.

| Field | Rule | Message | AC |
|---|---|---|---|
| — | No request DTO exists this spec | — | — |

## 8. Error Handling

Follows the RFC 7807 ProblemDetails envelope from `meta/coding-standards.md`.

| Condition | Code | Logged at | User-facing message |
|---|---|---|---|
| Database unreachable or schema not current | `system.status.database-unavailable` | Warning (in `EfDatabaseHealthCheck`) | ProblemDetails body reports `database.reachable`/`schemaCurrent`; no path or connection string (AC-11) |
| Missing `ConnectionStrings:Default` at backend startup | `system.startup.missing-configuration` | Critical, then `Environment.Exit(1)` | Console message names the exact key; no HTTP port opens (AC-20) |
| Missing `API_BASE_URL` when the frontend needs it | — (not an HTTP response; a thrown `Error`) | — | Message names the key; surfaces as the section's error state (AC-21) |
| Backend unreachable from the proxy handler | — | — | `502 { message: "Unable to reach the backend service." }`, no URL (AC-15) |
| Backend unreachable during server render | — | — | Rendered inline error text, page still 200 HTML (AC-30) |

## 9. Configuration

| Key | Type | Default | Required | Where consumed |
|---|---|---|---|---|
| `ConnectionStrings:Default` | string | `""` in `appsettings.json`; `Data Source=./data/app.db` in `appsettings.Development.json` | Yes | `AddDbCore` (fail-fast if blank) |
| `API_BASE_URL` | string (frontend env var, no `NEXT_PUBLIC_` prefix) | none — absent in `.env.example` | Yes | `invokeBackend` only (fail-fast on first call if absent) |

## 10. Database Migration

| Step | Change | Reversible |
|---|---|---|
| 1 | `dotnet ef migrations add InitialCreate --project src/Db` — generates an empty `Up`/`Down` migration | Yes — `Down()` is empty, a no-op |
| 2 | `dotnet ef database update --project src/Db` — creates the SQLite file at the configured path, applies WAL via the interceptor on first connection, and creates EF Core's own `__EFMigrationsHistory` table with one row | Yes — delete the SQLite file to fully reverse (no data to preserve at this schema version) |

No backfill; no downtime; this runs against a database that does not yet exist.

## 11. Test Plan

| Test | Type | Covers | Path |
|---|---|---|---|
| `SystemStatusService_WhenDatabaseHealthy_ReturnsHealthyResult` | Unit | AC-17 | `backend/tests/Ats.UnitTests/SystemStatusServiceTests.cs` |
| `SystemStatusService_WhenDatabaseUnreachable_ReturnsUnhealthyResult` | Unit | AC-11 (mapping), AC-17 | `backend/tests/Ats.UnitTests/SystemStatusServiceTests.cs` |
| `GetSystemStatus_WhenDatabaseMigrated_Returns200` | Integration | AC-10, AC-18 | `backend/tests/Ats.IntegrationTests/SystemStatusEndpointTests.cs` |
| `GetSystemStatus_WhenDatabaseFileMissing_Returns503WithoutLeakingPath` | Integration | AC-11, AC-18 | `backend/tests/Ats.IntegrationTests/SystemStatusEndpointTests.cs` |
| `GetSystemStatus_NeverReceivesAuthorizationHeader` | Integration | AC-27 | `backend/tests/Ats.IntegrationTests/SystemStatusEndpointTests.cs` |
| `Api_DoesNotReferenceDb` | Architecture (xUnit) | AC-7 (Rule 2 shortcut) | `backend/tests/Ats.ArchitectureTests/LayeringRuleTests.cs` |
| `Service_DoesNotReferenceAspNetCoreHttp` | Architecture (xUnit) | AC-7 (Rule 3) | `backend/tests/Ats.ArchitectureTests/LayeringRuleTests.cs` |
| `Db_DoesNotReferenceAspNetCoreHttpOrClaimsPrincipal` | Architecture (xUnit) | AC-7 (Rule 4) | `backend/tests/Ats.ArchitectureTests/LayeringRuleTests.cs` |
| `Shared_DoesNotReferenceApiServiceOrDb` | Architecture (xUnit) | AC-7 (Rule 5) | `backend/tests/Ats.ArchitectureTests/LayeringRuleTests.cs` |
| `ClientStatusPanel shows a loading state before the fetch resolves` | Component (Vitest) | AC-16, AC-19 | `frontend/tests/client-status-panel.test.tsx` |
| `ClientStatusPanel renders the browser-retrieved status on success` | Component (Vitest) | AC-12, AC-19, AC-29 | `frontend/tests/client-status-panel.test.tsx` |
| `ClientStatusPanel renders an error state without the backend URL on failure` | Component (Vitest) | AC-15, AC-19 | `frontend/tests/client-status-panel.test.tsx` |
| Migration applies cleanly to a fresh path, WAL journal mode confirmed | Manual / `/validate` command run | AC-8 | documented command in `tech-stack.md` |
| Migration re-run is a no-op, exits 0, database unchanged | Manual / `/validate` command run | AC-9 | documented command in `tech-stack.md` |
| Fresh-clone install fails on manifest/lockfile mismatch | Manual / `/validate` command run | AC-3, AC-4 | documented command in `tech-stack.md` |
| Backend build fails with zero tolerance for warnings | Manual / `/validate` build run | AC-5, NFR-2 | documented command in `tech-stack.md` |
| Frontend build fails on any type error | Manual / `/validate` build run | AC-6, NFR-3 | documented command in `tech-stack.md` |
| Backend startup fails fast when `ConnectionStrings:Default` is absent, no port opens | Manual / `/validate` run with the key cleared | AC-20 | documented command in `tech-stack.md` |
| Frontend fails naming `API_BASE_URL` when absent | Manual / `/validate` run with the key cleared | AC-21 | documented command in `tech-stack.md` |
| Production bundle grep finds neither the base URL value nor `API_BASE_URL` | Manual / `/validate` build-artifact inspection | AC-14 | `frontend/.next/` after `next build` |
| Browser network trace during landing-page load hits only the frontend's own origin | Manual / `/validate` browser inspection | AC-13 | running app |
| No-JS browser load still shows server-rendered status and no self-hop request | Manual / `/validate` browser inspection | AC-28 | running app |
| Lint / format commands report zero violations both sides | Manual / `/validate` command run | AC-22, AC-23, AC-26 | documented command in `tech-stack.md` |
| `tech-stack.md` Commands table fully filled except `Seed` | Manual / `/validate` document review | AC-24, AC-25 | `docs/specs/meta/tech-stack.md` |
| Fresh clone leaves no tracked/staged/untracked build output, dependency, env, or DB file | Manual / `/validate` `git status` check | AC-1, AC-2 | repository root |

Every `AC-n` in the spec appears at least once above.

## 12. Implementation Notes

- Build `src/Db` → `src/Service` → `src/Api` → `src/Shared` (order doesn't matter for
  `Shared`, it has no dependents) so the project-reference graph exists before the
  architecture tests run against it.
- The `Ats.ArchitectureTests` project takes a `ProjectReference` to all four `src/*`
  projects purely to guarantee they are built and to load their assemblies by name — this is
  test tooling, not a fifth application layer, and is outside the scope of the five Layering
  Rules (those govern `ui/api/service/db/shared`, not test projects).
- `backend`'s documented **Build** command is a compound command
  (`dotnet build && dotnet test tests/Ats.ArchitectureTests --no-build`) precisely so that
  AC-7's fourth case (`ClaimsPrincipal` in `db/core`) — the one case project topology alone
  cannot block — still fails "the backend build" in the literal sense the AC uses. See HLD
  D-1 for the full rationale; do not "simplify" this back to plain `dotnet build` without
  re-reading that decision.
- `EfDatabaseHealthCheck` must not let a caught exception's `Message` (which may contain a
  file path from SQLite) reach `DatabaseHealth` — only the two booleans cross that boundary.
- The empty `Ats.Shared` project should contain literally nothing but its `.csproj` — resist
  the urge to add a placeholder class "just so it isn't empty"; emptiness is the point (D-6).

## Related Specs

None — this is the first spec touching these components.

## Deviation Log

Appended by `/implement` when reality diverged from this design.

| Date | Task | Section | Designed | Actual | Reason |
|---|---|---|---|---|---|
