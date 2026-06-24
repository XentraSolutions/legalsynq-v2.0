using Commerce.Contracts.Catalog;

namespace Commerce.Application.Catalog.Abstractions;

public interface IFeatureCatalogService
{
    Task<FeatureResponse> CreateAsync(Guid productId, CreateFeatureRequest request, CancellationToken ct);
    Task<IReadOnlyList<FeatureResponse>> ListByProductAsync(Guid productId, CancellationToken ct);
    Task<FeatureResponse> UpdateAsync(Guid id, UpdateFeatureRequest request, CancellationToken ct);
    Task<FeatureResponse> ActivateAsync(Guid id, CancellationToken ct);
    Task<FeatureResponse> RetireAsync(Guid id, CancellationToken ct);
}
