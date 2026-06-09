namespace TenantBilling.Domain.Services;

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
