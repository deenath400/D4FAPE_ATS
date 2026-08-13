---
spec: 0006
title: Local Service Orchestration with Aspire
verdict: PASS-WITH-FINDINGS
date: 2026-08-14
---

# Validation Report — 0006 Local Service Orchestration with Aspire

**Spec:** `../spec.md` · **Validated:** 2026-08-14 · **Verdict:** PASS-WITH-FINDINGS

This is the **third** validation pass for 0006. History: pass 1 FAILed on F-1 (duplicate
solution files), F-2 (AC-11, missing `src/Api` launch profile), F-3 (false changelog claim),
and non-blocking F-4 (no automated tests for 0006). Direct fixes were applied for F-1/F-2/F-3.
Pass 2 FAILed on a new regression, F-5: the F-2 fix caused Aspire's `AddProject` to
auto-derive an `http` endpoint from `src/Api`'s new launch profile, colliding with the
AppHost's pre-existing explicit `.WithHttpEndpoint(...)` call and crashing the AppHost with
`DistributedApplicationException: Endpoint with name 'http' already exists`. A fix was applied
(removed the redundant `.WithHttpEndpoint(...)`/`.WithEnvironment(...)` calls from the backend
resource in `backend/src/AppHost/Program.cs`) and manually smoke-tested by the orchestrating
conversation, but not by an independent validation agent.

**This pass independently re-verified everything from scratch** — build, full test suite,
three separate live `dotnet run --project src/AppHost` startup/shutdown cycles (process- and
port-level), both standalone commands, the port-conflict scenario, and the DB
migration/persistence cycle — without trusting the prior smoke-test summary.

| Dimension | Result |
|---|---|
| Build | PASS — bare `dotnet build` (from `backend/`), exactly one solution file (`Ats.slnx`), no `MSB1011` |
| Unit tests | 161 passed, 0 failed |
| Integration tests | 105 passed, 0 failed |
| Architecture tests | 4 passed, 0 failed |
| Frontend tests | 59 passed, 0 failed |
| Lint | Backend: `dotnet build` (documented Lint command) — 0 errors. Frontend: 0 ESLint errors/warnings |
| Format | `dotnet format --verify-no-changes` still fails, but only on pre-existing spec-0004/0002 CRLF/CHARSET violations — no `AppHost`-related or `MSB1011` failures |
| Acceptance criteria | 14 of 14 covered; 13 PASS, 1 PASS-with-note (AC-10, unchanged from prior passes) |
| Architecture | 1 new finding (F-6, Low) — all F-1/F-2/F-3/F-5 confirmed resolved |
| Standards | 1 carried-forward finding (F-4, Medium, non-blocking) |

---

## 1. Test Execution

All commands below were re-run independently in this session, verbatim, from a clean process
baseline confirmed before and after each live orchestration run.

### Build (bare, from `backend/`)

```
$ cd backend && dotnet restore --use-lock-file
  Determining projects to restore...
  All projects are up-to-date for restore.

$ cd backend && dotnet build
  Determining projects to restore...
  All projects are up-to-date for restore.
  Ats.Shared -> ...\bin\Debug\net10.0\Ats.Shared.dll
  Ats.AppHost -> ...\bin\Debug\net10.0\Ats.AppHost.dll
  Ats.Db -> ...\bin\Debug\net10.0\Ats.Db.dll
  Ats.Service -> ...\bin\Debug\net10.0\Ats.Service.dll
  Ats.UnitTests -> ...\bin\Debug\net10.0\Ats.UnitTests.dll
  Ats.Api -> ...\bin\Debug\net10.0\Ats.Api.dll
  Ats.ArchitectureTests -> ...\bin\Debug\net10.0\Ats.ArchitectureTests.dll
  Ats.IntegrationTests -> ...\bin\Debug\net10.0\Ats.IntegrationTests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:06.78
```

No `MSB1011` — confirms exactly one solution file (`backend/Ats.slnx`) is discoverable; `git
status` and a directory listing both confirm `backend/Ats.sln` is gone (deleted, staged as `D`
in the working tree) and only `Ats.slnx` remains.

