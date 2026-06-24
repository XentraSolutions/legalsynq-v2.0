using TenantBilling.Domain.Entities;

namespace TenantBilling.Domain.Services;

/// <summary>
/// Single source of truth for invoice state-machine rules. Owns the allowed-
/// transition graph and the predicates the rest of the domain uses to gate
/// operations (Issue, Void, Mark Overdue, accept payment).
///
/// <para>
/// Allowed transitions (TBS-B05 spec, plus the refund extension already in
/// the domain since the refund block):
/// </para>
/// <list type="bullet">
///   <item>Draft → Issued</item>
///   <item>Draft → Voided</item>
///   <item>Issued → PartiallyPaid</item>
///   <item>Issued → Paid</item>
///   <item>Issued → Overdue</item>
///   <item>Issued → Voided</item>
///   <item>PartiallyPaid → Paid</item>
///   <item>PartiallyPaid → Overdue</item>
///   <item>PartiallyPaid → Voided</item>
///   <item>Overdue → PartiallyPaid</item>
///   <item>Overdue → Paid</item>
///   <item>Overdue → Voided</item>
///   <item>Paid → PartiallyRefunded (refund flow)</item>
///   <item>Paid → Refunded (refund flow, full reversal)</item>
///   <item>PartiallyRefunded → Refunded (refund top-up to full)</item>
/// </list>
/// <para>
/// Not in the graph:
/// </para>
/// <list type="bullet">
///   <item>Anything out of Voided or Refunded (terminal).</item>
///   <item>Paid → Voided (refund is the only reversal path).</item>
///   <item>Issued → Draft, PartiallyPaid → Issued, Overdue → Issued
///         (no rewind-to-earlier-state transitions).</item>
/// </list>
/// <para>
/// This engine validates structural transitions only. Operational
/// preconditions (e.g. "PartiallyPaid → Voided is structurally allowed but
/// only if no payments are recorded", "Mark Overdue requires the due date
/// to have passed") are the caller's responsibility — the domain services
/// own those guards.
/// </para>
/// </summary>
public sealed class InvoiceLifecycleService
{
    /// <summary>
    /// Every status the engine knows about. Anything else is rejected by
    /// <see cref="ValidateTransition"/> with
    /// <see cref="UnknownInvoiceStatusException"/>.
    /// </summary>
    private static readonly HashSet<string> KnownStatuses = new(StringComparer.Ordinal)
    {
        InvoiceStatus.Draft,
        InvoiceStatus.Issued,
        InvoiceStatus.PartiallyPaid,
        InvoiceStatus.Paid,
        InvoiceStatus.Overdue,
        InvoiceStatus.Voided,
        InvoiceStatus.PartiallyRefunded,
        InvoiceStatus.Refunded,
    };

    /// <summary>
    /// Allowed (from, to) edges. Identity edges (e.g. Issued → Issued) are
    /// not in the set: the caller / service layer is expected to no-op when
    /// nothing changed rather than ask the engine to validate a self-loop.
    /// </summary>
    private static readonly HashSet<(string From, string To)> AllowedTransitions = new()
    {
        (InvoiceStatus.Draft,         InvoiceStatus.Issued),
        (InvoiceStatus.Draft,         InvoiceStatus.Voided),

        (InvoiceStatus.Issued,        InvoiceStatus.PartiallyPaid),
        (InvoiceStatus.Issued,        InvoiceStatus.Paid),
        (InvoiceStatus.Issued,        InvoiceStatus.Overdue),
        (InvoiceStatus.Issued,        InvoiceStatus.Voided),

        (InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid),
        (InvoiceStatus.PartiallyPaid, InvoiceStatus.Overdue),
        (InvoiceStatus.PartiallyPaid, InvoiceStatus.Voided),

        (InvoiceStatus.Overdue,       InvoiceStatus.PartiallyPaid),
        (InvoiceStatus.Overdue,       InvoiceStatus.Paid),
        (InvoiceStatus.Overdue,       InvoiceStatus.Voided),

        // Refund flow (already implemented; encoded here so a future
        // refactor cannot accidentally reject these paths).
        (InvoiceStatus.Paid,              InvoiceStatus.PartiallyRefunded),
        (InvoiceStatus.Paid,              InvoiceStatus.Refunded),
        (InvoiceStatus.PartiallyRefunded, InvoiceStatus.Refunded),
    };

    /// <summary>
    /// True when both <paramref name="fromStatus"/> and
    /// <paramref name="toStatus"/> are known statuses and the edge is in
    /// the allowed transition set. Returns false (without throwing) for any
    /// other case — useful for non-throwing callers.
    /// </summary>
    public bool CanTransition(string fromStatus, string toStatus)
    {
        if (!KnownStatuses.Contains(fromStatus)) return false;
        if (!KnownStatuses.Contains(toStatus)) return false;
        return AllowedTransitions.Contains((fromStatus, toStatus));
    }

    /// <summary>
    /// Throws <see cref="UnknownInvoiceStatusException"/> if either status
    /// is unknown, otherwise <see cref="InvalidInvoiceTransitionException"/>
    /// when the (from, to) edge is not allowed. Returns silently on a legal
    /// transition.
    /// </summary>
    public void ValidateTransition(string fromStatus, string toStatus)
    {
        if (!KnownStatuses.Contains(fromStatus))
            throw new UnknownInvoiceStatusException(fromStatus);
        if (!KnownStatuses.Contains(toStatus))
            throw new UnknownInvoiceStatusException(toStatus);
        if (!AllowedTransitions.Contains((fromStatus, toStatus)))
            throw new InvalidInvoiceTransitionException(fromStatus, toStatus);
    }

    /// <summary>
    /// Terminal statuses cannot leave their state via any transition in the
    /// engine. Voided and Refunded are terminal in TBS-B05.
    /// </summary>
    public bool IsTerminal(string status) => InvoiceStatus.IsTerminal(status);

    /// <summary>
    /// Mirror of <see cref="InvoiceStatus.AcceptsPayments"/>. Centralised
    /// here so payment-recording callers can consult the engine.
    /// </summary>
    public bool CanAcceptPayment(string status) => InvoiceStatus.AcceptsPayments(status);

    /// <summary>
    /// True when an invoice currently in <paramref name="status"/> is in a
    /// state that the void path can move away from. Operational guards (no
    /// recorded payments, etc.) are the service layer's responsibility.
    /// </summary>
    public bool CanBeVoided(string status) =>
        KnownStatuses.Contains(status) && AllowedTransitions.Contains((status, InvoiceStatus.Voided));

    /// <summary>
    /// True when an invoice currently in <paramref name="status"/> is in a
    /// state that can move to Overdue. Whether the due date has actually
    /// passed is the service's call.
    /// </summary>
    public bool CanBeMarkedOverdue(string status) =>
        KnownStatuses.Contains(status) && AllowedTransitions.Contains((status, InvoiceStatus.Overdue));
}
