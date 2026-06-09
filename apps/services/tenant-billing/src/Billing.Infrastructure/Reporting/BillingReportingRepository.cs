using Microsoft.EntityFrameworkCore;
using Billing.Domain.Entities;
using Billing.Domain.Reporting;
using Billing.Infrastructure.Data;

namespace Billing.Infrastructure.Reporting;

/// <summary>
/// MS-BILL-WRITE-007 — EF Core implementation of
/// <see cref="IBillingReportingRepository"/>. Every query is
/// tenant-scoped at the SQL level (no in-memory filtering of cross-
/// tenant rows) and every aggregate excludes voided payments via the
/// repository-level predicate <c>p.Status != "Voided"</c>. Customer
/// names are projected from the join — the report does not depend on
/// the optional EF navigation collection being eager-loaded.
/// </summary>
public sealed class BillingReportingRepository : IBillingReportingRepository
{
    private readonly BillingDbContext _db;

    public BillingReportingRepository(BillingDbContext db)
    {
        _db = db;
    }

    private static (int Page, int Size) ClampPage(int page, int pageSize)
    {
        var p = page < 1 ? 1 : page;
        var s = pageSize <= 0 ? 100 : (pageSize > 1000 ? 1000 : pageSize);
        return (p, s);
    }

    public async Task<IReadOnlyList<AccountingSummaryRow>> ListAccountingSummaryAsync(
        Guid tenantId,
        Guid? customerId,
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (p, s) = ClampPage(page, pageSize);

        // Stage 1: page the invoices in SQL (status / customer / date filters).
        var invQuery = _db.Invoices
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId);

