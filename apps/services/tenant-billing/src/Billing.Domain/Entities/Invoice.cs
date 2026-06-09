namespace Billing.Domain.Entities;

/// <summary>
/// Invoice issued to a customer. Aggregate root for line items and payments
/// in B01 (no totals/transition engine yet — those land in later blocks).
/// </summary>
public class Invoice
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = "Draft";
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? IssuedAt { get; set; }

    // ---- INV-TPL-02: Template branding snapshot ----
    //
    // These columns capture the *appearance* of the invoice template
    // that was effective at the time this invoice was created or
    // (if not yet stamped) issued. Once written they are never
    // mutated again — that is the whole point: a later edit to the
    // template does not silently rewrite this invoice's branding.
    //
    // All branding/text fields are nullable so an invoice that was
    // created without an effective template (no explicit id and no
    // tenant default) can persist with a null-snapshot. The three
    // display flags default to false in that case (no template ⇒
    // nothing template-driven to display); when stamped they take
    // the template's values.
    //
    // No FK is declared on InvoiceTemplateId — the template can be
    // retired or even deleted without invalidating this snapshot.
    public Guid? InvoiceTemplateId { get; set; }
    public string? TemplateOwnerType { get; set; }
    public string? TemplateName { get; set; }
    public string? TemplateLogoUrl { get; set; }
    public string? TemplateAccentColor { get; set; }
    public string? TemplateHeaderText { get; set; }
    public string? TemplateFooterText { get; set; }
    public string? TemplatePaymentInstructions { get; set; }
    public string? TemplateTermsText { get; set; }
    public string? TemplateMemoPlaceholder { get; set; }
    public bool TemplateDisplayBillingAddress { get; set; }
    public bool TemplateDisplayPaymentInstructions { get; set; }
    public bool TemplateDisplayTerms { get; set; }
    public DateTime? TemplateStampedAtUtc { get; set; }

    // ---- INV-TPL-04: Issuer / seller identity snapshot ----
    //
    // Mirrors InvoiceTemplate.Issuer* at the moment of stamping.
    // Same immutability contract as the template branding snapshot:
    // once written these are never updated. A null IssuerStampedAtUtc
    // means no issuer was captured (template had no issuer fields,
    // or invoice was created without any template). The renderer
    // treats null/empty issuer fields as "no From block".
    //
    // Stamped on the same code paths as the template branding
    // snapshot (create-time + issue-time ensure-stamp). Idempotency
    // is provided by the existing InvoiceTemplateId check in
    // EnsureStampedInvoice — issuer fields move with template
    // fields, never independently.
    public string? IssuerDisplayName { get; set; }
    public string? IssuerLegalName { get; set; }
    public string? IssuerAddressLine1 { get; set; }
    public string? IssuerAddressLine2 { get; set; }
    public string? IssuerCity { get; set; }
    public string? IssuerStateRegion { get; set; }
    public string? IssuerPostalCode { get; set; }
    public string? IssuerCountry { get; set; }
    public string? IssuerEmail { get; set; }
    public string? IssuerPhone { get; set; }
    public string? IssuerTaxId { get; set; }
    public string? IssuerWebsite { get; set; }
    public DateTime? IssuerStampedAtUtc { get; set; }

    public Customer? Customer { get; set; }
    public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();

    /// <summary>
    /// MS-BILL-WRITE-005 — append-only adjustment / credit memo
    /// ledger. The collection is never mutated by the adjustment
    /// flow's effective-balance recomputation (<c>InvoiceAdjustmentService</c>
    /// reads it via the repository, not via this navigation
    /// collection — the EF nav is here for read-side joins on the
    /// invoice detail screen and to mirror the Refunds shape).
    /// </summary>
    public ICollection<InvoiceAdjustment> Adjustments { get; set; } = new List<InvoiceAdjustment>();
}
