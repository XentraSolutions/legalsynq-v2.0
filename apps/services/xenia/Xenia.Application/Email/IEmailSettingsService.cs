using Xenia.Domain.Email;

namespace Xenia.Application.Email;

/// <summary>
/// Application-layer service for managing tenant-scoped Email module settings.
///
/// One settings record per tenant. GetOrCreateAsync is idempotent — returns
/// the current settings or creates a new record with platform defaults if none exists.
/// </summary>
public interface IEmailSettingsService
{
    /// <summary>
    /// Returns the email settings for the tenant, creating defaults if none exist.
    /// Never returns null.
    /// </summary>
    Task<EmailSettingsDto> GetOrCreateAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Updates the email settings for the tenant.
    /// Creates default settings first if none exist.
    /// Returns the updated settings.
    /// </summary>
    Task<EmailSettingsDto> UpdateAsync(
        Guid tenantId,
        Guid? actorId,
        UpdateEmailSettingsRequest request,
        CancellationToken ct = default);
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record EmailSettingsDto
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; init; }
    public required int ConnectionTimeoutSeconds { get; init; }
    public required string AllowedProviderTypes { get; init; }
    public required int ValidationRetryLimit { get; init; }
    public required int ValidationHistoryRetentionDays { get; init; }
    public required string AllowedPorts { get; init; }
    public required bool RequireTls { get; init; }
    public required bool AllowCustomHosts { get; init; }
    public required string SsrfPolicyMode { get; init; }
    public required bool DefaultSourceEnabled { get; init; }
    public required int Version { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }

    public static EmailSettingsDto FromEntity(EmailSettings s) => new()
    {
        Id = s.Id,
        TenantId = s.TenantId,
        ConnectionTimeoutSeconds = s.ConnectionTimeoutSeconds,
        AllowedProviderTypes = s.AllowedProviderTypes,
        ValidationRetryLimit = s.ValidationRetryLimit,
        ValidationHistoryRetentionDays = s.ValidationHistoryRetentionDays,
        AllowedPorts = s.AllowedPorts,
        RequireTls = s.RequireTls,
        AllowCustomHosts = s.AllowCustomHosts,
        SsrfPolicyMode = s.SsrfPolicyMode,
        DefaultSourceEnabled = s.DefaultSourceEnabled,
        Version = s.Version,
        UpdatedAtUtc = s.UpdatedAtUtc,
    };
}

public sealed record UpdateEmailSettingsRequest
{
    public int? ConnectionTimeoutSeconds { get; init; }
    public string? AllowedProviderTypes { get; init; }
    public int? ValidationRetryLimit { get; init; }
    public int? ValidationHistoryRetentionDays { get; init; }
    public string? AllowedPorts { get; init; }
    public bool? RequireTls { get; init; }
    public bool? AllowCustomHosts { get; init; }
    public string? SsrfPolicyMode { get; init; }
    public bool? DefaultSourceEnabled { get; init; }
    public required int ExpectedVersion { get; init; }
}
