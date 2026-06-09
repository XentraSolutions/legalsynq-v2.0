using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Billing.Domain.Entities;

namespace Billing.Domain.Accounting.Erp;

/// <summary>
/// MS-BILL-ERP-001 — Pure (no-IO) helper that assembles the
/// canonical <see cref="AccountingExportPayload"/> from already-
/// loaded immutable Billing rows. NO new accounting math: invoice
/// effective totals reuse the WRITE-007 formula
/// <c>EffectiveTotal = TotalAmount + DebitSum - CreditSum</c>;
/// outstanding reuses <c>EffectiveTotal - PaidSum</c> with voided
/// payments excluded from <c>PaidSum</c>.
///
/// <para>
/// Also owns the deterministic fingerprint computation used by the
/// orchestrator's duplicate-prevention check (sha256 of
/// <c>tenantId | provider | exportType | windowFromUtc | windowToUtc</c>).
/// </para>
/// </summary>
public static class AccountingExportProjectionBuilder
{
    private const string AccountReceivable = "AccountsReceivable";
    private const string AccountRevenue = "Revenue";
    private const string AccountCash = "Cash";
    private const string AccountAdjustments = "RefundsAndAdjustments";
    private const string UnknownCustomerName = "(unknown)";

    /// <summary>
    /// Build the canonical payload. Inputs are already tenant-
    /// scoped and window-bounded by the repository; this method
    /// only composes them.
    /// </summary>
    public static AccountingExportPayload Build(
        Guid tenantId,
        string exportType,
        DateTime windowFromUtc,
        DateTime windowToUtc,
        string correlationId,
        IReadOnlyList<Invoice> invoices,
        IReadOnlyList<Payment> payments,
        IReadOnlyList<InvoiceAdjustment> adjustments,
        IReadOnlyDictionary<Guid, string> customerNames)
    {
        // Pre-aggregate per-invoice payment + adjustment sums so the
        // per-invoice projection runs in O(N) rather than O(N*M).
        // Voided payments are EXCLUDED from PaidSum (status
        // comparison is case-insensitive to mirror the WRITE-007
        // repository convention).
        var paidByInvoice = payments
            .Where(p => !string.Equals(p.Status, "Voided", StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => p.InvoiceId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var creditByInvoice = adjustments
            .Where(a => string.Equals(a.Type, "Credit", StringComparison.OrdinalIgnoreCase))
            .GroupBy(a => a.InvoiceId)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));

        var debitByInvoice = adjustments
            .Where(a => string.Equals(a.Type, "Debit", StringComparison.OrdinalIgnoreCase))
            .GroupBy(a => a.InvoiceId)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));

        var invoiceNumberById = invoices.ToDictionary(i => i.Id, i => i.InvoiceNumber);

        // Determine a single batch currency. If the tenant has
        // multi-currency rows we default to the first invoice's
        // currency (the projection still records each row's own
        // currency so the consumer can detect mismatch).
        var currency = invoices.FirstOrDefault()?.Currency
                       ?? payments.FirstOrDefault()?.Currency
                       ?? adjustments.FirstOrDefault()?.Currency
                       ?? "USD";

        // ---- Invoices ----
        var invoiceProj = new List<AccountingExportInvoice>(invoices.Count);
        foreach (var i in invoices)
        {
            var paid = paidByInvoice.TryGetValue(i.Id, out var p) ? p : 0m;
            var credit = creditByInvoice.TryGetValue(i.Id, out var c) ? c : 0m;
            var debit = debitByInvoice.TryGetValue(i.Id, out var d) ? d : 0m;
            var effectiveTotal = i.TotalAmount + debit - credit;
            var effectiveOutstanding = effectiveTotal - paid;

            invoiceProj.Add(new AccountingExportInvoice(
                InvoiceId: i.Id,
                InvoiceNumber: i.InvoiceNumber,
                CustomerId: i.CustomerId,
                CustomerName: ResolveCustomerName(customerNames, i.CustomerId),
                Status: i.Status,
                IssueDate: i.IssueDate,
                DueDate: i.DueDate,
                Subtotal: i.Subtotal,
                TaxAmount: i.TaxAmount,
                DiscountAmount: i.DiscountAmount,
                TotalAmount: i.TotalAmount,
                PaidAmount: paid,
                AdjustmentCreditSum: credit,
                AdjustmentDebitSum: debit,
                EffectiveTotal: effectiveTotal,
                EffectiveOutstanding: effectiveOutstanding,
                Currency: i.Currency,
                ExternalReference: null));
        }

        // ---- Payments ----
        var paymentProj = new List<AccountingExportPayment>(payments.Count);
        foreach (var p in payments)
        {
            paymentProj.Add(new AccountingExportPayment(
                PaymentId: p.Id,
                InvoiceId: p.InvoiceId,
                InvoiceNumber: invoiceNumberById.TryGetValue(p.InvoiceId, out var inum) ? inum : string.Empty,
                CustomerId: ResolveCustomerIdForInvoice(invoices, p.InvoiceId),
                Amount: p.Amount,
                Currency: p.Currency,
                Method: p.Method,
                Status: p.Status,
                TransactionReference: p.TransactionReference,
                PaidAt: p.PaidAt,
                Reversed: p.ReversedAt.HasValue,
                ReversedAt: p.ReversedAt));
        }

        // ---- Adjustments ----
        var adjustmentProj = new List<AccountingExportAdjustment>(adjustments.Count);
        foreach (var a in adjustments)
        {
            adjustmentProj.Add(new AccountingExportAdjustment(
                AdjustmentId: a.Id,
                InvoiceId: a.InvoiceId,
                InvoiceNumber: invoiceNumberById.TryGetValue(a.InvoiceId, out var inum) ? inum : string.Empty,
                CustomerId: a.CustomerId,
                Type: a.Type,
                Amount: a.Amount,
                Currency: a.Currency,
                Reason: a.Reason,
                ReferenceNumber: a.ReferenceNumber,
                CreatedAt: a.CreatedAt));
        }

        // ---- Derived journal entries ----
        var journal = BuildJournal(invoiceProj, paymentProj, adjustmentProj);

        return new AccountingExportPayload(
            TenantId: tenantId,
            ExportType: exportType,
            WindowFromUtc: windowFromUtc,
            WindowToUtc: windowToUtc,
            Currency: currency,
            CorrelationId: correlationId,
            Invoices: invoiceProj,
            Payments: paymentProj,
            Adjustments: adjustmentProj,
            JournalEntries: journal);
    }

    /// <summary>
    /// Deterministic dedupe fingerprint. Uppercased hex sha256 of
    /// <c>tenantId|provider|exportType|fromUtcRoundtrip|toUtcRoundtrip</c>.
    /// </summary>
    public static string ComputeFingerprint(
        Guid tenantId,
        string provider,
        string exportType,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var raw = string.Join("|",
            tenantId.ToString("N"),
            (provider ?? string.Empty).Trim().ToLowerInvariant(),
            (exportType ?? string.Empty).Trim(),
            fromUtc.ToString("O", CultureInfo.InvariantCulture),
            toUtc.ToString("O", CultureInfo.InvariantCulture));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    // ----------------------------------------------------------------

    private static string ResolveCustomerName(
        IReadOnlyDictionary<Guid, string> customerNames,
        Guid customerId)
        => customerNames.TryGetValue(customerId, out var n) && !string.IsNullOrWhiteSpace(n)
            ? n
            : UnknownCustomerName;

    private static Guid ResolveCustomerIdForInvoice(
        IReadOnlyList<Invoice> invoices,
        Guid invoiceId)
    {
        foreach (var i in invoices)
            if (i.Id == invoiceId)
                return i.CustomerId;
        return Guid.Empty;
    }

    /// <summary>
    /// Build the derived double-entry journal. Account labels are
    /// canonical Billing names — providers map them onto their own
    /// chart of accounts.
    /// </summary>
    private static IReadOnlyList<AccountingJournalEntry> BuildJournal(
        IReadOnlyList<AccountingExportInvoice> invoices,
        IReadOnlyList<AccountingExportPayment> payments,
        IReadOnlyList<AccountingExportAdjustment> adjustments)
    {
        var entries = new List<AccountingJournalEntry>(
            invoices.Count + payments.Count + adjustments.Count);

        // Invoice issued: Debit AR, Credit Revenue
        foreach (var i in invoices)
        {
            entries.Add(new AccountingJournalEntry(
                EntryType: "Invoice",
                SourceId: i.InvoiceId,
                EntryDate: i.IssueDate,
                DebitAccount: AccountReceivable,
                CreditAccount: AccountRevenue,
                Amount: i.TotalAmount,
                Currency: i.Currency,
                Memo: $"Invoice {i.InvoiceNumber}"));
        }

        // Payment received (excluding voided): Debit Cash, Credit AR
        foreach (var p in payments)
        {
            if (p.Reversed) continue;
            if (string.Equals(p.Status, "Voided", StringComparison.OrdinalIgnoreCase)) continue;
            entries.Add(new AccountingJournalEntry(
                EntryType: "Payment",
                SourceId: p.PaymentId,
                EntryDate: p.PaidAt,
                DebitAccount: AccountCash,
                CreditAccount: AccountReceivable,
                Amount: p.Amount,
                Currency: p.Currency,
                Memo: $"Payment for {p.InvoiceNumber}"));
        }

        // Credit memo: Debit RefundsAndAdjustments, Credit AR
        // Debit memo: Debit AR, Credit RefundsAndAdjustments
        foreach (var a in adjustments)
        {
            var isCredit = string.Equals(a.Type, "Credit", StringComparison.OrdinalIgnoreCase);
            entries.Add(new AccountingJournalEntry(
                EntryType: "Adjustment",
                SourceId: a.AdjustmentId,
                EntryDate: a.CreatedAt,
                DebitAccount: isCredit ? AccountAdjustments : AccountReceivable,
                CreditAccount: isCredit ? AccountReceivable : AccountAdjustments,
                Amount: a.Amount,
                Currency: a.Currency,
                Memo: $"{a.Type} adjustment on {a.InvoiceNumber}: {a.Reason}"));
        }

        return entries;
    }
}
