namespace Tenant.Application;

/// <summary>
/// Well-known default values for tenant provisioning.
/// Centralizes constants referenced across the Application and Api layers.
/// </summary>
public static class TenantDefaults
{
    /// <summary>
    /// IANA timezone applied when a new tenant is provisioned without an explicit timezone.
    /// Also used as the fallback value when <see cref="Domain.Tenant.TimeZone"/> is null.
    /// </summary>
    public const string Timezone = "America/Los_Angeles";

    /// <summary>
    /// Canonical tenant-setting key for timezone preferences.
    /// Kept in sync with <see cref="Domain.Tenant.TimeZone"/> for compatibility with
    /// settings-oriented read paths.
    /// </summary>
    public const string TimezoneSettingKey = "portal.timezone.default";

    /// <summary>
    /// Legacy tenant-setting key used by older admin aggregation code.
    /// Read as a fallback and updated only when an existing record is present.
    /// </summary>
    public const string LegacyTimezoneSettingKey = "timezone";
}
