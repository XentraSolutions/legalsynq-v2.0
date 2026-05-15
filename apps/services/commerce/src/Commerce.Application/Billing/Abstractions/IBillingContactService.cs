using Commerce.Contracts.Billing;

namespace Commerce.Application.Billing.Abstractions;

public interface IBillingContactService
{
    Task<BillingContactResponse> AddAsync(Guid accountId, CreateBillingContactRequest request, CancellationToken ct);
    Task<IReadOnlyList<BillingContactResponse>> ListAsync(Guid accountId, CancellationToken ct);
    Task<BillingContactResponse> UpdateAsync(Guid accountId, Guid contactId, UpdateBillingContactRequest request, CancellationToken ct);
    Task<BillingContactResponse> MakePrimaryAsync(Guid accountId, Guid contactId, CancellationToken ct);
}
