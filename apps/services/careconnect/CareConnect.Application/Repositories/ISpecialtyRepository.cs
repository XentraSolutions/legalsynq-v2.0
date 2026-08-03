using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface ISpecialtyRepository
{
    Task<List<Specialty>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<Specialty?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Specialty?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<List<Specialty>> GetActiveByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<List<Specialty>> GetActiveByCodesAsync(IEnumerable<string> codes, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task AddAsync(Specialty specialty, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
