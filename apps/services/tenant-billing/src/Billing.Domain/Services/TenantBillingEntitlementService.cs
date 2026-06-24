using Billing.Domain.Entities;
using Billing.Domain.Repositories;

namespace Billing.Domain.Services;

public sealed class TenantBillingEntitlementService : ITenantBillingEntitlementService
{
    private readonly ITenantBillingProfileRepository _profiles;
    private readonly ITenantBillingEntitlementSnapshotRepository _snapshots;
    private readonly TimeProvider _clock;

    public TenantBillingEntitlementService(
        ITenantBillingProfileRepository profiles,
        ITenantBillingEntitlementSnapshotRepository snapshots,
        TimeProvider? clock = null)
    {
        _profiles  = profiles;
        _snapshots = snapshots;
        _clock     = clock ?? TimeProvider.System;
    }

    public async Task<TenantBillingEntitlementSnapshot> ApplySnapshotAsync(
        Guid tenantId, ApplyEntitlementSnapshotRequest request, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must be non-empty.", nameof(tenantId));
        ArgumentNullException.ThrowIfNull(request);
        if (request.BillingAccountId == Guid.Empty)
            throw new ArgumentException("BillingAccountId is required.");

        // NOTE: we use the include-Closed lookup so we can distinguish
        // "no profile exists" (404) from "profile exists but is Closed"
        // (409). The open-only lookup would conflate both.
        var profile = await _profiles.GetAnyByBillingAccountAsync(
            tenantId, request.BillingAccountId, ct);
        if (profile is null)
        {
            // Cross-check: maybe a profile exists for the tenant but the
            // BillingAccountId in the request is wrong. Surface a clear
            // mismatch error vs a generic not-found so the operator can
            // tell which it is.
            var anyOpen = await _profiles.GetActiveByTenantAsync(tenantId, ct);
            if (anyOpen is not null && anyOpen.BillingAccountId != request.BillingAccountId)
            {
                throw new TenantBillingEntitlementProfileMismatchException(
                    $"Tenant {tenantId} has an Active profile for a different billing account " +
                    $"({anyOpen.BillingAccountId}); refusing to apply snapshot for {request.BillingAccountId}.");
            }
            throw new TenantBillingProfileNotFoundException(
                $"No profile for tenant {tenantId} and billing account {request.BillingAccountId}.");
        }

        if (profile.Status == TenantBillingProfileStatus.Closed)
        {
            throw new TenantBillingEntitlementClosedProfileException(
                $"Profile {profile.Id} is Closed; cannot apply entitlement snapshot.");
        }

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var existing = await _snapshots.GetByProfileIdAsync(tenantId, profile.Id, ct);

        try
        {
            if (existing is null)
            {
                var snap = TenantBillingEntitlementSnapshot.CreateFor(
                    profile,
                    request.SourceSystem,
                    request.EntitlementStatus,
                    request.AccessRecommendation,
                    request.SourceSnapshotId,
                    request.SourceSubscriptionId,
                    request.SourcePlanKey,
                    request.SourceProductKey,
                    request.Reason,
                    request.EffectiveFromUtc,
                    request.EffectiveToUtc,
                    request.RawSnapshotJson,
                    nowUtc);
                return await _snapshots.AddAsync(snap, ct);
            }

            existing.Apply(
                request.SourceSystem,
                request.EntitlementStatus,
                request.AccessRecommendation,
                request.SourceSnapshotId,
                request.SourceSubscriptionId,
                request.SourcePlanKey,
                request.SourceProductKey,
                request.Reason,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.RawSnapshotJson,
                nowUtc);
            return await _snapshots.UpdateAsync(existing, ct);
        }
        catch (ArgumentException ex)
            when (ex.ParamName == "rawSnapshotJson" || ex.Message.Contains("RawSnapshotJson", StringComparison.Ordinal))
        {
            throw new TenantBillingEntitlementInvalidJsonException(ex.Message);
        }
    }

    public async Task<TenantBillingEntitlementSnapshot?> GetCurrentSnapshotAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return null;

        // The contract is "snapshot for the tenant's open profile". Look up
        // an Active profile first; fall back to any open (Draft/Suspended).
        var profile = await _profiles.GetActiveByTenantAsync(tenantId, ct);
        if (profile is null) return null;
        return await _snapshots.GetByProfileIdAsync(tenantId, profile.Id, ct);
    }

    public Task<TenantBillingEntitlementSnapshot?> GetByProfileIdAsync(
        Guid tenantId, Guid profileId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || profileId == Guid.Empty)
            return Task.FromResult<TenantBillingEntitlementSnapshot?>(null);
        return _snapshots.GetByProfileIdAsync(tenantId, profileId, ct);
    }

    public async Task<TenantBillingAccessDecision> GetAccessRecommendationAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return TenantBillingAccessDecisions.NotEnabled("tenant id missing");

        var profile = await _profiles.GetActiveByTenantAsync(tenantId, ct);
        if (profile is null)
            return TenantBillingAccessDecisions.NotEnabled("no active tenant billing profile");

        var snapshot = await _snapshots.GetByProfileIdAsync(tenantId, profile.Id, ct);
        return TenantBillingAccessDecisions.Compute(profile, snapshot);
    }
}

