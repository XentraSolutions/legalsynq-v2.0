using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<List<Contact>> GetByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<(List<Contact> Items, int TotalCount)> SearchAsync(Guid tenantId, string? search, string? contactType, bool? isActive, int page, int pageSize, Guid? lawFirmId = null, Guid? facilityId = null, string? contactSubtype = null, CancellationToken ct = default);
    Task<(List<Contact> Items, int TotalCount)> SearchFacilityContactsAsync(Guid tenantId, string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default);
    Task<List<Contact>> GetAllByTypeAsync(Guid tenantId, string? contactType, bool? isActive, CancellationToken ct = default);
    Task<Dictionary<Guid, int>> GetActiveCaseCountsAsync(Guid tenantId, IReadOnlyCollection<Contact> contacts, CancellationToken ct = default);
    Task<Contact?> GetFacilityContactByReferenceAsync(Guid tenantId, Guid facilityReferenceId, CancellationToken ct = default);
    Task<Contact?> GetFacilityContactByNameAsync(Guid tenantId, string facilityName, CancellationToken ct = default);
    Task<List<Contact>> GetByFacilityAsync(Guid tenantId, Guid facilityId, string? contactSubtype = null, bool? isActive = true, CancellationToken ct = default);
    Task AddAsync(Contact entity, CancellationToken ct = default);
    Task UpdateAsync(Contact entity, CancellationToken ct = default);
}
