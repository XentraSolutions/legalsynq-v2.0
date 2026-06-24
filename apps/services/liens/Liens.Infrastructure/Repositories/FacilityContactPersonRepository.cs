using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public class FacilityContactPersonRepository : IFacilityContactPersonRepository
{
    private readonly LiensDbContext _db;

    public FacilityContactPersonRepository(LiensDbContext db) => _db = db;

    public async Task<FacilityContactPerson?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => await _db.FacilityContactPersons
            .Where(p => p.TenantId == tenantId && p.Id == id)
            .FirstOrDefaultAsync(ct);

    public async Task<List<FacilityContactPerson>> GetByFacilityAsync(
        Guid tenantId, Guid facilityId, CancellationToken ct = default)
        => await _db.FacilityContactPersons
            .Where(p => p.TenantId == tenantId && p.FacilityId == facilityId)
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .ToListAsync(ct);

    public async Task AddAsync(FacilityContactPerson entity, CancellationToken ct = default)
    {
        await _db.FacilityContactPersons.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(FacilityContactPerson entity, CancellationToken ct = default)
    {
        _db.FacilityContactPersons.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
