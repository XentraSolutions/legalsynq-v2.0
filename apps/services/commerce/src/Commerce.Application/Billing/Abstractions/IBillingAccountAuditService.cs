using Commerce.Contracts.Billing;

namespace Commerce.Application.Billing.Abstractions;

public interface IBillingAccountAuditService
{
    Task<IReadOnlyList<BillingAccountAuditEventResponse>> ListAsync(Guid accountId, CancellationToken ct);
}
