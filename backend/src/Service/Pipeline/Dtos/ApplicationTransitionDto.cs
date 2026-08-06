namespace Ats.Service.Pipeline.Dtos;

using System;

/// <summary>Response for a successful move/reject — the Application's resulting status plus the
/// transition that produced it (FR-8, FR-10). Mirrors api.md §3.6/§3.7.</summary>
public record ApplicationTransitionDto(
    Guid ApplicationId,
    Guid RequisitionId,
    Guid CurrentStageId,
    string CurrentStageName,
    bool IsRejected,
    StageTransitionDto Transition);
