namespace Notifications.Application.Options;

/// <summary>
/// LS-NOTIF-SMS-013: Configuration for SMS cost analytics.
///
/// These are operational cost estimates, not invoice-grade billing values.
/// Estimated costs are configurable assumptions per provider.
/// Actual provider billing data requires future Twilio adapter extension.
///
/// Safe defaults: if a provider cost is unconfigured, CostSource = "unavailable"
/// and no monetary amount is recorded — this avoids implying false spend.
/// </summary>
public sealed class SmsCostAnalyticsOptions
{
    public const string SectionName = "SmsCostAnalytics";

    /// <summary>When false, cost recording in the send path is skipped entirely.
    /// Existing cost analytics queries still work against any data already recorded.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>ISO 4217 currency code for all cost records. Default: "USD".</summary>
    public string DefaultCurrency { get; set; } = "USD";

    /// <summary>
    /// Estimated outbound SMS cost when provider is not twilio (or TwilioEstimatedOutboundSmsCost is unset).
    /// Set to 0.00 or leave null to record CostSource = "unavailable" for unknown providers.
    /// </summary>
    public decimal? DefaultEstimatedOutboundSmsCost { get; set; } = null;

    /// <summary>
    /// Estimated outbound SMS cost for Twilio. Typical Twilio US SMS rate ~$0.0075.
    /// Configure via environment variable SMS_COST_TWILIO_ESTIMATED_OUTBOUND_COST.
    /// </summary>
    public decimal? TwilioEstimatedOutboundSmsCost { get; set; } = 0.0075m;

    /// <summary>
    /// Cost policy for failed messages.
    /// "count_estimated_when_provider_accepted": cost only when attempt has a ProviderMessageId
    ///   (provider accepted the message before it failed/timed out).
    /// Default: count_estimated_when_provider_accepted.
    /// </summary>
    public string FailedMessageCostPolicy { get; set; } = "count_estimated_when_provider_accepted";

    /// <summary>
    /// Cost policy for retry/failover attempts.
    /// "per_attempt": each attempt hop is costed independently.
    /// Default: per_attempt.
    /// </summary>
    public string RetryCostPolicy { get; set; } = "per_attempt";

    /// <summary>
    /// Returns the estimated outbound cost for the given provider name, or null if unavailable.
    /// Provider name comparison is case-insensitive.
    /// </summary>
    public decimal? GetEstimatedCost(string provider)
    {
        if (string.Equals(provider, "twilio", StringComparison.OrdinalIgnoreCase))
            return TwilioEstimatedOutboundSmsCost;

        return DefaultEstimatedOutboundSmsCost;
    }
}
