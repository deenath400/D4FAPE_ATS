namespace Ats.Service.Pipeline.Dtos;

using System;
using System.Collections.Generic;

/// <summary>Staff view of a Requisition's pipeline: every configured Stage plus a separate
/// rejected group (FR-15). Mirrors api.md §3.8.</summary>
public record PipelineBoardDto(
    Guid RequisitionId,
    IReadOnlyList<PipelineStageGroupDto> Stages,
    PipelineRejectedGroupDto Rejected);
