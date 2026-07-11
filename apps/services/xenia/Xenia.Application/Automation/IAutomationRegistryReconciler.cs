namespace Xenia.Application.Automation;

/// <summary>
/// Compares discovered (DI-registered) automation providers against the persisted
/// registry and produces a consistent, idempotent, concurrency-safe reconciliation.
///
/// Responsibilities:
///   - Insert newly discovered providers.
///   - Update compatible metadata (hash, version, reconciledAt).
///   - Preserve global enablement, tenant enablement, configuration, and runtime state.
///   - Mark persisted-but-undiscovered providers as Unavailable.
///   - Restore Unavailable providers when they are rediscovered.
///   - Never auto-delete persisted providers.
///   - Record reconciliation timestamps.
///
/// Contract:
///   - <see cref="ReconcileAsync"/> is idempotent; running it twice produces the same DB state.
///   - Concurrent calls are concurrency-safe via optimistic row_version.
/// </summary>
public interface IAutomationRegistryReconciler
{
    /// <summary>
    /// Runs the full reconciliation cycle.
    /// Returns the count of: inserted, updated, marked-unavailable, restored providers.
    /// </summary>
    Task<ReconciliationSummary> ReconcileAsync(CancellationToken ct = default);
}

public sealed record ReconciliationSummary
{
    public int Inserted         { get; init; }
    public int Updated          { get; init; }
    public int MarkedUnavailable { get; init; }
    public int Restored          { get; init; }
    public int Unchanged         { get; init; }
    public DateTime ReconciledAt { get; init; }
    public string InstanceId    { get; init; } = string.Empty;
}