        if (customerId is Guid cid && cid != Guid.Empty)
            invQuery = invQuery.Where(i => i.CustomerId == cid);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            invQuery = invQuery.Where(i => i.Status == st);
        }
        if (fromDate is DateTime fd)
            invQuery = invQuery.Where(i => i.IssueDate >= fd);
        if (toDate is DateTime td)
            invQuery = invQuery.Where(i => i.IssueDate <= td);

        var invoiceSlice = await invQuery
            .OrderByDescending(i => i.IssueDate).ThenBy(i => i.InvoiceNumber)
            .Skip((p - 1) * s).Take(s)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.CustomerId,
                i.Status,
                i.Currency,
                i.TotalAmount,
                i.IssueDate,
                i.DueDate,
            })
            .ToListAsync(ct);

        if (invoiceSlice.Count == 0)
            return Array.Empty<AccountingSummaryRow>();

        var ids = invoiceSlice.Select(i => i.Id).ToList();

        // Stage 2: aggregate adjustments and (non-voided) payments for
        // exactly that page of invoice ids — single round-trip each.
        var adj = await _db.InvoiceAdjustments
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && ids.Contains(a.InvoiceId))
            .GroupBy(a => a.InvoiceId)
            .Select(g => new
            {
                InvoiceId = g.Key,
                CreditSum = g.Where(x => x.Type == "Credit").Sum(x => (decimal?)x.Amount) ?? 0m,
                DebitSum = g.Where(x => x.Type == "Debit").Sum(x => (decimal?)x.Amount) ?? 0m,
            })
            .ToDictionaryAsync(x => x.InvoiceId, ct);

        var paid = await _db.Payments
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && ids.Contains(p.InvoiceId) && p.Status != "Voided")
            .GroupBy(p => p.InvoiceId)
            .Select(g => new { InvoiceId = g.Key, Sum = g.Sum(x => (decimal?)x.Amount) ?? 0m })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Sum, ct);

        // Stage 3: project customer names for the slice.
        var custIds = invoiceSlice.Select(i => i.CustomerId).Distinct().ToList();
        var custNames = await _db.Customers
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && custIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        // Stage 4: assemble the rows in memory using the same formula
        // as InvoiceAccountingSummaryService (single source of truth).
        var rows = new List<AccountingSummaryRow>(invoiceSlice.Count);
        foreach (var i in invoiceSlice)
        {
            var (creditSum, debitSum) = adj.TryGetValue(i.Id, out var a)
                ? (a.CreditSum, a.DebitSum)
                : (0m, 0m);
            var paidSum = paid.TryGetValue(i.Id, out var pv) ? pv : 0m;
            var effectiveTotal = i.TotalAmount + debitSum - creditSum;
            var effectiveOutstanding = effectiveTotal - paidSum;

            rows.Add(new AccountingSummaryRow(
                InvoiceId: i.Id,
                InvoiceNumber: i.InvoiceNumber,
                CustomerId: i.CustomerId,
                CustomerName: custNames.TryGetValue(i.CustomerId, out var n) ? n : string.Empty,
                Status: i.Status,
                Currency: i.Currency,
                InvoiceTotal: i.TotalAmount,
                PaidSum: paidSum,
                AdjustmentCreditSum: creditSum,
                AdjustmentDebitSum: debitSum,
                EffectiveTotal: effectiveTotal,
                EffectiveOutstanding: effectiveOutstanding,
                IssueDate: i.IssueDate,
                DueDate: i.DueDate));
        }
        return rows;
    }

    public async Task<IReadOnlyList<InvoiceAgingRow>> ListInvoiceAgingAsync(
        Guid tenantId,
        Guid? customerId,
        DateTime nowUtc,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (p, s) = ClampPage(page, pageSize);

        // Open statuses only — terminal / draft / refund states are
        // reconciled elsewhere and would noise up the AR aging view.
        var openStatuses = new[] { InvoiceStatus.Issued, InvoiceStatus.PartiallyPaid, InvoiceStatus.Overdue };

        var invQuery = _db.Invoices
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && openStatuses.Contains(i.Status));

        if (customerId is Guid cid && cid != Guid.Empty)
            invQuery = invQuery.Where(i => i.CustomerId == cid);

        var invoiceSlice = await invQuery
            .OrderBy(i => i.DueDate).ThenBy(i => i.InvoiceNumber)
            .Skip((p - 1) * s).Take(s)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.CustomerId,
                i.Status,
                i.Currency,
                i.TotalAmount,
                i.DueDate,
            })
            .ToListAsync(ct);

        if (invoiceSlice.Count == 0)
            return Array.Empty<InvoiceAgingRow>();

        var ids = invoiceSlice.Select(i => i.Id).ToList();

        var adj = await _db.InvoiceAdjustments
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && ids.Contains(a.InvoiceId))
            .GroupBy(a => a.InvoiceId)
            .Select(g => new
            {
                InvoiceId = g.Key,
                CreditSum = g.Where(x => x.Type == "Credit").Sum(x => (decimal?)x.Amount) ?? 0m,
                DebitSum = g.Where(x => x.Type == "Debit").Sum(x => (decimal?)x.Amount) ?? 0m,
            })
            .ToDictionaryAsync(x => x.InvoiceId, ct);

        var paid = await _db.Payments
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && ids.Contains(p.InvoiceId) && p.Status != "Voided")
            .GroupBy(p => p.InvoiceId)
            .Select(g => new { InvoiceId = g.Key, Sum = g.Sum(x => (decimal?)x.Amount) ?? 0m })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Sum, ct);

        var custIds = invoiceSlice.Select(i => i.CustomerId).Distinct().ToList();
        var custNames = await _db.Customers
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && custIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var rows = new List<InvoiceAgingRow>(invoiceSlice.Count);
        foreach (var i in invoiceSlice)
        {
            var (creditSum, debitSum) = adj.TryGetValue(i.Id, out var a)
                ? (a.CreditSum, a.DebitSum)
                : (0m, 0m);
            var paidSum = paid.TryGetValue(i.Id, out var pv) ? pv : 0m;
            var effectiveTotal = i.TotalAmount + debitSum - creditSum;
            var effectiveOutstanding = effectiveTotal - paidSum;
            var daysOverdue = AgingBucket.DaysOverdue(i.DueDate, nowUtc);

            rows.Add(new InvoiceAgingRow(
                InvoiceId: i.Id,
                InvoiceNumber: i.InvoiceNumber,
                CustomerId: i.CustomerId,
                CustomerName: custNames.TryGetValue(i.CustomerId, out var n) ? n : string.Empty,
                Status: i.Status,
                Currency: i.Currency,
                InvoiceTotal: i.TotalAmount,
                PaidSum: paidSum,
                EffectiveTotal: effectiveTotal,
                EffectiveOutstanding: effectiveOutstanding,
                DueDate: i.DueDate,
                DaysOverdue: daysOverdue,
                AgingBucket: AgingBucket.ForDaysOverdue(daysOverdue)));
        }
        return rows;
    }

    public async Task<IReadOnlyList<AdjustmentReportRow>> ListAdjustmentsAsync(
        Guid tenantId,
        Guid? customerId,
        string? type,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (p, s) = ClampPage(page, pageSize);

        var q =
            from a in _db.InvoiceAdjustments.AsNoTracking()
            join i in _db.Invoices.AsNoTracking() on a.InvoiceId equals i.Id
            join c in _db.Customers.AsNoTracking() on a.CustomerId equals c.Id
            where a.TenantId == tenantId
                && i.TenantId == tenantId
                && c.TenantId == tenantId
            select new { a, i.InvoiceNumber, CustomerName = c.Name };

        if (customerId is Guid cid && cid != Guid.Empty)
            q = q.Where(x => x.a.CustomerId == cid);
        if (!string.IsNullOrWhiteSpace(type))
        {
            var t = type.Trim();
            q = q.Where(x => x.a.Type == t);
        }
        if (fromDate is DateTime fd)
            q = q.Where(x => x.a.CreatedAt >= fd);
        if (toDate is DateTime td)
            q = q.Where(x => x.a.CreatedAt <= td);

        var rows = await q
            .OrderByDescending(x => x.a.CreatedAt).ThenBy(x => x.a.Id)
            .Skip((p - 1) * s).Take(s)
            .Select(x => new AdjustmentReportRow(
                x.a.Id,
                x.a.InvoiceId,
                x.InvoiceNumber,
                x.a.CustomerId,
                x.CustomerName,
                x.a.Type,
                x.a.Amount,
                x.a.Currency,
                x.a.Reason,
                x.a.ReferenceNumber,
                x.a.CreatedAt))
            .ToListAsync(ct);

        return rows;
    }

    public async Task<IReadOnlyList<PaymentReportRow>> ListPaymentsAsync(
        Guid tenantId,
        Guid? customerId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (p, s) = ClampPage(page, pageSize);

        // Reconciliation visibility — voided / reversed payments
        // ARE included in the payments report so an operator can see
        // them. They are excluded from `paidSum` aggregates only in
        // the accounting-summary and aging projections (where the
        // `Status != "Voided"` predicate applies).
        var q =
            from pay in _db.Payments.AsNoTracking()
            join i in _db.Invoices.AsNoTracking() on pay.InvoiceId equals i.Id
            join c in _db.Customers.AsNoTracking() on i.CustomerId equals c.Id
            where pay.TenantId == tenantId
                && i.TenantId == tenantId
                && c.TenantId == tenantId
            select new { pay, i.InvoiceNumber, i.CustomerId, CustomerName = c.Name };

        if (customerId is Guid cid && cid != Guid.Empty)
            q = q.Where(x => x.CustomerId == cid);
        if (fromDate is DateTime fd)
            q = q.Where(x => x.pay.PaidAt >= fd);
        if (toDate is DateTime td)
            q = q.Where(x => x.pay.PaidAt <= td);

        var rows = await q
            .OrderByDescending(x => x.pay.PaidAt).ThenBy(x => x.pay.Id)
            .Skip((p - 1) * s).Take(s)
            .Select(x => new PaymentReportRow(
                x.pay.Id,
                x.pay.InvoiceId,
                x.InvoiceNumber,
                x.CustomerId,
                x.CustomerName,
                x.pay.Amount,
                x.pay.Currency,
                x.pay.Method,
                x.pay.Status,
                x.pay.TransactionReference,
                x.pay.PaidAt,
                x.pay.Status == "Voided",
                x.pay.ReversedAt))
            .ToListAsync(ct);

        return rows;
    }
}
