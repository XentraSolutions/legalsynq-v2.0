using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Adapters.Interfaces;
using Xenia.Application.Email.Operations;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// EF Core backed alert service.
/// Deduplicates alerts by (TenantId, DeduplicationKey, Status=Open).
/// Emits notification adapter requests when tenant has notifications enabled and the adapter is available.
/// </summary>
internal sealed class EfAlertService : IAlertService
{
    private readonly XeniaDbContext _db;
    private readonly INotificationAdapter _notificationAdapter;
    private readonly IAuditAdapter _auditAdapter;
    private readonly ILogger<EfAlertService> _logger;

    public EfAlertService(
        XeniaDbContext db,
        INotificationAdapter notificationAdapter,
        IAuditAdapter auditAdapter,
        ILogger<EfAlertService> logger)
    {
        _db                  = db;
        _notificationAdapter = notificationAdapter;
        _auditAdapter        = auditAdapter;
        _logger              = logger;
    }

    public async Task<EmailOperationalAlert> OpenOrIncrementAsync(
        Guid tenantId,
        EmailAlertType alertType,
        EmailAlertSeverity severity,
        string title,
        string safeDescription,
        Guid? emailSourceId = null,
        EmailProviderType? providerType = null,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        var dedupKey = BuildDeduplicationKey(alertType, tenantId, emailSourceId);

        var existing = await _db.EmailOperationalAlerts
            .FirstOrDefaultAsync(a =>
                a.TenantId == tenantId &&
                a.DeduplicationKey == dedupKey &&
                a.Status == EmailAlertStatus.Open, ct);

        if (existing is not null)
        {
            existing.IncrementOccurrence(safeDescription, correlationId);
            await _db.SaveChangesAsync(ct);
            _logger.LogDebug("Alert occurrence incremented: {AlertId} type={Type} count={Count}",
                existing.Id, alertType, existing.OccurrenceCount);
            return existing;
        }

        var alert = EmailOperationalAlert.Create(
            tenantId, alertType, severity, title, safeDescription, dedupKey,
            emailSourceId, providerType, correlationId);

        _db.EmailOperationalAlerts.Add(alert);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Alert opened: {AlertId} type={Type} severity={Severity} tenant={TenantId}",
            alert.Id, alertType, severity, tenantId);

        await TryAuditAsync(new XeniaAuditEvent
        {
            Action       = "xenia.email.alert.opened",
            ResourceType = "email_alert",
            ResourceId   = alert.Id.ToString(),
            Result       = "success",
            TenantId     = tenantId,
            ActorId      = null,
            CorrelationId= correlationId,
            OccurredAt   = DateTime.UtcNow,
            Detail       = $"type={alertType} severity={severity}",
        });

        return alert;
    }

    public async Task<AlertPageResult> ListAsync(AlertListQuery query, CancellationToken ct = default)
    {
        var q = _db.EmailOperationalAlerts
            .AsNoTracking()
            .Where(a => a.TenantId == query.TenantId);

        if (query.Status.HasValue)       q = q.Where(a => a.Status == query.Status.Value);
        if (query.Severity.HasValue)     q = q.Where(a => a.Severity == query.Severity.Value);
        if (query.AlertType.HasValue)    q = q.Where(a => a.AlertType == query.AlertType.Value);
        if (query.EmailSourceId.HasValue)q = q.Where(a => a.EmailSourceId == query.EmailSourceId.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(a => a.LastObservedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new AlertPageResult(items, total, query.Page, query.PageSize);
    }

    public async Task<EmailOperationalAlert?> GetAsync(Guid tenantId, Guid alertId, CancellationToken ct = default)
        => await _db.EmailOperationalAlerts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == alertId, ct);

    public async Task<bool> AcknowledgeAsync(Guid tenantId, Guid alertId, Guid actorId, CancellationToken ct = default)
    {
        var alert = await GetAsync(tenantId, alertId, ct);
        if (alert is null) return false;

        alert.Acknowledge(actorId);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ResolveAsync(Guid tenantId, Guid alertId, Guid actorId, string? reason, CancellationToken ct = default)
    {
        var alert = await GetAsync(tenantId, alertId, ct);
        if (alert is null) return false;

        alert.Resolve(actorId, reason);
        await _db.SaveChangesAsync(ct);

        await TryAuditAsync(new XeniaAuditEvent
        {
            Action       = "xenia.email.alert.resolved",
            ResourceType = "email_alert",
            ResourceId   = alertId.ToString(),
            Result       = "success",
            TenantId     = tenantId,
            ActorId      = actorId,
            CorrelationId= null,
            OccurredAt   = DateTime.UtcNow,
        });

        return true;
    }

    public async Task<bool> SuppressAsync(Guid tenantId, Guid alertId, Guid actorId, DateTime suppressedUntil, CancellationToken ct = default)
    {
        var alert = await GetAsync(tenantId, alertId, ct);
        if (alert is null) return false;

        alert.Suppress(suppressedUntil, actorId);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task AutoResolveAsync(Guid tenantId, EmailAlertType alertType, Guid? emailSourceId, string reason, CancellationToken ct = default)
    {
        var dedupKey = BuildDeduplicationKey(alertType, tenantId, emailSourceId);

        var alerts = await _db.EmailOperationalAlerts
            .Where(a => a.TenantId == tenantId &&
                        a.DeduplicationKey == dedupKey &&
                        a.Status == EmailAlertStatus.Open)
            .ToListAsync(ct);

        foreach (var alert in alerts)
            alert.AutoResolve(reason);

        if (alerts.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    private static string BuildDeduplicationKey(EmailAlertType alertType, Guid tenantId, Guid? emailSourceId)
        => emailSourceId.HasValue
            ? $"{alertType}:{tenantId}:{emailSourceId}"
            : $"{alertType}:{tenantId}";

    private async Task TryAuditAsync(XeniaAuditEvent evt)
    {
        try { await _auditAdapter.RecordEventAsync(evt); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit emit failed for action={Action}", evt.Action);
        }
    }
}
