// LSCC-009: Activation request repository.
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class ActivationRequestRepository : IActivationRequestRepository
{
    private readonly CareConnectDbContext _db;

    public ActivationRequestRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<ActivationRequest?> GetByReferralAndProviderAsync(
        Guid referralId,
        Guid providerId,
        CancellationToken ct = default)
    {
        return await _db.ActivationRequests
            .FirstOrDefaultAsync(a => a.ReferralId == referralId && a.ProviderId == providerId, ct);
    }

    public async Task<ActivationRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.ActivationRequests
            .Include(a => a.Provider)
            .Include(a => a.Referral)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<List<ActivationRequest>> GetPendingAsync(CancellationToken ct = default)
    {
        return await _db.ActivationRequests
            .Where(a => a.Status == ActivationRequestStatus.Pending)
            .Include(a => a.Provider)
            .Include(a => a.Referral)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ActivationRequest request, CancellationToken ct = default)
    {
        await _db.ActivationRequests.AddAsync(request, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
    }
}
