namespace Billing.Domain.Services;

/// <summary>
/// Thrown when an invoice cannot be persisted because another invoice with the
/// same (TenantId, InvoiceNumber) already exists. Maps to HTTP 409 at the API
/// boundary.
/// </summary>
public sealed class DuplicateInvoiceNumberException : Exception
{
    public Guid TenantId { get; }
    public string InvoiceNumber { get; }

    public DuplicateInvoiceNumberException(Guid tenantId, string invoiceNumber)
        : base($"An invoice with InvoiceNumber '{invoiceNumber}' already exists for tenant {tenantId}.")
    {
        TenantId = tenantId;
        InvoiceNumber = invoiceNumber;
    }
}
