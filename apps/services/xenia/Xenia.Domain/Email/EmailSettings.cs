using Xenia.Domain.Common;

namespace Xenia.Domain.Email;

/// <summary>
/// Tenant-scoped Email module configuration.
///
/// One record per tenant. Created on first access with platform defaults.
/// Never contains credentials — all settings are operational parameters only.
/// </summary>
public sealed class EmailSettings : AuditableEntityBase
{
    public const int AllowedProviderTypesMaxLength = 500;
    public const int AllowedPortsMaxLength = 200;
    public const int SsrfPolicyModeMaxLength = 50;

    public const int DefaultConnectionTimeoutSeconds = 30;
    public const int DefaultValidationRetryLimit = 2;
    public const int DefaultValidationHistoryRetentionDays = 90;
    public const string DefaultSsrfPolicyMode = "Strict";
    public const string DefaultAllowedPorts = "993,995,443";
    public const string DefaultAllowedProviderTypes = "M365,GoogleWorkspace,Imap,Pop3,ExchangeImap";

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Connection timeout for validation probes, in seconds. Range: 5–120.</summary>
    public int ConnectionTimeoutSeconds { get; private set; } = DefaultConnectionTimeoutSeconds;

    /// <summary>
    /// CSV list of allowed provider type names. Empty = all providers allowed.
    /// Example: "M365,GoogleWorkspace"
    /// </summary>
    public string AllowedProviderTypes { get; private set; } = DefaultAllowedProviderTypes;

    /// <summary>Maximum number of validation retries before recording failure. Range: 0–5.</summary>
    public int ValidationRetryLimit { get; private set; } = DefaultValidationRetryLimit;

    /// <summary>How many days to retain validation history per source. Range: 1–365.</summary>
    public int ValidationHistoryRetentionDays { get; private set; } = DefaultValidationHistoryRetentionDays;

    /// <summary>CSV list of allowed TCP ports for protocol-based sources. Example: "993,995".</summary>
    public string AllowedPorts { get; private set; } = DefaultAllowedPorts;

    /// <summary>Whether TLS is required for all sources. Default: true. Cannot be false in Strict mode.</summary>
    public bool RequireTls { get; private set; } = true;

    /// <summary>
    /// Whether tenants may configure sources with custom hosts.
    /// False = only known provider endpoints are permitted.
    /// </summary>
    public bool AllowCustomHosts { get; private set; } = false;

    /// <summary>SSRF policy: "Strict" (default) or "Permissive" (not available in production).</summary>
    public string SsrfPolicyMode { get; private set; } = DefaultSsrfPolicyMode;

    /// <summary>Whether new sources are enabled by default when created.</summary>
    public bool DefaultSourceEnabled { get; private set; } = false;

    /// <summary>Optimistic concurrency token.</summary>
    public int Version { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    private EmailSettings() { }

    public static EmailSettings CreateDefault(Guid tenantId) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
        };

    public void Update(
        int? connectionTimeoutSeconds,
        string? allowedProviderTypes,
        int? validationRetryLimit,
        int? validationHistoryRetentionDays,
        string? allowedPorts,
        bool? requireTls,
        bool? allowCustomHosts,
        string? ssrfPolicyMode,
        bool? defaultSourceEnabled,
        int expectedVersion,
        Guid? actorId)
    {
        if (Version != expectedVersion)
            throw new InvalidOperationException(
                $"Concurrency conflict: expected version {expectedVersion}, actual {Version}.");

        if (connectionTimeoutSeconds.HasValue)
        {
            if (connectionTimeoutSeconds < 5 || connectionTimeoutSeconds > 120)
                throw new ArgumentOutOfRangeException(nameof(connectionTimeoutSeconds), "Must be 5–120.");
            ConnectionTimeoutSeconds = connectionTimeoutSeconds.Value;
        }

        if (allowedProviderTypes is not null)
            AllowedProviderTypes = allowedProviderTypes.Length > AllowedProviderTypesMaxLength
                ? allowedProviderTypes[..AllowedProviderTypesMaxLength] : allowedProviderTypes;

        if (validationRetryLimit.HasValue)
        {
            if (validationRetryLimit < 0 || validationRetryLimit > 5)
                throw new ArgumentOutOfRangeException(nameof(validationRetryLimit), "Must be 0–5.");
            ValidationRetryLimit = validationRetryLimit.Value;
        }

        if (validationHistoryRetentionDays.HasValue)
        {
            if (validationHistoryRetentionDays < 1 || validationHistoryRetentionDays > 365)
                throw new ArgumentOutOfRangeException(nameof(validationHistoryRetentionDays), "Must be 1–365.");
            ValidationHistoryRetentionDays = validationHistoryRetentionDays.Value;
        }

        if (allowedPorts is not null)
        {
            ValidateAllowedPorts(allowedPorts);
            AllowedPorts = allowedPorts.Length > AllowedPortsMaxLength
                ? allowedPorts[..AllowedPortsMaxLength] : allowedPorts;
        }

        if (requireTls.HasValue)
            RequireTls = requireTls.Value;

        if (allowCustomHosts.HasValue)
            AllowCustomHosts = allowCustomHosts.Value;

        if (ssrfPolicyMode is not null)
        {
            if (ssrfPolicyMode != "Strict" && ssrfPolicyMode != "Permissive")
                throw new ArgumentException("SsrfPolicyMode must be 'Strict' or 'Permissive'.");
            SsrfPolicyMode = ssrfPolicyMode;
        }

        if (defaultSourceEnabled.HasValue)
            DefaultSourceEnabled = defaultSourceEnabled.Value;

        UpdatedBy = actorId;
        Version++;
    }

    public IReadOnlyList<int> GetAllowedPortsList()
    {
        if (string.IsNullOrWhiteSpace(AllowedPorts)) return [993, 995, 443];
        return AllowedPorts
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => int.TryParse(p, out var n) ? n : -1)
            .Where(n => n > 0)
            .ToList();
    }

    private static void ValidateAllowedPorts(string ports)
    {
        foreach (var part in ports.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var port) || port <= 0 || port > 65535)
                throw new ArgumentException($"Invalid port value: '{part}'. Must be 1–65535.");
        }
    }
}
