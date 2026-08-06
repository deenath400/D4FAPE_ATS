namespace Ats.Service.Pipeline.Dtos;

/// <summary>Request body for rejecting an Application (FR-10). Mirrors api.md §3.7.</summary>
public record RejectApplicationRequestDto(string? Note);
