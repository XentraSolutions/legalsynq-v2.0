using System.ComponentModel.DataAnnotations;
using TenantBilling.Domain.Entities;
using TenantBilling.Domain.StatementTemplates;

namespace TenantBilling.Api.Contracts;

/// <summary>
/// STAT-B02 — Request body for creating a statement template.
/// All fields except <see cref="Name"/> are optional and follow the
/// same null=skip / explicit=apply semantics as
/// <c>CreateInvoiceTemplateRequest</c>.
/// </summary>
public sealed class CreateStatementTemplateRequest
{
    [Required]
    [StringLength(StatementTemplateValidation.NameMaxLength, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? Status { get; set; }
    public bool? IsDefault { get; set; }

    public string? LogoUrl { get; set; }
    public string? AccentColor { get; set; }
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
    public string? PaymentInstructions { get; set; }
    public string? TermsText { get; set; }
    public string? MemoPlaceholder { get; set; }

    public bool? DisplayOutstandingTable { get; set; }
    public bool? DisplayPaymentInstructions { get; set; }
    public bool? DisplayTransactionMemos { get; set; }

    public string? StatementNumberPrefix { get; set; }

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

    public NewStatementTemplate ToCommand() => new(
        Name: Name,
        Description: Description,
        Status: Status,
        IsDefault: IsDefault,
        LogoUrl: LogoUrl,
        AccentColor: AccentColor,
        HeaderText: HeaderText,
        FooterText: FooterText,
        PaymentInstructions: PaymentInstructions,
        TermsText: TermsText,
        MemoPlaceholder: MemoPlaceholder,
        DisplayOutstandingTable: DisplayOutstandingTable,
        DisplayPaymentInstructions: DisplayPaymentInstructions,
        DisplayTransactionMemos: DisplayTransactionMemos,
        StatementNumberPrefix: StatementNumberPrefix,
        IssuerDisplayName: IssuerDisplayName,
        IssuerLegalName: IssuerLegalName,
        IssuerAddressLine1: IssuerAddressLine1,
        IssuerAddressLine2: IssuerAddressLine2,
        IssuerCity: IssuerCity,
        IssuerStateRegion: IssuerStateRegion,
        IssuerPostalCode: IssuerPostalCode,
        IssuerCountry: IssuerCountry,
        IssuerEmail: IssuerEmail,
        IssuerPhone: IssuerPhone,
        IssuerTaxId: IssuerTaxId,
        IssuerWebsite: IssuerWebsite);
}

/// <summary>
/// STAT-B02 — Update request body. Same null=no-change semantics
/// as <c>UpdateInvoiceTemplateRequest</c>.
/// </summary>
public sealed class UpdateStatementTemplateRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? AccentColor { get; set; }
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
    public string? PaymentInstructions { get; set; }
    public string? TermsText { get; set; }
    public string? MemoPlaceholder { get; set; }
    public bool? DisplayOutstandingTable { get; set; }
    public bool? DisplayPaymentInstructions { get; set; }
    public bool? DisplayTransactionMemos { get; set; }
    public string? StatementNumberPrefix { get; set; }
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

    public StatementTemplateUpdate ToCommand() => new(
        Name: Name,
        Description: Description,
        LogoUrl: LogoUrl,
        AccentColor: AccentColor,
        HeaderText: HeaderText,
        FooterText: FooterText,
        PaymentInstructions: PaymentInstructions,
        TermsText: TermsText,
        MemoPlaceholder: MemoPlaceholder,
        DisplayOutstandingTable: DisplayOutstandingTable,
        DisplayPaymentInstructions: DisplayPaymentInstructions,
        DisplayTransactionMemos: DisplayTransactionMemos,
        StatementNumberPrefix: StatementNumberPrefix,
        IssuerDisplayName: IssuerDisplayName,
        IssuerLegalName: IssuerLegalName,
        IssuerAddressLine1: IssuerAddressLine1,
        IssuerAddressLine2: IssuerAddressLine2,
        IssuerCity: IssuerCity,
        IssuerStateRegion: IssuerStateRegion,
        IssuerPostalCode: IssuerPostalCode,
        IssuerCountry: IssuerCountry,
        IssuerEmail: IssuerEmail,
        IssuerPhone: IssuerPhone,
        IssuerTaxId: IssuerTaxId,
        IssuerWebsite: IssuerWebsite);
}

/// <summary>
/// STAT-B02 — Full statement template view used in single-item
/// responses (create / get / update / lifecycle endpoints).
/// </summary>
public sealed record StatementTemplateResponse(
    Guid Id,
    Guid TenantId,
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
    bool DisplayOutstandingTable,
    bool DisplayPaymentInstructions,
    bool DisplayTransactionMemos,
    string? StatementNumberPrefix,
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
    public static StatementTemplateResponse From(StatementTemplate t) => new(
        Id: t.Id,
        TenantId: t.TenantId,
        Name: t.Name,
        Description: t.Description,
        Status: t.Status,
        IsDefault: t.IsDefault,
        LogoUrl: t.LogoUrl,
        AccentColor: t.AccentColor,
        HeaderText: t.HeaderText,
        FooterText: t.FooterText,
        PaymentInstructions: t.PaymentInstructions,
        TermsText: t.TermsText,
        MemoPlaceholder: t.MemoPlaceholder,
        DisplayOutstandingTable: t.DisplayOutstandingTable,
        DisplayPaymentInstructions: t.DisplayPaymentInstructions,
        DisplayTransactionMemos: t.DisplayTransactionMemos,
        StatementNumberPrefix: t.StatementNumberPrefix,
        IssuerDisplayName: t.IssuerDisplayName,
        IssuerLegalName: t.IssuerLegalName,
        IssuerAddressLine1: t.IssuerAddressLine1,
        IssuerAddressLine2: t.IssuerAddressLine2,
        IssuerCity: t.IssuerCity,
        IssuerStateRegion: t.IssuerStateRegion,
        IssuerPostalCode: t.IssuerPostalCode,
        IssuerCountry: t.IssuerCountry,
        IssuerEmail: t.IssuerEmail,
        IssuerPhone: t.IssuerPhone,
        IssuerTaxId: t.IssuerTaxId,
        IssuerWebsite: t.IssuerWebsite,
        CreatedAtUtc: t.CreatedAtUtc,
        UpdatedAtUtc: t.UpdatedAtUtc);
}

/// <summary>
/// STAT-B02 — Lighter projection for the list endpoint.
/// </summary>
public sealed record StatementTemplateSummaryResponse(
    Guid Id,
    string Name,
    string Status,
    bool IsDefault,
    DateTime UpdatedAtUtc)
{
    public static StatementTemplateSummaryResponse From(StatementTemplate t) =>
        new(t.Id, t.Name, t.Status, t.IsDefault, t.UpdatedAtUtc);
}

/// <summary>
/// STAT-B02 — Response wrapper for <c>POST .../make-default</c>
/// echoing the prior default id (if different) for client UI.
/// </summary>
public sealed record MakeDefaultStatementTemplateResponse(
    StatementTemplateResponse Template,
    Guid? PreviousDefaultId);
