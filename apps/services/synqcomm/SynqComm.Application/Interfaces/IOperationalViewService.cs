using SynqComm.Application.DTOs;

namespace SynqComm.Application.Interfaces;

public interface IOperationalViewService
{
    Task<OperationalQueryResponse> QueryConversationsAsync(
        Guid tenantId,
        Guid userId,
        OperationalQueryRequest request,
        CancellationToken ct = default);
}
