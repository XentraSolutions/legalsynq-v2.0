namespace Billing.Domain.Projections;

/// <summary>
/// MS-BILL-WRITE-006 — single authoritative read-side accounting
/// projection for an invoice. Computed on demand from the immutable
/// invoice total, the append-only adjustment ledger, and the
/// non-voided payment sum. Voided payments are excluded by the
/// repository contract (<c>IPaymentRepository.SumRecordedPaymentsForInvoiceAsync</c>
/// already filters <c>Status != "Voided"</c>).
///
/// Formula:
/// <code>
/// effectiveTotal       = invoice.TotalAmount + adjustmentDebitSum - adjustmentCreditSum
/// effectiveOutstanding = effectiveTotal - paidSum
/// </code>
/// Same arithmetic as the WRITE-005 over-credit guard, so the
/// write-path and read-path agree line for line.
///
/// All currency fields are decimal — never <c>double</c>. The shape
/// is intentionally a <c>record</c> (immutable, value-equality) so a
/// caller cannot mutate it after the projection returns. <c>Currency</c>
/// is the parent invoice's currency; the projection does not attempt
/// any FX conversion (out of scope for the read surface).
/// </summary>
public sealed record InvoiceAccountingSummary(
    Guid InvoiceId,
    string Currency,
    decimal InvoiceTotal,
    decimal PaidSum,
    decimal AdjustmentCreditSum,
    decimal AdjustmentDebitSum,
    decimal EffectiveTotal,
    decimal EffectiveOutstanding);
