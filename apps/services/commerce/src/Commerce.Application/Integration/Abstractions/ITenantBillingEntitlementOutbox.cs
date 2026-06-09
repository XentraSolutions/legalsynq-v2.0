namespace Commerce.Application.Integration.Abstractions;

/// <summary>
/// TB-INT-04 — durable outbox of pending Commerce → Tenant Billing
/// entitlement publish work. Trigger sites call
/// <see cref="EnqueueAsync"/> post-commit; the background processor
/// reads <c>Pending</c> rows whose <c>NextAttemptAtUtc</c> has come
/// due. Implementations must never throw out of the enqueue path
/// for any reason that would force the caller to roll back its
/// already-committed Commerce transaction.
/// </summary>
public interface ITenantBillingEntitlementOutbox
{
    /// <summary>
    /// Persist a new outbox row. Returns the new row id on success
    /// or <c>Guid.Empty</c> if the call short-circuited (invalid
    /// input). Any persistence exception is swallowed and surfaced
    /// to the caller as <c>Guid.Empty</c> with logging — this is by
    /// design (see class summary).
    /// </summary>
    Task<Guid> EnqueueAsync(
        Guid billingAccountId,
        string triggerSource,
        string? correlationId,
        CancellationToken ct);

    /// <summary>
    /// Cheap counts grouped by status, for the diagnostics endpoint.
    /// </summary>
    Task<TenantBillingEntitlementOutboxCounts> GetCountsAsync(CancellationToken ct);
}

/// <summary>
/// Status-bucket counts surfaced via diagnostics.
/// </summary>
public sealed record TenantBillingEntitlementOutboxCounts(
    int Pending,
    int Processing,
    int Published,
    int Failed,
    int Abandoned);
