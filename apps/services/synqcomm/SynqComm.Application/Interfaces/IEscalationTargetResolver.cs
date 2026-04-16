using SynqComm.Application.DTOs;

namespace SynqComm.Application.Interfaces;

public interface IEscalationTargetResolver
{
    Task<EscalationTarget?> ResolveAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
}