### Unit tests

```
$ dotnet test tests/Ats.UnitTests --no-build
Test run for ...Ats.UnitTests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed: 0, Passed: 161, Skipped: 0, Total: 161, Duration: 6 s - Ats.UnitTests.dll (net10.0)
```

### Integration tests

```
$ dotnet test tests/Ats.IntegrationTests --no-build
Test run for ...Ats.IntegrationTests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed: 0, Passed: 105, Skipped: 0, Total: 105, Duration: 45 s - Ats.IntegrationTests.dll (net10.0)
```

### Architecture tests

```
$ dotnet test tests/Ats.ArchitectureTests --no-build
Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 138 ms - Ats.ArchitectureTests.dll (net10.0)
```

Full re-run (`dotnet test`, all three backend test projects together, after the live
orchestration runs below completed) confirms the same totals hold with no test-order
sensitivity:

```
Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 77 ms - Ats.ArchitectureTests.dll (net10.0)
Passed!  - Failed: 0, Passed: 161, Skipped: 0, Total: 161, Duration: 3 s - Ats.UnitTests.dll (net10.0)
Passed!  - Failed: 0, Passed: 105, Skipped: 0, Total: 105, Duration: 22 s - Ats.IntegrationTests.dll (net10.0)
```

### Lint (backend documented command is `dotnet build`; already captured above — 0 errors)

### Frontend

```
$ cd frontend && npm run lint
> ats-frontend@0.1.0 lint
> npx --no-install next lint
✔ No ESLint warnings or errors

$ cd frontend && npm test
Test Files  15 passed (15)
     Tests  59 passed (59)
```

### Format (pre-existing, out-of-scope violations — not `AppHost`-related)

```
$ cd backend && dotnet format --verify-no-changes
...\tests\Ats.UnitTests\Storage\LocalDiskFileStorageTests.cs(...): error ENDOFLINE: Fix end of line marker. Replace 2 characters with '\n'. [...]
   (57 similar ENDOFLINE lines, all in LocalDiskFileStorageTests.cs — spec 0004)
...\src\Db\Migrations\20260805141657_AddAuthenticationAndRefreshTokens.cs(1,1): error CHARSET: Fix file encoding. [...]
...\src\Db\Migrations\20260805191845_AddApplicationsAndCvAttachments.cs(1,1): error CHARSET: Fix file encoding. [...]
```

Independently confirmed: `grep`-ing the full `dotnet format` output for `AppHost` or
`MSB1011`/"multiple solution files" returns zero matches. Every violation is in
`LocalDiskFileStorageTests.cs` (spec 0004) or an EF migration file predating spec 0006 (spec
0002's `AddAuthenticationAndRefreshTokens`, spec 0004's `AddApplicationsAndCvAttachments`).
This confirms both (a) F-1 is resolved — the format command no longer fails on solution-file
ambiguity — and (b) these CRLF/CHARSET violations are unrelated to and out of scope for 0006,
exactly as the prior report characterized them; they are not counted as an 0006 finding.

**Commands not run**

| Command | Why |
|---|---|
| — | All commands from `tech-stack.md`'s Commands table were run this pass, including both `Run (dev)` and `Run (orchestrated dev)`. |

---

## 2. Live Orchestration Verification

