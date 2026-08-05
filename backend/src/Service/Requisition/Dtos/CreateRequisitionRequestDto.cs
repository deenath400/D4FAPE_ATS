namespace Ats.Service.Requisition.Dtos;

/// <summary>Create-requisition request body. Same shape reused for edit (api.md §3.1/§3.4).</summary>
public record CreateRequisitionRequestDto(string Title, string Description);
