using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;

namespace Reports.Infrastructure.Adapters;

public sealed class MockProductDataAdapter : IProductDataAdapter
{
    private readonly ILogger<MockProductDataAdapter> _log;

    public MockProductDataAdapter(ILogger<MockProductDataAdapter> log) => _log = log;

    public Task<IReadOnlyList<string>> GetAvailableProductsAsync(string tenantId, CancellationToken ct)
    {
        _log.LogDebug("MockProductDataAdapter: GetAvailableProducts for {TenantId}", tenantId);
        return Task.FromResult<IReadOnlyList<string>>(new[] { "liens", "careconnect", "fund" });
    }

    public Task<object?> QueryProductDataAsync(string tenantId, string productCode, string queryKey, IDictionary<string, string>? parameters, CancellationToken ct)
    {
        _log.LogDebug("MockProductDataAdapter: QueryProductData {Product}/{QueryKey}", productCode, queryKey);
        return Task.FromResult<object?>(null);
    }
}
