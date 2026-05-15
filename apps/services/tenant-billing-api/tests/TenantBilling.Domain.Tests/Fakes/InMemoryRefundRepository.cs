using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Repositories;

namespace TenantBilling.Domain.Tests.Fakes;

internal sealed class InMemoryRefundRepository : IRefundRepository
{
    private readonly Dictionary<Guid, Refund> _refunds = new();
    private readonly InMemoryInvoiceRepository? _invoices;

    public InMemoryRefundRepository(InMemoryInvoiceRepository? invoices = null)
    {
        _invoices = invoices;
    }

    public Task<Refund> AddAsync(Refund refund, CancellationToken ct = default)
    {
        _refunds[refund.Id] = refund;
        // Mirror EF behavior: after SaveChanges, the parent invoice's Refunds
        // collection should reflect the newly-recorded refund on subsequent
        // reads.
        _invoices?.AttachRefund(refund.InvoiceId, refund);
        return Task.FromResult(refund);
    }

    public Task<Refund?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_refunds.TryGetValue(id, out var r) ? r : null);

    public Task<IReadOnlyList<Refund>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Refund>>(_refunds.Values.ToList());

    public Task<IReadOnlyList<Refund>> GetByInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Refund>>(
            _refunds.Values.Where(r => r.InvoiceId == invoiceId).ToList());
}
