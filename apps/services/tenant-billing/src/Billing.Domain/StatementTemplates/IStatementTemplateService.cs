using Billing.Domain.Entities;

namespace Billing.Domain.StatementTemplates;

/// <summary>
/// STAT-B02 — Input record for creating a new
/// <see cref="StatementTemplate"/>. Tenant scope is supplied by the
/// controller (always present, since statement templates are
/// tenant-only).
/// </summary>
public sealed record NewStatementTemplate(
    string Name,
    string? Description = null,
    string? Status = null,
    bool? IsDefault = null,
    string? LogoUrl = null,
    string? AccentColor = null,
    string? HeaderText = null,
    string? FooterText = null,
    string? PaymentInstructions = null,
    string? TermsText = null,
    string? MemoPlaceholder = null,
    bool? DisplayOutstandingTable = null,
    bool? DisplayPaymentInstructions = null,
    bool? DisplayTransactionMemos = null,
    string? StatementNumberPrefix = null,
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
/// STAT-B02 — Update record. Same null=no-change / value=replace
/// semantics as <c>InvoiceTemplateUpdate</c>. Status changes go
/// through dedicated activate / retire methods.
/// </summary>
public sealed record StatementTemplateUpdate(
    string? Name = null,
    string? Description = null,
    string? LogoUrl = null,
    string? AccentColor = null,
    string? HeaderText = null,
    string? FooterText = null,
    string? PaymentInstructions = null,
    string? TermsText = null,
    string? MemoPlaceholder = null,
    bool? DisplayOutstandingTable = null,
    bool? DisplayPaymentInstructions = null,
    bool? DisplayTransactionMemos = null,
    string? StatementNumberPrefix = null,
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
/// STAT-B02 — Service-layer entry point for the tenant-scoped
/// statement template catalogue. Mirrors
/// <c>IInvoiceTemplateService</c>.
/// </summary>
public interface IStatementTemplateService
{
    Task<StatementTemplate> CreateAsync(Guid tenantId, NewStatementTemplate input, CancellationToken ct = default);

    Task<StatementTemplate?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<StatementTemplate>> ListAsync(Guid tenantId, CancellationToken ct = default);

    Task<StatementTemplate?> GetDefaultAsync(Guid tenantId, CancellationToken ct = default);

    Task<StatementTemplate?> UpdateAsync(Guid tenantId, Guid id, StatementTemplateUpdate update, CancellationToken ct = default);

    Task<StatementTemplate?> ActivateAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<StatementTemplate?> RetireAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<StatementTemplate?> MakeDefaultAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// STAT-B02 — Read-only template lookup used by the persistence
/// service to pick the effective template for a generation. Kept on
/// a separate interface so the hot path depends on a tiny surface.
/// </summary>
public interface IStatementTemplateSelectionService
{
    /// <summary>
    /// Pick the effective template for a tenant-scoped statement
    /// generation given an optional explicit override. Implements
    /// the chain: explicit id → validated default → null. A null
    /// result is valid — generating a statement without any
    /// template is allowed.
    /// </summary>
    Task<StatementTemplate?> SelectForStatementAsync(
        Guid tenantId, Guid? explicitTemplateId, CancellationToken ct = default);
}
