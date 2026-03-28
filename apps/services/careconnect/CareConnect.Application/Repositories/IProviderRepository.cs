using CareConnect.Application.DTOs;
using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IProviderRepository
{
    Task<(List<Provider> Items, int TotalCount)> SearchAsync(Guid tenantId, GetProvidersQuery query, CancellationToken ct = default);
    Task<Provider?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(Provider provider, CancellationToken ct = default);
    Task UpdateAsync(Provider provider, CancellationToken ct = default);
    Task SyncCategoriesAsync(Guid providerId, List<Guid> categoryIds, CancellationToken ct = default);
}
