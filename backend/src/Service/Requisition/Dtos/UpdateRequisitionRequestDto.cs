namespace Ats.Service.Requisition.Dtos;

/// <summary>Edit-requisition request body (api.md §3.4).</summary>
public record UpdateRequisitionRequestDto(string Title, string Description);
