using Identity.Domain;

namespace Identity.Application;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default);
}
