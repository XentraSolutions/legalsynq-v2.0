// LSCC-002-01: Admin appointment org-ID backfill endpoint.
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Api.Endpoints;

/// <summary>
/// Admin-only data backfill endpoints.
///
/// POST /api/admin/appointments/backfill-org-ids
///
/// Finds legacy appointments where ReferringOrganizationId or ReceivingOrganizationId
/// is NULL, then copies both values from the parent Referral (if the Referral has them set).
///
/// The operation is:
///   - Tenant-scoped: only touches appointments in the requesting admin's tenant.
///   - Idempotent:    appointments already having both org IDs are never touched.
///   - Explicit:      org IDs are ONLY taken from the parent Referral — never guessed.
///   - Safe:          appointments whose parent Referral also has NULL org IDs are counted
///                    as "unresolved" and left unchanged.
///
/// Response (always 200):
///   updated    — appointments that had at least one NULL org ID and were backfilled.
///   skipped    — appointments whose parent Referral also had NULL org IDs (no action possible).
///   alreadySet — appointments already having both org IDs (not touched).
///
/// Authorization: PlatformOrTenantAdmin.
/// </summary>
// LSCC-002-01: POST /api/admin/appointments/backfill-org-ids
public static class AdminBackfillEndpoints
{
    public static IEndpointRouteBuilder MapAdminBackfillEndpoints(
        this IEndpointRouteBuilder routes)
    {
        routes
            .MapPost("/api/admin/appointments/backfill-org-ids", BackfillAppointmentOrgIdsAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        return routes;
    }

    // LSCC-002-01: Walk legacy appointments with missing org IDs; copy from parent Referral.
    // BLK-GOV-01: PlatformAdmin must supply ?tenantId=<guid> — operation is always tenant-scoped.
    //             TenantAdmin is automatically scoped to their own tenant.
    private static async Task<IResult> BackfillAppointmentOrgIdsAsync(
        CareConnectDbContext    db,
        ICurrentRequestContext ctx,
        [FromQuery] Guid?      tenantId,
        CancellationToken      ct)
    {
        Guid scopeTenantId;
        if (ctx.IsPlatformAdmin)
        {
            if (!tenantId.HasValue)
                return Results.BadRequest(new
                {
                    error = "PlatformAdmin must supply ?tenantId=<guid>. " +
                            "This endpoint operates on a single tenant at a time.",
                });
            scopeTenantId = tenantId.Value;
        }
        else
        {
            scopeTenantId = ctx.TenantId
                ?? throw new InvalidOperationException("tenant_id claim is missing.");
        }

        // Load all appointments where at least one org ID is missing.
        var candidates = await db.Appointments
            .Include(a => a.Referral)
            .Where(a => a.TenantId == scopeTenantId
                        && (a.ReferringOrganizationId == null || a.ReceivingOrganizationId == null))
            .ToListAsync(ct);

        int updated = 0, skipped = 0, alreadySet = 0;

        foreach (var appt in candidates)
        {
            // Guard: both already set (shouldn't reach here due to WHERE, but be safe).
            if (appt.ReferringOrganizationId.HasValue && appt.ReceivingOrganizationId.HasValue)
            {
                alreadySet++;
                continue;
            }

            var referral = appt.Referral;

            // Unresolved: parent referral has no org IDs — cannot derive values.
            if (referral is null
                || referral.ReferringOrganizationId is null
                || referral.ReceivingOrganizationId is null)
            {
                skipped++;
                continue;
            }

            // Safe to backfill — values come exclusively from the parent Referral.
            appt.BackfillOrgIds(
                referral.ReferringOrganizationId.Value,
                referral.ReceivingOrganizationId.Value);

            updated++;
        }

        if (updated > 0)
            await db.SaveChangesAsync(ct);

        return Results.Ok(new AppointmentBackfillReport(
            Updated:    updated,
            Skipped:    skipped,
            AlreadySet: alreadySet,
            Candidates: candidates.Count));
    }
}

/// <summary>LSCC-002-01: Appointment org-ID backfill operation result.</summary>
public sealed record AppointmentBackfillReport(
    int Updated,
    int Skipped,
    int AlreadySet,
    int Candidates);
