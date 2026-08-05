namespace Ats.Api;

public sealed record SystemStatusDto(string Version, DatabaseStatusDto Database);
public sealed record DatabaseStatusDto(bool Reachable, bool SchemaCurrent);
