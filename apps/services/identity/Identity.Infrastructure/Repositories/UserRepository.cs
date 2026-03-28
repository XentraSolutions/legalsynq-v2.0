using Identity.Application;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _db;

    public UserRepository(IdentityDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken ct = default) =>
        _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByTenantAndEmailAsync(Guid tenantId, string email, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == email, ct);

    public Task<List<User>> GetAllWithRolesAsync(CancellationToken ct = default) =>
        _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(ct);

    public async Task AddAsync(User user, IReadOnlyList<Guid> roleIds, CancellationToken ct = default)
    {
        await _db.Users.AddAsync(user, ct);

        foreach (var roleId in roleIds)
            await _db.UserRoles.AddAsync(UserRole.Create(user.Id, roleId), ct);

        await _db.SaveChangesAsync(ct);
    }

    public Task<UserOrganizationMembership?> GetPrimaryOrgMembershipAsync(
        Guid userId, CancellationToken ct = default) =>
        _db.UserOrganizationMemberships
            .Include(m => m.Organization)
                .ThenInclude(o => o.OrganizationProducts)
                    .ThenInclude(op => op.Product)
                        .ThenInclude(p => p.ProductRoles)
            .Where(m => m.UserId == userId && m.IsActive)
            .OrderBy(m => m.JoinedAtUtc)
            .FirstOrDefaultAsync(ct);
}
