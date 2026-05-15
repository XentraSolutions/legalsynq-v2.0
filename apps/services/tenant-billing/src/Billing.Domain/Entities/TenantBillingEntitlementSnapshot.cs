using System.Text.Json;

namespace Billing.Domain.Entities;

/// <summary>
/// TB-DATA-02 — local mirror of the Commerce-side entitlement decision for a
/// single <see cref="TenantBillingProfile"/>. One current row per profile
/// (enforced by a UNIQUE index on <see cref="TenantBillingProfileId"/> on
/// relational providers); the bridge updates the row in place when a fresh
/// snapshot arrives.
///
/// <para>
/// All Commerce-side identifiers (<see cref="SourceSnapshotId"/>,
/// <see cref="SourceSubscriptionId"/>, <see cref="SourcePlanKey"/>,
/// <see cref="SourceProductKey"/>) are stored as opaque strings: this block
/// adds NO project reference to Commerce and makes NO live HTTP call to
/// validate them. The bridge is push-only.
/// </para>
///
/// <para>
/// <see cref="RawSnapshotJson"/> is optional and trace-only — the resolver
/// reads only the typed columns. When supplied, the service validates that
/// it is well-formed JSON before persisting.
/// </para>
/// </summary>
public sealed class TenantBillingEntitlementSnapshot
{
    public Guid Id { get; private set; }
    public Guid TenantBillingProfileId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BillingAccountId { get; private set; }

    /// <summary>
    /// Free-form short identifier of the system that produced this snapshot
    /// (e.g. "commerce", "manual-admin"). Not interpreted by Tenant Billing.
    /// </summary>
    public string SourceSystem { get; private set; } = "unknown";

    public string? SourceSnapshotId { get; private set; }
    public string? SourceSubscriptionId { get; private set; }
    public string? SourcePlanKey { get; private set; }
    public string? SourceProductKey { get; private set; }

    public string EntitlementStatus { get; private set; } = TenantBillingEntitlementStatus.Unknown;
    public string AccessRecommendation { get; private set; } = TenantBillingAccessRecommendation.Unknown;

    public string? Reason { get; private set; }

    public DateTime? EffectiveFromUtc { get; private set; }
    public DateTime? EffectiveToUtc { get; private set; }
    public DateTime LastSyncedAtUtc { get; private set; }

