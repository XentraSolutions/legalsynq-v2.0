using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface IPayoffQuoteService
{
    Task<PayoffQuoteResult> GetOrGenerateAsync(
        Guid tenantId,
        Guid orgId,
        Guid actingUserId,
        Guid caseId,
        string assignedTo,
        CancellationToken ct = default);
}
