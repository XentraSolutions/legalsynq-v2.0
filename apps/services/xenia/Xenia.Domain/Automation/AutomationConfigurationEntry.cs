using Xenia.Domain.Common;

namespace Xenia.Domain.Automation;

/// <summary>
/// Durable configuration entry for an automation at platform or tenant scope.
///
/// Stores configuration JSON and secret reference keys only — no resolved secret values.
/// Schema version is required for future validation.
///
/// Rules:
/// - ConfigurationJson must not contain resolved secret values.
/// - SecretReferencesJson contains reference keys only (e.g., key vault references).
/// - Size of ConfigurationJson is bounded to 65536 characters.
/// - Tenant-scoped queries are enforced at the service layer.
/// </summary>
public sealed class AutomationConfigurationEntry : AuditableEntityBase
{
    public const int ScopeTypeMaxLength              = 50;
    public const int AutomationKeyMaxLength          = 200;
    public const int NamespaceMaxLength              = 200;
    public const int SchemaVersionMaxLength          = 50;
    public const int UpdatedByMaxLength              = 200;
    public const int ConfigurationJsonMaxLength      = 65536;
    public const int SecretReferencesJsonMaxLength   = 4096;

    private AutomationConfigurationEntry() { }

    public Guid Id { get; private set; }

    /// <summary>"Platform" or "Tenant".</summary>
    public AutomationConfigurationScope ScopeType { get; private set; }

    /// <summary>Null for platform scope; required for tenant scope.</summary>
    public Guid? TenantId { get; private set; }

    public string AutomationKey { get; private set; } = string.Empty;

    public string ConfigurationNamespace { get; private set; } = string.Empty;

    /// <summary>Configuration JSON. No resolved secrets. Max 64 KB.</summary>
    public string ConfigurationJson { get; private set; } = "{}";

    public string SchemaVersion { get; private set; } = string.Empty;

    /// <summary>Secret reference keys only — no resolved values.</summary>
    public string? SecretReferencesJson { get; private set; }

    public string? UpdatedBy { get; private set; }

    /// <summary>Optimistic concurrency token.</summary>
    public uint RowVersion { get; private set; }

    public static AutomationConfigurationEntry CreatePlatform(
        string automationKey,
        string configurationNamespace,
        string configurationJson,
        string schemaVersion,
        string? secretReferencesJson = null,
        string? updatedBy = null)
    {
        return new AutomationConfigurationEntry
        {
            Id                     = Guid.CreateVersion7(),
            ScopeType              = AutomationConfigurationScope.Platform,
            TenantId               = null,
            AutomationKey          = automationKey,
            ConfigurationNamespace = configurationNamespace,
            ConfigurationJson      = configurationJson,
            SchemaVersion          = schemaVersion,
            SecretReferencesJson   = secretReferencesJson,
            UpdatedBy              = updatedBy,
            RowVersion             = 0,
        };
    }

    public static AutomationConfigurationEntry CreateTenant(
        Guid tenantId,
        string automationKey,
        string configurationNamespace,
        string configurationJson,
        string schemaVersion,
        string? secretReferencesJson = null,
        string? updatedBy = null)
    {
        return new AutomationConfigurationEntry
        {
            Id                     = Guid.CreateVersion7(),
            ScopeType              = AutomationConfigurationScope.Tenant,
            TenantId               = tenantId,
            AutomationKey          = automationKey,
            ConfigurationNamespace = configurationNamespace,
            ConfigurationJson      = configurationJson,
            SchemaVersion          = schemaVersion,
            SecretReferencesJson   = secretReferencesJson,
            UpdatedBy              = updatedBy,
            RowVersion             = 0,
        };
    }

    public void Update(
        string configurationJson,
        string schemaVersion,
        string? secretReferencesJson,
        string? updatedBy,
        uint expectedRowVersion)
    {
        if (RowVersion != expectedRowVersion)
            throw new InvalidOperationException(
                $"Concurrency conflict updating configuration '{AutomationKey}' in namespace '{ConfigurationNamespace}'. " +
                $"Expected RowVersion {expectedRowVersion}, actual {RowVersion}.");

        ConfigurationJson    = configurationJson;
        SchemaVersion        = schemaVersion;
        SecretReferencesJson = secretReferencesJson;
        UpdatedBy            = updatedBy;
        RowVersion++;
    }
}