Independently re-run from scratch — not trusting the implementation's smoke-test summary.
Baseline process/port state was captured before the first run and re-confirmed identical after
every shutdown (only 3 pre-existing, unrelated `node.exe` processes from another repository/
session and a handful of MSBuild node-reuse `dotnet.exe` workers remained at every checkpoint;
these are explicitly excluded per the task's own scoping note).

### F-5 code inspection

`backend/src/AppHost/Program.cs:22` (current working tree):

```csharp
var backend = builder.AddProject("api", apiProjectPath);
```

No `.WithHttpEndpoint(...)` and no `.WithEnvironment("ASPNETCORE_ENVIRONMENT", ...)` call on
the backend resource — both were present in the last-committed version (`git diff` confirms
their removal) and are now absent. `backend.GetEndpoint("http")` at line 56 is unchanged and
still wires the frontend's `API_BASE_URL` against the endpoint Aspire now auto-derives from
`backend/src/Api/Properties/launchSettings.json`'s `"http"` profile
(`applicationUrl: http://localhost:5000`, `ASPNETCORE_ENVIRONMENT: Development` — confirmed by
reading that file directly). This is the exact fix the changelog claims, verified by direct
inspection of the diff and both files, not by trusting the changelog's prose.

### Live run 1 — startup, three-endpoint reachability, F-5 regression check

```
$ cd backend && dotnet run --project src/AppHost
Using launch settings from src\AppHost\Properties\launchSettings.json...
Building...
info: Aspire.Hosting.DistributedApplication[0]
      Aspire version: 13.0.0+7512c2944094a58904b6c803aa824c4a4ce42e11
info: Aspire.Hosting.DistributedApplication[0]
      Distributed application starting.
info: Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager[63]
      User profile is available. Using '...\DataProtection-Keys' as key repository...
info: Aspire.Hosting.Dcp.DcpHost[0]
      Starting DCP with arguments: start-apiserver --monitor 1020 --detach --kubeconfig "..."
info: Aspire.Hosting.DistributedApplication[0]
      Distributed application started. Press Ctrl+C to shut down.
info: Aspire.Hosting.DistributedApplication[0]
      Now listening on: http://localhost:17203
info: Aspire.Hosting.DistributedApplication[0]
      Login to the dashboard at http://localhost:17203/login?t=...
```

**No `DistributedApplicationException` — full log searched for "exception", "error", "fail":
zero matches.** This directly refutes F-5's crash signature; the AppHost reaches "Distributed
application started" and stays there.

Live process/port check while running (`Get-CimInstance`/`Get-NetTCPConnection`):

```
ProcessName       Id
-----------       --
dcp            18524
dcpctrl        29904
dcpproc          952
dcpproc         1708
dcpproc        10168

LocalPort  State OwningProcess
---------  ----- -------------
    17203 Listen         29904
     5000 Listen         29904
     3000 Listen         21536
```

Reachability:

```
$ curl -s -o /tmp/b.json -w "HTTP_CODE:%{http_code}\n" http://localhost:5000/api/system/status
HTTP_CODE:200
{"version":"1.0.0.0","database":{"reachable":true,"schemaCurrent":true}}

$ curl -s -o /tmp/f.json -w "HTTP_CODE:%{http_code}\n" http://localhost:3000/api/bff/system-status
HTTP_CODE:200
{"version":"1.0.0.0","database":{"reachable":true,"schemaCurrent":true}}

$ curl -s -o /dev/null -w "HTTP_CODE:%{http_code}\n" http://localhost:17203/
HTTP_CODE:302
```

Backend (:5000), frontend BFF proxy (:3000, AC-6/AC-7), and dashboard (:17203) all confirmed
independently reachable and correct.

### Shutdown 1 — process/port cleanup

The AppHost's own `dotnet.exe` PID was identified via `Get-CimInstance Win32_Process` filtering
on command line (`"dotnet.exe" run --project src/AppHost`), then terminated with `taskkill
/PID <pid> /F` — **without** `/T` (no forced sub-tree kill), so that only Aspire's own shutdown
cascade (not an external forced tree-kill) is responsible for cleaning up DCP and the frontend.
This environment has no interactive console attached to send a genuine `CTRL_C_EVENT`; killing
only the top-level PID and observing whether children exit on their own is the same evidence
standard the implementation used and is noted as a distinct (not identical) proxy for Ctrl+C.

```
$ taskkill /PID 8104 /F
SUCCESS: The process with PID 8104 has been terminated.

# 5 seconds later:
$ Get-Process -Name dcp,dcpctrl,dcpproc,Aspire* -ErrorAction SilentlyContinue
(no matches — all gone)
$ Get-NetTCPConnection -LocalPort 5000,3000,17203 -ErrorAction SilentlyContinue
(no matches — all released)
$ curl :5000 / :3000 / :17203
HTTP_CODE:000 (connection refused) on all three
```

All 5 Aspire-managed processes (`dcp`, `dcpctrl`, 3× `dcpproc`) and the frontend's 4 `node.exe`
processes exited within 5 seconds of the AppHost's own PID being killed, with no explicit
signal sent to them. Only the pre-existing, unrelated processes from the baseline snapshot
remained. AC-4 confirmed.

### Standalone backend (AC-11)

```
$ cd backend && dotnet run --project src/Api
Using launch settings from src\Api\Properties\launchSettings.json...
Building...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Development

$ curl -s -o /tmp/api_standalone.json -w "HTTP_CODE:%{http_code}\n" http://localhost:5000/api/system/status
HTTP_CODE:200
{"version":"1.0.0.0","database":{"reachable":true,"schemaCurrent":true}}
```

Confirms the `src/Api/Properties/launchSettings.json` fix (F-2) holds under this pass: the
independent, bare `dotnet run --project src/Api` — no environment variable overrides — now
picks up the `Development` launch profile automatically and serves requests. Stopped via
`taskkill /PID <pid> /F` afterward.

### Standalone frontend (AC-12)

```
$ cd frontend && npm run dev
> ats-frontend@0.1.0 dev
> npx --no-install next dev
   ▲ Next.js 15.1.7
   - Local:        http://localhost:3000
 ✓ Ready in 4s

$ curl -s -o /dev/null -w "HTTP_CODE:%{http_code}\n" http://localhost:3000/
HTTP_CODE:200
```

Stopped via `taskkill` on the `next dev`/`node` process tree afterward; port 3000 confirmed
refused post-kill.

### Port-conflict scenario (AC-13)

A second, independent live cycle was run specifically for this check: AppHost restarted
(confirmed clean start, no exception, dashboard reachable at :17203), then the independent
backend command attempted while Aspire's instance held port 5000:

```
$ cd backend && dotnet run --project src/Api
Using launch settings from src\Api\Properties\launchSettings.json...
Building...
fail: Microsoft.Extensions.Hosting.Internal.Host[11]
      Hosting failed to start
      System.IO.IOException: Failed to bind to address http://127.0.0.1:5000: address already in use.
       ---> Microsoft.AspNetCore.Connections.AddressInUseException: Only one usage of each
            socket address (protocol/network address/port) is normally permitted.
       ---> System.Net.Sockets.SocketException (10048): Only one usage of each socket address
            (protocol/network address/port) is normally permitted.
...
Unhandled exception. System.IO.IOException: Failed to bind to address http://127.0.0.1:5000: address already in use.

$ curl -s -o /dev/null -w "Aspire backend still up, HTTP_CODE:%{http_code}\n" http://localhost:5000/api/system/status
Aspire backend still up, HTTP_CODE:200
```

The error names the exact port (`http://127.0.0.1:5000`) and states "address already in use."
Aspire's own backend instance was confirmed unaffected and still serving requests. This second
AppHost instance was then shut down the same way (PID-only `taskkill /F`, no `/T`) and the
process/port sweep confirmed the identical clean-baseline result as Shutdown 1.

### Database migration / persistence (AC-8, AC-9)

A third live AppHost cycle was run to independently reproduce the migration/persistence
behaviour without relying on the implementation's own prior capture:

```
$ curl http://localhost:5000/api/system/status
{"version":"1.0.0.0","database":{"reachable":true,"schemaCurrent":true}}

$ dotnet ef database update --project src/Db --startup-project src/Api    # while Aspire is running
Build succeeded.
Acquiring an exclusive lock for migration application...
No migrations were applied. The database is already up to date.
Done.

$ dotnet ef migrations list --project src/Db --startup-project src/Api
20260805133328_InitialCreate
20260805141657_AddAuthenticationAndRefreshTokens
20260805171525_AddRequisitionsAndStages
20260805191845_AddApplicationsAndCvAttachments
20260806080934_AddPipelineProgression
```

All five migrations listed with no `(Pending)` marker; a re-run is a confirmed no-op; the
system-status endpoint reports `schemaCurrent: true` — consistent across two separate AppHost
process lifetimes within this session (run 1 and run 3), confirming the database file and its
migration history persist across a full stop/restart cycle, not just within a single process
lifetime. This AppHost instance was then also cleanly shut down and confirmed via the same
process/port sweep (clean baseline restored a third time).

---

## 3. Acceptance Criteria Traceability

| AC | Requirement | Covering evidence | Result |
|---|---|---|---|
| AC-1 | Exactly one Aspire AppHost project exists and can be identified and built | `find backend -iname "*AppHost*" -not -path "*/bin/*" -not -path "*/obj/*"` → exactly `src/AppHost/Ats.AppHost.csproj` and its folder; included in the bare `dotnet build` above, 0 errors | MANUAL (file-system inspection + successful build) — PASS |
| AC-2 | AppHost declares a backend resource and a frontend resource | `Program.cs:22` `builder.AddProject("api", apiProjectPath)`; `Program.cs:40-46` `builder.AddExecutable(name: "frontend", ...)` — read in full | MANUAL (code inspection) — PASS |
| AC-3 | `dotnet run --project src/AppHost` starts successfully; both services reported running; dashboard available | Live run 1: console shows "Distributed application started", dashboard URL printed, :17203 returns 302 | MANUAL (live run) — PASS |
| AC-4 | Ctrl+C (or process termination) shuts down both services cleanly, no orphaned children | Shutdown 1: all 5 Aspire processes + 4 frontend `node.exe` processes gone within 5s of killing only the AppHost's own PID; ports released; repeated identically on runs 2 and 3 | MANUAL (live run, PID-only kill as Ctrl+C proxy — no interactive console available) — PASS |
| AC-5 | AppHost configures port bindings for both services and exposes them for inter-service communication | `Program.cs:46` `.WithHttpEndpoint(port: 3000, ...)` on frontend; backend's port comes from the auto-derived `http` endpoint (port 5000, from `src/Api`'s launch profile); `Program.cs:56` `frontend.WithEnvironment("API_BASE_URL", backend.GetEndpoint("http"))` | MANUAL (code inspection) — PASS |
| AC-6 | Frontend BFF proxy request to backend succeeds | Live run 1: `curl :3000/api/bff/system-status` → 200 with backend payload | MANUAL (live run) — PASS |
| AC-7 | Backend receives and processes request via Aspire network topology | Same evidence as AC-6 — the 200 response originates from the backend through the Aspire-assigned addresses | MANUAL (live run) — PASS |
| AC-8 | `dotnet ef database update` succeeds for the orchestrated backend | Live run 3: migration command run while Aspire active; "No migrations were applied. The database is already up to date." (already-applied state, consistent with prior checkpoints; command succeeds either way) | MANUAL (live run) — PASS |
| AC-9 | DB file and migration history persist across backend stop/restart under Aspire | `schemaCurrent: true` confirmed in both run 1 and run 3 (separate AppHost process lifetimes); `dotnet ef migrations list` shows all 5 migrations applied, none pending | MANUAL (live run, two separate lifetimes) — PASS |
| AC-10 | `tech-stack.md` Commands table documents the orchestrated run command | `tech-stack.md:59` — new "Run (orchestrated dev)" row: `dotnet run --project src/AppHost` | MANUAL (document inspection) — PASS-with-note (unchanged from prior passes: AC's literal wording asked for the existing "Run (dev)" row to gain the command; a distinct new row was added instead — functionally satisfies the requirement, does not block) |
| AC-11 | `dotnet run --project src/Api` starts backend independently, unchanged behaviour | Standalone backend run: bare command, no env override, starts on :5000, `curl` → 200 | MANUAL (live run) — PASS (F-2 fix holds) |
| AC-12 | `npm run dev` starts frontend independently, unchanged behaviour | Standalone frontend run: bare command, starts on :3000, `curl` → 200 | MANUAL (live run) — PASS |
| AC-13 | Port conflict between independent and Aspire-orchestrated backend is reported clearly | Port-conflict live run: `AddressInUseException` naming `http://127.0.0.1:5000`; Aspire's own instance unaffected | MANUAL (live run) — PASS |
| AC-14 | No production-deployment or multi-environment configuration exists in the AppHost | Directory listing of `src/AppHost` (source only): `Ats.AppHost.csproj`, `Program.cs`, `packages.lock.json`, `Properties/launchSettings.json` — no Dockerfile/compose/Production profile; `grep -rniE "docker\|container\|azure\|aws\|kubernetes\|k8s\|production"` across those files returns only `packages.lock.json`'s transitive `KubernetesClient` dependency (DCP's own internal local-orchestrator API, not a real cluster); `launchSettings.json` has exactly one profile, hardcoded `Development` | MANUAL (code/file inspection) — PASS |

