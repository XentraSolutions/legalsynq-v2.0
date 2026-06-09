namespace Billing.Domain.Exceptions;

/// <summary>
/// MS-BILL-WRITE-005 — request-time validation failure: <c>Type</c>
/// is null/blank or not one of the canonical adjustment kinds
/// (<c>"Credit"</c>, <c>"Debit"</c>, case-insensitive on input).
/// Surfaces as a 400 ProblemDetails at the API.
/// </summary>
public sealed class InvalidAdjustmentTypeException : Exception
{
    public string SuppliedType { get; }

    public InvalidAdjustmentTypeException(string suppliedType)
        : base($"Adjustment type \"{suppliedType}\" is not recognised. " +
               "Expected \"Credit\" or \"Debit\".")
    {
        SuppliedType = suppliedType ?? string.Empty;
    }
}

/// <summary>
/// MS-BILL-WRITE-005 — fail-closed over-credit guard.
///
/// Thrown when a Credit adjustment would drive the invoice's
/// effective outstanding balance below zero — i.e. the credit memo
/// would refund more money than the customer actually owes (or has
/// paid). The adjustment is NOT inserted; the API surfaces this as
/// a 400 ProblemDetails so the operator can see the prior context
/// (effective owed, requested credit, paid sum) and re-submit a
/// smaller credit if appropriate. Refunds for over-paid amounts
/// have a dedicated flow (<c>POST /invoices/{id}/refund</c>).
/// </summary>
public sealed class OverCreditException : Exception
{
    public decimal EffectiveOwed { get; }
    public decimal RequestedCredit { get; }
    public decimal PaidSum { get; }

    public OverCreditException(decimal effectiveOwed, decimal requestedCredit, decimal paidSum)
        : base($"Credit adjustment of {requestedCredit:0.00} exceeds the " +
               $"effective outstanding balance ({effectiveOwed:0.00} owed, " +
               $"{paidSum:0.00} paid). Refund the over-paid amount via the " +
               $"refund endpoint instead, or submit a smaller credit.")
    {
        EffectiveOwed = effectiveOwed;
        RequestedCredit = requestedCredit;
        PaidSum = paidSum;
    }
}

/// <summary>
/// MS-BILL-WRITE-005 — terminal-state guard.
///
/// Thrown when the parent invoice is in a state where adjustments
/// no longer make accounting sense: <c>Voided</c> (cancelled),
/// <c>Refunded</c> (fully refunded — no obligation to adjust), or
/// <c>PartiallyRefunded</c> (refund flow has already partitioned
/// the balance). Surfaces as a 400 ProblemDetails at the API; the
/// UI hides the "New adjustment" button on these statuses as a
/// defence-in-depth measure.
/// </summary>
public sealed class InvoiceNotAdjustableException : Exception
{
    public string Status { get; }

    public InvoiceNotAdjustableException(string status)
        : base($"Invoice is in status \"{status}\" and cannot accept new " +
               "adjustments. Voided, Refunded, and PartiallyRefunded invoices " +
               "are out of scope for the adjustment workflow.")
    {
        Status = status ?? string.Empty;
    }
}
