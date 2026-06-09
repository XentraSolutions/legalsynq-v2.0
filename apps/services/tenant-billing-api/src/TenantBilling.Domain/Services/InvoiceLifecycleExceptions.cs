namespace TenantBilling.Domain.Services;

/// <summary>
/// Thrown when a caller asks the lifecycle engine to validate / perform a
/// transition that is not in the allowed transition graph (e.g.
/// <c>Voided -&gt; Issued</c>, <c>Paid -&gt; Draft</c>). Maps to HTTP 400 at
/// the API boundary. Inherits <see cref="InvalidOperationException"/> so the
/// pre-typed-exception suite of <c>Assert.ThrowsAsync&lt;InvalidOperationException&gt;</c>
/// tests keeps matching.
/// </summary>
public sealed class InvalidInvoiceTransitionException : InvalidOperationException
{
    public string FromStatus { get; }
    public string ToStatus { get; }

    public InvalidInvoiceTransitionException(string fromStatus, string toStatus)
        : base($"Invalid invoice status transition: '{fromStatus}' -> '{toStatus}'.")
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }
}

/// <summary>
/// Thrown when the lifecycle engine is given a status string it does not
/// recognise. Maps to HTTP 400. This exists separately from
/// <see cref="InvalidInvoiceTransitionException"/> so callers (and test
/// assertions) can distinguish "you tried a bad transition" from "you sent
/// a typo / legacy status the engine doesn't know about".
/// </summary>
public sealed class UnknownInvoiceStatusException : InvalidOperationException
{
    public string Status { get; }

    public UnknownInvoiceStatusException(string status)
        : base($"Unknown invoice status: '{status}'.")
    {
        Status = status;
    }
}

/// <summary>
/// Thrown when an invoice cannot be acted on because of operational
/// preconditions beyond a pure transition check (e.g. trying to mark an
/// invoice overdue whose due date has not passed yet, or trying to void an
/// invoice that has recorded payments). Maps to HTTP 400.
/// </summary>
public sealed class InvalidInvoiceStateException : InvalidOperationException
{
    public Guid InvoiceId { get; }
    public string Status { get; }

    public InvalidInvoiceStateException(Guid invoiceId, string status, string reason)
        : base($"Invoice {invoiceId} in status '{status}' cannot complete the requested operation: {reason}")
    {
        InvoiceId = invoiceId;
        Status = status;
    }
}
