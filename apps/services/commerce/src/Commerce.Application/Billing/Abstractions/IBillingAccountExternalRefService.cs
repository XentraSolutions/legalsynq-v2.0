using Commerce.Contracts.Billing;

namespace Commerce.Application.Billing.Abstractions;

public interface IBillingAccountExternalRefService
{
    Task<ExternalRefResponse> AddAsync(Guid accountId, CreateExternalRefRequest request, CancellationToken ct);
    Task<IReadOnlyList<ExternalRefResponse>> ListAsync(Guid accountId, CancellationToken ct);
    Task<ExternalRefResponse> UpdateAsync(Guid accountId, Guid refId, UpdateExternalRefRequest request, CancellationToken ct);
    Task<ExternalRefResponse> MakePrimaryAsync(Guid accountId, Guid refId, CancellationToken ct);
}
