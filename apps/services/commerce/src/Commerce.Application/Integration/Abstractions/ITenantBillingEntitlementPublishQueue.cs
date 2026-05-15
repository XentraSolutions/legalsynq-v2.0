namespace Commerce.Application.Integration.Abstractions;

/// <summary>
/// TB-INT-03 — bounded in-process queue that decouples Commerce
/// lifecycle commits from the Tenant Billing HTTP publish call. The
/// queue is config-gated by
/// <c>Commerce:TenantBilling:AutoPublishEnabled</c> and disabled by
/// default; trigger sites enqueue post-commit and never await the
/// publisher inline.
///
/// <para>Contract: <see cref="Enqueue"/> never blocks, never throws,
/// and never reports failure outwards in a way that would cause the
/// caller to roll back its own commit. Duplicate
/// <see cref="TenantBillingEntitlementPublishWorkItem.BillingAccountId"/>
/// values are allowed; idempotency is the responsibility of Tenant
/// Billing's snapshot upsert.</para>
/// </summary>
public interface ITenantBillingEntitlementPublishQueue
{
    /// <summary>
    /// Try to add a work item. Returns the enqueue outcome — always
    /// synchronous. Trigger sites should log/metric the result and
    /// otherwise treat all outcomes as best-effort.
    /// </summary>
    EnqueueResult Enqueue(TenantBillingEntitlementPublishWorkItem item);

    /// <summary>
    /// Stream items as they arrive. Completes when the queue is
    /// closed (host shutdown). The hosted worker is the sole reader.
    /// </summary>
    IAsyncEnumerable<TenantBillingEntitlementPublishWorkItem> ReadAllAsync(
        CancellationToken cancellationToken);

    /// <summary>Configured maximum number of pending items.</summary>
    int Capacity { get; }

    /// <summary>Current number of pending items.</summary>
    int Depth { get; }

    /// <summary>
    /// Mirrors <c>Commerce:TenantBilling:AutoPublishEnabled</c>. When
    /// false, <see cref="Enqueue"/> short-circuits with
    /// <see cref="EnqueueResult.SkippedDisabled"/>.
    /// </summary>
    bool AutoPublishEnabled { get; }
}

/// <summary>
/// One row of work for the auto-publish worker. Carries only what the
/// worker needs to call
/// <see cref="ITenantBillingEntitlementPublisher.PublishForBillingAccountAsync"/>
/// and to log/metric the source of the publish.
/// </summary>
public sealed record TenantBillingEntitlementPublishWorkItem(
    Guid BillingAccountId,
    string TriggerSource,
    DateTime EnqueuedAtUtc,
    string? CorrelationId);

/// <summary>Outcome of a single <see cref="ITenantBillingEntitlementPublishQueue.Enqueue"/> call.</summary>
public enum EnqueueResult
{
    /// <summary>Item is accepted and will be processed by the worker.</summary>
    Accepted = 1,

    /// <summary>Auto-publish is disabled by config; nothing was enqueued.</summary>
    SkippedDisabled = 2,

    /// <summary>Bounded queue was full; item was dropped without blocking.</summary>
    DroppedQueueFull = 3,

    /// <summary>
    /// Item was rejected because the trigger source was empty or the
    /// billing account id was <see cref="Guid.Empty"/>. Indicates a
    /// caller bug; never returned for valid inputs.
    /// </summary>
    Invalid = 4,
}
