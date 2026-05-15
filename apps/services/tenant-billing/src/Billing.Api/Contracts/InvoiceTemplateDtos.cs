using System.ComponentModel.DataAnnotations;
using Billing.Domain.Entities;
using Billing.Domain.Services;

namespace Billing.Api.Contracts;

/// <summary>
/// Request body for <c>POST /api/invoice-templates/{platform|tenant}</c>.
/// Owner scope is NEVER read off the body — the controller derives it
/// from which route was hit + the tenant context — so a tenant cannot
/// accidentally create a Platform template.
/// </summary>
public sealed class CreateInvoiceTemplateRequest
{
    [Required, MaxLength(InvoiceTemplateValidation.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(InvoiceTemplateValidation.DescriptionMaxLength)]
    public string? Description { get; set; }

    /// <summary>Optional. Default Draft if omitted. Allowed: Draft, Active.</summary>
    public string? Status { get; set; }

    public bool? IsDefault { get; set; }

    [MaxLength(InvoiceTemplateValidation.LogoUrlMaxLength)]
    public string? LogoUrl { get; set; }

    [MaxLength(7)]
    public string? AccentColor { get; set; }

    [MaxLength(InvoiceTemplateValidation.HeaderTextMaxLength)]
    public string? HeaderText { get; set; }

    [MaxLength(InvoiceTemplateValidation.FooterTextMaxLength)]
    public string? FooterText { get; set; }

    [MaxLength(InvoiceTemplateValidation.PaymentInstructionsMaxLength)]
    public string? PaymentInstructions { get; set; }

    [MaxLength(InvoiceTemplateValidation.TermsTextMaxLength)]
    public string? TermsText { get; set; }

    [MaxLength(InvoiceTemplateValidation.MemoPlaceholderMaxLength)]
    public string? MemoPlaceholder { get; set; }

    [Range(InvoiceTemplateValidation.DefaultDueDaysMin, InvoiceTemplateValidation.DefaultDueDaysMax)]
    public int? DefaultDueDays { get; set; }

    [MaxLength(InvoiceTemplateValidation.InvoiceNumberPrefixMaxLength)]
    public string? InvoiceNumberPrefix { get; set; }

    [MaxLength(InvoiceTemplateValidation.InvoiceNumberFormatMaxLength)]
    public string? InvoiceNumberFormat { get; set; }

    public bool? DisplayBillingAddress { get; set; }
    public bool? DisplayPaymentInstructions { get; set; }
    public bool? DisplayTerms { get; set; }

    // ---- INV-TPL-04: issuer / seller identity ----
    [MaxLength(InvoiceTemplateValidation.IssuerDisplayNameMaxLength)]
    public string? IssuerDisplayName { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerLegalNameMaxLength)]
    public string? IssuerLegalName { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerAddressLineMaxLength)]
    public string? IssuerAddressLine1 { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerAddressLineMaxLength)]
    public string? IssuerAddressLine2 { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerCityMaxLength)]
    public string? IssuerCity { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerStateRegionMaxLength)]
    public string? IssuerStateRegion { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerPostalCodeMaxLength)]
    public string? IssuerPostalCode { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerCountryMaxLength)]
    public string? IssuerCountry { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerEmailMaxLength)]
    public string? IssuerEmail { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerPhoneMaxLength)]
    public string? IssuerPhone { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerTaxIdMaxLength)]
    public string? IssuerTaxId { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerWebsiteMaxLength)]
    public string? IssuerWebsite { get; set; }

    public NewInvoiceTemplate ToCommand() => new(
        Name,
        Description,
        Status,
        IsDefault,
        LogoUrl,
        AccentColor,
        HeaderText,
        FooterText,
        PaymentInstructions,
        TermsText,
        MemoPlaceholder,
        DefaultDueDays,
        InvoiceNumberPrefix,
        InvoiceNumberFormat,
        DisplayBillingAddress,
        DisplayPaymentInstructions,
        DisplayTerms,
        IssuerDisplayName,
        IssuerLegalName,
        IssuerAddressLine1,
        IssuerAddressLine2,
        IssuerCity,
        IssuerStateRegion,
        IssuerPostalCode,
        IssuerCountry,
        IssuerEmail,
        IssuerPhone,
        IssuerTaxId,
        IssuerWebsite);
}

/// <summary>
/// Request body for <c>PUT /api/invoice-templates/.../{id}</c>. Every
/// field is nullable; null = leave existing value alone, value =
/// replace. Status changes go through dedicated activate / retire
/// routes, not this PUT.
/// </summary>
public sealed class UpdateInvoiceTemplateRequest
{
    [MaxLength(InvoiceTemplateValidation.NameMaxLength)]
    public string? Name { get; set; }

    [MaxLength(InvoiceTemplateValidation.DescriptionMaxLength)]
    public string? Description { get; set; }

    [MaxLength(InvoiceTemplateValidation.LogoUrlMaxLength)]
    public string? LogoUrl { get; set; }

    [MaxLength(7)]
    public string? AccentColor { get; set; }

    [MaxLength(InvoiceTemplateValidation.HeaderTextMaxLength)]
    public string? HeaderText { get; set; }

    [MaxLength(InvoiceTemplateValidation.FooterTextMaxLength)]
    public string? FooterText { get; set; }

    [MaxLength(InvoiceTemplateValidation.PaymentInstructionsMaxLength)]
    public string? PaymentInstructions { get; set; }

    [MaxLength(InvoiceTemplateValidation.TermsTextMaxLength)]
    public string? TermsText { get; set; }

    [MaxLength(InvoiceTemplateValidation.MemoPlaceholderMaxLength)]
    public string? MemoPlaceholder { get; set; }

