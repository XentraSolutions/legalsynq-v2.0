using Billing.Domain.Entities;

namespace Billing.Domain.Services;

/// <summary>
/// TB-DATA-02 — application service for the entitlement bridge. Apply a
/// snapshot pushed by the source system (Commerce today, manual ops later)
/// to the matching <see cref="TenantBillingProfile"/>; read the current
/// snapshot back. Tenant-scoped: every call takes the tenant id sourced
/// from the X-Tenant-Id header.
/// </summary>
public interface ITenantBillingEntitlementService
{
    /// <summary>
    /// Push a fresh snapshot. The matching profile is the open profile for
    /// <paramref name="tenantId"/> AND <paramref name="request"/>'s
    /// <see cref="ApplyEntitlementSnapshotRequest.BillingAccountId"/>.
    ///
    /// Throws:
    /// <list type="bullet">
    ///   <item><see cref="TenantBillingProfileNotFoundException"/> — no open
    ///         profile matches the tenant + billing account.</item>
    ///   <item><see cref="TenantBillingEntitlementClosedProfileException"/>
    ///         — profile exists but is Closed.</item>
    ///   <item><see cref="TenantBillingEntitlementInvalidJsonException"/> —
    ///         RawSnapshotJson is not valid JSON.</item>
    ///   <item><see cref="ArgumentException"/> — invalid enum / oversized
    ///         field.</item>
    /// </list>
    /// </summary>
    Task<TenantBillingEntitlementSnapshot> ApplySnapshotAsync(
        Guid tenantId, ApplyEntitlementSnapshotRequest request, CancellationToken ct = default);

    /// <summary>
    /// The current snapshot for the tenant's open (non-Closed) profile, or
    /// null when no profile / no snapshot exists.
    /// </summary>
    Task<TenantBillingEntitlementSnapshot?> GetCurrentSnapshotAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Snapshot for an explicit profile id. Returns null when the id is
    /// unknown OR belongs to a different tenant — surfaces as 404 in the API.
    /// </summary>
    Task<TenantBillingEntitlementSnapshot?> GetByProfileIdAsync(
        Guid tenantId, Guid profileId, CancellationToken ct = default);

    Task<TenantBillingAccessDecision> GetAccessRecommendationAsync(
        Guid tenantId, CancellationToken ct = default);
}

/// <summary>
/// Plain, controller-friendly request shape for ApplySnapshot.
/// Mirrors the Commerce <c>CommerceEntitlementSnapshot</c> wire fields so a
/// future publisher can pass values through unchanged.
/// </summary>
public sealed record ApplyEntitlementSnapshotRequest(
    Guid BillingAccountId,
    string SourceSystem,
    string EntitlementStatus,
    string AccessRecommendation,
    string? SourceSnapshotId,
    string? SourceSubscriptionId,
    string? SourcePlanKey,
    string? SourceProductKey,
    string? Reason,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? RawSnapshotJson);

/// <summary>
/// Computed access decision for a tenant. <see cref="IsEnabled"/> is the
/// strict yes/no for billing operations; <see cref="WriteAccessAllowed"/>
/// is reserved for future ReadOnly / GraceLimited differentiation.
/// </summary>
public sealed record TenantBillingAccessDecision(
    bool IsEnabled,
    bool WriteAccessAllowed,
    string EntitlementStatus,
    string AccessRecommendation,
    string Reason,
    Guid? TenantBillingProfileId,
    Guid? BillingAccountId,
    Guid? SourceSubscriptionIdGuid,
    string? SourceSubscriptionId,
    string? SourcePlanKey,
    DateTime? LastSyncedAtUtc);
