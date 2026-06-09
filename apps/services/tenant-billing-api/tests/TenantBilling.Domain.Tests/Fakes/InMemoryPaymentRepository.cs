using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Repositories;

namespace TenantBilling.Domain.Tests.Fakes;

internal sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly Dictionary<Guid, Payment> _payments = new();
    private readonly InMemoryInvoiceRepository? _invoices;

    public InMemoryPaymentRepository(InMemoryInvoiceRepository? invoices = null)
    {
        _invoices = invoices;
    }

    public Task<Payment> AddAsync(Payment payment, CancellationToken ct = default)
    {
        _payments[payment.Id] = payment;
        // Mirror the EF behavior where saving a Payment makes it visible on
        // the parent Invoice's Payments collection on subsequent reads.
        _invoices?.AttachPayment(payment.InvoiceId, payment);
        return Task.FromResult(payment);
    }

    public Task<Payment> UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        _payments[payment.Id] = payment;
        return Task.FromResult(payment);
    }

    public Task<Payment?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        if (_payments.TryGetValue(id, out var p) && p.TenantId == tenantId)
            return Task.FromResult<Payment?>(p);
        return Task.FromResult<Payment?>(null);
    }

    public Task<IReadOnlyList<Payment>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Payment>>(_payments.Values.Where(p => p.TenantId == tenantId).ToList());

    /// <summary>
    /// STAT-B01 in-memory equivalent of the EF join: tenant +
    /// customer scoped recorded-payment list. The join goes through
    /// the sibling <see cref="InMemoryInvoiceRepository"/> so the
    /// fake reproduces the EF behaviour where a payment's tenant
    /// must match its parent invoice's tenant. When the fake is
    /// constructed without an invoice repo (the legacy single-arg
    /// path) the join returns an empty list — statement tests must
    /// pass the invoice repo so the relationship is observable.
    /// </summary>
    public Task<IReadOnlyList<Payment>> GetRecordedPaymentsForCustomerAsync(
        Guid tenantId, Guid customerId, CancellationToken ct = default)
    {
        if (_invoices is null)
            return Task.FromResult<IReadOnlyList<Payment>>(Array.Empty<Payment>());

        var invoiceIds = _invoices
            .GetInvoicesForCustomerAsync(tenantId, customerId, ct)
            .GetAwaiter().GetResult()
            .Select(i => i.Id)
            .ToHashSet();

        var results = _payments.Values
            .Where(p => p.TenantId == tenantId
                && p.Status != "Voided"
                && invoiceIds.Contains(p.InvoiceId))
            .OrderBy(p => p.PaidAt)
            .ThenBy(p => p.Id)
            .ToList();
        return Task.FromResult<IReadOnlyList<Payment>>(results);
    }

    public Task<bool> ExistsByTenantAndReferenceAsync(Guid tenantId, string transactionReference, CancellationToken ct = default)
        => Task.FromResult(_payments.Values.Any(p =>
            p.TenantId == tenantId &&
            p.TransactionReference == transactionReference));

    public Task<IReadOnlyList<Payment>> GetByInvoiceIdAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Payment>>(_payments.Values
            .Where(p => p.TenantId == tenantId && p.InvoiceId == invoiceId)
            .OrderByDescending(p => p.PaidAt)
            .ThenByDescending(p => p.CreatedAt)
            .ToList());

    public Task<IReadOnlyList<Payment>> ListAsync(
        Guid tenantId,
        Guid? invoiceId,
        string? status,
        string? method,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var skip = Math.Max(0, (page - 1) * pageSize);
        var filtered = ApplyFilters(_payments.Values, tenantId, invoiceId, status, method, fromDate, toDate)
            .OrderByDescending(p => p.PaidAt)
            .ThenByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToList();
        return Task.FromResult<IReadOnlyList<Payment>>(filtered);
    }

    public Task<int> CountAsync(
        Guid tenantId,
        Guid? invoiceId,
        string? status,
        string? method,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct = default)
        => Task.FromResult(ApplyFilters(_payments.Values, tenantId, invoiceId, status, method, fromDate, toDate).Count());

    public Task<decimal> SumRecordedPaymentsForInvoiceAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
        => Task.FromResult(_payments.Values
            .Where(p => p.TenantId == tenantId
                && p.InvoiceId == invoiceId
                && !string.Equals(p.Status, "Voided", StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Amount));

    private static IEnumerable<Payment> ApplyFilters(
        IEnumerable<Payment> source,
        Guid tenantId,
        Guid? invoiceId,
        string? status,
        string? method,
        DateTime? fromDate,
        DateTime? toDate)
    {
        var q = source.Where(p => p.TenantId == tenantId);

        if (invoiceId.HasValue && invoiceId.Value != Guid.Empty)
            q = q.Where(p => p.InvoiceId == invoiceId.Value);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            q = q.Where(p => string.Equals(p.Status, st, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(method))
        {
            var m = method.Trim();
            q = q.Where(p => string.Equals(p.Method, m, StringComparison.OrdinalIgnoreCase));
        }

        if (fromDate.HasValue)
            q = q.Where(p => p.PaidAt >= fromDate.Value);
        if (toDate.HasValue)
            q = q.Where(p => p.PaidAt <= toDate.Value);

        return q;
    }
}