**14 of 14 ACs covered.** 13 PASS, 1 PASS-with-note (AC-10, non-blocking). No AC is uncovered
or failing. All ACs are MANUAL — this spec has zero automated test coverage (see F-4).

---

## 4. Architectural Conformance

Checked against `plan/hld.md`, `plan/lld.md`, and `docs/specs/meta/architecture.md`.

| Check | Result | Note |
|---|---|---|
| Files match the LLD manifest | PASS | `src/AppHost/{Ats.AppHost.csproj, Program.cs, packages.lock.json, Properties/launchSettings.json}` all present and match; `backend/src/Api/Properties/launchSettings.json` (F-2 fix) is a declared deviation, recorded in the changelog |
| Exactly one solution file (F-1) | PASS | `backend/Ats.sln` deleted; `backend/Ats.slnx` extended with `AppHost`; confirmed via `git status`, directory listing, and a clean bare `dotnet build` |
| No `http` endpoint collision (F-5) | PASS | `Program.cs:22` no longer calls `.WithHttpEndpoint(...)`/`.WithEnvironment(...)` on the backend resource; confirmed via code inspection and three separate clean live starts with zero `DistributedApplicationException` occurrences |
| No unauthorized cross-component dependency | PASS | AppHost has no `ProjectReference` to `Api.csproj`; discovers it by file path only, as designed |
| Component map in `architecture.md` reflects reality | PASS | `infra/build` row names the AppHost and cites 0006; two Change Log rows appended (CP-1, CP-2) |
| Deviations recorded in the changelog | PASS | F-1, F-2, F-5 all recorded as "Correction (post-validation)" entries in `implementation/changelog.md`, each accurately describing the problem, cause, and fix (independently verified against the code diff — see F-6 for a related but distinct gap) |
| LLD (`plan/lld.md`) patched for F-5 | **FAIL** (see F-6) | §2.2's "as implemented" code block and Deviation Log entry D-6 still show the pre-F-5 code (`WithHttpEndpoint`/`WithEnvironment` on the backend resource) — not updated to match the current `Program.cs` |

