using Commerce.Contracts.Catalog;

namespace Commerce.Application.Catalog.Abstractions;

public interface IProductCatalogService
{
    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct);
    Task<IReadOnlyList<ProductResponse>> ListAsync(CancellationToken ct);
    Task<ProductResponse> GetAsync(Guid id, CancellationToken ct);
    Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct);
    Task<ProductResponse> ActivateAsync(Guid id, CancellationToken ct);
    Task<ProductResponse> RetireAsync(Guid id, CancellationToken ct);
}
