// LSCC-01-004: Admin Queue & Operational Visibility
//
// Three read-only admin endpoints that surface operational health data:
//
//   GET /api/admin/dashboard               — aggregate metrics (counts, trends)
//   GET /api/admin/providers/blocked       — paged blocked-access log, grouped per user
//   GET /api/admin/referrals               — referral monitor (platform-wide for PlatformAdmin,
//                                            tenant-scoped for TenantAdmin)
//
// All endpoints require PlatformOrTenantAdmin.  They query CareConnectDbContext
// directly (no application-layer service needed — queries are purely read-only
// projections with no domain behaviour).
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Api.Endpoints;

/// <summary>
/// LSCC-01-004: Admin dashboard, blocked-provider queue, and referral-monitor endpoints.
/// </summary>
public static class AdminDashboardEndpoints
{
    public static IEndpointRouteBuilder MapAdminDashboardEndpoints(
        this IEndpointRouteBuilder routes)
    {
        routes
            .MapGet("/api/admin/dashboard", GetDashboardAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        routes
            .MapGet("/api/admin/providers/blocked", GetBlockedProvidersAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        routes
            .MapGet("/api/admin/referrals", GetAdminReferralsAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        return routes;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/admin/dashboard
    // ──────────────────────────────────────────────────────────────────────────
    // Returns aggregate counts over rolling 24-hour and 7-day windows:
    //   - referralCountToday / referralCountLast7Days
    //   - blockedAccessCountToday / blockedAccessCountLast7Days
    //   - distinctBlockedUsersToday
    //   - openReferrals (status ∈ {New, Accepted, InProgress})
    //
    // No tenant filter — PlatformAdmin sees the full platform; TenantAdmin sees
    // the same aggregates (cross-tenant read is implicit for admins in CC).
    private static async Task<IResult> GetDashboardAsync(
        CareConnectDbContext db,
        CancellationToken    ct)
    {
        var now    = DateTime.UtcNow;
        var today  = now.AddHours(-24);
        var week   = now.AddDays(-7);

        var referralToday   = await db.Referrals.CountAsync(r => r.CreatedAtUtc >= today, ct);
        var referralWeek    = await db.Referrals.CountAsync(r => r.CreatedAtUtc >= week,  ct);
        var openReferrals   = await db.Referrals.CountAsync(
            r => r.Status == "New" || r.Status == "NewOpened" || r.Status == "Accepted" || r.Status == "InProgress", ct);

        var blockedToday    = await db.BlockedProviderAccessLogs.CountAsync(l => l.AttemptedAtUtc >= today, ct);
        var blockedWeek     = await db.BlockedProviderAccessLogs.CountAsync(l => l.AttemptedAtUtc >= week,  ct);
        var blockedUsersToday = await db.BlockedProviderAccessLogs
            .Where(l => l.AttemptedAtUtc >= today && l.UserId != null)
            .Select(l => l.UserId)
            .Distinct()
            .CountAsync(ct);

        return Results.Ok(new
        {
            referralCountToday      = referralToday,
            referralCountLast7Days  = referralWeek,
            openReferrals           = openReferrals,
            blockedAccessToday      = blockedToday,
            blockedAccessLast7Days  = blockedWeek,
            distinctBlockedUsersToday = blockedUsersToday,
            generatedAtUtc          = now,
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/admin/providers/blocked
    // ──────────────────────────────────────────────────────────────────────────
    // Returns the most-recent blocked-access log entry per (UserId, FailureReason)
    // pair, with an attempt count over the last 7 days.  Supports pagination via
    // ?page=1&pageSize=25 and an optional ?since=<ISO-datetime> window filter.
    //
    // Remediation link is returned as a relative path so the frontend can
    // construct the full URL without knowing the host:
    //   remediationPath: /careconnect/admin/providers/provisioning?userId=<id>
    private static async Task<IResult> GetBlockedProvidersAsync(
        CareConnectDbContext db,
        [FromQuery] int      page     = 1,
        [FromQuery] int      pageSize = 25,
        [FromQuery] string?  since    = null,
        CancellationToken    ct       = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var window = since is not null && DateTime.TryParse(since, out var parsedSince)
            ? parsedSince.ToUniversalTime()
            : DateTime.UtcNow.AddDays(-7);

        // Group by UserId+FailureReason to produce one row per unique block reason.
        // Take the most recent log row from each group for the display columns.
        var query = db.BlockedProviderAccessLogs
            .Where(l => l.AttemptedAtUtc >= window)
            .GroupBy(l => new { l.UserId, l.FailureReason })
            .Select(g => new
            {
                UserId         = g.Key.UserId,
                FailureReason  = g.Key.FailureReason,
                AttemptCount   = g.Count(),
                LastAttemptUtc = g.Max(l => l.AttemptedAtUtc),
                UserEmail      = g.OrderByDescending(l => l.AttemptedAtUtc)
                                  .Select(l => l.UserEmail)
                                  .FirstOrDefault(),
                OrganizationId = g.OrderByDescending(l => l.AttemptedAtUtc)
                                  .Select(l => l.OrganizationId)
                                  .FirstOrDefault(),
                TenantId       = g.OrderByDescending(l => l.AttemptedAtUtc)
                                  .Select(l => l.TenantId)
                                  .FirstOrDefault(),
            })
            .OrderByDescending(x => x.LastAttemptUtc);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var result = items.Select(x => new
        {
            userId          = x.UserId,
            userEmail       = x.UserEmail,
            organizationId  = x.OrganizationId,
            tenantId        = x.TenantId,
            failureReason   = x.FailureReason,
            attemptCount    = x.AttemptCount,
            lastAttemptUtc  = x.LastAttemptUtc,
            remediationPath = x.UserId is not null
                ? $"/careconnect/admin/providers/provisioning?userId={x.UserId}"
                : null,
        });

        return Results.Ok(new
        {
            items      = result,
            total,
            page,
            pageSize,
            windowFrom = window,
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/admin/referrals
    // ──────────────────────────────────────────────────────────────────────────
    // Referral monitor for admins. Joins to Provider for name.
    // PlatformAdmin: platform-wide view (optional ?tenantId filter).
    // TenantAdmin: restricted to their own tenant only.
    // Supports:
    //   ?page=1&pageSize=25
    //   ?status=New|Accepted|InProgress|Completed|Declined|Cancelled
    //   ?tenantId=<guid>  (PlatformAdmin only; ignored for TenantAdmin)
    //   ?since=<ISO-datetime>
    private static async Task<IResult> GetAdminReferralsAsync(
        CareConnectDbContext    db,
        ICurrentRequestContext  ctx,
        [FromQuery] int      page     = 1,
        [FromQuery] int      pageSize = 25,
        [FromQuery] string?  status   = null,
        [FromQuery] Guid?    tenantId = null,
        [FromQuery] string?  since    = null,
        CancellationToken    ct       = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Referrals
            .Include(r => r.Provider)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        if (ctx.IsPlatformAdmin)
        {
            // PlatformAdmin may optionally narrow to a specific tenant.
            if (tenantId.HasValue)
                query = query.Where(r => r.TenantId == tenantId.Value);
        }
        else
        {
            // TenantAdmin is always scoped to their own tenant — ignore caller-supplied tenantId.
            var callerTenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            query = query.Where(r => r.TenantId == callerTenantId);
        }

        if (since is not null && DateTime.TryParse(since, out var parsedSince))
            query = query.Where(r => r.CreatedAtUtc >= parsedSince.ToUniversalTime());

        query = query.OrderByDescending(r => r.CreatedAtUtc);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                id                      = r.Id,
                tenantId                = r.TenantId,
                status                  = r.Status,
                urgency                 = r.Urgency,
                requestedService        = r.RequestedService,
                providerName            = r.Provider != null ? r.Provider.Name : null,
                providerEmail           = r.Provider != null ? r.Provider.Email : null,
                referringOrganizationId = r.ReferringOrganizationId,
                receivingOrganizationId = r.ReceivingOrganizationId,
                referrerName            = r.ReferrerName,
                referrerEmail           = r.ReferrerEmail,
                createdAtUtc            = r.CreatedAtUtc,
                updatedAtUtc            = r.UpdatedAtUtc,
            })
            .ToListAsync(ct);

        return Results.Ok(new
        {
            items,
            total,
            page,
            pageSize,
        });
    }
}
