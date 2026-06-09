namespace TenantBilling.Domain.Entities;

/// <summary>
/// Owner-scope discriminator for an <see cref="InvoiceTemplate"/>.
///
/// <c>Platform</c> templates are used when the platform invoices a
/// tenant (Platform Billing layer). They are not owned by any tenant
/// and have a null <see cref="InvoiceTemplate.BillingAccountId"/>.
///
/// <c>Tenant</c> templates are used when a tenant invoices its own
/// clients (Tenant Billing layer). They MUST carry a non-null
/// <see cref="InvoiceTemplate.BillingAccountId"/> (which in this
/// service maps 1:1 onto the existing <c>TenantId</c> column — the
/// codebase has no separate <c>BillingAccount</c> aggregate yet).
/// </summary>
public static class InvoiceTemplateOwnerType
{
    public const string Platform = "Platform";
    public const string Tenant = "Tenant";

    public static bool IsValid(string? value) =>
        value is Platform or Tenant;
}

/// <summary>
/// Lifecycle status for an <see cref="InvoiceTemplate"/>.
///
/// <list type="bullet">
///   <item><c>Draft</c> — fully editable; cannot be selected for
///     invoice creation/rendering and cannot be made default.</item>
///   <item><c>Active</c> — selectable and may be made default. Still
///     editable (we deliberately allow brand updates without forcing
///     a new template; the "no historical re-render" rule means past
///     invoices are unaffected).</item>
///   <item><c>Retired</c> — soft-removed. Cannot be selected, cannot
///     be made default, and is locked against branding/text edits.
///     A retired template that was previously default is unset from
///     default by the retirement transition.</item>
/// </list>
/// </summary>
public static class InvoiceTemplateStatus
{
    public const string Draft = "Draft";
    public const string Active = "Active";
    public const string Retired = "Retired";

    public static bool IsValid(string? value) =>
        value is Draft or Active or Retired;
}

/// <summary>
/// Configuration aggregate for branding + defaults applied when
/// rendering or creating an invoice. Templates are scoped per the
/// rules on <see cref="InvoiceTemplateOwnerType"/>; the platform and
/// each tenant maintain their own catalogues with at most one default
/// per scope.
///
/// This aggregate is configuration only — it never references or is
/// referenced from <see cref="Invoice"/> aggregate roots, so changing
/// or retiring a template does not mutate historical invoices. When
/// PDF/HTML rendering lands in a later block it is expected to read
/// the active default at render time (or snapshot at issue time, TBD).
/// </summary>
public sealed class InvoiceTemplate
{
    public Guid Id { get; set; }

    /// <summary>
    /// One of <see cref="InvoiceTemplateOwnerType.Platform"/> or
    /// <see cref="InvoiceTemplateOwnerType.Tenant"/>.
    /// </summary>
    public string OwnerType { get; set; } = InvoiceTemplateOwnerType.Tenant;

    /// <summary>
    /// Tenant scope reference for tenant-owned templates. Maps onto the
    /// service's existing <c>TenantId</c> column. MUST be null when
    /// <see cref="OwnerType"/> = Platform; MUST be non-null otherwise.
    /// </summary>
    public Guid? BillingAccountId { get; set; }

    /// <summary>
    /// Reserved for a future <c>TenantBillingProfile</c> aggregate.
    /// This service does not have such a concept yet, so the column
    /// exists but is always null in INV-TPL-01. Callers MUST leave it
    /// null; the service will reject non-null values until the
    /// aggregate is introduced.
    /// </summary>
    public Guid? TenantBillingProfileId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>One of <see cref="InvoiceTemplateStatus"/> values.</summary>
    public string Status { get; set; } = InvoiceTemplateStatus.Draft;

    public bool IsDefault { get; set; }

    // ---- Branding ----
    public string? LogoUrl { get; set; }
    public string? AccentColor { get; set; }
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }

    // ---- Payment / terms ----
    public string? PaymentInstructions { get; set; }
    public string? TermsText { get; set; }
    public string? MemoPlaceholder { get; set; }

    // ---- Numbering / due-date defaults ----
    public int? DefaultDueDays { get; set; }
    public string? InvoiceNumberPrefix { get; set; }
    public string? InvoiceNumberFormat { get; set; }

    // ---- Display toggles ----
    public bool DisplayBillingAddress { get; set; } = true;
    public bool DisplayPaymentInstructions { get; set; } = true;
    public bool DisplayTerms { get; set; } = true;

    // ---- INV-TPL-04: issuer / seller identity ----
    //
    // Source-of-truth for the "From" block on rendered invoices. All
    // nullable so existing templates created before this block remain
    // valid; the render service treats an entirely-null issuer set as
    // "no From block" and the HTML renderer omits the section.
    //
    // These are the LIVE template values — rendering ALWAYS reads
    // them off the matching invoice snapshot columns instead, so a
    // template edit cannot rewrite a historical invoice's From block.
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

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
