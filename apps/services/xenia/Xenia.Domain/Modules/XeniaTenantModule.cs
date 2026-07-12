using Xenia.Domain.Common;

namespace Xenia.Domain.Modules;

/// <summary>
/// Tracks per-tenant enablement and configuration for a Xenia module.
/// The global module must exist in <see cref="XeniaModule"/> before a tenant entry can be created.
/// </summary>
public sealed class XeniaTenantModule : AuditableEntityBase
{
    public const int ModuleKeyMaxLength = XeniaModule.KeyMaxLength;
    public const int ConfigurationMaxLength = 8000;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ModuleKey { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }

    /// <summary>
    /// Tenant-specific module configuration as a JSON string.
    /// Overrides the global module configuration for this tenant.
    /// </summary>
    public string? ModuleConfiguration { get; private set; }

    /// <summary>EF Core constructor. Do not use from application code.</summary>
    private XeniaTenantModule() { }

    public XeniaTenantModule(Guid id, Guid tenantId, string moduleKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));

        Id = id;
        TenantId = tenantId;
        ModuleKey = moduleKey.Trim();
        Enabled = false;
    }

    public void Enable() => Enabled = true;
    public void Disable() => Enabled = false;

    public void SetConfiguration(string? json) => ModuleConfiguration = json;
}
