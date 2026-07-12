using Xenia.Domain.Common;

namespace Xenia.Domain.Automation;

/// <summary>
/// Per-tenant enablement and lifecycle override for an automation.
///
/// Each row represents one tenant's relationship to one automation key.
/// No cross-tenant foreign-key path exists from this entity.
///
/// Rules:
/// - No credentials or secret values.
/// - Configuration stored separately in <see cref="AutomationConfigurationEntry"/>.
/// </summary>
public sealed class TenantAutomationState : AuditableEntityBase
{
    public const int AutomationKeyMaxLength     = 200;
    public const int LifecycleOverrideMaxLength = 50;
    public const int ConfigVersionMaxLength     = 50;
    public const int UpdatedByMaxLength         = 200;

    private TenantAutomationState() { }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string AutomationKey { get; private set; } = string.Empty;

    /// <summary>Whether this tenant has the automation enabled.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Optional tenant-level lifecycle override (e.g. "Suspended").</summary>
    public string? LifecycleOverride { get; private set; }

    /// <summary>Current configuration version in effect for this tenant.</summary>
    public string? ConfigurationVersion { get; private set; }

    /// <summary>When the tenant automation state was last validated.</summary>
    public DateTime? LastValidatedAt { get; private set; }

    /// <summary>Actor who last updated this record.</summary>
    public string? UpdatedBy { get; private set; }

    /// <summary>Optimistic concurrency token.</summary>
    public uint RowVersion { get; private set; }

    public static TenantAutomationState Create(
        Guid tenantId,
        string automationKey,
        bool enabled = false,
        string? updatedBy = null)
    {
        return new TenantAutomationState
        {
            Id              = Guid.CreateVersion7(),
            TenantId        = tenantId,
            AutomationKey   = automationKey,
            Enabled         = enabled,
            UpdatedBy       = updatedBy,
            RowVersion      = 0,
        };
    }

    public void Enable(string? updatedBy = null)
    {
        Enabled   = true;
        UpdatedBy = updatedBy;
        RowVersion++;
    }

    public void Disable(string? updatedBy = null)
    {
        Enabled   = false;
        UpdatedBy = updatedBy;
        RowVersion++;
    }

    public void SetLifecycleOverride(string? override_, string? updatedBy = null)
    {
        LifecycleOverride = override_;
        UpdatedBy         = updatedBy;
        RowVersion++;
    }

    public void Validate()
    {
        LastValidatedAt = DateTime.UtcNow;
        RowVersion++;
    }
}
