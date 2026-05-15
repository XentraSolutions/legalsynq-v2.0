using Billing.Domain.Entities;
using Billing.Domain.Repositories;

namespace Billing.Domain.Statements;

/// <summary>
/// STAT-B01 — Default <see cref="ICustomerStatementService"/>. Pure
/// composition over existing repositories:
///
/// <list type="number">
///   <item>load the customer tenant-scoped via
///     <see cref="ICustomerRepository.GetActiveByIdAsync"/> (cross-tenant
///     or unknown id ⇒ <c>null</c>);</item>
///   <item>load every invoice for that customer via the new
///     <see cref="IInvoiceRepository.GetInvoicesForCustomerAsync"/>
///     (tenant-scoped at the repository);</item>
///   <item>load every recorded payment for that customer via the
///     new
///     <see cref="IPaymentRepository.GetRecordedPaymentsForCustomerAsync"/>
///     (also tenant-scoped — and the join through
///     <c>Payment.Invoice.CustomerId</c> means a payment whose
///     parent invoice belongs to a different tenant cannot leak
///     into the result even if the same payment id is somehow
///     enumerated);</item>
///   <item>partition the loaded rows into pre-period (drives the
///     opening balance), in-period (drives the transaction stream
///     and the period totals), and full-history (drives the
///     outstanding-invoice block);</item>
///   <item>build the deterministic <see cref="CustomerStatementDocument"/>.
///   </item>
/// </list>
///
/// The implementation is intentionally side-effect-free: no writes,
/// no transactions, no calls to <see cref="IUnitOfWork"/>.
/// </summary>
public sealed class CustomerStatementService : ICustomerStatementService
{
    /// <summary>Maximum allowed reporting period, inclusive day count.</summary>
    public const int MaxRangeDays = 366;

    /// <summary>
    /// Recorded payment status — only payments in this status count
    /// toward the customer's paid totals. Mirrors
    /// <see cref="Services.PaymentService.RecordedStatus"/> so the
    /// engine never drifts from the system-of-record.
    /// </summary>
    public const string RecordedPaymentStatus = "Recorded";

    private readonly ICustomerRepository _customers;
    private readonly IInvoiceRepository _invoices;
    private readonly IPaymentRepository _payments;
    private readonly ICustomerStatementHtmlRenderer _html;
    private readonly TimeProvider _time;

    public CustomerStatementService(
        ICustomerRepository customers,
        IInvoiceRepository invoices,
        IPaymentRepository payments,
        ICustomerStatementHtmlRenderer html,
        TimeProvider? time = null)
    {
        _customers = customers;
        _invoices = invoices;
        _payments = payments;
        _html = html;
        _time = time ?? TimeProvider.System;
    }

