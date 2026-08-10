using Xenia.Domain.Common;

namespace Xenia.Domain.Configuration;

/// <summary>
/// Top-level Xenia settings for a specific tenant.
/// Controls whether Xenia automation is enabled for the tenant at all.
/// </summary>
public sealed class XeniaTenantSettings : AuditableEntityBase
{
    public const int SettingsMaxLength = 8000;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Whether Xenia is enabled for this tenant.</summary>
    public bool Enabled { get; private set; }

    /// <summary>
    /// Tenant-level Xenia settings as a JSON string.
    /// Used for tenant-specific Xenia feature flags and preferences.
    /// </summary>
    public string? Settings { get; private set; }

    /// <summary>EF Core constructor. Do not use from application code.</summary>
    private XeniaTenantSettings() { }

    public XeniaTenantSettings(Guid id, Guid tenantId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        Id = id;
        TenantId = tenantId;
        Enabled = true;
    }

    public void Enable() => Enabled = true;
    public void Disable() => Enabled = false;
    public void UpdateSettings(string? json) => Settings = json;
}
