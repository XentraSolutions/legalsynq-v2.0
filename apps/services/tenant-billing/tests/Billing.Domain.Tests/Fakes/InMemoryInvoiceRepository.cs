using Billing.Domain.Entities;
using Billing.Domain.Repositories;
using Billing.Domain.Services;

namespace Billing.Domain.Tests.Fakes;

internal sealed class InMemoryInvoiceRepository : IInvoiceRepository
{
    private readonly Dictionary<Guid, Invoice> _invoices = new();

    // INV-TPL-02: stamping service is pure (no I/O), so the in-memory
    // fake can construct its own and call it directly. Tests that
    // need to assert "ApplyStampAsync ran" go through this path the
    // same way a request would.
    private readonly IInvoiceTemplateStampingService _stamping = new InvoiceTemplateStampingService();

    public Task<Invoice> AddAsync(Invoice invoice, CancellationToken ct = default)
    {
        _invoices[invoice.Id] = invoice;
        return Task.FromResult(invoice);
    }

    public Task<Invoice?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        if (_invoices.TryGetValue(id, out var i) && i.TenantId == tenantId)
            return Task.FromResult<Invoice?>(i);
        return Task.FromResult<Invoice?>(null);
    }

    public Task<IReadOnlyList<Invoice>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Invoice>>(_invoices.Values.Where(i => i.TenantId == tenantId).ToList());

    /// <summary>
    /// STAT-B01 in-memory equivalent of the EF query: tenant +
    /// customer scoped invoice list, ordered ascending by IssueDate
    /// then InvoiceNumber. Returns an empty list for unknown /
    /// cross-tenant ids.
    /// </summary>
    public Task<IReadOnlyList<Invoice>> GetInvoicesForCustomerAsync(
        Guid tenantId, Guid customerId, CancellationToken ct = default)
    {
        var results = _invoices.Values
            .Where(i => i.TenantId == tenantId && i.CustomerId == customerId)
            .OrderBy(i => i.IssueDate)
            .ThenBy(i => i.InvoiceNumber)
            .ToList();
        return Task.FromResult<IReadOnlyList<Invoice>>(results);
    }

    public Task<IReadOnlyList<Invoice>> ListAsync(
        Guid tenantId,
        string? search,
        string? status,
        Guid? customerId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var skip = Math.Max(0, (page - 1) * pageSize);
        var filtered = ApplyFilters(_invoices.Values, tenantId, search, status, customerId, fromDate, toDate)
            .OrderByDescending(i => i.CreatedAt)
            .ThenBy(i => i.InvoiceNumber)
            .Skip(skip)
            .Take(pageSize)
            .ToList();
        return Task.FromResult<IReadOnlyList<Invoice>>(filtered);
    }

    public Task<int> CountAsync(
        Guid tenantId,
        string? search,
        string? status,
        Guid? customerId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct = default)
        => Task.FromResult(ApplyFilters(_invoices.Values, tenantId, search, status, customerId, fromDate, toDate).Count());

    private static IEnumerable<Invoice> ApplyFilters(
        IEnumerable<Invoice> source,
        Guid tenantId,
        string? search,
        string? status,
        Guid? customerId,
        DateTime? fromDate,
        DateTime? toDate)
    {
        var q = source.Where(i => i.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(i =>
                i.InvoiceNumber.Contains(s, StringComparison.OrdinalIgnoreCase)
                || (i.Notes != null && i.Notes.Contains(s, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            q = q.Where(i => string.Equals(i.Status, st, StringComparison.OrdinalIgnoreCase));
        }

        if (customerId.HasValue && customerId.Value != Guid.Empty)
            q = q.Where(i => i.CustomerId == customerId.Value);

        if (fromDate.HasValue)
            q = q.Where(i => i.IssueDate >= fromDate.Value);
        if (toDate.HasValue)
            q = q.Where(i => i.IssueDate <= toDate.Value);

        return q;
    }

    public Task<bool> ExistsByTenantAndNumberAsync(
        Guid tenantId,
        string invoiceNumber,
        Guid? excludingInvoiceId = null,
        CancellationToken ct = default)
        => Task.FromResult(_invoices.Values.Any(i =>
            i.TenantId == tenantId
            && i.InvoiceNumber == invoiceNumber
            && (!excludingInvoiceId.HasValue || i.Id != excludingInvoiceId.Value)));

    public Task<string?> GetLatestInvoiceNumberAsync(Guid tenantId, int year, CancellationToken ct = default)
    {
        var prefix = $"INV-{year:D4}-";
        var latest = _invoices.Values
            .Where(i => i.TenantId == tenantId && i.InvoiceNumber.StartsWith(prefix, StringComparison.Ordinal))
            .OrderByDescending(i => i.InvoiceNumber, StringComparer.Ordinal)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefault();
        return Task.FromResult<string?>(latest);
    }

    public Task<Invoice?> UpdateStatusAsync(
        Guid tenantId,
        Guid invoiceId,
        string status,
        DateTime updatedAt,
        DateTime? issuedAt = null,
        CancellationToken ct = default)
    {
        if (!_invoices.TryGetValue(invoiceId, out var invoice) || invoice.TenantId != tenantId)
            return Task.FromResult<Invoice?>(null);
        invoice.Status = status;
        invoice.UpdatedAt = updatedAt;
        if (issuedAt.HasValue && invoice.IssuedAt is null)
            invoice.IssuedAt = issuedAt;
        return Task.FromResult<Invoice?>(invoice);
    }

    public Task<IReadOnlyList<Invoice>> GetInvoicesEligibleForOverdueAsync(
        Guid? tenantId,
        DateTime nowUtc,
        int take,
        CancellationToken ct = default)
    {
        if (take <= 0) return Task.FromResult<IReadOnlyList<Invoice>>(Array.Empty<Invoice>());

        // Mirror EF: date-boundary semantics so single + batch agree.
        var dueBefore = nowUtc.Date;

        var q = _invoices.Values.Where(i =>
            (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
            && i.DueDate < dueBefore);

        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            q = q.Where(i => i.TenantId == tenantId.Value);

        var result = (IReadOnlyList<Invoice>)q
            .OrderBy(i => i.DueDate)
            .Take(take)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<Invoice?> TryMarkOverdueAsync(
        Guid tenantId,
        Guid invoiceId,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        if (!_invoices.TryGetValue(invoiceId, out var invoice) || invoice.TenantId != tenantId)
            return Task.FromResult<Invoice?>(null);

        // Re-check the eligibility predicate at write time. This mirrors
        // the EF impl's conditional update and is what gives the batch
        // path its race-safety against concurrent payment / void writes.
        var dueBefore = nowUtc.Date;
        var stillEligible =
            (invoice.Status == InvoiceStatus.Issued || invoice.Status == InvoiceStatus.PartiallyPaid)
            && invoice.DueDate < dueBefore;
        if (!stillEligible) return Task.FromResult<Invoice?>(null);

        invoice.Status = InvoiceStatus.Overdue;
        invoice.UpdatedAt = nowUtc;
        return Task.FromResult<Invoice?>(invoice);
    }

    public Task<Invoice?> ApplyStampAsync(
        Guid tenantId,
        Guid invoiceId,
        InvoiceTemplate template,
        DateTime stampedAtUtc,
        CancellationToken ct = default)
    {
        if (template is null) throw new ArgumentNullException(nameof(template));
        if (!_invoices.TryGetValue(invoiceId, out var invoice) || invoice.TenantId != tenantId)
            return Task.FromResult<Invoice?>(null);

        // Mirror EF: idempotency lives in the stamping service. An
        // already-stamped invoice short-circuits without touching
        // UpdatedAt — that matches the "true no-op" contract on the
        // EF impl when EnsureStampedInvoice returns false.
        var applied = _stamping.EnsureStampedInvoice(invoice, template, stampedAtUtc);
        if (applied)
        {
            invoice.UpdatedAt = stampedAtUtc;
        }
        return Task.FromResult<Invoice?>(invoice);
    }

    /// <summary>Test helper: directly inject a payment so the invoice's nav
    /// collection reflects what the repository would have loaded.</summary>
    public void AttachPayment(Guid invoiceId, Payment payment)
    {
        if (_invoices.TryGetValue(invoiceId, out var invoice))
        {
            invoice.Payments.Add(payment);
        }
    }

    /// <summary>Test helper: directly inject a refund so the invoice's nav
    /// collection reflects what the repository would have loaded.</summary>
    public void AttachRefund(Guid invoiceId, Refund refund)
    {
        if (_invoices.TryGetValue(invoiceId, out var invoice))
        {
            invoice.Refunds.Add(refund);
        }
    }

    /// <summary>MS-BILL-WRITE-005 test helper: directly inject an adjustment
    /// so the invoice's nav collection reflects what the repository would
    /// have loaded.</summary>
    public void AttachAdjustment(Guid invoiceId, InvoiceAdjustment adjustment)
    {
        if (_invoices.TryGetValue(invoiceId, out var invoice))
        {
            invoice.Adjustments.Add(adjustment);
        }
    }
}
