// LSCC-002: Shared participant-scoping logic for CareConnect records.
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Domain;

namespace CareConnect.Application.Authorization;

/// <summary>
/// Centralised participant-access helpers for CareConnect.
/// Used consistently for list scoping and row-level access across Referral and Appointment.
///
/// Golden rule: when org context is missing or the caller is not a participant,
/// access is DENIED — never widened silently.
/// </summary>
// LSCC-002: Shared participant-scoping logic — single definition for all CareConnect records
public static class CareConnectParticipantHelper
{
    private const string CareConnectReceiverRole = "CARECONNECT_RECEIVER";

    // ── Admin bypass ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the caller is a PlatformAdmin or TenantAdmin.
    /// These roles bypass all org-participant checks per platform conventions.
    /// </summary>
    public static bool IsAdmin(ICurrentRequestContext ctx) =>
        ctx.IsPlatformAdmin || ctx.Roles.Contains(Roles.TenantAdmin, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true when the caller should be treated as a CareConnect receiver/provider
    /// for participant scoping. Prefer receiver product-role checks and fall back to
    /// org-type for compatibility with older role payloads.
    /// </summary>
    public static bool IsReceiverContext(ICurrentRequestContext ctx)
    {
        if (ctx.ProductRoles.Any(role =>
                role.Equals(CareConnectReceiverRole, StringComparison.OrdinalIgnoreCase) ||
                role.EndsWith($":{CareConnectReceiverRole}", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return string.Equals(ctx.OrgType, OrgType.Provider, StringComparison.OrdinalIgnoreCase);
    }

    // ── Referral participant checks ───────────────────────────────────────────

    /// <summary>
    /// Returns true when the caller's org is a participant in the given referral
    /// (i.e. they are the referring or receiving org).
    /// </summary>
    /// <param name="referral">The loaded referral entity.</param>
    /// <param name="callerOrgId">
    /// The org ID from the caller's JWT context. A null value always returns false
    /// (missing context must never widen access).
    /// </param>
    // LSCC-002: CareConnect permission enforcement — referral participant check
    public static bool IsReferralParticipant(Referral referral, Guid? callerOrgId)
    {
        if (callerOrgId is null) return false;

        return referral.ReferringOrganizationId == callerOrgId
            || referral.ReceivingOrganizationId == callerOrgId;
    }

    /// <summary>
    /// Determines the referral org filter fields for a list query based on caller context.
    /// Returns (referringOrgId: null, receivingOrgId: null) for admins — no filter applied.
    /// </summary>
    public static (Guid? ReferringOrgId, Guid? ReceivingOrgId) GetReferralOrgScope(
        ICurrentRequestContext ctx,
        bool callerIsReceiver)
    {
        if (IsAdmin(ctx)) return (null, null);

        if (callerIsReceiver)
            return (null, ctx.OrgId);

        return (ctx.OrgId, null);
    }

    /// <summary>
    /// Applies assistant referral search scope using participant identity rather than the
    /// currently selected tenant alone. This keeps assistant search useful for referrer-side
    /// orgs whose visible referrals may live in a different tenant than the current portal tenant.
    /// </summary>
    public static void ApplyAssistantReferralScope(GetReferralsQuery query, ICurrentRequestContext ctx)
    {
        if (IsAdmin(ctx))
            return;

        if (IsReceiverContext(ctx))
        {
            query.CrossTenantReceiver = true;
            query.ReceivingOrgId = ctx.OrgId;
            return;
        }

        if (ctx.OrgId.HasValue || !string.IsNullOrWhiteSpace(ctx.Email))
        {
            query.CrossTenantReferrer = true;
            query.ReferringOrgId = ctx.OrgId;
            query.ReferrerEmail = ctx.Email;
        }
    }

    /// <summary>
    /// Assistant lookups should use global referral fetches whenever participant identity is
    /// available, then rely on row-level participant checks to enforce final access.
    /// </summary>
    public static bool ShouldUseAssistantGlobalReferralLookup(ICurrentRequestContext ctx)
    {
        if (ctx.IsPlatformAdmin)
            return true;

        if (IsAdmin(ctx))
            return false;

        if (IsReceiverContext(ctx))
            return true;

        return ctx.OrgId.HasValue || !string.IsNullOrWhiteSpace(ctx.Email);
    }

    /// <summary>
    /// Returns true when an authenticated referral operation may resolve the record outside
    /// the caller's currently selected tenant. Row-level participant checks must still run
    /// after the global lookup.
    /// </summary>
    public static bool ShouldUseGlobalReferralLookup(ICurrentRequestContext ctx, bool isProviderOrg)
    {
        if (ctx.IsPlatformAdmin)
            return true;

        // TenantAdmin bypass applies only inside the selected tenant. Combining it with
        // a global lookup would allow an admin from one tenant to bypass participant checks
        // on another tenant's referral.
        if (IsAdmin(ctx))
            return false;

        if (isProviderOrg)
            return true;

        var tenantIds = ctx.TenantIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (tenantIds.Count == 0 && ctx.TenantId.HasValue)
            tenantIds.Add(ctx.TenantId.Value);

        if (tenantIds.Count <= 1)
            return false;

        return ctx.ProductRoles.Any(role =>
            role.EndsWith(":CARECONNECT_REFERRER", StringComparison.OrdinalIgnoreCase));
    }

    // ── Appointment participant checks ────────────────────────────────────────

    /// <summary>
    /// Returns true when the caller's org is a participant in the given appointment
    /// (i.e. they are the referring or receiving org denormalized from the source referral).
    /// </summary>
    /// <param name="appointment">The loaded appointment entity.</param>
    /// <param name="callerOrgId">
    /// The org ID from the caller's JWT context. A null value always returns false.
    /// </param>
    // LSCC-002: CareConnect permission enforcement — appointment participant check
    public static bool IsAppointmentParticipant(Appointment appointment, Guid? callerOrgId)
    {
        if (callerOrgId is null) return false;

        return appointment.ReferringOrganizationId == callerOrgId
            || appointment.ReceivingOrganizationId == callerOrgId;
    }

    /// <summary>
    /// Determines the appointment org filter fields for a list query based on caller context.
    /// Follows the same convention as referral scoping:
    /// receivers filter by receiving org; all others filter by referring org.
    /// Returns (null, null) for admins.
    /// </summary>
    public static (Guid? ReferringOrgId, Guid? ReceivingOrgId) GetAppointmentOrgScope(
        ICurrentRequestContext ctx,
        bool callerIsReceiver)
    {
        if (IsAdmin(ctx)) return (null, null);

        if (callerIsReceiver)
            return (null, ctx.OrgId);

        return (ctx.OrgId, null);
    }
}
