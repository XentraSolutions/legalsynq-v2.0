namespace Reports.Contracts.Adapters;

public interface IProductDataAdapter
{
    Task<IReadOnlyList<string>> GetAvailableProductsAsync(string tenantId, CancellationToken ct = default);
    Task<object?> QueryProductDataAsync(string tenantId, string productCode, string queryKey, IDictionary<string, string>? parameters = null, CancellationToken ct = default);
}
