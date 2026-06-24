namespace Billing.Infrastructure.Accounting.Erp.Providers.QuickBooks;

/// <summary>
/// MS-BILL-ERP-002 — Strongly-typed configuration for the
/// QuickBooks Online <see cref="Billing.Domain.Accounting.Erp.IAccountingExportProvider"/>.
///
/// <para>
/// Bound from the <c>Billing:Erp:QuickBooks</c> configuration
/// section. Every value is server-side; none of these fields are
/// ever exposed to the browser, included in API responses, or
/// logged outside of structured "missing" / "present" markers.
/// </para>
///
/// <para>
/// The companion provider
/// <see cref="QuickBooksAccountingExportProvider"/> reports
/// <c>IsConfigured = false</c> when any required field is missing
/// or whitespace, which causes
/// <c>AccountingExportService</c> to short-circuit with the same
/// deterministic <c>ProviderUnavailable</c> outcome the NoOp
/// default returns. This means a half-configured deployment does
/// NOT silently send to the wrong place — it surfaces the same
/// operator-facing banner as no provider at all.
/// </para>
///
/// <para>
/// Required fields: <see cref="RealmId"/>, <see cref="ClientId"/>,
/// <see cref="ClientSecret"/>, <see cref="RefreshToken"/>,
/// <see cref="AccountsReceivableRef"/>, <see cref="IncomeAccountRef"/>,
/// <see cref="UndepositedFundsRef"/>, <see cref="AdjustmentAccountRef"/>.
/// Optional: <see cref="Environment"/> (default <c>production</c>),
/// <see cref="MinorVersion"/> (default <c>70</c>),
/// <see cref="TimeoutSeconds"/> (default <c>20</c>).
/// </para>
/// </summary>
public sealed class QuickBooksOptions
{
    /// <summary>
    /// Configuration section name. Bound in DI as
    /// <c>configuration.GetSection(QuickBooksOptions.SectionName)</c>.
    /// </summary>
    public const string SectionName = "Billing:Erp:QuickBooks";

    public const string ProductionEnvironment = "production";
    public const string SandboxEnvironment = "sandbox";

    private const string ProductionApiBase = "https://quickbooks.api.intuit.com";
    private const string SandboxApiBase = "https://sandbox-quickbooks.api.intuit.com";
    private const string OAuthTokenEndpoint = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";

    /// <summary>
    /// QuickBooks Online realm (company) ID. Identifies the QBO
    /// company every JournalEntry is posted into. Pinned in
    /// configuration; never browser-supplied.
    /// </summary>
    public string RealmId { get; set; } = string.Empty;

    /// <summary>Intuit OAuth2 client id. Server-side only.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Intuit OAuth2 client secret. Server-side only.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Long-lived refresh token. Used by
    /// <see cref="QuickBooksTokenProvider"/> to mint short-lived
    /// access tokens. Operators rotate this out of band; this
    /// service does not ship a rotation UI.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// <c>"production"</c> or <c>"sandbox"</c>. Defaults to
    /// production. Selects the QBO API base URL.
    /// </summary>
    public string Environment { get; set; } = ProductionEnvironment;

    /// <summary>
    /// QBO API minor version. Default 70 (current at time of
    /// implementation). Forwarded as <c>?minorversion=N</c>.
    /// </summary>
    public int MinorVersion { get; set; } = 70;

    /// <summary>QBO Account ref id used as the credit side of an Income journal line.</summary>
    public string IncomeAccountRef { get; set; } = string.Empty;

    /// <summary>QBO Account ref id for AR (debit on invoice issue, credit on payment receipt).</summary>
    public string AccountsReceivableRef { get; set; } = string.Empty;

    /// <summary>QBO Account ref id used for the cash side of a payment journal entry.</summary>
    public string UndepositedFundsRef { get; set; } = string.Empty;

    /// <summary>QBO Account ref id used as the offset for invoice adjustments / credit memos.</summary>
    public string AdjustmentAccountRef { get; set; } = string.Empty;