---

## 5. Coding Standards Conformance

Checked against `docs/specs/meta/coding-standards.md`. 0006 ships no application code (only
orchestration config and one new `launchSettings.json`), so most rules (error envelopes,
logging, secrets, parameterised queries) are not applicable to what this spec touches — the
prior report's scope-limited standards checks (no secrets in `Program.cs`, no swallowed
exceptions, naming conventions on the one new file) were re-confirmed unchanged this pass.

| Rule | Result | Note |
|---|---|---|
| No secrets in source | PASS | `Program.cs` and `launchSettings.json` (both AppHost's and Api's) contain no credentials; `Development`-only environment name is not a secret |
| No swallowed exceptions / empty catches | PASS (N/A) | `Program.cs` has no exception handling to check — a 4-statement builder pipeline |
| Naming conventions | PASS | Resource names (`"api"`, `"frontend"`), file names, and env var names all consistent with the rest of the codebase |
| Automated test coverage for this spec's ACs | **FAIL (non-blocking)** | See F-4 — carried forward unchanged, zero new tests |

---

## 6. Findings

Ranked most severe first.

### F-4 — No automated tests for any of 0006's 14 ACs *(Severity: Medium · Standards/Coverage · carried forward, unchanged)*

**Location:** `docs/specs/0006-aspire-orchestration/plan/tasks.md` T-3 through T-9; confirmed
absent via `grep -ril "AppHost\|Aspire" backend/tests` (zero matches) and the same search
across `frontend/tests` (zero matches).

