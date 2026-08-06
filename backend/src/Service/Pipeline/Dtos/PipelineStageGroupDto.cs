namespace Ats.Service.Pipeline.Dtos;

using System;
using System.Collections.Generic;

/// <summary>One Stage column of the staff pipeline board (FR-15, AC-19 — present even at zero
/// count). Mirrors api.md §4.</summary>
public record PipelineStageGroupDto(
    Guid StageId,
    string StageName,
    int SortOrder,
    int Count,
    IReadOnlyList<PipelineBoardApplicationDto> Applications);