    /// <summary>Optional raw JSON payload from the source system, for trace.</summary>
    public string? RawSnapshotJson { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private TenantBillingEntitlementSnapshot() { }

    /// <summary>
    /// Factory: create the FIRST snapshot for a profile. The service layer is
    /// responsible for verifying that no row already exists for the profile
    /// before calling this (otherwise <see cref="Apply"/> on the existing row
    /// must be used).
    /// </summary>
    public static TenantBillingEntitlementSnapshot CreateFor(
        TenantBillingProfile profile,
        string sourceSystem,
        string entitlementStatus,
        string accessRecommendation,
        string? sourceSnapshotId,
        string? sourceSubscriptionId,
        string? sourcePlanKey,
        string? sourceProductKey,
        string? reason,
        DateTime? effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? rawSnapshotJson,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Guard(sourceSystem, entitlementStatus, accessRecommendation,
              sourceSnapshotId, sourceSubscriptionId, sourcePlanKey,
              sourceProductKey, reason, rawSnapshotJson);

        var snap = new TenantBillingEntitlementSnapshot
        {
            Id                     = Guid.NewGuid(),
            TenantBillingProfileId = profile.Id,
            TenantId               = profile.TenantId,
            BillingAccountId       = profile.BillingAccountId,
            CreatedAtUtc           = nowUtc,
        };
        snap.ApplyValues(sourceSystem, entitlementStatus, accessRecommendation,
                         sourceSnapshotId, sourceSubscriptionId, sourcePlanKey,
                         sourceProductKey, reason,
                         effectiveFromUtc, effectiveToUtc, rawSnapshotJson, nowUtc);
        return snap;
    }

    /// <summary>
    /// Replace this snapshot's mutable fields in place with the new values
    /// from the source system. <see cref="TenantId"/>,
    /// <see cref="BillingAccountId"/> and <see cref="TenantBillingProfileId"/>
    /// are immutable for the row — the service layer enforces that the
    /// incoming snapshot belongs to the same profile before calling Apply.
    /// </summary>
    public void Apply(
        string sourceSystem,
        string entitlementStatus,
        string accessRecommendation,
        string? sourceSnapshotId,
        string? sourceSubscriptionId,
        string? sourcePlanKey,
        string? sourceProductKey,
        string? reason,
        DateTime? effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? rawSnapshotJson,
        DateTime nowUtc)
    {
        Guard(sourceSystem, entitlementStatus, accessRecommendation,
              sourceSnapshotId, sourceSubscriptionId, sourcePlanKey,
              sourceProductKey, reason, rawSnapshotJson);
        ApplyValues(sourceSystem, entitlementStatus, accessRecommendation,
                    sourceSnapshotId, sourceSubscriptionId, sourcePlanKey,
                    sourceProductKey, reason,
                    effectiveFromUtc, effectiveToUtc, rawSnapshotJson, nowUtc);
    }

    private void ApplyValues(
        string sourceSystem,
        string entitlementStatus,
        string accessRecommendation,
        string? sourceSnapshotId,
        string? sourceSubscriptionId,
        string? sourcePlanKey,
        string? sourceProductKey,
        string? reason,
        DateTime? effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? rawSnapshotJson,
        DateTime nowUtc)
    {
        SourceSystem         = sourceSystem.Trim();
        EntitlementStatus    = entitlementStatus;
        AccessRecommendation = accessRecommendation;
        SourceSnapshotId     = NormalizeOptional(sourceSnapshotId, 200);
        SourceSubscriptionId = NormalizeOptional(sourceSubscriptionId, 200);
        SourcePlanKey        = NormalizeOptional(sourcePlanKey, 100);
        SourceProductKey     = NormalizeOptional(sourceProductKey, 100);
        Reason               = NormalizeOptional(reason, 1000);
        EffectiveFromUtc     = effectiveFromUtc;
        EffectiveToUtc       = effectiveToUtc;
        RawSnapshotJson      = string.IsNullOrWhiteSpace(rawSnapshotJson) ? null : rawSnapshotJson;
        LastSyncedAtUtc      = nowUtc;
        UpdatedAtUtc         = nowUtc;
    }

    private static void Guard(
        string sourceSystem,
        string entitlementStatus,
        string accessRecommendation,
        string? sourceSnapshotId,
        string? sourceSubscriptionId,
        string? sourcePlanKey,
        string? sourceProductKey,
        string? reason,
        string? rawSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(sourceSystem))
            throw new ArgumentException("SourceSystem is required.", nameof(sourceSystem));
        if (sourceSystem.Length > 100)
            throw new ArgumentException("SourceSystem exceeds 100 chars.", nameof(sourceSystem));
        if (!TenantBillingEntitlementStatus.IsValid(entitlementStatus))
            throw new ArgumentException($"Unknown EntitlementStatus '{entitlementStatus}'.", nameof(entitlementStatus));
        if (!TenantBillingAccessRecommendation.IsValid(accessRecommendation))
            throw new ArgumentException($"Unknown AccessRecommendation '{accessRecommendation}'.", nameof(accessRecommendation));

        EnsureMaxLen(sourceSnapshotId,     200, nameof(sourceSnapshotId));
        EnsureMaxLen(sourceSubscriptionId, 200, nameof(sourceSubscriptionId));
        EnsureMaxLen(sourcePlanKey,        100, nameof(sourcePlanKey));
        EnsureMaxLen(sourceProductKey,     100, nameof(sourceProductKey));
        EnsureMaxLen(reason,              1000, nameof(reason));

        if (!string.IsNullOrWhiteSpace(rawSnapshotJson))
        {
            try { using var _ = JsonDocument.Parse(rawSnapshotJson); }
            catch (JsonException ex)
            {
                throw new ArgumentException(
                    $"RawSnapshotJson is not valid JSON: {ex.Message}",
                    nameof(rawSnapshotJson));
            }
        }
    }

    private static void EnsureMaxLen(string? value, int max, string param)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Length > max)
            throw new ArgumentException($"{param} exceeds max length {max}.", param);
    }

    private static string? NormalizeOptional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > max)
            throw new ArgumentException($"Value exceeds max length {max}.");
        return trimmed;
    }
}
