using Commerce.Contracts.Catalog;

namespace Commerce.Application.Catalog.Abstractions;

public interface IBundleCatalogService
{
    Task<BundleResponse> CreateAsync(CreateBundleRequest request, CancellationToken ct);
    Task<IReadOnlyList<BundleResponse>> ListAsync(CancellationToken ct);
    Task<BundleResponse> GetAsync(Guid id, CancellationToken ct);
    Task<BundleResponse> UpdateAsync(Guid id, UpdateBundleRequest request, CancellationToken ct);
    Task<BundleResponse> ActivateAsync(Guid id, CancellationToken ct);
    Task<BundleResponse> RetireAsync(Guid id, CancellationToken ct);

    Task<BundleItemResponse> AddItemAsync(Guid bundleId, AddBundleItemRequest request, CancellationToken ct);
    Task<IReadOnlyList<BundleItemResponse>> ListItemsAsync(Guid bundleId, CancellationToken ct);
    Task RemoveItemAsync(Guid bundleId, Guid itemId, CancellationToken ct);
}
