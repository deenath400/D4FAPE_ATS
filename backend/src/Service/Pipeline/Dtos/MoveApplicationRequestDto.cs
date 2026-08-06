namespace Ats.Service.Pipeline.Dtos;

using System;

/// <summary>Request body for moving an Application to another Stage (FR-8, FR-22). Mirrors api.md §3.6.</summary>
public record MoveApplicationRequestDto(Guid TargetStageId, Guid ExpectedCurrentStageId, string? Note);
