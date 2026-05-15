using TenantBilling.Domain.Entities;

namespace TenantBilling.Domain.Services;

/// <summary>
/// Input record for creating a new <see cref="InvoiceTemplate"/>. The
/// owner-scope is supplied by the controller (Platform call vs Tenant
/// call); callers do NOT pass it via the request body so a tenant
/// cannot accidentally create a Platform template.
/// </summary>
public sealed record NewInvoiceTemplate(
    string Name,
    string? Description,
    string? Status,
    bool? IsDefault,
    string? LogoUrl,
    string? AccentColor,
    string? HeaderText,
    string? FooterText,
    string? PaymentInstructions,
    string? TermsText,
    string? MemoPlaceholder,
    int? DefaultDueDays,
    string? InvoiceNumberPrefix,
    string? InvoiceNumberFormat,
    bool? DisplayBillingAddress,
    bool? DisplayPaymentInstructions,
    bool? DisplayTerms,
    // INV-TPL-04: issuer / seller identity (all optional; null means
    // "no issuer info on this template" and rendered invoices will
    // omit the From block).
    string? IssuerDisplayName = null,
    string? IssuerLegalName = null,
    string? IssuerAddressLine1 = null,
    string? IssuerAddressLine2 = null,
    string? IssuerCity = null,
    string? IssuerStateRegion = null,
    string? IssuerPostalCode = null,
    string? IssuerCountry = null,
    string? IssuerEmail = null,
    string? IssuerPhone = null,
    string? IssuerTaxId = null,
    string? IssuerWebsite = null);

/// <summary>
/// Input record for updating an existing template. All fields are
/// nullable: <c>null</c> means "no change" and an explicit value means
/// "replace". Status changes go through dedicated activate / retire
/// methods rather than this update path.
/// </summary>
public sealed record InvoiceTemplateUpdate(
    string? Name,
    string? Description,
    string? LogoUrl,
    string? AccentColor,
    string? HeaderText,
    string? FooterText,
    string? PaymentInstructions,
    string? TermsText,
    string? MemoPlaceholder,
    int? DefaultDueDays,
    string? InvoiceNumberPrefix,
    string? InvoiceNumberFormat,
    bool? DisplayBillingAddress,
    bool? DisplayPaymentInstructions,
    bool? DisplayTerms,
    // INV-TPL-04: issuer fields. Null = no change; an explicit value
    // (including the empty string after trimming) = "clear this
    // field" (treated as null by the normalizer).
    string? IssuerDisplayName = null,
    string? IssuerLegalName = null,
    string? IssuerAddressLine1 = null,
    string? IssuerAddressLine2 = null,
    string? IssuerCity = null,
    string? IssuerStateRegion = null,
    string? IssuerPostalCode = null,
    string? IssuerCountry = null,
    string? IssuerEmail = null,
    string? IssuerPhone = null,
    string? IssuerTaxId = null,
    string? IssuerWebsite = null);

/// <summary>
/// Service-layer entry point for the platform- and tenant-scoped
/// invoice template catalogues. The <c>tenantId</c> parameter
/// expresses scope: <c>null</c> means "Platform call" and the service
/// owns the no-tenant-scope rules; non-null means "Tenant call" and
/// the service enforces both ownership and the no-cross-tenant rule.
/// </summary>
public interface IInvoiceTemplateService
{
    Task<InvoiceTemplate> CreateAsync(Guid? tenantId, NewInvoiceTemplate input, CancellationToken ct = default);

    Task<InvoiceTemplate?> GetAsync(Guid? tenantId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<InvoiceTemplate>> ListAsync(Guid? tenantId, CancellationToken ct = default);

    Task<InvoiceTemplate?> GetDefaultAsync(Guid? tenantId, CancellationToken ct = default);

    /// <summary>
    /// Update editable fields on a Draft or Active template. Retired
    /// templates raise
    /// <see cref="InvalidInvoiceTemplateStatusTransitionException"/>;
    /// missing/cross-scope ids return null so the controller maps to
    /// 404 (no existence leak).
    /// </summary>
    Task<InvoiceTemplate?> UpdateAsync(Guid? tenantId, Guid id, InvoiceTemplateUpdate update, CancellationToken ct = default);

    Task<InvoiceTemplate?> ActivateAsync(Guid? tenantId, Guid id, CancellationToken ct = default);

    Task<InvoiceTemplate?> RetireAsync(Guid? tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Make this template the default in its owner-scope. Atomically
    /// unsets the previous default (if any) in the same scope. Throws
    /// <see cref="RetiredInvoiceTemplateCannotBeDefaultException"/>
    /// when the target template is retired.
    /// </summary>
    Task<InvoiceTemplate?> MakeDefaultAsync(Guid? tenantId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Read-only template lookup used by invoice creation (and, in later
/// blocks, by rendering / email). Kept on a separate interface so the
/// hot path for invoice creation depends on a tiny surface and the
/// admin write surface can evolve independently.
/// </summary>
public interface IInvoiceTemplateSelectionService
{
    /// <summary>
    /// Returns the active default template for this tenant, or null
    /// when the tenant has not configured one. Used by invoice
    /// creation to derive defaults like DueDate when the request
    /// omits them.
    /// </summary>
    Task<InvoiceTemplate?> GetDefaultForTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns the active default Platform template, or null when the
    /// platform has not configured one yet.
    /// </summary>
    Task<InvoiceTemplate?> GetDefaultPlatformAsync(CancellationToken ct = default);

    /// <summary>
    /// INV-TPL-02: pick the effective template for a tenant invoice
    /// given an optional explicit override. Implements the chain:
    ///
    /// <list type="number">
    ///   <item>If <paramref name="explicitTemplateId"/> is supplied,
    ///     load it in the tenant's scope. A null result throws
    ///     <see cref="InvoiceTemplateNotFoundInScopeException"/>; a
    ///     non-Active status throws
    ///     <see cref="InvoiceTemplateNotSelectableException"/>.</item>
    ///   <item>Otherwise fall back to
    ///     <see cref="GetDefaultForTenantAsync"/>.</item>
    ///   <item>If neither yields a template, return <c>null</c> —
    ///     creating an invoice without any template is valid.</item>
    /// </list>
    ///
    /// The result is the template object the caller should pass
    /// straight into the stamping service; no further status checks
    /// are needed downstream.
    /// </summary>
    Task<InvoiceTemplate?> SelectForTenantInvoiceAsync(
        Guid tenantId, Guid? explicitTemplateId, CancellationToken ct = default);

    /// <summary>
    /// INV-TPL-02: platform-scoped variant of
    /// <see cref="SelectForTenantInvoiceAsync"/>. The chain is
    /// identical except scope is the platform catalogue.
    /// </summary>
    Task<InvoiceTemplate?> SelectForPlatformInvoiceAsync(
        Guid? explicitTemplateId, CancellationToken ct = default);
}
