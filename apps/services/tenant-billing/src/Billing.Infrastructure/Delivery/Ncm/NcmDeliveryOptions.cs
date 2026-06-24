namespace Billing.Infrastructure.Delivery.Ncm;

/// <summary>
/// MS-BILL-INT-002 — Strongly-typed configuration for the
/// NCM-backed <see cref="Billing.Domain.Statements.Delivery.IStatementDeliveryProvider"/>.
///
/// <para>
/// Bound from the <c>Billing:Delivery:Ncm</c> configuration
/// section. Every value is server-side; none of these fields are
/// ever exposed to the browser, included in API responses, or
/// logged outside of structured "missing" / "present" markers.
/// </para>
///
/// <para>
/// The companion provider <see cref="NcmStatementDeliveryProvider"/>
/// reports <c>IsConfigured = false</c> when any required field is
/// missing or whitespace, which causes
/// <c>StatementDeliveryService</c> to short-circuit with the same
/// deterministic <c>ProviderUnavailable / ProviderNotConfigured</c>
/// outcome the NoOp default returns. This means a half-configured
/// deployment does NOT silently send to the wrong place — it
/// surfaces the same operator-facing banner as no provider at all.
/// </para>
///
/// <para>
/// Required fields: <see cref="BaseUrl"/>, <see cref="ApiKey"/>,
/// <see cref="TemplateCode"/>.
/// Optional fields: <see cref="FromEmail"/>, <see cref="FromName"/>,
/// <see cref="TimeoutSeconds"/>.
/// </para>
/// </summary>
public sealed class NcmDeliveryOptions
{
    /// <summary>
    /// Configuration section name. Bound in DI as
    /// <c>configuration.GetSection(NcmDeliveryOptions.SectionName)</c>.
    /// </summary>
    public const string SectionName = "Billing:Delivery:Ncm";

    /// <summary>
    /// Absolute base URL of the NCM service (e.g.
    /// <c>https://ncm.internal.example.com</c>). The provider
    /// appends <c>/api/ntc/SendEmail</c>; do NOT include the path
    /// here. Trailing slashes are tolerated.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Pre-issued NCM API/JWT credential. Forwarded as
    /// <c>Authorization: Bearer {ApiKey}</c> on every send. Never
    /// logged, never echoed in <c>FailureReason</c>, never
    /// surfaced to the browser. Rotation is an operations concern
    /// — a rotated key takes effect on the next process start
    /// (options are bound at registration, not per-request).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// NCM template code (the <c>TemplateCode</c> field NCM's
    /// <c>SendEmail</c> handler requires). The HTML body and the
    /// subject line live in NCM's template store; Billing
    /// supplies substitution tokens (<c>StatementNumber</c>,
    /// <c>RecipientName</c>) per request.
    /// </summary>
    public string TemplateCode { get; set; } = string.Empty;

    /// <summary>
    /// Optional friendly From address. NCM's template config is
    /// authoritative; this is forwarded only as a substitution
    /// token (<c>FromEmail</c>) for templates that interpolate it.
    /// </summary>
    public string? FromEmail { get; set; }

    /// <summary>
    /// Optional friendly From name. Same forwarding semantics as
    /// <see cref="FromEmail"/>.
    /// </summary>
    public string? FromName { get; set; }

    /// <summary>
    /// Per-request transport timeout. Defaults to 15 seconds.
    /// Values &lt;= 0 are treated as "use default". Provider maps
    /// transport timeout to <c>RetryableFailure</c>.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// True when every required field is non-empty. The DI bootstrap
    /// uses this to decide whether to register
    /// <see cref="NcmStatementDeliveryProvider"/> or fall back to
    /// the NoOp default; the provider also re-checks at request
    /// time so a config swap mid-process is handled deterministically.
    /// </summary>
    public bool HasRequired() =>
        !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(TemplateCode);
}
