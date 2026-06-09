namespace Billing.Domain.Services;

/// <summary>
/// Thrown when a payment cannot be recorded because the target invoice does
/// not exist or belongs to a different tenant. Maps to HTTP 404 at the API
/// boundary. Inherits <see cref="InvalidOperationException"/> so existing
/// callers (and the suite of <c>Assert.ThrowsAsync&lt;InvalidOperationException&gt;</c>
/// tests written before typed exceptions existed) keep matching.
/// </summary>
public sealed class InvoiceNotFoundException : InvalidOperationException
{
    public Guid TenantId { get; }
    public Guid InvoiceId { get; }

    public InvoiceNotFoundException(Guid tenantId, Guid invoiceId)
        : base($"Invoice {invoiceId} not found.")
    {
        TenantId = tenantId;
        InvoiceId = invoiceId;
    }
}

/// <summary>
/// Thrown when a payment amount fails the basic shape rules (non-positive,
/// or otherwise unusable as a money value). Maps to HTTP 400.
/// </summary>
public sealed class InvalidPaymentAmountException : InvalidOperationException
{
    public decimal Amount { get; }

    public InvalidPaymentAmountException(decimal amount)
        : base($"Payment amount {amount} must be greater than zero.")
    {
        Amount = amount;
    }
}

/// <summary>
/// Thrown when a payment is attempted against an invoice whose lifecycle
/// status does not accept payments (Draft, Paid, Voided, Refunded, ...).
/// Maps to HTTP 400.
/// </summary>
public sealed class InvalidInvoicePaymentStateException : InvalidOperationException
{
    public Guid InvoiceId { get; }
    public string CurrentStatus { get; }

    public InvalidInvoicePaymentStateException(Guid invoiceId, string currentStatus)
        : base($"Invoice {invoiceId} in status '{currentStatus}' cannot accept payments.")
    {
        InvoiceId = invoiceId;
        CurrentStatus = currentStatus;
    }
}

/// <summary>
/// Thrown when accepting a payment would push the invoice's recorded paid
/// total above its <c>TotalAmount</c>. Maps to HTTP 400.
/// </summary>
public sealed class OverpaymentException : InvalidOperationException
{
    public Guid InvoiceId { get; }
    public decimal AttemptedAmount { get; }
    public decimal RemainingBalance { get; }

    public OverpaymentException(Guid invoiceId, decimal attemptedAmount, decimal remainingBalance)
        : base($"Payment of {attemptedAmount} would overpay invoice {invoiceId}. " +
               $"Outstanding balance is {remainingBalance}.")
    {
        InvoiceId = invoiceId;
        AttemptedAmount = attemptedAmount;
        RemainingBalance = remainingBalance;
    }
}

/// <summary>
/// Thrown when a payment's currency does not match the invoice currency. We
/// never silently convert. Maps to HTTP 400.
/// </summary>
public sealed class CurrencyMismatchException : InvalidOperationException
{
    public string PaymentCurrency { get; }
    public string InvoiceCurrency { get; }

    public CurrencyMismatchException(string paymentCurrency, string invoiceCurrency)
        : base($"Payment currency '{paymentCurrency}' does not match invoice currency '{invoiceCurrency}'.")
    {
        PaymentCurrency = paymentCurrency;
        InvoiceCurrency = invoiceCurrency;
    }
}

// ============================================================================
// MS-BILL-WRITE-002 — payment reversal exceptions.
// ============================================================================

/// <summary>
/// MS-BILL-WRITE-002 — thrown when the requested payment id does not exist
/// for the calling tenant. Cross-tenant access surfaces as the same exception
/// (no existence leak). Maps to HTTP 404.
/// </summary>
public sealed class PaymentNotFoundException : InvalidOperationException
{
    public Guid TenantId { get; }
    public Guid PaymentId { get; }

    public PaymentNotFoundException(Guid tenantId, Guid paymentId)
        : base($"Payment {paymentId} not found.")
    {
        TenantId = tenantId;
        PaymentId = paymentId;
    }
}

/// <summary>
/// MS-BILL-WRITE-002 — thrown when a reversal is attempted on a payment that
/// is already in the terminal <c>"Voided"</c> lifecycle state. The reversal
/// lifecycle is one-way; a second reversal is a no-op from the caller's
/// perspective and surfaces as HTTP 409 so duplicate browser submissions
/// are observable in BFF audit logs without recording a phantom action.
/// </summary>
public sealed class PaymentAlreadyReversedException : InvalidOperationException
{
    public Guid PaymentId { get; }

    public PaymentAlreadyReversedException(Guid paymentId)
        : base($"Payment {paymentId} has already been reversed.")
    {
        PaymentId = paymentId;
    }
}

/// <summary>
/// MS-BILL-WRITE-002 — thrown when a reversal is attempted on a payment whose
/// current lifecycle status is something other than <c>"Recorded"</c> or
/// <c>"Voided"</c> (e.g. a legacy <c>"Pending"</c> row that pre-dates the
/// recorded-by-default contract). Distinct from
/// <see cref="PaymentAlreadyReversedException"/> so the BFF audit log can
/// distinguish the two cases. Maps to HTTP 409.
/// </summary>
public sealed class PaymentNotReversibleException : InvalidOperationException
{
    public Guid PaymentId { get; }
    public string CurrentStatus { get; }

    public PaymentNotReversibleException(Guid paymentId, string currentStatus)
        : base($"Payment {paymentId} in status '{currentStatus}' cannot be reversed.")
    {
        PaymentId = paymentId;
        CurrentStatus = currentStatus;
    }
}

/// <summary>
/// MS-BILL-WRITE-002 — thrown when the reversal reason is missing, blank, or
/// exceeds the column-bounded length. Maps to HTTP 400. The browser-facing
/// message names the bound so the UI can surface a useful hint.
/// </summary>
public sealed class InvalidReversalReasonException : InvalidOperationException
{
    public int MaxLength { get; }

    public InvalidReversalReasonException(string message, int maxLength)
        : base(message)
    {
        MaxLength = maxLength;
    }
}

// ============================================================================
// MS-BILL-WRITE-003 — payment notes-edit exceptions.
// ============================================================================

/// <summary>
/// MS-BILL-WRITE-003 — thrown when the supplied notes value violates the
/// length bound (the value is otherwise free-form: nullable, may be cleared
/// by passing empty/whitespace). Maps to HTTP 400. The browser-facing
/// message names the bound so the UI can surface a useful hint without
/// echoing back the rejected payload.
/// </summary>
public sealed class InvalidPaymentNotesException : InvalidOperationException
{
    public int MaxLength { get; }

    public InvalidPaymentNotesException(string message, int maxLength)
        : base(message)
    {
        MaxLength = maxLength;
    }
}
