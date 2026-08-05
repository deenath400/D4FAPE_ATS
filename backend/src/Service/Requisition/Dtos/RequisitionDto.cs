namespace Ats.Service.Requisition.Dtos;

using System;

/// <summary>Staff-facing view of a Requisition, any status. Mirrors api.md §4.</summary>
public record RequisitionDto(
    Guid Id,
    string Title,
    string Description,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
