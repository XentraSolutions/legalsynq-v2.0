using Xenia.Domain.Common;

namespace Xenia.Domain.Automation;

/// <summary>
/// Durable registry entry for a discovered automation provider.
///
/// Records the current lifecycle state, version, and reconciliation metadata for
/// an automation key. This is the mutable platform-level record; provider manifests
/// remain code-defined and immutable.
///
/// Rules:
/// - No secret values or credentials.
/// - No arbitrary executable assembly paths.
/// - No tenant-specific state — use <see cref="TenantAutomationState"/> for that.
/// </summary>
public sealed class AutomationRegistration : AuditableEntityBase
{
    public const int AutomationKeyMaxLength = 200;
    public const int ProviderMaxLength      = 200;
    public const int CategoryMaxLength      = 100;
    public const int VersionMaxLength       = 50;
    public const int StatusMaxLength        = 50;
    public const int ManifestHashMaxLength  = 64;
    public const int PlatformVersionMaxLength = 50;

    private AutomationRegistration() { }

    public Guid Id { get; private set; }

    /// <summary>Stable unique key for this automation (e.g. "email.sync").</summary>
    public string AutomationKey { get; private set; } = string.Empty;

    /// <summary>Provider class name or canonical identifier.</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>High-level category (e.g. "Email", "Document").</summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>Current active version string.</summary>
    public string CurrentVersion { get; private set; } = string.Empty;

    /// <summary>Current lifecycle status of this automation at the platform level.</summary>
    public AutomationLifecycleState LifecycleStatus { get; private set; }

    /// <summary>Whether the automation is globally enabled on the platform.</summary>
    public bool GloballyEnabled { get; private set; }

    /// <summary>SHA-256 hash of the canonical manifest JSON for change detection.</summary>
    public string ManifestHash { get; private set; } = string.Empty;

    /// <summary>Minimum platform version required to run this automation.</summary>
    public string? MinimumPlatformVersion { get; private set; }

    /// <summary>When this automation was first registered in the durable store.</summary>
    public DateTime RegisteredAt { get; private set; }

    /// <summary>When the last startup reconciliation touched this record.</summary>
    public DateTime? LastReconciledAt { get; private set; }

    /// <summary>Optimistic concurrency token — incremented on every update.</summary>
    public uint RowVersion { get; private set; }

    public static AutomationRegistration Create(
        string automationKey,
        string provider,
        string category,
        string currentVersion,
        string manifestHash,
        string? minimumPlatformVersion = null)
    {
        return new AutomationRegistration
        {
            Id                     = Guid.CreateVersion7(),
            AutomationKey          = automationKey,
            Provider               = provider,
            Category               = category,
            CurrentVersion         = currentVersion,
            LifecycleStatus        = AutomationLifecycleState.Registered,
            GloballyEnabled        = false,
            ManifestHash           = manifestHash,
            MinimumPlatformVersion = minimumPlatformVersion,
            RegisteredAt           = DateTime.UtcNow,
            LastReconciledAt       = DateTime.UtcNow,
            RowVersion             = 0,
        };
    }

    public void Reconcile(string currentVersion, string manifestHash, DateTime reconciledAt)
    {
        CurrentVersion     = currentVersion;
        ManifestHash       = manifestHash;
        LastReconciledAt   = reconciledAt;
        RowVersion++;
    }

    public void MarkUnavailable()
    {
        LifecycleStatus = AutomationLifecycleState.Unavailable;
        RowVersion++;
    }

    public void Enable()
    {
        GloballyEnabled = true;
        if (LifecycleStatus == AutomationLifecycleState.Disabled)
            LifecycleStatus = AutomationLifecycleState.Registered;
        RowVersion++;
    }

    public void Disable()
    {
        GloballyEnabled = false;
        LifecycleStatus = AutomationLifecycleState.Disabled;
        RowVersion++;
    }

    public void Retire()
    {
        LifecycleStatus = AutomationLifecycleState.Retired;
        GloballyEnabled = false;
        RowVersion++;
    }

    public void SetLifecycle(AutomationLifecycleState state)
    {
        LifecycleStatus = state;
        RowVersion++;
    }
}
