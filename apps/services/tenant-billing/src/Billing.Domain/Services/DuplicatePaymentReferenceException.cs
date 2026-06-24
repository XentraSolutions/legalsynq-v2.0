namespace Billing.Domain.Services;

/// <summary>
/// Thrown when a payment cannot be persisted because another payment with the
/// same (TenantId, TransactionReference) already exists. This protects against
/// duplicate webhook deliveries (e.g., the same Stripe charge being recorded
/// twice and inflating an invoice's paid total). Maps to HTTP 409 at the API
/// boundary.
/// </summary>
public sealed class DuplicatePaymentReferenceException : Exception
{
    public Guid TenantId { get; }
    public string TransactionReference { get; }

    public DuplicatePaymentReferenceException(Guid tenantId, string transactionReference)
        : base($"A payment with TransactionReference '{transactionReference}' already exists for tenant {tenantId}.")
    {
        TenantId = tenantId;
        TransactionReference = transactionReference;
    }
}
