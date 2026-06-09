using Commerce.Contracts.Catalog;

namespace Commerce.Application.Catalog.Abstractions;

public interface IPriceCatalogService
{
    Task<PriceResponse> CreateAsync(CreatePriceRequest request, CancellationToken ct);
    Task<IReadOnlyList<PriceResponse>> ListAsync(CancellationToken ct);
    Task<PriceResponse> GetAsync(Guid id, CancellationToken ct);
    Task<PriceResponse> UpdateAsync(Guid id, UpdatePriceRequest request, CancellationToken ct);
    Task<PriceResponse> ActivateAsync(Guid id, CancellationToken ct);
    Task<PriceResponse> RetireAsync(Guid id, CancellationToken ct);
}
