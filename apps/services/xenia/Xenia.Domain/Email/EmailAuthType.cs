namespace Xenia.Domain.Email;

/// <summary>
/// Authentication mechanisms supported by email sources.
/// Not every provider supports every auth type — see EmailProviderDefinitions for valid combinations.
/// </summary>
public enum EmailAuthType
{
    /// <summary>OAuth 2.0 delegated or application flow. Requires OAuth callback infrastructure.</summary>
    OAuth2,

    /// <summary>Username + password credential pair. Stored via secret reference only.</summary>
    UsernamePassword,

    /// <summary>Application-specific password (e.g. Google app password, Microsoft app password).</summary>
    AppPassword,

    /// <summary>
    /// OAuth 2.0 client credentials flow. Enterprise use. Connection metadata only;
    /// secrets stored via secret reference.
    /// </summary>
    ClientCredentials,

    /// <summary>
    /// A previously registered platform secret reference. Resolves via ISecretReferenceService.
    /// Preferred for all credential types — avoids direct credential input entirely.
    /// </summary>
    SecretReference,
}
