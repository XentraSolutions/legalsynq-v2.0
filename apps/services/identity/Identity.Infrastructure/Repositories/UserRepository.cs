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
            // Phase G: ScopedRoleAssignments is the sole authoritative role source.
            .Include(u => u.ScopedRoleAssignments.Where(s => s.IsActive))
                .ThenInclude(s => s.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByTenantAndEmailAsync(Guid tenantId, string email, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == email, ct);

    public Task<List<User>> GetAllWithRolesAsync(CancellationToken ct = default) =>
        _db.Users
            // Phase G: ScopedRoleAssignments is the sole authoritative role source.
            .Include(u => u.ScopedRoleAssignments.Where(s => s.IsActive))
                .ThenInclude(s => s.Role)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(ct);

    public async Task AddAsync(User user, IReadOnlyList<Guid> roleIds, CancellationToken ct = default)
    {
        await _db.Users.AddAsync(user, ct);

        foreach (var roleId in roleIds)
        {
            // Phase G: single write — ScopedRoleAssignment only.
            // UserRoles table dropped by migration 20260330200004.
            var scoped = ScopedRoleAssignment.Create(
                userId:    user.Id,
                roleId:    roleId,
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
