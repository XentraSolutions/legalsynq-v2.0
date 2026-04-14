using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ILienOfferRepository
{
    Task<LienOffer?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<List<LienOffer>> GetByLienIdAsync(Guid tenantId, Guid lienId, CancellationToken ct = default);
    Task<(List<LienOffer> Items, int TotalCount)> SearchAsync(Guid tenantId, Guid? lienId, string? status, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(LienOffer entity, CancellationToken ct = default);
    Task UpdateAsync(LienOffer entity, CancellationToken ct = default);
}
