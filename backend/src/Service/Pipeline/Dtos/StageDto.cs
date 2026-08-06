namespace Ats.Service.Pipeline.Dtos;

using System;

/// <summary>One Stage of a Requisition's pipeline, in pipeline order. Mirrors api.md §4.</summary>
public record StageDto(
    Guid Id,
    Guid RequisitionId,
    string Name,
    int SortOrder);
