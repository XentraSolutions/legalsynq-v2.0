namespace Billing.Domain.Reporting;

/// <summary>
/// MS-BILL-WRITE-007 — single row of the accounting-summary report.
/// One row per tenant-scoped invoice that matched the filters.
/// Currency math is identical to <see cref="Services.IInvoiceAccountingSummaryService"/>:
/// <c>EffectiveTotal = InvoiceTotal + AdjustmentDebitSum - AdjustmentCreditSum</c>,
/// <c>EffectiveOutstanding = EffectiveTotal - PaidSum</c>. Voided
/// payments are excluded by the repository contract.
/// </summary>
public sealed record AccountingSummaryRow(
    System.Guid InvoiceId,
    string InvoiceNumber,
    System.Guid CustomerId,
    string CustomerName,
    string Status,
    string Currency,
    decimal InvoiceTotal,
    decimal PaidSum,
    decimal AdjustmentCreditSum,
    decimal AdjustmentDebitSum,
    decimal EffectiveTotal,
    decimal EffectiveOutstanding,
    System.DateTime IssueDate,
    System.DateTime DueDate);

/// <summary>
/// MS-BILL-WRITE-007 — single row of the invoice-aging report. Only
/// invoices whose status is <c>Issued</c>, <c>PartiallyPaid</c>, or
/// <c>Overdue</c> appear (terminal / draft / refund states are
/// reconciled elsewhere). The row exposes the same accounting math
/// as <see cref="AccountingSummaryRow"/> so a CSV consumer can join
/// the two reports if needed.
/// </summary>
public sealed record InvoiceAgingRow(
    System.Guid InvoiceId,
    string InvoiceNumber,
    System.Guid CustomerId,
    string CustomerName,
    string Status,
    string Currency,
    decimal InvoiceTotal,
    decimal PaidSum,
    decimal EffectiveTotal,
    decimal EffectiveOutstanding,
    System.DateTime DueDate,
    int DaysOverdue,
    string AgingBucket);

/// <summary>
/// MS-BILL-WRITE-007 — single row of the adjustment report. Pure
/// projection over the append-only <c>InvoiceAdjustment</c> ledger;
/// no <c>CreatedBy</c> exposure (kept server-side per WRITE-005).
/// </summary>
public sealed record AdjustmentReportRow(
    System.Guid AdjustmentId,
    System.Guid InvoiceId,
    string InvoiceNumber,
    System.Guid CustomerId,
    string CustomerName,
    string Type,
    decimal Amount,
    string Currency,
    string Reason,
    string? ReferenceNumber,
    System.DateTime CreatedAt);

/// <summary>
/// MS-BILL-WRITE-007 — single row of the payments report. Voided
/// payments are excluded by default (the repository filter); the
/// <see cref="Reversed"/> flag is retained on the row shape for
/// forward-compatibility with a future "include voided" toggle and
/// is always <c>false</c> today.
/// </summary>
public sealed record PaymentReportRow(
    System.Guid PaymentId,
    System.Guid InvoiceId,
    string InvoiceNumber,
    System.Guid CustomerId,
    string CustomerName,
    decimal Amount,
    string Currency,
    string Method,
    string Status,
    string? TransactionReference,
    System.DateTime PaidAt,
    bool Reversed,
    System.DateTime? ReversedAt);
