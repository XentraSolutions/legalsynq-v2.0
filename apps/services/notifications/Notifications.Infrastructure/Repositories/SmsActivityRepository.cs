using Microsoft.EntityFrameworkCore;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

/// <summary>
/// LS-NOTIF-SMS-006: Bounded EF Core queries for SMS activity logs.
///
/// All queries:
///  - Filter by Channel = "sms" as the primary predicate.
///  - Join ntf_NotificationAttempts with ntf_Notifications (left join)
///    to include RecipientJson for phone masking at the service layer.
///  - Apply optional tenant/provider/status/date filters.
///  - Never project CredentialsJson, SettingsJson, or any provider secret.
/// </summary>
public sealed class SmsActivityRepository : ISmsActivityRepository
{
    private readonly NotificationsDbContext _db;

    public SmsActivityRepository(NotificationsDbContext db) => _db = db;

    public async Task<(List<SmsActivityRawRecord> Items, int Total)> QueryAsync(
        SmsActivityQuery query,
        CancellationToken ct = default)
    {
        var q = BuildBaseQuery(query);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(x => x.a.CreatedAt)
            .Skip(query.Offset)
            .Take(query.Limit)
            .Select(x => new SmsActivityRawRecord(
                x.a.Id,
                x.a.NotificationId,
                x.a.TenantId,
                x.a.Provider,
                x.a.ProviderConfigId,
                x.a.ProviderOwnershipMode,
                x.a.ProviderMessageId,
                x.a.Status,
                x.a.FailureCategory,
                x.a.ErrorMessage,
                x.a.IsFailover,
                x.a.AttemptNumber,
                x.a.CompletedAt,
                x.a.CreatedAt,
                x.a.UpdatedAt,
                x.RecipientJson))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<SmsActivitySummaryDto> SummarizeAsync(
        SmsActivityQuery query,
        CancellationToken ct = default)
    {
        var q = BuildBaseQuery(query).Select(x => x.a);

        var rows = await q
            .Select(a => new
            {
                a.Status,
                a.ProviderOwnershipMode,
                a.CreatedAt,
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            return new SmsActivitySummaryDto { Total = 0 };
        }

        // Status buckets
        int sent = 0, delivered = 0, failed = 0, deadLetter = 0, inProgress = 0;
        int tenantOwned = 0, platformOwned = 0, unknownAttr = 0;

        foreach (var r in rows)
        {
            switch (r.Status)
            {
                case "sent":       sent++;       break;
                case "delivered":  delivered++;  break;
                case "failed":     failed++;     break;
                case "dead_letter": deadLetter++; break;
                default:           inProgress++; break; // pending/sending/queued/processing/retrying
            }

            switch (r.ProviderOwnershipMode)
            {
                case "tenant":   tenantOwned++;   break;
                case "platform": platformOwned++; break;
                default:         unknownAttr++;   break;
            }
        }

        return new SmsActivitySummaryDto
        {
            Total               = rows.Count,
            Sent                = sent,
            Delivered           = delivered,
            Failed              = failed,
            DeadLetter          = deadLetter,
            InProgress          = inProgress,
            TenantOwned         = tenantOwned,
            PlatformOwned       = platformOwned,
            UnknownAttribution  = unknownAttr,
            EarliestAt          = rows.Min(r => r.CreatedAt),
            LatestAt            = rows.Max(r => r.CreatedAt),
        };
    }

    // ── Query builder ─────────────────────────────────────────────────────────

    private IQueryable<(Domain.NotificationAttempt a, string? RecipientJson)> BuildBaseQuery(SmsActivityQuery query)
    {
        // Left join NotificationAttempts ← Notifications to get RecipientJson
        var joined =
            from a in _db.NotificationAttempts
            join n in _db.Notifications on a.NotificationId equals n.Id into nJoin
            from n in nJoin.DefaultIfEmpty()
            select new { a, RecipientJson = n != null ? n.RecipientJson : null };

        // Always filter by channel
        var q = joined.Where(x => x.a.Channel == "sms");

        // ── Tenant / platform scope ───────────────────────────────────────────
        if (query.TenantId.HasValue)
        {
            var tid = query.TenantId.Value;
            q = q.Where(x => x.a.TenantId == tid);
        }

        if (!query.IncludePlatformActivity)
        {
            // Exclude explicitly platform-owned attempts
            q = q.Where(x => x.a.ProviderOwnershipMode != "platform");
        }

        // ── Provider filters ──────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(query.Provider))
        {
            var prov = query.Provider.Trim().ToLowerInvariant();
            q = q.Where(x => x.a.Provider == prov);
        }

        if (query.ProviderConfigId.HasValue)
        {
            var pcid = query.ProviderConfigId.Value;
            q = q.Where(x => x.a.ProviderConfigId == pcid);
        }

        if (!string.IsNullOrWhiteSpace(query.ProviderOwnershipMode))
        {
            var mode = query.ProviderOwnershipMode.Trim().ToLowerInvariant();
            q = q.Where(x => x.a.ProviderOwnershipMode == mode);
        }

        if (!string.IsNullOrWhiteSpace(query.ProviderMessageId))
        {
            var sid = query.ProviderMessageId.Trim();
            q = q.Where(x => x.a.ProviderMessageId == sid);
        }

        // ── Status / failure filters ──────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToLowerInvariant();
            q = q.Where(x => x.a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.FailureCategory))
        {
            var cat = query.FailureCategory.Trim().ToLowerInvariant();
            q = q.Where(x => x.a.FailureCategory == cat);
        }

        // ── Date range ────────────────────────────────────────────────────────
        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value.ToUniversalTime();
            q = q.Where(x => x.a.CreatedAt >= from);
        }

        if (query.ToDate.HasValue)
        {
            var to = query.ToDate.Value.ToUniversalTime();
            q = q.Where(x => x.a.CreatedAt <= to);
        }

        return q.Select(x => new ValueTuple<Domain.NotificationAttempt, string?>(x.a, x.RecipientJson));
    }
}
