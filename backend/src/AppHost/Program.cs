using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Resolve the Api project path relative to this AppHost project's location.
// AppHost lives at backend/src/AppHost, Api at backend/src/Api (one level up, then into Api).
// Aspire discovers the backend by file path rather than by a compile-time ProjectReference,
// so Ats.AppHost.csproj stays a plain Microsoft.NET.Sdk project with no coupling to Api's build.
var apiProjectPath = Path.Combine(builder.AppHostDirectory, "..", "Api", "Ats.Api.csproj");

// 1. Declare the backend service.
//    - Resource name: "api"
//    - Port: 5000 (matches API_BASE_URL default in tech-stack.md)
//    - No code changes to Api.csproj; Aspire runs it via `dotnet run` internally.
//    - isProxied: false — for a non-container (project/executable) resource, DCP rejects
//      a proxied endpoint whose port and targetPort are equal; the process binds the port
//      directly, so Aspire routes to it without an intermediary proxy.
//    - ASPNETCORE_ENVIRONMENT: Api has no Properties/launchSettings.json of its own, so
//      without an explicit environment Aspire (like a bare `dotnet run`) launches it as
//      Production, which never loads appsettings.Development.json and leaves
//      ConnectionStrings:Default empty. Setting Development here reproduces the same
//      environment an independent `dotnet run --project src/Api` is expected to use.
var backend = builder
    .AddProject("api", apiProjectPath)
    .WithHttpEndpoint(port: 5000, targetPort: 5000, isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

// 2. Declare the frontend service.
//    - Working directory: repo-root/frontend (Node.js project). AppHost sits three levels
//      below the repo root (backend/src/AppHost), not two, so the frontend directory is
//      reached with "..", "..", "..", "frontend".
//    - Port: 3000 (matches independent `npm run dev` convention)
//    - Not a .NET project, so it is declared as an executable Aspire manages directly.
//    - isProxied: false for the same reason as the backend endpoint above.
//    - On Windows, `npm` resolves to `npm.cmd`, a shell script shim that the DCP orchestrator
//      cannot exec directly (it is not a native executable). Routing through cmd.exe /c is the
//      standard workaround for launching npm-based executables under Aspire on Windows.
//    - The working directory is resolved to a full, normalized path via Path.GetFullPath so
//      DCP receives an unambiguous absolute path rather than one containing "..\" segments.
var isWindows = OperatingSystem.IsWindows();
var npmCommand = isWindows ? "cmd.exe" : "npm";
var npmArgs = isWindows ? new[] { "/c", "npm", "run", "dev" } : new[] { "run", "dev" };
var frontendDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", "frontend"));
var frontend = builder
    .AddExecutable(
        name: "frontend",
        command: npmCommand,
        workingDirectory: frontendDirectory,
        args: npmArgs)
    .WithHttpEndpoint(port: 3000, targetPort: 3000, isProxied: false);

// 3. Wire service discovery: bind the frontend's API_BASE_URL to the backend's
//    Aspire-resolved HTTP endpoint, rather than hard-coding the address.
//    Aspire.Hosting 13.0.0 exposes this via GetEndpoint("http") — WithHttpEndpoint's
//    default endpoint name — rather than a GetHttpEndpoint() convenience method.
//    The EndpointReference itself (not its .Url property) is passed to WithEnvironment:
//    .Url reads the allocated port eagerly and throws before the app has started, whereas
//    the WithEnvironment(string, EndpointReference) overload resolves the URL lazily once
//    the resource is actually running.
frontend.WithEnvironment("API_BASE_URL", backend.GetEndpoint("http"));

// 4. Build and run the orchestrator.
//    - Aspire automatically starts/stops both services.
//    - Dashboard is enabled by default.
var app = builder.Build();
await app.RunAsync();
