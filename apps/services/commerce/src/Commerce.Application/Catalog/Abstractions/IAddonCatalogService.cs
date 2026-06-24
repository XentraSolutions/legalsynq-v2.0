using Commerce.Contracts.Catalog;

namespace Commerce.Application.Catalog.Abstractions;

public interface IAddonCatalogService
{
    Task<AddonResponse> CreateAsync(CreateAddonRequest request, CancellationToken ct);
    Task<IReadOnlyList<AddonResponse>> ListAsync(CancellationToken ct);
    Task<AddonResponse> GetAsync(Guid id, CancellationToken ct);
    Task<AddonResponse> UpdateAsync(Guid id, UpdateAddonRequest request, CancellationToken ct);
    Task<AddonResponse> ActivateAsync(Guid id, CancellationToken ct);
    Task<AddonResponse> RetireAsync(Guid id, CancellationToken ct);
}