    [Range(InvoiceTemplateValidation.DefaultDueDaysMin, InvoiceTemplateValidation.DefaultDueDaysMax)]
    public int? DefaultDueDays { get; set; }

    [MaxLength(InvoiceTemplateValidation.InvoiceNumberPrefixMaxLength)]
    public string? InvoiceNumberPrefix { get; set; }

    [MaxLength(InvoiceTemplateValidation.InvoiceNumberFormatMaxLength)]
    public string? InvoiceNumberFormat { get; set; }

    public bool? DisplayBillingAddress { get; set; }
    public bool? DisplayPaymentInstructions { get; set; }
    public bool? DisplayTerms { get; set; }

    // ---- INV-TPL-04: issuer / seller identity ----
    [MaxLength(InvoiceTemplateValidation.IssuerDisplayNameMaxLength)]
    public string? IssuerDisplayName { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerLegalNameMaxLength)]
    public string? IssuerLegalName { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerAddressLineMaxLength)]
    public string? IssuerAddressLine1 { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerAddressLineMaxLength)]
    public string? IssuerAddressLine2 { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerCityMaxLength)]
    public string? IssuerCity { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerStateRegionMaxLength)]
    public string? IssuerStateRegion { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerPostalCodeMaxLength)]
    public string? IssuerPostalCode { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerCountryMaxLength)]
    public string? IssuerCountry { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerEmailMaxLength)]
    public string? IssuerEmail { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerPhoneMaxLength)]
    public string? IssuerPhone { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerTaxIdMaxLength)]
    public string? IssuerTaxId { get; set; }
    [MaxLength(InvoiceTemplateValidation.IssuerWebsiteMaxLength)]
    public string? IssuerWebsite { get; set; }

    public InvoiceTemplateUpdate ToCommand() => new(
        Name,
        Description,
        LogoUrl,
        AccentColor,
        HeaderText,
        FooterText,
        PaymentInstructions,
        TermsText,
        MemoPlaceholder,
        DefaultDueDays,
        InvoiceNumberPrefix,
        InvoiceNumberFormat,
        DisplayBillingAddress,
        DisplayPaymentInstructions,
        DisplayTerms,
        IssuerDisplayName,
        IssuerLegalName,
        IssuerAddressLine1,
        IssuerAddressLine2,
        IssuerCity,
        IssuerStateRegion,
        IssuerPostalCode,
        IssuerCountry,
        IssuerEmail,
        IssuerPhone,
        IssuerTaxId,
        IssuerWebsite);
}

/// <summary>
/// Full template projection. Avoids exposing internal navigation
/// references — InvoiceTemplate has none today, but keeping the
/// projection explicit guards against accidental leakage if a future
/// block adds one.
/// </summary>
public sealed record InvoiceTemplateResponse(
    Guid Id,
    string OwnerType,
    Guid? BillingAccountId,
    Guid? BillingProfileId,
    string Name,
    string? Description,
    string Status,
    bool IsDefault,
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
    bool DisplayBillingAddress,
    bool DisplayPaymentInstructions,
    bool DisplayTerms,
    string? IssuerDisplayName,
    string? IssuerLegalName,
    string? IssuerAddressLine1,
    string? IssuerAddressLine2,
    string? IssuerCity,
    string? IssuerStateRegion,
    string? IssuerPostalCode,
    string? IssuerCountry,
    string? IssuerEmail,
    string? IssuerPhone,
    string? IssuerTaxId,
    string? IssuerWebsite,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static InvoiceTemplateResponse From(InvoiceTemplate t) => new(
        t.Id,
        t.OwnerType,
        t.BillingAccountId,
        t.BillingProfileId,
        t.Name,
        t.Description,
        t.Status,
        t.IsDefault,
        t.LogoUrl,
        t.AccentColor,
        t.HeaderText,
        t.FooterText,
        t.PaymentInstructions,
        t.TermsText,
        t.MemoPlaceholder,
        t.DefaultDueDays,
        t.InvoiceNumberPrefix,
        t.InvoiceNumberFormat,
        t.DisplayBillingAddress,
        t.DisplayPaymentInstructions,
        t.DisplayTerms,
        t.IssuerDisplayName,
        t.IssuerLegalName,
        t.IssuerAddressLine1,
        t.IssuerAddressLine2,
        t.IssuerCity,
        t.IssuerStateRegion,
        t.IssuerPostalCode,
        t.IssuerCountry,
        t.IssuerEmail,
        t.IssuerPhone,
        t.IssuerTaxId,
        t.IssuerWebsite,
        t.CreatedAtUtc,
        t.UpdatedAtUtc);
}

/// <summary>
/// Slim projection used for list endpoints where callers only need
/// the catalogue overview (id, name, status, default flag, branding
/// colour). Keeps response payloads small for tenants that maintain
/// many templates.
/// </summary>
public sealed record InvoiceTemplateSummaryResponse(
    Guid Id,
    string OwnerType,
    string Name,
    string Status,
    bool IsDefault,
    string? AccentColor,
    int? DefaultDueDays,
    DateTime UpdatedAtUtc)
{
    public static InvoiceTemplateSummaryResponse From(InvoiceTemplate t) => new(
        t.Id, t.OwnerType, t.Name, t.Status, t.IsDefault,
        t.AccentColor, t.DefaultDueDays, t.UpdatedAtUtc);
}

/// <summary>
/// Response for <c>POST .../make-default</c>. Echoes the new default
/// (full projection) plus the previous default's id so an admin UI
/// can update its in-memory list without a follow-up GET.
/// </summary>
public sealed record MakeDefaultTemplateResponse(
    InvoiceTemplateResponse Template,
    Guid? PreviousDefaultTemplateId);
