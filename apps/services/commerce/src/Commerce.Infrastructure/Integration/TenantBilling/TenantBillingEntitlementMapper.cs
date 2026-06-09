using System.Text.Json;
using Commerce.Contracts.Integration;

namespace Commerce.Infrastructure.Integration.TenantBilling;

/// <summary>
/// Pure mapping from a Commerce <see cref="CommerceEntitlementSnapshot"/>
/// to the Tenant Billing apply request payload. Stateless and
/// side-effect-free so it can be unit-tested in isolation.
///
/// <para>Mapping policy is documented in TB-INT-01 §9. Recommendation
/// is taken verbatim from <see cref="CommerceEntitlementSnapshot.AccessRecommendation"/>
/// (Commerce is the authority for that value); entitlement status is
/// derived from <see cref="CommerceEntitlementSnapshot.AccountStandingStatus"/>.</para>
/// </summary>
internal static class TenantBillingEntitlementMapper
{
    private static readonly JsonSerializerOptions RawSerializer = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Builds the wire DTO. The caller is responsible for resolving the
    /// tenant id (the apply endpoint reads it from the X-Tenant-Id
    /// header, never from the body).
    /// </summary>
    public static TenantBillingApplyRequestDto Map(CommerceEntitlementSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var status = MapEntitlementStatus(snapshot.AccountStandingStatus);
        var recommendation = MapAccessRecommendation(snapshot.AccessRecommendation);

        // Pick a deterministic "primary" active subscription/plan/product so
        // the receiver gets a stable foreign key for joins. We just take
        // the first subscription in the snapshot — the snapshot service
        // already filters out inactive statuses by default.
        var primarySub = snapshot.Subscriptions.Count > 0 ? snapshot.Subscriptions[0] : null;
        var primaryItem = primarySub is { Items.Count: > 0 } ? primarySub.Items[0] : null;
        var primaryPlanKey = primaryItem?.PlanKey;
        var primaryProductKey = primaryPlanKey is null
            ? null
            : snapshot.Plans
                .FirstOrDefault(p => string.Equals(p.PlanKey, primaryPlanKey, StringComparison.Ordinal))
                ?.ProductKey;

        return new TenantBillingApplyRequestDto
        {
            BillingAccountId = snapshot.BillingAccountId,
            SourceSystem = "commerce",
            EntitlementStatus = status,
            AccessRecommendation = recommendation,
            // Use the snapshot's GeneratedAtUtc as a deterministic id —
            // round-trippable via ISO-8601 and stable for replays.
            SourceSnapshotId = snapshot.GeneratedAtUtc.ToString("O"),
            SourceSubscriptionId = primarySub?.SubscriptionId.ToString(),
            SourcePlanKey = Truncate(primaryPlanKey, 100),
            SourceProductKey = Truncate(primaryProductKey, 100),
            Reason = Truncate(snapshot.AccountStandingReason, 1000),
            // EffectiveFrom == when Commerce computed the snapshot.
            // EffectiveTo == grace-period end if applicable; otherwise
            // null (open-ended until a fresh publish supersedes it).
            EffectiveFromUtc = snapshot.GeneratedAtUtc,
            EffectiveToUtc = snapshot.AccountStandingGracePeriodEndsAtUtc,
            RawSnapshotJson = JsonSerializer.Serialize(snapshot, RawSerializer),
        };
    }

    /// <summary>
    /// Map Commerce account standing → Tenant Billing entitlement
    /// status. See TB-INT-01 §9 for the rationale per case.
    /// </summary>
    public static string MapEntitlementStatus(string? accountStandingStatus)
    {
        if (string.IsNullOrWhiteSpace(accountStandingStatus))
            return "Unknown";

        return accountStandingStatus.Trim() switch
        {
            "Good"        => "Enabled",
            "Trialing"    => "Enabled",
            "GracePeriod" => "Enabled",
            // Per TB-INT-01 §9: PastDue keeps the tenant Enabled and
            // relies on the recommendation field to signal degradation.
            "PastDue"     => "Enabled",
            "Suspended"   => "Suspended",
            "Cancelled"   => "Disabled",
            "Closed"      => "Disabled",
            _             => "Unknown",
        };
    }

    /// <summary>
    /// Map Commerce <see cref="AccessRecommendation"/> enum → Tenant
    /// Billing recommendation string. The two enums share the same five
    /// names so this is a verbatim pass-through, but we centralise it so
    /// any future drift is caught here instead of producing a 400 from
    /// the apply endpoint's <c>[Required, MaxLength(16)]</c> check.
    /// </summary>
    public static string MapAccessRecommendation(AccessRecommendation rec) => rec switch
    {
        AccessRecommendation.Allow        => "Allow",
        AccessRecommendation.ReadOnly     => "ReadOnly",
        AccessRecommendation.GraceLimited => "GraceLimited",
        AccessRecommendation.Block        => "Block",
        _                                 => "Unknown",
    };

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}
