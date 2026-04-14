namespace Reports.Contracts.Persistence;

public interface IReportRepository
{
    Task<string> SaveAsync(object reportEntity, CancellationToken ct = default);
    Task<object?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<object>> ListByTenantAsync(string tenantId, int page = 1, int pageSize = 20, CancellationToken ct = default);
}
