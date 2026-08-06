namespace Ats.Service.Pipeline.Dtos;

using System.Collections.Generic;

/// <summary>The rejected-Applications group of the staff pipeline board, separate from the Stage
/// columns (FR-15). Mirrors api.md §4.</summary>
public record PipelineRejectedGroupDto(
    int Count,
    IReadOnlyList<PipelineBoardApplicationDto> Applications);
