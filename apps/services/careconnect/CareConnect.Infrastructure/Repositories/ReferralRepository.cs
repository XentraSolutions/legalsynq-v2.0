// BLK-PERF-01: All read-only queries use AsNoTracking() to avoid EF Core change-tracking overhead.
using CareConnect.Application.DTOs;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class ReferralRepository : IReferralRepository
{
    private readonly CareConnectDbContext _db;

    public ReferralRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<(List<Referral> Items, int TotalCount)> SearchAsync(Guid tenantId, GetReferralsQuery query, CancellationToken ct = default)
    {
        IQueryable<Referral> q;

        if (query.CrossTenantReceiver && query.ReceivingOrgId.HasValue)
        {
            q = _db.Referrals
                .AsNoTracking()
                .Where(r => r.ReceivingOrganizationId == query.ReceivingOrgId.Value);
        }
        else
        {
            q = _db.Referrals
                .AsNoTracking()
                .Where(r => r.TenantId == tenantId);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(r => r.Status == query.Status);

        if (query.ProviderId.HasValue)
            q = q.Where(r => r.ProviderId == query.ProviderId.Value);

        if (!string.IsNullOrWhiteSpace(query.ClientName))
        {
            var name = query.ClientName.Trim().ToLower();
            q = q.Where(r =>
                r.ClientFirstName.ToLower().Contains(name) ||
                r.ClientLastName.ToLower().Contains(name) ||
                (r.ClientFirstName.ToLower() + " " + r.ClientLastName.ToLower()).Contains(name));
        }

        if (!string.IsNullOrWhiteSpace(query.CaseNumber))
        {
            var cn = query.CaseNumber.Trim().ToLower();
            q = q.Where(r => r.CaseNumber != null && r.CaseNumber.ToLower().Contains(cn));
        }

        if (!string.IsNullOrWhiteSpace(query.Urgency))
            q = q.Where(r => r.Urgency == query.Urgency);

        if (query.CreatedFrom.HasValue)
            q = q.Where(r => r.CreatedAtUtc >= query.CreatedFrom.Value);

        if (query.CreatedTo.HasValue)
            q = q.Where(r => r.CreatedAtUtc <= query.CreatedTo.Value);

        if (query.ReferringOrgId.HasValue)
            q = q.Where(r => r.ReferringOrganizationId == query.ReferringOrgId.Value);

        if (!query.CrossTenantReceiver && query.ReceivingOrgId.HasValue)
            q = q.Where(r => r.ReceivingOrganizationId == query.ReceivingOrgId.Value);

        var totalCount = await q.CountAsync(ct);

        var skip = (query.Page - 1) * query.PageSize;
        var items = await q
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip(skip)
            .Take(query.PageSize)
            .Include(r => r.Provider)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Referral?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.Referrals
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Id == id)
            .Include(r => r.Provider)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Referral?> GetByIdGlobalAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Referrals
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Include(r => r.Provider)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(Referral referral, CancellationToken ct = default)
    {
        await _db.Referrals.AddAsync(referral, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Referral referral, ReferralStatusHistory? history = null, ReferralProviderReassignment? providerReassignment = null, CancellationToken ct = default)
    {
        _db.Referrals.Update(referral);

        if (history is not null)
            await _db.ReferralStatusHistories.AddAsync(history, ct);

        if (providerReassignment is not null)
            await _db.ReferralProviderReassignments.AddAsync(providerReassignment, ct);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<ReferralStatusHistory>> GetHistoryByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default)
    {
        return await _db.ReferralStatusHistories
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.ReferralId == referralId)
            .OrderByDescending(h => h.ChangedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddProviderReassignmentAsync(ReferralProviderReassignment reassignment, CancellationToken ct = default)
    {
        await _db.ReferralProviderReassignments.AddAsync(reassignment, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<ReferralProviderReassignment>> GetProviderReassignmentsByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default)
    {
        return await _db.ReferralProviderReassignments
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.ReferralId == referralId)
            .OrderBy(r => r.ReassignedAtUtc)
            .ToListAsync(ct);
    }
}