/// <summary>
/// Pure decision logic — kept on a static helper so both the entitlement
/// service and the enablement resolver can call into the same matrix.
/// </summary>
internal static class TenantBillingAccessDecisions
{
    public static TenantBillingAccessDecision NotEnabled(string reason)
        => new(IsEnabled: false,
               WriteAccessAllowed: false,
               EntitlementStatus: TenantBillingEntitlementStatus.Unknown,
               AccessRecommendation: TenantBillingAccessRecommendation.Unknown,
               Reason: reason,
               TenantBillingProfileId: null,
               BillingAccountId: null,
               SourceSubscriptionIdGuid: null,
               SourceSubscriptionId: null,
               SourcePlanKey: null,
               LastSyncedAtUtc: null);

    public static TenantBillingAccessDecision Compute(
        TenantBillingProfile profile,
        TenantBillingEntitlementSnapshot? snapshot)
    {
        // Active profile is a precondition before this is called for the
        // happy path, but we still defend against being handed a non-Active
        // profile by future callers.
        if (profile.Status != TenantBillingProfileStatus.Active)
        {
            return new TenantBillingAccessDecision(
                IsEnabled: false,
                WriteAccessAllowed: false,
                EntitlementStatus: snapshot?.EntitlementStatus ?? TenantBillingEntitlementStatus.Unknown,
                AccessRecommendation: snapshot?.AccessRecommendation ?? TenantBillingAccessRecommendation.Unknown,
                Reason: $"profile status is {profile.Status}",
                TenantBillingProfileId: profile.Id,
                BillingAccountId: profile.BillingAccountId,
                SourceSubscriptionIdGuid: null,
                SourceSubscriptionId: snapshot?.SourceSubscriptionId,
                SourcePlanKey: snapshot?.SourcePlanKey,
                LastSyncedAtUtc: snapshot?.LastSyncedAtUtc);
        }

        if (snapshot is null)
        {
            return new TenantBillingAccessDecision(
                IsEnabled: false,
                WriteAccessAllowed: false,
                EntitlementStatus: TenantBillingEntitlementStatus.Unknown,
                AccessRecommendation: TenantBillingAccessRecommendation.Unknown,
                Reason: "no entitlement snapshot",
                TenantBillingProfileId: profile.Id,
                BillingAccountId: profile.BillingAccountId,
                SourceSubscriptionIdGuid: null,
                SourceSubscriptionId: null,
                SourcePlanKey: null,
                LastSyncedAtUtc: null);
        }

        var status = snapshot.EntitlementStatus;
        var rec    = snapshot.AccessRecommendation;

        var enabled = status == TenantBillingEntitlementStatus.Enabled
                      && rec == TenantBillingAccessRecommendation.Allow;
        var write   = enabled;

        var reason = (status, rec) switch
        {
            (TenantBillingEntitlementStatus.Disabled,  _) => "entitlement disabled",
            (TenantBillingEntitlementStatus.Suspended, _) => "entitlement suspended",
            (TenantBillingEntitlementStatus.Expired,   _) => "entitlement expired",
            (TenantBillingEntitlementStatus.Unknown,   _) => "entitlement unknown",
            (_, TenantBillingAccessRecommendation.Block)        => "access blocked",
            (_, TenantBillingAccessRecommendation.ReadOnly)     => "read-only access; writes not allowed",
            (_, TenantBillingAccessRecommendation.GraceLimited) => "grace-limited access; writes not allowed",
            (_, TenantBillingAccessRecommendation.Unknown)      => "no access recommendation",
            _ => snapshot.Reason ?? "ok",
        };

        return new TenantBillingAccessDecision(
            IsEnabled: enabled,
            WriteAccessAllowed: write,
            EntitlementStatus: status,
            AccessRecommendation: rec,
            Reason: reason,
            TenantBillingProfileId: profile.Id,
            BillingAccountId: profile.BillingAccountId,
            SourceSubscriptionIdGuid: null,
            SourceSubscriptionId: snapshot.SourceSubscriptionId,
            SourcePlanKey: snapshot.SourcePlanKey,
            LastSyncedAtUtc: snapshot.LastSyncedAtUtc);
    }
}
