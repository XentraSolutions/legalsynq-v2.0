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
            // Primary role source (flat user→role mapping)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            // Phase 4: also load active ScopedRoleAssignments so GLOBAL-scoped roles
            // can be merged into the JWT role claims alongside UserRoles.
            .Include(u => u.ScopedRoleAssignments.Where(s => s.IsActive))
                .ThenInclude(s => s.Role)
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

        // TODO [LEGACY — Phase F]: UserRoles maps to user_roles (UserRole join entity),
        // which is the simple user-to-role table predating ScopedRoleAssignment.
        // New callers should create ScopedRoleAssignment (scope=GLOBAL) instead.
        // This path is retained for backward compatibility — do not add new callers.
        foreach (var roleId in roleIds)
            await _db.UserRoles.AddAsync(UserRole.Create(user.Id, roleId), ct);

        await _db.SaveChangesAsync(ct);
    }

    public Task<UserOrganizationMembership?> GetPrimaryOrgMembershipAsync(
        Guid userId, CancellationToken ct = default) =>
        _db.UserOrganizationMemberships
            // Chain 1: products → roles → Phase 3 org-type eligibility rules
            .Include(m => m.Organization)
                .ThenInclude(o => o.OrganizationProducts)
                    .ThenInclude(op => op.Product)
                        .ThenInclude(p => p.ProductRoles)
                            .ThenInclude(pr => pr.OrgTypeRules)
                                .ThenInclude(r => r.OrganizationType)
            // Chain 2: Phase 1 — canonical OrganizationType catalog record on the org itself
            .Include(m => m.Organization)
                .ThenInclude(o => o.OrganizationTypeRef)
            .Where(m => m.UserId == userId && m.IsActive)
            .OrderBy(m => m.JoinedAtUtc)
            .FirstOrDefaultAsync(ct);
}