**Problem.** Every one of 0006's 14 ACs is verified by MANUAL live inspection only — none by
an automated test that would catch a regression like F-5 on a future code change without a
human re-running the orchestrator by hand.

**Impact.** Exactly the class of defect this spec has now shipped twice (F-1, F-5) — both were
console/process-level regressions invisible to `dotnet build`/`dotnet test` and caught only by
a human (or validation agent) manually starting the AppHost. Nothing in CI or a bare `dotnet
test` run would catch a third one.

**Status this pass.** Unchanged. No new tests were added between pass 2 and this pass — this
finding is carried forward exactly as before. **Not blocking**, consistent with prior passes,
because process-orchestration behaviour (starting external processes, binding OS ports,
reading console output for a specific exception type) is inherently difficult to unit-test and
the spec's own LLD §11 Testing & Validation table designed every one of these checks as
"Manual" from the start — this is a design choice made at `/plan` time, not an oversight
introduced during implementation.

**Suggested fix.** Out of this validation's scope to prescribe in detail, but a plausible
follow-up: a lightweight integration test that shells out to `dotnet run --project src/AppHost`
with a timeout, asserts the process exits 0 or is still running after N seconds (not crashed),
and checks the console output does not contain `DistributedApplicationException`. This would
not need real port binding to catch F-1/F-5-class defects.

