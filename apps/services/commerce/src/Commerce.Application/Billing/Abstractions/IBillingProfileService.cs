using Commerce.Contracts.Billing;

namespace Commerce.Application.Billing.Abstractions;

public interface IBillingProfileService
{
    Task<BillingProfileResponse> GetAsync(Guid accountId, CancellationToken ct);
    Task<BillingProfileResponse> UpdateAsync(Guid accountId, UpdateBillingProfileRequest request, CancellationToken ct);
}
