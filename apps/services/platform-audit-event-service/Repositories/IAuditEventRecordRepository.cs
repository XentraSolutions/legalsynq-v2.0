using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Entities;
using AuditRecordQueryRequest = PlatformAuditEventService.DTOs.Query.AuditEventQueryRequest;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// Persistence contract for canonical audit event records.
///
/// Append-only contract: no update or delete operations are exposed.
/// The only exception is that the ingest service may compute and update
/// the Hash/PreviousHash fields immediately after the initial append;
/// that is handled by appending a fully-populated entity (with hashes set
/// by the service before calling AppendAsync).
/// </summary>
public interface IAuditEventRecordRepository
{
    /// <summary>
    /// Persist a new, fully-populated audit event record.
    /// Throws on duplicate IdempotencyKey (unique index violation).
    /// </summary>
    Task<AuditEventRecord> AppendAsync(AuditEventRecord record, CancellationToken ct = default);

    /// <summary>
    /// Retrieve a single record by its public <see cref="AuditEventRecord.AuditId"/>.
    /// Returns null if not found.
    /// </summary>
    Task<AuditEventRecord?> GetByAuditIdAsync(Guid auditId, CancellationToken ct = default);

    /// <summary>
    /// Checks whether a record with the given idempotency key already exists.
    /// Used by the ingest pipeline before persisting to perform a lightweight dedup probe.
    /// Returns false when <paramref name="key"/> is null or empty.
    /// </summary>
    Task<bool> ExistsIdempotencyKeyAsync(string? key, CancellationToken ct = default);

    /// <summary>
    /// Execute a filtered, paginated query over persisted audit event records.
    /// TenantId is the primary isolation boundary; callers must enforce scope
    /// before passing a query object.
    /// </summary>
    Task<PagedResult<AuditEventRecord>> QueryAsync(
        AuditRecordQueryRequest query,
        CancellationToken ct = default);

    /// <summary>
    /// Return the total number of persisted audit event records.
    /// Used for health/diagnostic reporting.
    /// </summary>
    Task<long> CountAsync(CancellationToken ct = default);

    /// <summary>
    /// Return the most recent record in a (TenantId, SourceSystem) chain.
    /// Used by the ingest service to populate PreviousHash for chain integrity.
    /// Returns null if no prior record exists in the chain.
    /// </summary>
    Task<AuditEventRecord?> GetLatestInChainAsync(
        string? tenantId,
        string sourceSystem,
        CancellationToken ct = default);

    /// <summary>
    /// Stream filtered audit event records as an async enumerable.
    ///
    /// Intended for the export worker, which must iterate potentially millions of
    /// records without loading the full result set into memory. Unlike
    /// <see cref="QueryAsync"/>, this method does not paginate — the caller is
    /// responsible for writing each record to the output stream as it arrives.
    ///
    /// Ordering: ascending by OccurredAtUtc, then by Id (insertion order) for
    /// deterministic, reproducible export files.
    ///
    /// The pagination fields (Page, PageSize, SortBy, SortDescending) on the
    /// <paramref name="filter"/> object are ignored; only filter predicates apply.
    ///
    /// Caller must consume the enumerable within the scope of a single request /
    /// background job — the underlying DbContext is disposed when the enumerable
    /// is fully consumed or the cancellation token fires.
    /// </summary>
    IAsyncEnumerable<AuditEventRecord> StreamForExportAsync(
        AuditRecordQueryRequest filter,
        CancellationToken ct = default);
}
