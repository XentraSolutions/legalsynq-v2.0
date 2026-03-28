using Identity.Domain;

namespace Identity.Application;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByTenantAndEmailAsync(Guid tenantId, string email, CancellationToken ct = default);
    Task<List<User>> GetAllWithRolesAsync(CancellationToken ct = default);
    Task AddAsync(User user, IReadOnlyList<Guid> roleIds, CancellationToken ct = default);
}
