namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-014: Persisted SMS routing decision for audit, debug, and reporting.
/// Created by SmsRoutingEngine before the send attempt. AttemptId linked post-send.
///
/// Security: No credentials, CredentialsJson, SettingsJson, auth tokens,
/// webhook URLs, or raw phone numbers stored in this entity.
/// ProviderConfigId is opaque Guid — no credential data.
/// CandidateProvidersJson/ExcludedProvidersJson contain provider type strings only.
/// </summary>
public class SmsRoutingDecision
{
    public Guid    Id             { get; set; } = Guid.NewGuid();
    public Guid?   TenantId       { get; set; }
    public Guid?   NotificationId { get; set; }

    /// <summary>Linked to NotificationAttempt.Id after send completes.</summary>
    public Guid?   AttemptId          { get; set; }

    /// <summary>Matched routing policy, if any.</summary>
    public Guid?   RoutingPolicyId    { get; set; }

    /// <summary>Routing mode used: priority | cost_optimized | health_optimized | hybrid | regional | no_route</summary>
    public string  RoutingMode        { get; set; } = string.Empty;

    public string  SelectedProvider   { get; set; } = string.Empty;
    public Guid?   SelectedProviderConfigId { get; set; }
    public string? ProviderOwnershipMode    { get; set; }

    /// <summary>JSON array of candidate provider type strings at decision time.</summary>
    public string? CandidateProvidersJson { get; set; }

    /// <summary>JSON array of excluded provider type strings.</summary>
    public string? ExcludedProvidersJson  { get; set; }

    /// <summary>Human-readable reason for selection or failure.</summary>
    public string  DecisionReason         { get; set; } = string.Empty;

    public decimal? EstimatedCostAmount   { get; set; }
    public string?  CostCurrency          { get; set; }

    /// <summary>Reserved for future health snapshot data (JSON).</summary>
    public string?  HealthSnapshotJson    { get; set; }

    public string?  Region      { get; set; }
    public string?  CountryCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
