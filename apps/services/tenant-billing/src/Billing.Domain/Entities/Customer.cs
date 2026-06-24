namespace Billing.Domain.Entities;

/// <summary>
/// Billing customer (the entity that gets invoiced and pays).
/// Owned by a tenant; persisted in the Tenant Billing schema.
/// Soft-deleted (never physically removed) so historical invoices/payments
/// remain referentially intact.
/// </summary>
public class Customer
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }

    /// <summary>
    /// Legacy free-form billing address text (single bag-of-text). Kept
    /// for backward compatibility with customers created before the
    /// INV-TPL-04 structured-address columns existed. New writes should
    /// prefer the structured fields below; the render service falls
    /// back to this text in <c>CustomerAddress.Line1</c> only when none
    /// of the structured fields are populated.
    /// </summary>
    public string? BillingAddress { get; set; }

    // ---- INV-TPL-04: structured billing address ----
    //
    // All six are nullable so existing customers (created before this
    // block landed) remain valid without backfill. Render documents
    // expose them as the canonical "Bill To" address; when every
    // structured field is null the renderer falls back to the legacy
    // BillingAddress text above.
    public string? BillingAddressLine1 { get; set; }
    public string? BillingAddressLine2 { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingStateRegion { get; set; }
    public string? BillingPostalCode { get; set; }
    public string? BillingCountry { get; set; }

    public string? ExternalReference { get; set; }
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
