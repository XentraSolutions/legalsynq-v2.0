using Commerce.Contracts.Integration;

namespace Commerce.Application.Integration.Abstractions;

/// <summary>
/// TB-INT-01 / TB-INT-02 — publishes a Commerce entitlement snapshot
/// to the canonical Tenant Billing service via its HTTP apply
/// contract. Output-only: no Commerce state is mutated regardless of
/// outcome. The bridge is config-gated; when disabled the publisher
/// returns <see cref="PublishEntitlementOutcome.Skipped"/> without
/// making a network call.
/// </summary>
public interface ITenantBillingEntitlementPublisher
{
    /// <summary>
    /// Build a snapshot for <paramref name="billingAccountId"/> via
    /// <see cref="ICommerceEntitlementSnapshotService"/>, then publish.
    /// Returns <see cref="PublishEntitlementOutcome.Skipped"/> if the
    /// account is unknown to Commerce.
    /// </summary>
    Task<PublishEntitlementResult> PublishForBillingAccountAsync(
        Guid billingAccountId,
        CancellationToken ct);

    /// <summary>
    /// Publish an already-built snapshot for the given resolved tenant id.
    /// Useful when the caller has already paid the cost of building the
    /// snapshot or wants to publish a hand-supplied tenant id.
    /// </summary>
    Task<PublishEntitlementResult> PublishSnapshotAsync(
        CommerceEntitlementSnapshot snapshot,
        Guid tenantId,
        CancellationToken ct);

    /// <summary>
    /// TB-INT-02 — Build the snapshot, resolve the tenant id and map
    /// the Tenant Billing apply payload <em>without</em> sending any
    /// HTTP request and without mutating Commerce state. Returns
    /// <c>null</c> if the billing account does not exist (caller
    /// surfaces 404).
    /// </summary>
    Task<PreviewEntitlementResult?> PreviewForBillingAccountAsync(
        Guid billingAccountId,
        CancellationToken ct);

    /// <summary>
    /// TB-INT-02 — Snapshot of publisher configuration / readiness for
    /// internal diagnostics. Never returns the internal token.
    /// </summary>
    Task<TenantBillingDiagnostics> GetDiagnosticsAsync(CancellationToken ct);
}

/// <summary>Outcome bucket for <see cref="PublishEntitlementResult"/>.</summary>
public enum PublishEntitlementOutcome
{
    /// <summary>Tenant Billing accepted the apply request (2xx).</summary>
    Published = 1,

    /// <summary>
    /// We deliberately did not call Tenant Billing — e.g. publisher
    /// disabled by config, billing account unknown to Commerce, or
    /// no resolvable GUID tenant id. Not an error.
    /// </summary>
    Skipped = 2,

    /// <summary>
    /// We attempted to call Tenant Billing and it returned a non-2xx
    /// status, threw, or timed out. Commerce state is unchanged.
    /// </summary>
    Failed = 3,
}

/// <summary>
/// Deterministic, log-friendly outcome of a single publish attempt.
/// </summary>
public sealed record PublishEntitlementResult(
    PublishEntitlementOutcome Outcome,
    Guid BillingAccountId,
    Guid? TenantId,
    int? HttpStatus,
    string Reason,
    string? ResponseBodySummary,
    int Attempts = 1)
{
    public bool IsSuccess => Outcome == PublishEntitlementOutcome.Published;

    public static PublishEntitlementResult Published(
        Guid billingAccountId, Guid tenantId, int httpStatus, int attempts = 1)
        => new(PublishEntitlementOutcome.Published, billingAccountId, tenantId,
            httpStatus, "published", null, attempts);

    public static PublishEntitlementResult Skipped(
        Guid billingAccountId, string reason, Guid? tenantId = null)
        => new(PublishEntitlementOutcome.Skipped, billingAccountId, tenantId,
            null, reason, null, 0);

    public static PublishEntitlementResult Failed(
        Guid billingAccountId, string reason,
        Guid? tenantId = null, int? httpStatus = null,
        string? responseBodySummary = null,
        int attempts = 1)
        => new(PublishEntitlementOutcome.Failed, billingAccountId, tenantId,
            httpStatus, reason, responseBodySummary, attempts);
}

/// <summary>
/// TB-INT-02 — preview result for a hypothetical publish call. Never
/// reflects an actual send.
/// </summary>
public sealed record PreviewEntitlementResult(
    Guid BillingAccountId,
    Guid? TenantId,
    bool CanPublish,
    string? SkipReason,
    TenantBillingPreviewPayload? TenantBillingPayload);

/// <summary>
/// TB-INT-02 — public, on-the-wire shape of the Tenant Billing apply
/// payload, exposed for the preview endpoint. Mirrors the internal
/// infrastructure DTO field-for-field.
/// </summary>
public sealed record TenantBillingPreviewPayload(
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
/// TB-INT-02 — non-secret view of publisher configuration + runtime
/// readiness state. Returned by the diagnostics endpoint.
/// </summary>
public sealed record TenantBillingDiagnostics(
    bool Enabled,
    bool BaseUrlConfigured,
    bool InternalTokenConfigured,
    int TimeoutSeconds,
    int RetryAttempts,
    int RetryDelayMilliseconds,
    bool CircuitBreakerEnabled,
    int CircuitBreakerFailures,
    int CircuitBreakerDurationSeconds,
    string CircuitBreakerState,
    string TargetRoute,
    string Mode,
    // TB-INT-03 — auto-publish posture.
    bool AutoPublishEnabled = false,
    int AutoPublishQueueCapacity = 0,
    int AutoPublishQueueDepth = 0,
    bool WorkerRegistered = false,
    // TB-INT-04 — durable outbox posture.
    bool OutboxEnabled = false,
    int OutboxBatchSize = 0,
    int OutboxPollSeconds = 0,
    int OutboxMaxAttempts = 0,
    int OutboxRetryBaseDelaySeconds = 0,
    int OutboxPendingCount = 0,
    int OutboxFailedCount = 0,
    int OutboxProcessingCount = 0,
    int OutboxAbandonedCount = 0,
    int OutboxPublishedCount = 0,
    bool OutboxWorkerRegistered = false);
