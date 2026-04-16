using SynqComm.Application.Repositories;
using SynqComm.Domain.Entities;
using SynqComm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SynqComm.Infrastructure.Repositories;

public class ExternalParticipantIdentityRepository : IExternalParticipantIdentityRepository
{
    private readonly SynqCommDbContext _db;

    public ExternalParticipantIdentityRepository(SynqCommDbContext db)
    {
        _db = db;
    }

    public async Task<ExternalParticipantIdentity?> FindByEmailAsync(Guid tenantId, string normalizedEmail, CancellationToken ct = default)
    {
        return await _db.ExternalParticipantIdentities
            .Where(e => e.TenantId == tenantId && e.NormalizedEmail == normalizedEmail && e.IsActive)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(ExternalParticipantIdentity entity, CancellationToken ct = default)
    {
        await _db.ExternalParticipantIdentities.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ExternalParticipantIdentity entity, CancellationToken ct = default)
    {
        _db.ExternalParticipantIdentities.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
