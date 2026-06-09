namespace TenantBilling.Domain.Entities;

/// <summary>
/// Invoice lifecycle status values and the transition logic that drives them.
///
/// State machine (TBS-B02 + refund extension):
///
///   Draft ──issue──▶ Issued ──auto──▶ PartiallyPaid ──auto──▶ Paid
///     │                │                  │                     │
///     │                │                  └──reevaluate──▶ Overdue (if not paid by DueDate)
///     │                │                                       │
///     │                ├──reevaluate──▶ Overdue (if past due, no payments)
///     │                │                                       │
///     │                └──void──▶ Voided (only if no payments) │
///     │                                                        ▼
///     └──void──▶ Voided                              Paid ──refund(partial)──▶ PartiallyRefunded
///                                                       │                          │
///                                                       └──refund(full)──▶ Refunded ◀──refund(remaining)──┘
///
/// Auto transitions are driven by <see cref="ComputeStatus"/> from the sum of
/// payment amounts vs <c>Invoice.TotalAmount</c> and the invoice's
/// <c>DueDate</c>. Refunded and Voided are terminal — they are never re-derived
/// from payments. PartiallyRefunded is a stable post-refund state: the invoice
/// has been Paid, more refunds may follow, but no new payments are accepted.
/// Draft is also never re-derived; it requires an explicit Issue action to
/// enter the active lifecycle.
/// </summary>
public static class InvoiceStatus
{
    public const string Draft = "Draft";
    public const string Issued = "Issued";
    public const string PartiallyPaid = "PartiallyPaid";
    public const string Paid = "Paid";
    public const string Overdue = "Overdue";
    public const string Voided = "Voided";
    public const string PartiallyRefunded = "PartiallyRefunded";
    public const string Refunded = "Refunded";

    /// <summary>
    /// Returns true if the invoice is in a terminal state (no further auto
    /// transitions; no further payments accepted).
    /// </summary>
    public static bool IsTerminal(string status) =>
        status == Voided || status == Refunded;

    /// <summary>
    /// Returns true if a payment can be recorded against an invoice currently
    /// in <paramref name="status"/>. Draft must be Issued first; terminal
    /// states cannot accept payments. PartiallyRefunded was Paid before any
    /// refunds, so it does not accept additional payments either.
    /// </summary>
    public static bool AcceptsPayments(string status) =>
        status == Issued || status == PartiallyPaid || status == Overdue;

    /// <summary>
    /// Returns true if a refund can be recorded against an invoice currently
    /// in <paramref name="status"/>. Only fully Paid invoices and invoices
    /// already in PartiallyRefunded (sequential refund top-ups) qualify.
    /// </summary>
    public static bool AcceptsRefunds(string status) =>
        status == Paid || status == PartiallyRefunded;

    /// <summary>
    /// Pure function that derives the new status of an invoice given its
    /// current status, total amount, the sum of recorded payment amounts, the
    /// due date, and the current time. Voided/Refunded/Draft are preserved
    /// as-is — they only change via explicit actions. PartiallyRefunded is
    /// likewise preserved: it follows a Paid invoice and only changes via
    /// further refund actions, never from payment-derived recomputation.
    /// </summary>
    public static string ComputeStatus(
        string currentStatus,
        decimal totalAmount,
        decimal paidSum,
        DateTime dueDate,
        DateTime now)
    {
        // Terminal, refund-modified, and pre-active states are never
        // re-derived automatically.
        if (currentStatus == Draft
            || currentStatus == PartiallyRefunded
            || IsTerminal(currentStatus)) return currentStatus;

        if (paidSum >= totalAmount) return Paid;

        // Past-due rule (tightened in TBS-B05): an invoice that has gone past
        // its due date and is not yet fully paid is Overdue, regardless of
        // whether some money has already come in. This prevents a partial
        // payment on an overdue invoice from "downgrading" the status back to
        // PartiallyPaid and silently dropping it out of dunning. We compare
        // on date boundaries so an invoice due today is not yet Overdue.
        if (now.Date > dueDate.Date) return Overdue;

        if (paidSum > 0m) return PartiallyPaid;

        // Not past due, no payments yet: still Issued.
        return Issued;
    }
}