    /// <summary>Per-request transport timeout. Defaults to 20 seconds. Values &lt;= 0 → default.</summary>
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// MS-BILL-ERP-003 — provider-wide export-mode toggle. One of
    /// <see cref="ExportModeJournalEntry"/> (default; preserves the
    /// ERP-002 JournalEntry-first behaviour) or
    /// <see cref="ExportModeInvoiceFirst"/> (post QBO Invoices for
    /// every <c>AccountingExportInvoice</c> against the resolved
    /// QBO customer ref). A per-mapping override on a
    /// <see cref="Billing.Domain.Accounting.Erp.QuickBooks.QuickBooksCustomerMapping"/>
    /// row wins over this toggle when present.
    ///
    /// Values other than the two literals fall through to
    /// <see cref="ExportModeJournalEntry"/> — half-configured
    /// deployments NEVER silently flip to Invoice-first.
    /// </summary>
    public string ExportMode { get; set; } = ExportModeJournalEntry;

    /// <summary>
    /// MS-BILL-ERP-003 — optional fallback QBO customer ref. When
    /// non-empty AND <see cref="FallbackCustomerEnabled"/> is true,
    /// the InvoiceFirst path uses this ref for any Billing customer
    /// without an explicit mapping. When NULL/empty or
    /// <see cref="FallbackCustomerEnabled"/> is false, an unmapped
    /// Billing customer surfaces deterministic Failed (with a
    /// human-readable reason); the provider NEVER auto-creates a
    /// QBO customer.
    /// </summary>
    public string? FallbackCustomerRef { get; set; }

    /// <summary>
    /// Master switch for <see cref="FallbackCustomerRef"/>. Defaults
    /// to false (strict mapping-only mode) so a half-configured
    /// fallback ref does NOT silently route every unmapped customer
    /// to a generic AR row.
    /// </summary>
    public bool FallbackCustomerEnabled { get; set; }

    public const string ExportModeJournalEntry = "JournalEntry";
    public const string ExportModeInvoiceFirst = "InvoiceFirst";

    /// <summary>
    /// Resolved export mode that survives configuration typos by
    /// falling back to <see cref="ExportModeJournalEntry"/>. Used
    /// by the provider to pick the export branch.
    /// </summary>
    public string ResolveExportMode() =>
        string.Equals(ExportMode, ExportModeInvoiceFirst, System.StringComparison.OrdinalIgnoreCase)
            ? ExportModeInvoiceFirst
            : ExportModeJournalEntry;

    /// <summary>
    /// True when every required credential and account ref is
    /// non-empty. The DI bootstrap uses this to decide whether to
    /// register the real provider; the provider also re-checks at
    /// request time so a config swap mid-process is handled
    /// deterministically.
    /// </summary>
    public bool HasRequired() =>
        !string.IsNullOrWhiteSpace(RealmId)
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(RefreshToken)
        && !string.IsNullOrWhiteSpace(IncomeAccountRef)
        && !string.IsNullOrWhiteSpace(AccountsReceivableRef)
        && !string.IsNullOrWhiteSpace(UndepositedFundsRef)
        && !string.IsNullOrWhiteSpace(AdjustmentAccountRef);

    /// <summary>
    /// Resolved API base URL based on <see cref="Environment"/>.
    /// Anything other than the literal "sandbox" (case-insensitive)
    /// falls through to production — half-configured environments
    /// never silently flip to sandbox.
    /// </summary>
    public string ResolveApiBaseUrl() =>
        string.Equals(Environment, SandboxEnvironment, System.StringComparison.OrdinalIgnoreCase)
            ? SandboxApiBase
            : ProductionApiBase;

    /// <summary>OAuth2 token endpoint URI. Constant for both prod and sandbox per Intuit docs.</summary>
    public string ResolveTokenEndpoint() => OAuthTokenEndpoint;
}
