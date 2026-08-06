namespace Ats.Service.Pipeline.Dtos;

using System;

/// <summary>One row of an Application's transition history (FR-12, FR-14). Mirrors api.md §4.</summary>
public record StageTransitionDto(
    Guid Id,
    Guid ApplicationId,
    Guid? FromStageId,
    string FromStageName,
    Guid? ToStageId,
    string? ToStageName,
    string Kind,
    string ActorDisplayLabel,
    string? Note,
    DateTime OccurredAtUtc);
