namespace Contracts.Commerce;

/// <summary>
/// Canonical Commerce ecosystem event type string constants.
///
/// <para>
/// Naming rules (mirroring <c>NotificationTemplateKeys</c>):
/// <list type="bullet">
///   <item><description>Lower-case dotted segments: <c>{domain}.{noun}.{verb}</c>.</description></item>
///   <item><description>First segment is always <c>commerce</c> to avoid collisions with other domains.</description></item>
///   <item><description>Second segment is the affected domain entity (<c>account</c>, <c>subscription</c>, <c>entitlement</c>, etc.).</description></item>
///   <item><description>Third segment is the action/transition verb in past tense where possible.</description></item>
/// </list>
/// </para>
///
/// <para>
/// Producers SHOULD use these constants rather than literal strings so that
/// a rename surfaces as a compile-time break, not a silent runtime miss.
/// </para>
/// </summary>
public static class CommerceEventTypes
{
    // ── Tenant lifecycle ──────────────────────────────────────────────────────
    public const string TenantCreated   = "commerce.tenant.created";
    public const string TenantActivated = "commerce.tenant.activated";
    public const string TenantSuspended = "commerce.tenant.suspended";
    public const string TenantClosed    = "commerce.tenant.closed";
    public const string TenantReopened  = "commerce.tenant.reopened";

    // ── Product / entitlement lifecycle ──────────────────────────────────────
    public const string ProductEnabled  = "commerce.product.enabled";
    public const string ProductDisabled = "commerce.product.disabled";

    // ── Subscription lifecycle ────────────────────────────────────────────────
    public const string SubscriptionActivated    = "commerce.subscription.activated";
    public const string SubscriptionSuspended    = "commerce.subscription.suspended";
    public const string SubscriptionCancelled    = "commerce.subscription.cancelled";
    public const string SubscriptionRenewed      = "commerce.subscription.renewed";
    public const string SubscriptionTrialStarted = "commerce.subscription.trial.started";
    public const string SubscriptionTrialExpired = "commerce.subscription.trial.expired";

    // ── Entitlement changes ───────────────────────────────────────────────────
    public const string EntitlementGranted  = "commerce.entitlement.granted";
    public const string EntitlementRevoked  = "commerce.entitlement.revoked";
    public const string EntitlementChanged  = "commerce.entitlement.changed";

    // ── Billing standing ─────────────────────────────────────────────────────
    public const string BillingStandingChanged      = "commerce.billing.standing.changed";
    public const string BillingGracePeriodStarted   = "commerce.billing.gracePeriod.started";
    public const string BillingGracePeriodExpired   = "commerce.billing.gracePeriod.expired";

    // ── Access recommendation ─────────────────────────────────────────────────
    public const string AccessRecommendationChanged = "commerce.access.recommendation.changed";

    // ── Provisioning hooks ────────────────────────────────────────────────────
    public const string ProvisioningRequested   = "commerce.provisioning.requested";
    public const string DeprovisioningRequested = "commerce.deprovisioning.requested";
    public const string SuspensionRequested     = "commerce.suspension.requested";
    public const string ResumeRequested         = "commerce.resume.requested";

    // ── Integration health (emitted by consuming services) ────────────────────
    public const string IntegrationEntitlementCheckSucceeded = "commerce.integration.entitlement.check.succeeded";
    public const string IntegrationEntitlementCheckFailed    = "commerce.integration.entitlement.check.failed";
    public const string IntegrationEntitlementCheckSkipped   = "commerce.integration.entitlement.check.skipped";
}
