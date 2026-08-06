namespace Ats.Service.Pipeline.Dtos;

/// <summary>Request body for renaming a Stage (FR-2). Mirrors api.md §3.3.</summary>
public record RenameStageRequestDto(string Name);
