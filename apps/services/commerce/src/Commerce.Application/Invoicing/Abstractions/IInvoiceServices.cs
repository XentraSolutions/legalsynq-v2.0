using Commerce.Contracts.Invoicing;

namespace Commerce.Application.Invoicing.Abstractions;

public interface IInvoiceService
{
    Task<InvoiceResponse> CreateAsync(CreateInvoiceRequest request, CancellationToken ct);
    Task<InvoiceResponse> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<InvoiceResponse>> ListAsync(int take, CancellationToken ct);
    Task<IReadOnlyList<InvoiceResponse>> ListForBillingAccountAsync(Guid billingAccountId, CancellationToken ct);
    Task<IReadOnlyList<InvoiceResponse>> ListForSubscriptionAsync(Guid subscriptionId, CancellationToken ct);
}