### F-6 — LLD not patched for the F-5 correction *(Severity: Low · Architecture/Documentation · new this pass)*

**Location:** `docs/specs/0006-aspire-orchestration/plan/lld.md:76-80` (§2.2 "as implemented"
code block) and `:281` (Deviation Log entry D-6).

**Problem.** `plan/lld.md`'s own header (line 8) states: *"This file is living: when
implementation diverges from this design, `/implement` patches the affected section here and
records the deviation in `../implementation/changelog.md`."* The F-5 fix was correctly recorded
in the changelog (verified accurate — see below), but `lld.md` §2.2's code block still shows
the backend resource declaration exactly as it was **before** the F-5 fix:

```csharp
var backend = builder
    .AddProject("api", apiProjectPath)
    .WithHttpEndpoint(port: 5000, targetPort: 5000, isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");
```

This no longer matches the actual `Program.cs:22` (`var backend =
builder.AddProject("api", apiProjectPath);` — no further calls). Deviation Log entry D-6 (line
281) likewise still describes the now-removed `.WithEnvironment("ASPNETCORE_ENVIRONMENT",
"Development")` call as the current state, with no corresponding D-8 entry documenting its
later removal.

**Impact.** Low — the changelog (the authoritative "what actually shipped" record per
`conventions.md` §1) is accurate and was the artifact this validation cross-checked against the
live code and confirmed correct. A future reader who consults only `lld.md` as the design
authority (as intended by its own "living document" contract) would be misled about why D-6's
environment-variable workaround was needed and would not learn it was later removed. This does
not affect runtime behaviour, test results, or any AC.

**Suggested fix.** Patch `lld.md` §2.2's code block to match current `Program.cs`, and append a
D-8 Deviation Log entry documenting the F-5 correction (mirroring the changelog's existing,
accurate description).

---

## 7. Findings Summary

