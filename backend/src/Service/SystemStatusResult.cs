namespace Ats.Service;

public sealed record SystemStatusResult(string Version, bool DatabaseReachable, bool DatabaseSchemaCurrent);
