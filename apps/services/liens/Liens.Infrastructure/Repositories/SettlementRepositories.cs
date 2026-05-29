using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public class LienReductionRepository : ILienReductionRepository
{
    private readonly LiensDbContext _db;
    public LienReductionRepository(LiensDbContext db) => _db = db;

    public Task<LienReduction?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.LienReductions.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id && !r.IsDeleted, ct);

    public Task<List<LienReduction>> GetByCaseIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default) =>
        _db.LienReductions
            .Where(r => r.TenantId == tenantId && r.CaseId == caseId && !r.IsDeleted)
            .OrderByDescending(r => r.ReductionDate)
            .ToListAsync(ct);

    public Task<List<LienReduction>> GetByLienIdAsync(Guid tenantId, Guid lienId, CancellationToken ct = default) =>
        _db.LienReductions
            .Where(r => r.TenantId == tenantId && r.LienId == lienId && !r.IsDeleted)
            .OrderByDescending(r => r.ReductionDate)
            .ToListAsync(ct);

    public async Task AddAsync(LienReduction reduction, CancellationToken ct = default)
    {
        _db.LienReductions.Add(reduction);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LienReduction reduction, CancellationToken ct = default)
    {
        _db.LienReductions.Update(reduction);
        await _db.SaveChangesAsync(ct);
    }
}

public class LienSettlementRepository : ILienSettlementRepository
{
    private readonly LiensDbContext _db;
    public LienSettlementRepository(LiensDbContext db) => _db = db;

    public Task<LienSettlement?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.LienSettlements.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id && !s.IsDeleted, ct);

    public Task<List<LienSettlement>> GetByCaseIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default) =>
        _db.LienSettlements
            .Where(s => s.TenantId == tenantId && s.CaseId == caseId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<List<LienSettlement>> GetByLienIdAsync(Guid tenantId, Guid lienId, CancellationToken ct = default) =>
        _db.LienSettlements
            .Where(s => s.TenantId == tenantId && s.LienId == lienId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task AddAsync(LienSettlement settlement, CancellationToken ct = default)
    {
        _db.LienSettlements.Add(settlement);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LienSettlement settlement, CancellationToken ct = default)
    {
        _db.LienSettlements.Update(settlement);
        await _db.SaveChangesAsync(ct);
    }
}

public class SettlementPaymentDetailRepository : ISettlementPaymentDetailRepository
{
    private readonly LiensDbContext _db;
    public SettlementPaymentDetailRepository(LiensDbContext db) => _db = db;

    public Task<SettlementPaymentDetail?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.SettlementPaymentDetails.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id && !p.IsDeleted, ct);

    public Task<List<SettlementPaymentDetail>> GetByCaseIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default) =>
        _db.SettlementPaymentDetails
            .Where(p => p.TenantId == tenantId && p.CaseId == caseId && !p.IsDeleted)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(ct);

    public Task<List<SettlementPaymentDetail>> GetByLienIdAsync(Guid tenantId, Guid lienId, CancellationToken ct = default) =>
        _db.SettlementPaymentDetails
            .Where(p => p.TenantId == tenantId && p.LienId == lienId && !p.IsDeleted)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(ct);

    public async Task AddAsync(SettlementPaymentDetail detail, CancellationToken ct = default)
    {
        _db.SettlementPaymentDetails.Add(detail);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(SettlementPaymentDetail detail, CancellationToken ct = default)
    {
        _db.SettlementPaymentDetails.Update(detail);
        await _db.SaveChangesAsync(ct);
    }
}
