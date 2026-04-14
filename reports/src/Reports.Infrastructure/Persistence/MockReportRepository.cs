using Microsoft.Extensions.Logging;
using Reports.Contracts.Persistence;

namespace Reports.Infrastructure.Persistence;

public sealed class MockReportRepository : IReportRepository
{
    private readonly ILogger<MockReportRepository> _log;

    public MockReportRepository(ILogger<MockReportRepository> log) => _log = log;

    public Task<string> SaveAsync(object reportEntity, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString();
        _log.LogDebug("MockReportRepository: Saved entity with id {Id}", id);
        return Task.FromResult(id);
    }

    public Task<object?> GetByIdAsync(string id, CancellationToken ct)
    {
        _log.LogDebug("MockReportRepository: GetById {Id}", id);
        return Task.FromResult<object?>(null);
    }

    public Task<IReadOnlyList<object>> ListByTenantAsync(string tenantId, int page, int pageSize, CancellationToken ct)
    {
        _log.LogDebug("MockReportRepository: ListByTenant {TenantId} page={Page}", tenantId, page);
        return Task.FromResult<IReadOnlyList<object>>(Array.Empty<object>());
    }
}
