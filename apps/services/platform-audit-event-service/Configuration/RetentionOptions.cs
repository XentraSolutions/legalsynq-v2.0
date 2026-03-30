namespace PlatformAuditEventService.Configuration;

/// <summary>
/// Audit event retention policy options.
/// Bound from "Retention" section in appsettings.
/// Environment variable override prefix: Retention__
/// </summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>
    /// Default number of days to retain audit events.
    /// 0 = retain indefinitely (recommended for compliance audit trails).
    /// </summary>
    public int DefaultRetentionDays { get; set; } = 0;

    /// <summary>
    /// Per-category retention overrides.
    /// Key = category name (e.g. "security"), Value = retention days (0 = indefinite).
    /// Example: { "system": 90, "debug": 30 }
    /// </summary>
    public Dictionary<string, int> CategoryOverrides { get; set; } = new();

    /// <summary>
    /// Per-tenant retention overrides.
    /// Key = tenantId, Value = retention days.
    /// Tenant-specific agreements may require longer or shorter windows.
    /// </summary>
    public Dictionary<string, int> TenantOverrides { get; set; } = new();

    /// <summary>
    /// When true, the RetentionPolicyJob is enabled and runs on schedule.
    /// </summary>
    public bool JobEnabled { get; set; } = false;

    /// <summary>
    /// Cron expression for the retention job schedule.
    /// Default: daily at 02:00 UTC.
    /// </summary>
    public string JobCronUtc { get; set; } = "0 2 * * *";

    /// <summary>
    /// Maximum number of records deleted per retention job run.
    /// Guards against large single-batch deletes that lock the table.
    /// </summary>
    public int MaxDeletesPerRun { get; set; } = 10_000;

    /// <summary>
    /// When true, expired records are archived before deletion.
    /// Requires ExportOptions:Provider to be configured.
    /// </summary>
    public bool ArchiveBeforeDelete { get; set; } = false;
}
