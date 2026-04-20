namespace Monitoring.Api.Authentication;

/// <summary>
/// Strongly-typed configuration for JWT Bearer (RS256) validation.
/// Bound from the configuration section <c>Authentication:Jwt</c>.
/// </summary>
public class JwtAuthenticationOptions
{
    public const string SectionName = "Authentication:Jwt";

    /// <summary>Expected token issuer ("iss"). Required.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Expected token audience ("aud"). Required.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Optional OIDC authority. When set, JWKS-based key resolution is used.
    /// Either <see cref="Authority"/> or <see cref="PublicKeyPem"/> must be provided.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// Optional explicit RSA public key in PEM format (SubjectPublicKeyInfo).
    /// Used when <see cref="Authority"/> is not configured.
    /// </summary>
    public string? PublicKeyPem { get; set; }

    /// <summary>
    /// Whether the metadata endpoint requires HTTPS. Defaults to true.
    /// Only relevant when <see cref="Authority"/> is set.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>Allowed clock skew, in seconds, when validating token lifetime.</summary>
    public int ClockSkewSeconds { get; set; } = 30;
}
