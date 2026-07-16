using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public class LienRepository : ILienRepository
{
    private readonly LiensDbContext _db;

    public LienRepository(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<Lien?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.Liens
            .Where(l => l.TenantId == tenantId && l.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Lien?> GetByIdAnyTenantAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Liens
            .Where(l => l.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Lien?> GetByLienNumberAsync(Guid tenantId, string lienNumber, CancellationToken ct = default)
    {
        return await _db.Liens
            .Where(l => l.TenantId == tenantId && l.LienNumber == lienNumber)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(List<Lien> Items, int TotalCount)> SearchAsync(
        Guid tenantId, string? search, string? status, string? lienType,
        Guid? caseId, Guid? facilityId,
        int page, int pageSize,
        CancellationToken ct = default,
        DateTime? createdFromUtc = null,
        DateTime? createdToUtc = null,
        Guid? visibleOrgId = null,
        bool includeSellerOrg = false,
        bool includeBuyerOrg = false,
        bool includeHolderOrg = false,
        bool includeMarketplace = false)
    {
        var q = _db.Liens.Where(l => l.TenantId == tenantId);

        if (visibleOrgId.HasValue)
        {
            var orgId = visibleOrgId.Value;
            q = q.Where(l =>
                (includeSellerOrg && (l.OrgId == orgId || l.SellingOrgId == orgId)) ||
                (includeBuyerOrg && l.BuyingOrgId == orgId) ||
                (includeHolderOrg && l.HoldingOrgId == orgId) ||
                (includeMarketplace && (l.Status == LienStatus.Offered || l.Status == LienStatus.UnderReview)));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(l =>
                l.LienNumber.Contains(term) ||
                (l.SubjectFirstName != null && l.SubjectFirstName.Contains(term)) ||
                (l.SubjectLastName != null && l.SubjectLastName.Contains(term)) ||
                (l.Description != null && l.Description.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(l => l.Status == status);

        if (!string.IsNullOrWhiteSpace(lienType))
            q = q.Where(l => l.LienType == lienType);

        if (caseId.HasValue)
            q = q.Where(l => l.CaseId == caseId.Value);

        if (facilityId.HasValue)
            q = q.Where(l => l.FacilityId == facilityId.Value);

        if (createdFromUtc.HasValue)
            q = q.Where(l => l.CreatedAtUtc >= createdFromUtc.Value);

        if (createdToUtc.HasValue)
            q = q.Where(l => l.CreatedAtUtc <= createdToUtc.Value);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(List<Lien> PageItems, List<Lien> AllItems, int TotalCount)> SearchReportAsync(
        Guid tenantId,
        string? search,
        IReadOnlyCollection<string> lienStatuses,
        IReadOnlyCollection<string> caseStatuses,
        DateOnly? purchaseDateFrom,
        DateOnly? purchaseDateTo,
        DateTime? closedDateFrom,
        DateTime? closedDateTo,
        string? isBulk,
        IReadOnlyCollection<Guid> caseIds,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var q = _db.Liens.Where(l => l.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(l =>
                l.LienNumber.Contains(term) ||
                (l.SubjectFirstName != null && l.SubjectFirstName.Contains(term)) ||
                (l.SubjectLastName != null && l.SubjectLastName.Contains(term)) ||
                (l.Description != null && l.Description.Contains(term)));
        }

        if (lienStatuses.Count > 0)
        {
            var statusList = lienStatuses.ToList();
            q = q.Where(l => statusList.Contains(l.Status));
        }

        if (caseStatuses.Count > 0)
        {
            var statusList = caseStatuses.ToList();
            q = q.Where(l =>
                l.CaseId.HasValue &&
                _db.Cases.Any(c =>
                    c.TenantId == tenantId &&
                    c.Id == l.CaseId.Value &&
                    statusList.Contains(c.Status)));
        }

        if (purchaseDateFrom.HasValue)
            q = q.Where(l => l.IncidentDate.HasValue && l.IncidentDate.Value >= purchaseDateFrom.Value);

        if (purchaseDateTo.HasValue)
            q = q.Where(l => l.IncidentDate.HasValue && l.IncidentDate.Value <= purchaseDateTo.Value);

        if (closedDateFrom.HasValue)
            q = q.Where(l => l.ClosedAtUtc.HasValue && l.ClosedAtUtc.Value >= closedDateFrom.Value);

        if (closedDateTo.HasValue)
            q = q.Where(l => l.ClosedAtUtc.HasValue && l.ClosedAtUtc.Value <= closedDateTo.Value);

        if (!string.IsNullOrWhiteSpace(isBulk))
        {
            var bulk = isBulk.Trim();
            q = q.Where(l => l.IsBulk == bulk);
        }

        if (caseIds.Count > 0)
        {
            var ids = caseIds.ToList();
            q = q.Where(l => l.CaseId.HasValue && ids.Contains(l.CaseId.Value));
        }

        var ordered = q.OrderByDescending(l => l.CreatedAtUtc);
        var totalCount = await ordered.CountAsync(ct);
        var allItems = await ordered.ToListAsync(ct);
        var pageItems = allItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (pageItems, allItems, totalCount);
    }

    public async Task<List<Lien>> GetByCaseIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        return await _db.Liens
            .Where(l => l.TenantId == tenantId && l.CaseId == caseId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<Lien>> GetByFacilityIdAsync(Guid tenantId, Guid facilityId, CancellationToken ct = default)
    {
        return await _db.Liens
            .Where(l => l.TenantId == tenantId && l.FacilityId == facilityId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Lien entity, CancellationToken ct = default)
    {
        await _db.Liens.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Lien entity, CancellationToken ct = default)
    {
        _db.Liens.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
