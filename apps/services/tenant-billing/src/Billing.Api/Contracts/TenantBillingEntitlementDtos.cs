using System.ComponentModel.DataAnnotations;
using Billing.Domain.Entities;
using Billing.Domain.Services;

namespace Billing.Api.Contracts;

/// <summary>
/// TB-DATA-02 — request body for
/// <c>POST /api/tenant-billing/entitlements/apply</c>. Tenant id is taken
/// from the X-Tenant-Id header, never from the body.
///
/// Wire field names mirror Commerce's <c>CommerceEntitlementSnapshot</c> so
/// a future publisher can pass values through unchanged.
/// </summary>
public sealed class ApplyEntitlementSnapshotRequestDto
{
    [Required] public Guid BillingAccountId { get; set; }

    [Required, MaxLength(100)]
    public string SourceSystem { get; set; } = "commerce";

    [Required, MaxLength(16)]
    public string EntitlementStatus { get; set; } = TenantBillingEntitlementStatus.Unknown;

    [Required, MaxLength(16)]
    public string AccessRecommendation { get; set; } = TenantBillingAccessRecommendation.Unknown;

    [MaxLength(200)] public string? SourceSnapshotId { get; set; }
    [MaxLength(200)] public string? SourceSubscriptionId { get; set; }
    [MaxLength(100)] public string? SourcePlanKey { get; set; }
    [MaxLength(100)] public string? SourceProductKey { get; set; }

    [MaxLength(1000)] public string? Reason { get; set; }

    public DateTime? EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }

    /// <summary>
    /// Optional raw payload from the source system, stored for trace.
    /// Validated as well-formed JSON before persist.
    /// </summary>
    public string? RawSnapshotJson { get; set; }

    internal ApplyEntitlementSnapshotRequest ToDomain() => new(
        BillingAccountId,
        SourceSystem,
        EntitlementStatus,
        AccessRecommendation,
        SourceSnapshotId,
        SourceSubscriptionId,
        SourcePlanKey,
        SourceProductKey,
        Reason,
        EffectiveFromUtc,
        EffectiveToUtc,
        RawSnapshotJson);
}

public sealed record TenantBillingEntitlementSnapshotResponse(
    Guid Id,
    Guid TenantBillingProfileId,
    Guid TenantId,
    Guid BillingAccountId,
    string SourceSystem,
    string? SourceSnapshotId,
    string? SourceSubscriptionId,
    string? SourcePlanKey,
    string? SourceProductKey,
    string EntitlementStatus,
    string AccessRecommendation,
    string? Reason,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    DateTime LastSyncedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? RawSnapshotJson)
{
    public static TenantBillingEntitlementSnapshotResponse From(TenantBillingEntitlementSnapshot s) => new(
        s.Id, s.TenantBillingProfileId, s.TenantId, s.BillingAccountId,
        s.SourceSystem, s.SourceSnapshotId, s.SourceSubscriptionId,
        s.SourcePlanKey, s.SourceProductKey,
        s.EntitlementStatus, s.AccessRecommendation, s.Reason,
        s.EffectiveFromUtc, s.EffectiveToUtc, s.LastSyncedAtUtc,
        s.CreatedAtUtc, s.UpdatedAtUtc, s.RawSnapshotJson);
}

public sealed record TenantBillingAccessDecisionResponse(
    bool IsEnabled,
    bool WriteAccessAllowed,
    string EntitlementStatus,
    string AccessRecommendation,
    string Reason,
    Guid? TenantBillingProfileId,
    Guid? BillingAccountId,
    string? SourceSubscriptionId,
    string? SourcePlanKey,
    DateTime? LastSyncedAtUtc)
{
    public static TenantBillingAccessDecisionResponse From(TenantBillingAccessDecision d) => new(
        d.IsEnabled, d.WriteAccessAllowed,
        d.EntitlementStatus, d.AccessRecommendation, d.Reason,
        d.TenantBillingProfileId, d.BillingAccountId,
        d.SourceSubscriptionId, d.SourcePlanKey, d.LastSyncedAtUtc);
}
