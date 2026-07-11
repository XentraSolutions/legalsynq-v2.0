using Xenia.Domain.Common;

namespace Xenia.Domain.Automation;

/// <summary>
/// Durable record of a specific version of an automation provider manifest.
///
/// Immutable fields: AutomationKey, Version, ManifestJson, RegisteredAt.
/// Mutable fields: Status (activation/retirement), ActivatedAt, RetiredAt.
///
/// Rules:
/// - ManifestJson must not contain credentials or secret values.
/// - Size is bounded to 65536 characters.
/// - Schema version is required for future validation.
/// </summary>
public sealed class AutomationVersionRecord : AuditableEntityBase
{
    public const int AutomationKeyMaxLength       = 200;
    public const int VersionMaxLength             = 50;
    public const int ManifestSchemaVersionMaxLength = 50;
    public const int StatusMaxLength              = 50;
    public const int ManifestJsonMaxLength        = 65536;

    private AutomationVersionRecord() { }

    public Guid Id { get; private set; }

    /// <summary>Automation key this version belongs to.</summary>
    public string AutomationKey { get; private set; } = string.Empty;

    /// <summary>Semantic version string (e.g. "1.0.0").</summary>
    public string Version { get; private set; } = string.Empty;

    /// <summary>Serialized manifest JSON snapshot. No credentials. Max 64 KB.</summary>
    public string ManifestJson { get; private set; } = string.Empty;

    /// <summary>Schema version of the manifest format.</summary>
    public string ManifestSchemaVersion { get; private set; } = string.Empty;

    /// <summary>Serialized compatibility constraints JSON.</summary>
    public string? CompatibilityJson { get; private set; }

    public DateTime RegisteredAt { get; private set; }
    public DateTime? ActivatedAt { get; private set; }
    public DateTime? RetiredAt { get; private set; }

    public AutomationVersionStatus Status { get; private set; }

    /// <summary>Optimistic concurrency token.</summary>
    public uint RowVersion { get; private set; }

    public static AutomationVersionRecord Create(
        string automationKey,
        string version,
        string manifestJson,
        string manifestSchemaVersion,
        string? compatibilityJson = null)
    {
        return new AutomationVersionRecord
        {
            Id                    = Guid.CreateVersion7(),
            AutomationKey         = automationKey,
            Version               = version,
            ManifestJson          = manifestJson,
            ManifestSchemaVersion = manifestSchemaVersion,
            CompatibilityJson     = compatibilityJson,
            RegisteredAt          = DateTime.UtcNow,
            Status                = AutomationVersionStatus.Registered,
            RowVersion            = 0,
        };
    }

    public void Activate()
    {
        Status      = AutomationVersionStatus.Active;
        ActivatedAt = DateTime.UtcNow;
        RowVersion++;
    }

    public void Retire()
    {
        Status    = AutomationVersionStatus.Retired;
        RetiredAt = DateTime.UtcNow;
        RowVersion++;
    }
}
