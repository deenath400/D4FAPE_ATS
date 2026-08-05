namespace Ats.Service.Requisition.Dtos;

using System;

/// <summary>
/// Anonymous-facing view of a Requisition — only ever produced for a `published` row, so it
/// deliberately omits `status` (api.md §4: nothing to leak by construction).
/// </summary>
public record PublicRequisitionDto(
    Guid Id,
    string Title,
    string Description,
    DateTime UpdatedAtUtc);
