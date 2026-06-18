using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public class SellingPortfolioRepository : ISellingPortfolioRepository
{
    private readonly LiensDbContext _db;

    public SellingPortfolioRepository(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<SellingPortfolio?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.SellingPortfolios
            .Include(p => p.Liens)
            .Include(p => p.Buyers)
            .Include(p => p.StatusHistory)
            .Where(p => p.TenantId == tenantId && p.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<SellingPortfolio?> GetByPortfolioNumberAsync(Guid tenantId, string portfolioNumber, CancellationToken ct = default)
    {
        return await _db.SellingPortfolios
            .Where(p => p.TenantId == tenantId && p.PortfolioNumber == portfolioNumber)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(List<SellingPortfolio> Items, int TotalCount)> SearchAsync(
        Guid tenantId,
        Guid? sellerOrgId,
        string? search,
        string? status,
        Guid? buyerOrgId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var q = _db.SellingPortfolios
            .Include(p => p.Liens)
            .Include(p => p.Buyers)
            .Where(p => p.TenantId == tenantId);

        if (sellerOrgId.HasValue)
            q = q.Where(p => p.SellerOrgId == sellerOrgId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(p =>
                p.PortfolioNumber.Contains(term) ||
                p.Name.Contains(term) ||
                (p.Description != null && p.Description.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(p => p.Status == status);

        if (buyerOrgId.HasValue)
            q = q.Where(p => p.Buyers.Any(b => b.BuyerOrgId == buyerOrgId.Value));

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<List<SellingPortfolioStatusHistory>> GetStatusHistoryAsync(Guid tenantId, Guid portfolioId, CancellationToken ct = default)
    {
        return await _db.SellingPortfolioStatusHistory
            .Where(h => h.TenantId == tenantId && h.PortfolioId == portfolioId)
            .OrderBy(h => h.ChangedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(SellingPortfolio entity, CancellationToken ct = default)
    {
        await _db.SellingPortfolios.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(SellingPortfolio entity, CancellationToken ct = default)
    {
        foreach (var lien in entity.Liens)
        {
            var entry = _db.Entry(lien);
            if (entry.State == EntityState.Detached ||
                (entry.State == EntityState.Modified &&
                 !await _db.SellingPortfolioLiens.AnyAsync(existing => existing.Id == lien.Id, ct)))
            {
                await _db.SellingPortfolioLiens.AddAsync(lien, ct);
            }
        }

        foreach (var buyer in entity.Buyers)
        {
            var entry = _db.Entry(buyer);
            if (entry.State == EntityState.Detached ||
                (entry.State == EntityState.Modified &&
                 !await _db.SellingPortfolioBuyers.AnyAsync(existing => existing.Id == buyer.Id, ct)))
            {
                await _db.SellingPortfolioBuyers.AddAsync(buyer, ct);
            }
        }

        foreach (var history in entity.StatusHistory)
        {
            var entry = _db.Entry(history);
            if (entry.State == EntityState.Detached ||
                (entry.State == EntityState.Modified &&
                 !await _db.SellingPortfolioStatusHistory.AnyAsync(existing => existing.Id == history.Id, ct)))
            {
                await _db.SellingPortfolioStatusHistory.AddAsync(history, ct);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
