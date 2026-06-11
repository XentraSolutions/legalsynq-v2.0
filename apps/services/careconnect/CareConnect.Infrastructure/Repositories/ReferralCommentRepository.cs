using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class ReferralCommentRepository : IReferralCommentRepository
{
    private readonly CareConnectDbContext _db;

    public ReferralCommentRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public Task<List<ReferralComment>> GetByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default) =>
        _db.ReferralComments
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ReferralId == referralId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(ReferralComment comment, CancellationToken ct = default)
    {
        await _db.ReferralComments.AddAsync(comment, ct);
        await _db.SaveChangesAsync(ct);
    }
}
