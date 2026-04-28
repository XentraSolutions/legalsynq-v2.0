using MySqlConnector;

namespace Support.Api.Notifications;

public interface IPlatformSettingStore
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// Reads platform-wide settings from the <c>platform_settings</c> table in the
/// Tenant DB.  The table is created lazily (CREATE TABLE IF NOT EXISTS) on first
/// use so no migration is needed.
///
/// Values are cached in-process for <see cref="CacheTtl"/> to avoid a DB round-
/// trip on every notification.  The cache is invalidated automatically at TTL
/// expiry, so changes made via the Control Center take effect within minutes.
///
/// Failures are swallowed so a misconfigured or temporarily unavailable DB never
/// blocks notification dispatch.
/// </summary>
public sealed class TenantDbPlatformSettingStore : IPlatformSettingStore
{
    public static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly string? _cs;
    private readonly ILogger<TenantDbPlatformSettingStore> _log;

    private readonly Dictionary<string, (string? Value, DateTime Expires)>
        _cache = new(StringComparer.OrdinalIgnoreCase);

    private bool _tableEnsured;
    private readonly SemaphoreSlim _ensureLock = new(1, 1);

    public TenantDbPlatformSettingStore(IConfiguration config, ILogger<TenantDbPlatformSettingStore> log)
    {
        _cs  = config.GetConnectionString("TenantDb");
        _log = log;

        if (string.IsNullOrWhiteSpace(_cs))
            _log.LogWarning(
                "ConnectionStrings:TenantDb is not configured — platform setting store is disabled");
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_cs)) return null;

        if (_cache.TryGetValue(key, out var hit) && hit.Expires > DateTime.UtcNow)
            return hit.Value;

        try
        {
            await EnsureTableAsync(ct);

            await using var conn = new MySqlConnection(_cs);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT `setting_value` FROM `platform_settings` WHERE `setting_key` = @k LIMIT 1";
            cmd.Parameters.AddWithValue("@k", key);

            var raw   = await cmd.ExecuteScalarAsync(ct);
            var value = raw as string;

            _cache[key] = (value, DateTime.UtcNow.Add(CacheTtl));
            return value;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to read platform setting key={Key}", key);
            return null;
        }
    }

    private async Task EnsureTableAsync(CancellationToken ct)
    {
        if (_tableEnsured) return;

        await _ensureLock.WaitAsync(ct);
        try
        {
            if (_tableEnsured) return;

            await using var conn = new MySqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS `platform_settings` (
                    `setting_key`   VARCHAR(200) NOT NULL,
                    `setting_value` TEXT         NOT NULL DEFAULT '',
                    `updated_at`    DATETIME(3)  NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
                    PRIMARY KEY (`setting_key`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
            await cmd.ExecuteNonQueryAsync(ct);
            _tableEnsured = true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to create platform_settings table; will retry on next request");
        }
        finally
        {
            _ensureLock.Release();
        }
    }
}
