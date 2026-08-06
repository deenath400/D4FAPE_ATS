namespace Ats.Service.Pipeline.Dtos;

using System;
using System.Collections.Generic;

/// <summary>Request body for reordering a Requisition's entire Stage set (FR-3). Mirrors api.md §3.4.</summary>
public record ReorderStagesRequestDto(IReadOnlyList<Guid> StageIds);
