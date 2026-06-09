namespace LegalSynq.AuditClient.Enums;

/// <summary>
/// Canonical audit event type strings for Commerce ecosystem events
/// (LS-COMMERCE-ECO-01).
///
/// <para>
/// These constants are used in <see cref="LegalSynq.AuditClient.DTOs.IngestAuditEventRequest.EventType"/>
/// when emitting Commerce-related events to the Platform Audit Event Service.
/// </para>
///
/// <para>
/// Categorisation: use <see cref="EventCategory.Business"/> for subscription/entitlement
/// lifecycle events, <see cref="EventCategory.Administrative"/> for account management,
/// <see cref="EventCategory.Integration"/> for cross-service provisioning hook events,
/// and <see cref="EventCategory.Security"/> for access-recommendation downgrades.
/// </para>
/// </summary>
public static class CommerceAuditEventTypes
{
    // ── Billing account ───────────────────────────────────────────────────────
    public const string BillingAccountCreated   = "commerce.account.created";
    public const string BillingAccountUpdated   = "commerce.account.updated";
    public const string BillingAccountSuspended = "commerce.account.suspended";
    public const string BillingAccountClosed    = "commerce.account.closed";
    public const string BillingAccountReopened  = "commerce.account.reopened";

    // ── Subscription lifecycle ────────────────────────────────────────────────
    public const string SubscriptionActivated    = "commerce.subscription.activated";
    public const string SubscriptionSuspended    = "commerce.subscription.suspended";
    public const string SubscriptionCancelled    = "commerce.subscription.cancelled";
    public const string SubscriptionRenewed      = "commerce.subscription.renewed";
    public const string SubscriptionTrialStarted = "commerce.subscription.trial.started";
    public const string SubscriptionTrialExpired = "commerce.subscription.trial.expired";

    // ── Entitlement / product changes ─────────────────────────────────────────
    public const string EntitlementGranted = "commerce.entitlement.granted";
    public const string EntitlementRevoked = "commerce.entitlement.revoked";
    public const string EntitlementChanged = "commerce.entitlement.changed";

    // ── Billing standing ──────────────────────────────────────────────────────
    public const string BillingStandingChanged    = "commerce.billing.standing.changed";
    public const string BillingGracePeriodStarted = "commerce.billing.grace.started";
    public const string BillingGracePeriodExpired = "commerce.billing.grace.expired";

    // ── Access recommendation ─────────────────────────────────────────────────
    public const string AccessRecommendationChanged = "commerce.access.recommendation.changed";

    // ── Provisioning hooks (Commerce → Host) ──────────────────────────────────
    public const string ProvisioningHookDispatched = "commerce.provisioning.hook.dispatched";
    public const string ProvisioningHookDelivered  = "commerce.provisioning.hook.delivered";
    public const string ProvisioningHookFailed     = "commerce.provisioning.hook.failed";

    // ── Integration health (consuming service → Audit) ────────────────────────
    /// <summary>Consuming service successfully fetched an entitlement snapshot from Commerce.</summary>
    public const string IntegrationCheckSucceeded = "commerce.integration.entitlement.check.succeeded";

    /// <summary>Consuming service failed to fetch an entitlement snapshot from Commerce.</summary>
    public const string IntegrationCheckFailed    = "commerce.integration.entitlement.check.failed";

    /// <summary>
    /// Consuming service skipped the Commerce entitlement check
    /// (integration disabled, tenant not mapped, or permissive fallback applied).
    /// </summary>
    public const string IntegrationCheckSkipped   = "commerce.integration.entitlement.check.skipped";
}