| # | Severity | Area | Location | Issue |
|---|---|---|---|---|
| F-4 | Medium | Standards/Coverage | `plan/tasks.md` T-3–T-9 | No automated tests for any of 0006's 14 ACs (carried forward, unchanged, non-blocking) |
| F-6 | Low | Architecture/Documentation | `plan/lld.md:76-80,281` | LLD §2.2 code block and Deviation Log D-6 not patched for the F-5 correction; changelog is accurate, LLD is stale |

**F-1, F-2, F-3, F-5 — all confirmed resolved this pass**, independently, via direct code
inspection and live re-execution:

- **F-1** (duplicate solution files) — resolved. `backend/Ats.sln` deleted; exactly one
  solution file (`Ats.slnx`) remains; bare `dotnet build`/`dotnet restore`/`dotnet format` all
  proceed past the solution-selection step with no `MSB1011`.
- **F-2** (AC-11, missing `src/Api` launch profile) — resolved. `dotnet run --project src/Api`,
  run bare with no environment overrides, now starts successfully and serves 200 on `:5000`.
- **F-3** (false changelog claim about `Ats.sln`) — resolved. The changelog's `Ats.sln`/
  `Ats.slnx` row now accurately describes the original false premise, the resulting F-1 defect,
  and the correction — cross-checked against `git log`/`git diff`, which confirm `Ats.slnx`
  indeed predates 0006.
- **F-5** (endpoint collision, `DistributedApplicationException`) — resolved. The redundant
  `.WithHttpEndpoint(...)`/`.WithEnvironment(...)` calls are gone from `Program.cs`; three
  separate live `dotnet run --project src/AppHost` starts this pass produced zero exceptions
  and reached "Distributed application started" every time.

---

## 8. Not Verified

- **Genuine interactive Ctrl+C/SIGINT shutdown.** This environment has no interactive console
  attached to the AppHost's process group in any of the three live runs. A PID-only forced
  `taskkill /F` (no `/T`) was used as the closest available proxy — strong evidence that
  Aspire's own shutdown cascade (not an external forced tree-kill) cleaned up DCP and the
  frontend, since only the top-level PID was targeted, but not identical to a genuine SIGINT.
- **Aspire dashboard's rendered UI content in a browser.** Only HTTP-level reachability (302
  redirect to `/login`) was confirmed, consistent with prior passes.
- **The alternative AC-13 ordering** (start the independent backend first, then Aspire, and
  confirm Aspire itself fails fast) — not exercised this pass either; the LLD marks it optional
  and the primary direction (Aspire first, then independent) is sufficient evidence.
- **Full historical accuracy of every implementation-changelog claim outside the F-1/F-2/F-3/
  F-5 corrections** — this pass focused verification on the corrections named in the task and
  the live-run evidence; it did not re-audit every sentence of CP-1/CP-2's original narrative.

---

## 9. Status Decision

**Verdict: PASS-WITH-FINDINGS.** Every command run this pass was green (build, all three
backend test suites, frontend lint/tests), all 14 ACs are covered and passing (one with a
non-blocking wording note carried from prior passes), and F-1/F-2/F-3/F-5 are all confirmed
resolved through independent live re-verification, not by trusting the prior smoke-test
summary. The only findings are F-4 (Medium, no automated tests — a known, declared, non-
blocking design tradeoff carried unchanged across all three validation passes) and F-6 (Low, a
newly observed documentation-freshness gap in the LLD that does not affect runtime behaviour).
No finding reaches High, and no AC is uncovered or failing, so per the verdict rule this clears
the bar for PASS-WITH-FINDINGS.

**Spec status:** `implemented` → **`validated`**. `docs/specs/0006-aspire-orchestration/spec.md`
frontmatter and `docs/specs/index.md`'s 0006 row updated accordingly in this same turn.

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| 0001 Project Scaffolding and Walking Skeleton | 1 | Established the two-deployable architecture, the independent run commands this spec must preserve (AC-11/AC-12), and the system-status endpoint used to exercise AC-6/AC-7 live |

Considered and skipped: 0002–0005 (all business logic; no component or entity overlap with
orchestration infrastructure — consistent with the spec's own Related Specs scoping).
Cap reached: no.
