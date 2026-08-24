using Xenia.Domain.Common;

namespace Xenia.Domain.Modules;

/// <summary>
/// Represents a registered Xenia automation module.
/// Modules are the extensibility unit of the Xenia platform. Each module
/// encapsulates a specific automation capability (e.g. Email, SMS).
///
/// This entity tracks the global module definition. Per-tenant enablement
/// is managed separately via <see cref="XeniaTenantModule"/>.
/// </summary>
public sealed class XeniaModule : AuditableEntityBase
{
    public const int KeyMaxLength = 100;
    public const int NameMaxLength = 200;
    public const int VersionMaxLength = 50;
    public const int DescriptionMaxLength = 1000;
    public const int NamespaceMaxLength = 200;

    public Guid Id { get; private set; }

    /// <summary>
    /// Unique, stable, human-readable key for this module.
    /// Example: <c>xenia.email</c>, <c>xenia.sms</c>.
    /// </summary>
    public string ModuleKey { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;
    public string Version { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    /// <summary>Whether this module is enabled globally (platform-level switch).</summary>
    public bool GlobalEnabled { get; private set; }

    /// <summary>Last known runtime status of this module.</summary>
    public ModuleStatus Status { get; private set; }

    /// <summary>
    /// Configuration namespace under which this module reads its settings.
    /// Example: <c>xenia.email</c>.
    /// </summary>
    public string ConfigurationNamespace { get; private set; } = string.Empty;

    /// <summary>EF Core constructor. Do not use from application code.</summary>
    private XeniaModule() { }

    public XeniaModule(
        Guid id,
        string moduleKey,
        string name,
        string version,
        string description,
        string configurationNamespace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        Id = id;
        ModuleKey = moduleKey.Trim();
        Name = name.Trim();
        Version = version.Trim();
        Description = description?.Trim() ?? string.Empty;
        ConfigurationNamespace = configurationNamespace?.Trim() ?? moduleKey.Trim();
        GlobalEnabled = false;
        Status = ModuleStatus.Unknown;
    }

    public void Enable() => GlobalEnabled = true;
    public void Disable() => GlobalEnabled = false;

    public void UpdateStatus(ModuleStatus status) => Status = status;
}
