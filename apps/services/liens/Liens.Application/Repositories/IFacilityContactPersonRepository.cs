using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface IFacilityContactPersonRepository
{
    Task<FacilityContactPerson?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<List<FacilityContactPerson>> GetByFacilityAsync(Guid tenantId, Guid facilityId, CancellationToken ct = default);
    Task AddAsync(FacilityContactPerson entity, CancellationToken ct = default);
    Task UpdateAsync(FacilityContactPerson entity, CancellationToken ct = default);
}
