namespace Ats.Service.Requisition;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ats.Service.Common;
using Ats.Service.Requisition.Dtos;

public interface IRequisitionService
{
    Task<Result<RequisitionDto>> CreateAsync(CreateRequisitionRequestDto dto, CancellationToken ct = default);
    Task<Result<RequisitionDto>> UpdateContentAsync(Guid id, UpdateRequisitionRequestDto dto, CancellationToken ct = default);
    Task<Result<RequisitionDto>> PublishAsync(Guid id, CancellationToken ct = default);
    Task<Result<RequisitionDto>> UnpublishAsync(Guid id, CancellationToken ct = default);
    Task<Result<RequisitionDto>> CloseAsync(Guid id, CancellationToken ct = default);
    Task<Result<RequisitionDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RequisitionDto>>> ListAsync(CancellationToken ct = default);
    Task<Result<PublicRequisitionDto>> GetPublicByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<PagedResult<PublicRequisitionDto>>> SearchPublicAsync(
        string? keyword, int page, int pageSize, CancellationToken ct = default);
}
