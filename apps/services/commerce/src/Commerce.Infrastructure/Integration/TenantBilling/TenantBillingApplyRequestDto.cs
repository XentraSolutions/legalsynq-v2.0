using System.Text.Json.Serialization;

namespace Commerce.Infrastructure.Integration.TenantBilling;

/// <summary>
/// On-the-wire body sent to
/// <c>POST {TenantBillingBaseUrl}/api/tenant-billing/entitlements/apply</c>.
/// Field names match the Tenant Billing
/// <c>ApplyEntitlementSnapshotRequestDto</c> property casing exactly so
/// the JSON serialiser produces a payload Tenant Billing's model binder
/// will accept.
/// </summary>
internal sealed class TenantBillingApplyRequestDto
{
    [JsonPropertyName("billingAccountId")]
    public Guid BillingAccountId { get; set; }

    [JsonPropertyName("sourceSystem")]
    public string SourceSystem { get; set; } = "commerce";

    [JsonPropertyName("entitlementStatus")]
    public string EntitlementStatus { get; set; } = "Unknown";

    [JsonPropertyName("accessRecommendation")]
    public string AccessRecommendation { get; set; } = "Unknown";

    [JsonPropertyName("sourceSnapshotId")]
    public string? SourceSnapshotId { get; set; }

    [JsonPropertyName("sourceSubscriptionId")]
    public string? SourceSubscriptionId { get; set; }

    [JsonPropertyName("sourcePlanKey")]
    public string? SourcePlanKey { get; set; }

    [JsonPropertyName("sourceProductKey")]
    public string? SourceProductKey { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("effectiveFromUtc")]
    public DateTime? EffectiveFromUtc { get; set; }

    [JsonPropertyName("effectiveToUtc")]
    public DateTime? EffectiveToUtc { get; set; }

    [JsonPropertyName("rawSnapshotJson")]
    public string? RawSnapshotJson { get; set; }
}
