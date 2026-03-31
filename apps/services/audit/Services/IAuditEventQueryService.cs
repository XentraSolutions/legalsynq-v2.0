using PlatformAuditEventService.DTOs.Query;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Read-only query surface for persisted audit event records.
///
/// Implementations are responsible for:
/// - Applying pagination, sorting, and all filter predicates.
/// - Mapping persistence entities to response DTOs.
/// - Conditionally populating integrity hash and redacting network identifiers
///   based on the active <see cref="Configuration.QueryAuthOptions"/>.
///
/// This interface is intentionally separate from the ingest pipeline.
/// The write path is owned by <see cref="IAuditEventIngestionService"/>.
/// </summary>
public interface IAuditEventQueryService
{
    /// <summary>
    /// Retrieve a single audit event record by its stable public identifier.
    /// Returns null when no record with the given <paramref name="auditId"/> exists.
    /// </summary>
    /// <param name="auditId">The platform-assigned AuditId (not the surrogate Id).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AuditEventRecordResponse?> GetByAuditIdAsync(Guid auditId, CancellationToken ct = default);

    /// <summary>
    /// Execute a filtered, paginated query over persisted audit event records.
    ///
    /// All filter fields on <paramref name="request"/> are optional.
    /// Unset filters are ignored — only set fields narrow the result set.
    ///
    /// The response includes pagination metadata (<c>TotalCount</c>, <c>Page</c>,
    /// <c>PageSize</c>, <c>TotalPages</c>, <c>HasNext</c>, <c>HasPrev</c>) and
    /// time-range metadata (<c>EarliestOccurredAtUtc</c>, <c>LatestOccurredAtUtc</c>)
    /// covering the full filtered result set, not just the current page.
    /// </summary>
    /// <param name="request">Filter and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AuditEventQueryResponse> QueryAsync(AuditEventQueryRequest request, CancellationToken ct = default);
}
