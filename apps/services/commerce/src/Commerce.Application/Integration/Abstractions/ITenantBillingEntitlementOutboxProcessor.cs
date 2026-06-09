namespace Commerce.Application.Integration.Abstractions;

/// <summary>
/// TB-INT-04 — drains due
/// <see cref="ITenantBillingEntitlementOutbox"/> rows and dispatches
/// each through <see cref="ITenantBillingEntitlementPublisher"/>.
/// Implementations must never throw unhandled exceptions out of
/// <see cref="ProcessDueAsync"/>; per-row failures are recorded and
/// the next row is processed.
/// </summary>
public interface ITenantBillingEntitlementOutboxProcessor
{
    /// <summary>
    /// Process up to <paramref name="batchSize"/> due rows in one
    /// pass. Returns a summary of what was attempted and the final
    /// outcome bucket counts.
    /// </summary>
    Task<TenantBillingEntitlementOutboxBatchResult> ProcessDueAsync(
        int batchSize,
        CancellationToken ct);
}

public sealed record TenantBillingEntitlementOutboxBatchResult(
    int Considered,
    int Recovered,
    int Published,
    int Retried,
    int Abandoned,
    int Skipped);
