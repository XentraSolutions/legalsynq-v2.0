using Billing.Domain.Entities;
using Billing.Domain.Services;

namespace Billing.Domain.Tests.Fakes;

internal sealed class NoTemplateSelectionService : IInvoiceTemplateSelectionService
{
    public Task<InvoiceTemplate?> GetDefaultForTenantAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<InvoiceTemplate?>(null);

    public Task<InvoiceTemplate?> GetDefaultPlatformAsync(CancellationToken ct = default)
        => Task.FromResult<InvoiceTemplate?>(null);

    public Task<InvoiceTemplate?> SelectForTenantInvoiceAsync(
        Guid tenantId, Guid? explicitTemplateId, CancellationToken ct = default)
        => Task.FromResult<InvoiceTemplate?>(null);

    public Task<InvoiceTemplate?> SelectForPlatformInvoiceAsync(
        Guid? explicitTemplateId, CancellationToken ct = default)
        => Task.FromResult<InvoiceTemplate?>(null);
}
