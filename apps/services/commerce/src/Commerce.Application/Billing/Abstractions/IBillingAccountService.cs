using Commerce.Contracts.Billing;

namespace Commerce.Application.Billing.Abstractions;

public interface IBillingAccountService
{
    Task<BillingAccountResponse> CreateAsync(CreateBillingAccountRequest request, CancellationToken ct);
    Task<IReadOnlyList<BillingAccountResponse>> ListAsync(CancellationToken ct);
    Task<BillingAccountResponse> GetAsync(Guid id, CancellationToken ct);
    Task<BillingAccountResponse> UpdateAsync(Guid id, UpdateBillingAccountRequest request, CancellationToken ct);
    Task<BillingAccountResponse> ActivateAsync(Guid id, CancellationToken ct);
    Task<BillingAccountResponse> SuspendAsync(Guid id, CancellationToken ct);
    Task<BillingAccountResponse> CloseAsync(Guid id, CancellationToken ct);
}
