namespace Commerce.Infrastructure.Integration.HostAdapters;

/// <summary>
/// LS-INT-01 — configuration for LegalSynq platform identity integration.
/// Bound from the <c>LegalSynq:Identity</c> section.
///
/// Safe defaults: <see cref="Enabled"/> = false preserves standalone behavior.
/// When <see cref="Enabled"/> is true, Commerce validates Bearer JWTs issued by
/// LegalSynq Identity and populates <see cref="LegalSynqJwtHostIdentityContextAccessor"/>
/// from the validated claims.
///
/// In production, supply <see cref="SigningKey"/> via the
/// <c>COMMERCE_LEGALSYNQ_SIGNING_KEY</c> environment variable — never commit
/// a real key to appsettings.
/// </summary>
public sealed class LegalSynqIdentityOptions
{
    public const string SectionName = "LegalSynq:Identity";

    /// <summary>
    /// Master switch. When false, JWT authentication middleware is not
    /// registered and <see cref="LocalHostIdentityContextAccessor"/> handles
    /// all requests (anonymous / standalone mode, unchanged behavior).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Expected JWT issuer. Must match <c>Jwt:Issuer</c> in LegalSynq Identity
    /// service configuration (default: <c>legalsynq-identity</c>).
    /// </summary>
    public string Issuer { get; set; } = "legalsynq-identity";

    /// <summary>
    /// Expected JWT audience. Must match <c>Jwt:Audience</c> in LegalSynq
    /// Identity (default: <c>legalsynq-platform</c>).
    /// </summary>
    public string Audience { get; set; } = "legalsynq-platform";

    /// <summary>
    /// HS256 signing key. Must match <c>Jwt:SigningKey</c> / <c>Jwt__SigningKey</c>
    /// in LegalSynq Identity. Populated at runtime from environment variable
    /// <c>COMMERCE_LEGALSYNQ_SIGNING_KEY</c> when blank in configuration.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Host-platform key written into <see cref="Commerce.Contracts.Integration.HostIdentityContext"/>
    /// for all LegalSynq-authenticated requests. Defaults to <c>legalsynq</c>.
    /// </summary>
    public string HostPlatformKey { get; set; } = "legalsynq";
}
