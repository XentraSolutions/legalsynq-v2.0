namespace Billing.Domain.Accounting.Erp.BulkImport;

/// <summary>
/// MS-BILL-ERP-006 — Tenant-scoped persistence contract for the
/// import-history audit row. Every method takes an explicit
/// <c>tenantId</c> as the first argument; the implementation MUST
/// apply <c>Where(x =&gt; x.TenantId == tenantId)</c> at the SQL
/// level so cross-tenant probes silently return empty rather than
/// 403.
/// </summary>
public interface IBulkMappingImportHistoryRepository
{
    /// <summary>
    /// Append a new audit row. Throws
    /// <see cref="BulkMappingImportReplayException"/> on the unique
    /// <c>(TenantId, IdempotencyKey)</c> violation so the service can
    /// short-circuit a replayed commit BEFORE re-executing any
    /// mapping writes.
    /// </summary>
    Task AppendAsync(BulkMappingImportHistory row, CancellationToken ct = default);

    /// <summary>
    /// Update the totals + summary on an already-reserved audit row.
    /// Used by the reserve-first commit flow so the audit row is
    /// guaranteed to exist before any per-row mapping write runs.
    /// </summary>
    Task FinalizeAsync(
        Guid tenantId,
        Guid historyId,
        DateTime completedAtUtc,
        int acceptedRows,
        int rejectedRows,
        string summaryJson,
        CancellationToken ct = default);

    /// <summary>
    /// Idempotency lookup. Returns the prior audit row if the same
    /// tenant has already committed a bulk import under
    /// <paramref name="idempotencyKey"/>; null otherwise.
    /// </summary>
    Task<BulkMappingImportHistory?> FindByIdempotencyKeyAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken ct = default);

    Task<IReadOnlyList<BulkMappingImportHistory>> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default);
}

/// <summary>
/// Thrown by <see cref="IBulkMappingImportHistoryRepository.AppendAsync"/>
/// when a row with the same <c>(TenantId, IdempotencyKey)</c> is
/// already persisted. The service catches this, fetches the prior
/// row, and replays the original outcome instead of re-executing
/// the commit.
/// </summary>
public sealed class BulkMappingImportReplayException : InvalidOperationException
{
    public BulkMappingImportReplayException(string message) : base(message) { }
}
