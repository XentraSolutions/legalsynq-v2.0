namespace Billing.Domain.Accounting.Erp.Remediation;

/// <summary>
/// MS-BILL-ERP-005 — One row in the unmapped-customer projection
/// surfaced to the tenant-admin remediation page.
///
/// <para>
/// Pure read projection. The repository populates every field from
/// existing tenant-scoped tables; this DTO never round-trips back
/// to the database. <see cref="ExistingMappingStatus"/> is non-null
/// only when a row exists in <c>quickbooks_customer_mappings</c> with
/// a non-Active status (i.e. the customer is "unresolved" because the
/// only mapping is Disabled). When NULL, the customer has no row at
/// all in the mapping table.
/// </para>
/// </summary>
public sealed record UnmappedCustomerRow(
    Guid BillingCustomerId,
    string BillingCustomerName,
    DateTime? LastInvoiceDate,
    string? LastExportFailureReason,
    DateTime? LastExportFailureAtUtc,
    string? ExportBlockedReason,
    string? ExistingMappingStatus);

/// <summary>
/// MS-BILL-ERP-005 — One QuickBooks customer returned by the
/// governed server-side search. Field set is the minimum needed to
/// let an operator pick the right candidate; nothing here is a
/// secret or PII beyond what is already visible inside the QBO
/// company.
/// </summary>
public sealed record QuickBooksCustomerSearchHit(
    string QuickBooksCustomerId,
    string DisplayName,
    bool Active,
    string? PrimaryEmail);

/// <summary>
/// Outcome wrapper for the customer-search call. Mirrors the
/// deterministic result shape used by ERP-002 / ERP-003 so the BFF
/// and UI render the same banner regardless of provider state.
/// </summary>
public sealed record QuickBooksCustomerSearchResult(
    QuickBooksCustomerLookupOutcome Outcome,
    IReadOnlyList<QuickBooksCustomerSearchHit> Hits,
    string? FailureReason)
{
    public static QuickBooksCustomerSearchResult Ok(IReadOnlyList<QuickBooksCustomerSearchHit> hits)
        => new(QuickBooksCustomerLookupOutcome.Ok, hits, null);

    public static QuickBooksCustomerSearchResult ConfigurationRequired()
        => new(QuickBooksCustomerLookupOutcome.ConfigurationRequired,
            Array.Empty<QuickBooksCustomerSearchHit>(),
            "QuickBooks provider configuration is incomplete.");

    public static QuickBooksCustomerSearchResult ProviderUnavailable(string reason)
        => new(QuickBooksCustomerLookupOutcome.ProviderUnavailable,
            Array.Empty<QuickBooksCustomerSearchHit>(),
            reason);

    public static QuickBooksCustomerSearchResult Failed(string reason)
        => new(QuickBooksCustomerLookupOutcome.Failed,
            Array.Empty<QuickBooksCustomerSearchHit>(),
            reason);
}

/// <summary>
/// Closed enum of governed lookup outcomes. Mirrors the
/// <c>AccountingExportStatus</c> deterministic shape so the UI can
/// reuse the same provider-availability banner.
/// </summary>
public enum QuickBooksCustomerLookupOutcome
{
    Ok = 0,
    ConfigurationRequired = 1,
    ProviderUnavailable = 2,
    Failed = 3,
}

/// <summary>
/// Inputs for the validation endpoint. All fields are
/// browser-supplied EXCEPT the tenant id, which is resolved from
/// the IDM session and injected by the BFF before the call reaches
/// Billing.Api.
/// </summary>
public sealed record MappingValidationCommand(
    Guid BillingCustomerId,
    string QuickBooksCustomerId);

/// <summary>
/// Closed enum of issue codes returned by the validation endpoint.
/// Each code maps 1:1 to a structured UI message; the BFF / UI
/// renders the message from the code so the wire format never
/// leaks raw exception text.
/// </summary>
public static class MappingValidationIssueCode
{
    public const string BillingCustomerNotFound        = "BillingCustomerNotFound";
    public const string BillingCustomerAlreadyMapped   = "BillingCustomerAlreadyMapped";
    public const string QuickBooksCustomerAlreadyMapped = "QuickBooksCustomerAlreadyMapped";
    public const string QuickBooksCustomerNotFound     = "QuickBooksCustomerNotFound";
    public const string ProviderUnavailable            = "ProviderUnavailable";
    public const string ProviderConfigurationRequired  = "ProviderConfigurationRequired";
    public const string InvalidQuickBooksCustomerId    = "InvalidQuickBooksCustomerId";
}

/// <summary>
/// One structured validation issue. <see cref="Code"/> is the
/// stable machine-readable tag (see <see cref="MappingValidationIssueCode"/>);
/// <see cref="Message"/> is the operator-facing string, NEVER raw
/// exception text and never carrying QBO secrets.
/// </summary>
public sealed record MappingValidationIssue(string Code, string Message);

/// <summary>
/// Validation outcome. <see cref="Outcome"/> is either
/// <c>"Ok"</c> (mapping is safe to persist via the existing ERP-003
/// POST) or <c>"Issues"</c> (one or more blockers). On Ok we also
/// echo back the QBO display name so the confirmation modal can
/// render it verbatim.
/// </summary>
public sealed record MappingValidationResult(
    string Outcome,
    string? QuickBooksDisplayName,
    IReadOnlyList<MappingValidationIssue> Issues)
{
    public const string OutcomeOk = "Ok";
    public const string OutcomeIssues = "Issues";

    public static MappingValidationResult Ok(string? quickBooksDisplayName)
        => new(OutcomeOk, quickBooksDisplayName, Array.Empty<MappingValidationIssue>());

    public static MappingValidationResult WithIssues(IReadOnlyList<MappingValidationIssue> issues)
        => new(OutcomeIssues, null, issues);
}
