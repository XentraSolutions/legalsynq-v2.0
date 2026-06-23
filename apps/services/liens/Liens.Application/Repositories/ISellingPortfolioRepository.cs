using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ISellingPortfolioRepository
{
    Task<SellingPortfolio?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<SellingPortfolio?> GetByPortfolioNumberAsync(Guid tenantId, string portfolioNumber, CancellationToken ct = default);
    Task<(List<SellingPortfolio> Items, int TotalCount)> SearchAsync(
        Guid tenantId,
        Guid? sellerOrgId,
        string? search,
        string? status,
        Guid? buyerOrgId,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<List<SellingPortfolioStatusHistory>> GetStatusHistoryAsync(Guid tenantId, Guid portfolioId, CancellationToken ct = default);
    Task<bool> IsLienAssignedToPortfolioAsync(Guid tenantId, Guid lienId, CancellationToken ct = default);
    Task AddAsync(SellingPortfolio entity, CancellationToken ct = default);
    Task UpdateAsync(SellingPortfolio entity, CancellationToken ct = default);
}
