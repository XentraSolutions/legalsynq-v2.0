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
            // Step 6 Phase B: ScopedRoleAssignments (GLOBAL) is now the primary role source.
            // UserRoles kept for fallback until all environments run migration 20260330200002.
            .Include(u => u.ScopedRoleAssignments.Where(s => s.IsActive))
                .ThenInclude(s => s.Role)
            // Fallback source: legacy UserRoles.
            // TODO [Phase G — UserRoles Retirement]: Remove this Include once ScopedRoleAssignment
            //   coverage is confirmed at 100% on all environments and the dual-write period ends.
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByTenantAndEmailAsync(Guid tenantId, string email, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == email, ct);

    public Task<List<User>> GetAllWithRolesAsync(CancellationToken ct = default) =>
        _db.Users
            // Step 6 Phase B: load ScopedRoleAssignments as primary source.
            .Include(u => u.ScopedRoleAssignments.Where(s => s.IsActive))
                .ThenInclude(s => s.Role)
            // TODO [Phase G — UserRoles Retirement]: Remove once ScopedRoleAssignment
            //   coverage is confirmed 100% on all environments.
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(ct);

    public async Task AddAsync(User user, IReadOnlyList<Guid> roleIds, CancellationToken ct = default)
    {
        await _db.Users.AddAsync(user, ct);

        foreach (var roleId in roleIds)
        {
            // TODO [Phase G — UserRoles Retirement]: Remove this UserRoles write once all environments
            //   have run migration 20260330200002 and ScopedRoleAssignment coverage is 100%.
            //   After removal: only the ScopedRoleAssignment insert below is needed.
            await _db.UserRoles.AddAsync(UserRole.Create(user.Id, roleId), ct);

            // Phase 4: dual-write — also create a GLOBAL-scoped ScopedRoleAssignment for every
            // role so the modern table is kept in sync from the first creation.
            // This is additive; both records co-exist during the incremental migration period.
            var scoped = ScopedRoleAssignment.Create(
                userId:  user.Id,
                roleId:  roleId,
                scopeType: ScopedRoleAssignment.ScopeTypes.Global);

            await _db.ScopedRoleAssignments.AddAsync(scoped, ct);
        }

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
