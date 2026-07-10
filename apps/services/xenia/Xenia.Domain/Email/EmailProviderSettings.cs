using Xenia.Domain.Common;

namespace Xenia.Domain.Email;

/// <summary>
/// Tenant-scoped provider-specific metadata for an email source.
///
/// Stores non-secret configuration such as host, port, TLS mode, OAuth tenant ID,
/// client ID (public), scopes, and connection options as JSON.
///
/// MUST NOT contain passwords, tokens, client secrets, or app passwords.
/// Those belong in the secret reference pointed to by EmailSource.SecretReferenceId.
/// </summary>
public sealed class EmailProviderSettings : AuditableEntityBase
{
    public const int ConfigurationJsonMaxLength = 8000;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmailSourceId { get; private set; }
    public EmailProviderType ProviderType { get; private set; }

    /// <summary>
    /// Safe (non-secret) provider configuration serialized as JSON.
    /// Example fields: host, port, tlsMode, oauthTenantId, publicClientId, scopes, timeoutSeconds.
    /// </summary>
    public string? ConfigurationJson { get; private set; }

    /// <summary>Monotonic version counter. Incremented on each settings update.</summary>
    public int ConfigurationVersion { get; private set; } = 1;

    private EmailProviderSettings() { }

    public EmailProviderSettings(
        Guid id,
        Guid tenantId,
        Guid emailSourceId,
        EmailProviderType providerType)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (emailSourceId == Guid.Empty)
            throw new ArgumentException("EmailSourceId cannot be empty.", nameof(emailSourceId));

        Id = id;
        TenantId = tenantId;
        EmailSourceId = emailSourceId;
        ProviderType = providerType;
        ConfigurationVersion = 1;
    }

    public void SetConfiguration(string? configurationJson)
    {
        if (configurationJson?.Length > ConfigurationJsonMaxLength)
            throw new ArgumentException(
                $"ConfigurationJson must not exceed {ConfigurationJsonMaxLength} characters.");

        ConfigurationJson = configurationJson;
        ConfigurationVersion++;
    }
}
