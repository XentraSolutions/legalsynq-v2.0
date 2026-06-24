using Billing.Domain.Entities;
using Billing.Domain.Repositories;

namespace Billing.Domain.Tests.Fakes;

/// <summary>
/// MS-BILL-WRITE-005 — in-memory fake mirroring the EF
/// <c>InvoiceAdjustmentRepository</c> shape. Append-only by
/// contract: no Update/Delete surface. The optional
/// <c>InMemoryInvoiceRepository</c> hook exists so the parent
/// invoice's <c>Adjustments</c> nav collection mirrors what EF
/// would have loaded after <c>SaveChanges</c>.
/// </summary>
internal sealed class InMemoryInvoiceAdjustmentRepository : IInvoiceAdjustmentRepository
{
    private readonly Dictionary<Guid, InvoiceAdjustment> _rows = new();
    private readonly InMemoryInvoiceRepository? _invoices;

    public InMemoryInvoiceAdjustmentRepository(InMemoryInvoiceRepository? invoices = null)
    {
        _invoices = invoices;
    }

    public Task<InvoiceAdjustment> AddAsync(InvoiceAdjustment adjustment, CancellationToken ct = default)
    {
        _rows[adjustment.Id] = adjustment;
        _invoices?.AttachAdjustment(adjustment.InvoiceId, adjustment);
        return Task.FromResult(adjustment);
    }

    public Task<InvoiceAdjustment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        if (_rows.TryGetValue(id, out var row) && row.TenantId == tenantId)
        {
            return Task.FromResult<InvoiceAdjustment?>(row);
        }
        return Task.FromResult<InvoiceAdjustment?>(null);
    }

    public Task<IReadOnlyList<InvoiceAdjustment>> GetByInvoiceAsync(
        Guid tenantId, Guid invoiceId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<InvoiceAdjustment>>(
            _rows.Values
                .Where(a => a.TenantId == tenantId && a.InvoiceId == invoiceId)
                .OrderBy(a => a.CreatedAt)
                .ToList());

    public Task<(decimal CreditSum, decimal DebitSum)> SumByInvoiceAsync(
        Guid tenantId, Guid invoiceId, CancellationToken ct = default)
    {
        var rows = _rows.Values
            .Where(a => a.TenantId == tenantId && a.InvoiceId == invoiceId)
            .ToList();
        var credit = rows.Where(a => a.Type == "Credit").Sum(a => a.Amount);
        var debit = rows.Where(a => a.Type == "Debit").Sum(a => a.Amount);
        return Task.FromResult((credit, debit));
    }
}
