namespace Billing.Api.LegalSynq;

/// <summary>
/// LS-INT-01 — LegalSynq Identity JWT options for Tenant Billing.
/// Bound from the <c>LegalSynq:Identity</c> configuration section.
///
/// Safe defaults: <see cref="Enabled"/> = false — zero behavior change on deploy.
/// When true, JWT authentication middleware is added to the pipeline and
/// <see cref="LegalSynqJwtTenantContextResolver"/> can extract tenant context
/// from validated claims.
///
/// Supply <see cref="SigningKey"/> via <c>BILLING_LEGALSYNQ_SIGNING_KEY</c>
/// environment variable in production — never commit a real key.
/// </summary>
public sealed class LegalSynqIdentityOptions
{
    public const string SectionName = "LegalSynq:Identity";

    /// <summary>
    /// Master switch. When false, no JWT middleware is registered; the
    /// existing <c>X-Internal-Token</c> + <c>X-Tenant-Id</c> pipeline is
    /// completely unchanged.
    /// </summary>
    public bool Enabled { get; set; }

    public string Issuer { get; set; } = "legalsynq-identity";
    public string Audience { get; set; } = "legalsynq-platform";

    /// <summary>
    /// HS256 signing key — must match <c>Jwt__SigningKey</c> from LegalSynq Identity.
    /// Resolved at startup from env var <c>BILLING_LEGALSYNQ_SIGNING_KEY</c> when blank.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;
}
