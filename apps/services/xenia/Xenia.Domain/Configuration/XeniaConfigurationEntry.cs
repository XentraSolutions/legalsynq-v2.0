using Xenia.Domain.Common;

namespace Xenia.Domain.Configuration;

/// <summary>
/// A single persisted configuration value within the Xenia layered configuration system.
///
/// Precedence (lowest → highest):
///   Global → Tenant → Module → TenantModule
///
/// Secret values are stored as references (e.g. secret manager key), not plaintext.
/// The <see cref="IsSecret"/> flag prevents secret values from being returned by APIs.
/// </summary>
public sealed class XeniaConfigurationEntry : AuditableEntityBase
{
    public const int NamespaceMaxLength = 200;
    public const int KeyMaxLength = 200;
    public const int ValueMaxLength = 4000;
    public const int ValueTypeMaxLength = 50;

    public Guid Id { get; private set; }
    public ScopeType ScopeType { get; private set; }

    /// <summary>
    /// Identifies the scope target (tenant ID or module key).
    /// Null for Global scope.
    /// For TenantModule scope: formatted as <c>{tenantId}:{moduleKey}</c>.
    /// </summary>
    public string? ScopeId { get; private set; }

    public string Namespace { get; private set; } = string.Empty;
    public string ConfigurationKey { get; private set; } = string.Empty;
    public string? ConfigurationValue { get; private set; }

    /// <summary>Optional type hint: string, int, bool, json, secret-ref.</summary>
    public string? ValueType { get; private set; }

    /// <summary>
    /// When true, this entry holds a secret reference (not the secret itself).
    /// The /configuration API omits the value for secret entries.
    /// </summary>
    public bool IsSecret { get; private set; }

    /// <summary>Optimistic concurrency version.</summary>
    public int Version { get; private set; }

    /// <summary>EF Core constructor. Do not use from application code.</summary>
    private XeniaConfigurationEntry() { }

    public XeniaConfigurationEntry(
        Guid id,
        ScopeType scopeType,
        string? scopeId,
        string @namespace,
        string configurationKey,
        string? configurationValue,
        string? valueType = null,
        bool isSecret = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationKey);

        Id = id;
        ScopeType = scopeType;
        ScopeId = scopeId?.Trim();
        Namespace = @namespace.Trim();
        ConfigurationKey = configurationKey.Trim();
        ConfigurationValue = configurationValue;
        ValueType = valueType;
        IsSecret = isSecret;
        Version = 1;
    }

    public void UpdateValue(string? value, bool? isSecret = null)
    {
        ConfigurationValue = value;
        if (isSecret.HasValue) IsSecret = isSecret.Value;
        Version++;
    }
}
