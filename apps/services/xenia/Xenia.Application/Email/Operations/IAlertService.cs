using Xenia.Domain.Email;

namespace Xenia.Application.Email.Operations;

/// <summary>
/// Manages the lifecycle of email operational alerts for a tenant.
///
/// Alert creation is deduplicated — opening an alert for a condition already
/// represented by an open alert increments its occurrence count instead of
/// creating a second row.
///
/// Security: all methods require and enforce tenant scope.
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Opens or increments an alert.
    /// If an open alert already exists for the deduplication key, its
    /// occurrence count is incremented and the method returns that alert.
    /// Otherwise a new alert is created.
    /// </summary>
    Task<EmailOperationalAlert> OpenOrIncrementAsync(
        Guid tenantId,
        EmailAlertType alertType,
        EmailAlertSeverity severity,
        string title,
        string safeDescription,
        Guid? emailSourceId = null,
        EmailProviderType? providerType = null,
        string? correlationId = null,
        CancellationToken ct = default);

    /// <summary>Retrieves a paginated list of alerts for the tenant.</summary>
    Task<AlertPageResult> ListAsync(AlertListQuery query, CancellationToken ct = default);

    /// <summary>Returns a single alert or null if not found / cross-tenant.</summary>
    Task<EmailOperationalAlert?> GetAsync(Guid tenantId, Guid alertId, CancellationToken ct = default);

    /// <summary>Acknowledges an open or suppressed alert. Returns false if not found.</summary>
    Task<bool> AcknowledgeAsync(Guid tenantId, Guid alertId, Guid actorId, CancellationToken ct = default);

    /// <summary>Resolves an alert. Returns false if not found.</summary>
    Task<bool> ResolveAsync(Guid tenantId, Guid alertId, Guid actorId, string? reason, CancellationToken ct = default);

    /// <summary>Suppresses an alert until the given UTC time.</summary>
    Task<bool> SuppressAsync(Guid tenantId, Guid alertId, Guid actorId, DateTime suppressedUntil, CancellationToken ct = default);

    /// <summary>Auto-resolves alerts whose condition has cleared.</summary>
    Task AutoResolveAsync(Guid tenantId, EmailAlertType alertType, Guid? emailSourceId, string reason, CancellationToken ct = default);
}

public sealed record AlertListQuery(
    Guid TenantId,
    EmailAlertStatus? Status = null,
    EmailAlertSeverity? Severity = null,
    EmailAlertType? AlertType = null,
    Guid? EmailSourceId = null,
    int Page = 1,
    int PageSize = 50);

public sealed record AlertPageResult(
    IReadOnlyList<EmailOperationalAlert> Items,
    int TotalCount,
    int Page,
    int PageSize);
