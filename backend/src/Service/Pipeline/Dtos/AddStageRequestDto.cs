namespace Ats.Service.Pipeline.Dtos;

/// <summary>Request body for adding a Stage to a Requisition's pipeline (FR-1). Mirrors api.md §3.1.</summary>
public record AddStageRequestDto(string Name, int? Position);
