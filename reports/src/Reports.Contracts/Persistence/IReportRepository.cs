namespace Reports.Contracts.Persistence;

/// <summary>
/// Bootstrap placeholder — persistence contract for report entities.
/// <para>
/// This interface is scaffolding introduced in Epic 00 (LS-REPORTS-00-001).
/// It is NOT a finalized persistence contract. Method signatures use
/// <c>object</c> intentionally to avoid premature type coupling.
/// Expect this interface to be redesigned when MySQL integration begins
/// in LS-REPORTS-00-002+.
/// </para>
/// </summary>
public interface IReportRepository
{
    Task<string> SaveAsync(object reportEntity, CancellationToken ct = default);
    Task<object?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<object>> ListByTenantAsync(string tenantId, int page = 1, int pageSize = 20, CancellationToken ct = default);
}