    public async Task<CustomerStatementDocument?> BuildStatementAsync(
        Guid tenantId,
        Guid customerId,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken ct = default)
    {
        // ---- Input validation. Repository / service preconditions
        // throw ArgumentException; user-input shape (date order, span)
        // throws StatementValidationException so the API layer maps it
        // cleanly to 400. ----
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId is required.", nameof(customerId));

        var startDay = periodStart.Date;
        var endDay = periodEnd.Date;
        if (endDay < startDay)
        {
            throw new StatementValidationException(
                "Statement period 'from' must be on or before 'to'.");
        }

        // Inclusive day-count: [from, to] of the same day is 1 day.
        var inclusiveDays = (endDay - startDay).Days + 1;
        if (inclusiveDays > MaxRangeDays)
        {
            throw new StatementValidationException(
                $"Statement period spans {inclusiveDays} days; the maximum allowed is {MaxRangeDays} days.");
        }

        // ---- Tenant-scoped customer load. A null result is the only
        // signal we forward to the controller for "missing or
        // cross-tenant" so the 404 response is uniform either way. ----
        var customer = await _customers.GetActiveByIdAsync(tenantId, customerId, ct);
        if (customer is null) return null;

        var invoices = await _invoices.GetInvoicesForCustomerAsync(tenantId, customerId, ct);
        var payments = await _payments.GetRecordedPaymentsForCustomerAsync(tenantId, customerId, ct);

        var generatedAtUtc = _time.GetUtcNow().UtcDateTime;
        var generationDay = generatedAtUtc.Date;

        // ---- Currency consistency check (Option A, see report §12).
        // We collect distinct currencies across BOTH invoices and
        // payments because the opening balance pulls from pre-period
        // history that may predate any in-period activity. An empty
        // history naturally collapses to a default currency below. ----
        var distinctCurrencies = invoices.Select(i => i.Currency)
            .Concat(payments.Select(p => p.Currency))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
        if (distinctCurrencies.Count > 1)
        {
            throw new StatementValidationException(
                "Statement cannot span multiple currencies. The customer has activity in: "
                + string.Join(", ", distinctCurrencies));
        }
        var currency = distinctCurrencies.Count == 1 ? distinctCurrencies[0] : "USD";

        // ---- Balance computation. Day-boundary semantics so an
        // invoice issued at noon on the period start counts as
        // in-period, not pre-period. ----
        decimal openingBalance =
            invoices.Where(i => i.IssueDate.Date < startDay).Sum(i => i.TotalAmount)
            - payments.Where(p => p.PaidAt.Date < startDay).Sum(p => p.Amount);

        var periodInvoices = invoices
            .Where(i => i.IssueDate.Date >= startDay && i.IssueDate.Date <= endDay)
            .ToList();
        var periodPayments = payments
            .Where(p => p.PaidAt.Date >= startDay && p.PaidAt.Date <= endDay)
            .ToList();

        decimal totalInvoiced = periodInvoices.Sum(i => i.TotalAmount);
        decimal totalPaid = periodPayments.Sum(p => p.Amount);
        decimal totalAdjustments = 0m; // explicitly out of scope (see report §15)
        decimal closingBalance = openingBalance + totalInvoiced - totalPaid + totalAdjustments;

        // ---- Transaction stream. Invoice rows debit (raise the
        // balance), payment rows credit (lower it). Sort key:
        // (Date, TypePriority) where Invoice = 0 and Payment = 1 so
        // a same-day pair shows the invoice first — the conventional
        // accounting presentation order. ----
        var transactionsRaw = periodInvoices
            .Select(i => new
            {
                Date = i.IssueDate,
                TypeOrder = 0,
                Type = CustomerStatementTransactionType.Invoice,
                ReferenceId = i.Id,
                ReferenceNumber = (string?)i.InvoiceNumber,
                Description = $"Invoice {i.InvoiceNumber}",
                Debit = i.TotalAmount,
                Credit = 0m,
            })
            .Concat(periodPayments.Select(p => new
            {
                Date = p.PaidAt,
                TypeOrder = 1,
                Type = CustomerStatementTransactionType.Payment,
                ReferenceId = p.Id,
                ReferenceNumber = string.IsNullOrWhiteSpace(p.TransactionReference)
                    ? null
                    : (string?)p.TransactionReference,
                Description = string.IsNullOrWhiteSpace(p.Method)
                    ? "Payment received"
                    : $"Payment received ({p.Method})",
                Debit = 0m,
                Credit = p.Amount,
            }))
            .OrderBy(t => t.Date.Date)
            .ThenBy(t => t.TypeOrder)
            .ThenBy(t => t.Date)
            .ThenBy(t => t.ReferenceId)
            .ToList();

        var transactions = new List<CustomerStatementTransaction>(transactionsRaw.Count);
        decimal running = openingBalance;
        foreach (var row in transactionsRaw)
        {
            running = running + row.Debit - row.Credit;
            transactions.Add(new CustomerStatementTransaction(
                TransactionDate: row.Date,
                Type: row.Type,
                ReferenceId: row.ReferenceId,
                ReferenceNumber: row.ReferenceNumber,
                Description: row.Description,
                DebitAmount: row.Debit,
                CreditAmount: row.Credit,
                RunningBalance: running));
        }

        // ---- Outstanding invoices: every non-Voided / non-Refunded
        // invoice with AmountDue > 0 as of generation. Includes
        // invoices outside the period (a long-stale unpaid invoice
        // still surfaces). Per-invoice paid total is computed from
        // the pre-loaded recorded payments, grouped by InvoiceId, so
        // there is no per-invoice round-trip. ----
        var paidByInvoice = payments
            .GroupBy(p => p.InvoiceId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var outstanding = new List<CustomerStatementOutstandingInvoice>();
        foreach (var invoice in invoices.OrderBy(i => i.IssueDate).ThenBy(i => i.InvoiceNumber))
        {
            if (IsExcludedFromOutstanding(invoice.Status)) continue;

            var amountPaid = paidByInvoice.TryGetValue(invoice.Id, out var paid) ? paid : 0m;
            var amountDue = invoice.TotalAmount - amountPaid;
            if (amountDue <= 0m) continue;

            var daysPastDue = (generationDay - invoice.DueDate.Date).Days;
            if (daysPastDue < 0) daysPastDue = 0;

            outstanding.Add(new CustomerStatementOutstandingInvoice(
                InvoiceId: invoice.Id,
                InvoiceNumber: invoice.InvoiceNumber,
                IssueDate: invoice.IssueDate,
                DueDate: invoice.DueDate,
                Status: invoice.Status,
                Currency: invoice.Currency,
                TotalAmount: invoice.TotalAmount,
                AmountPaid: amountPaid,
                AmountDue: amountDue,
                DaysPastDue: daysPastDue));
        }

        var outstandingBalance = outstanding.Sum(o => o.AmountDue);

        return new CustomerStatementDocument(
            StatementId: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerId: customerId,
            CustomerName: customer.Name ?? string.Empty,
            CustomerEmail: customer.Email,
            PeriodStartDate: startDay,
            PeriodEndDate: endDay,
            GeneratedAtUtc: generatedAtUtc,
            Currency: currency,
            OpeningBalance: openingBalance,
            TotalInvoiced: totalInvoiced,
            TotalPaid: totalPaid,
            TotalAdjustments: totalAdjustments,
            ClosingBalance: closingBalance,
            OutstandingBalance: outstandingBalance,
            Transactions: transactions,
            OutstandingInvoices: outstanding);
    }

    public async Task<string?> RenderHtmlAsync(
        Guid tenantId,
        Guid customerId,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken ct = default)
    {
        var doc = await BuildStatementAsync(tenantId, customerId, periodStart, periodEnd, ct);
        return doc is null ? null : _html.Render(doc);
    }

    /// <summary>
    /// An invoice in <see cref="InvoiceStatus.Voided"/> or
    /// <see cref="InvoiceStatus.Refunded"/> is never reported as
    /// outstanding regardless of any <see cref="Invoice.TotalAmount"/>
    /// arithmetic — that matches the lifecycle's "terminal, no
    /// further activity" semantics.
    /// </summary>
    private static bool IsExcludedFromOutstanding(string status) =>
        status == InvoiceStatus.Voided || status == InvoiceStatus.Refunded;
}
