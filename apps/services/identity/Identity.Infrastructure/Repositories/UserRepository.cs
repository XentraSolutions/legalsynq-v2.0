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
            .AsNoTracking()
            // Phase G: ScopedRoleAssignments is the sole authoritative role source.
            .Include(u => u.ScopedRoleAssignments.Where(s => s.IsActive))
                .ThenInclude(s => s.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByTenantAndEmailAsync(Guid tenantId, string email, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == email, ct);

    /// <summary>
    /// Single tracked query used by the login path — loads the user and their active
    /// ScopedRoleAssignments so RecordLogin() can be saved without a second DB hit.
    /// </summary>
    public Task<User?> GetByTenantAndEmailWithRolesAsync(Guid tenantId, string email, CancellationToken ct = default) =>
        _db.Users
            .Include(u => u.ScopedRoleAssignments.Where(s => s.IsActive))
                .ThenInclude(s => s.Role)
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == email, ct);

    /// <summary>
    /// Lightweight login path — returns the user's primary org with only its
    /// OrganizationTypeRef. Does NOT load products, roles, or OrgTypeRules.
    /// </summary>
    public async Task<Organization?> GetPrimaryOrganizationForLoginAsync(Guid userId, CancellationToken ct = default) =>
        (await _db.UserOrganizationMemberships
            .AsNoTracking()
            .Include(m => m.Organization)
                .ThenInclude(o => o.OrganizationTypeRef)
            .Where(m => m.UserId == userId && m.IsActive)
            .OrderBy(m => m.JoinedAtUtc)
            .FirstOrDefaultAsync(ct))?.Organization;

    public Task<List<User>> GetAllWithRolesAsync(CancellationToken ct = default) =>
        _db.Users
            .AsNoTracking()
            // Phase G: ScopedRoleAssignments is the sole authoritative role source.
            .Include(u => u.ScopedRoleAssignments.Where(s => s.IsActive))
                .ThenInclude(s => s.Role)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(ct);

    public Task<List<User>> GetByTenantWithRolesAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId)
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

    public async Task UpdateAvatarAsync(Guid userId, Guid? avatarDocumentId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        if (avatarDocumentId.HasValue)
            user.SetAvatar(avatarDocumentId.Value);
        else
            user.ClearAvatar();

        await _db.SaveChangesAsync(ct);
    }

    public Task<UserOrganizationMembership?> GetPrimaryOrgMembershipAsync(
        Guid userId, CancellationToken ct = default) =>
        _db.UserOrganizationMemberships
            .AsNoTracking()
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

    public Task<List<UserOrganizationMembership>> GetActiveMembershipsWithProductsAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default) =>
        _db.UserOrganizationMemberships
            .AsNoTracking()
            .Include(m => m.Organization)
                .ThenInclude(o => o.OrganizationProducts.Where(op => op.IsEnabled))
                    .ThenInclude(op => op.Product)
                        .ThenInclude(p => p.ProductRoles.Where(pr => pr.IsActive))
                            .ThenInclude(pr => pr.OrgTypeRules)
                                .ThenInclude(r => r.OrganizationType)
            .Include(m => m.Organization)
                .ThenInclude(o => o.OrganizationTypeRef)
            .Where(m => m.UserId == userId
                     && m.IsActive
                     && m.Organization.IsActive
                     && m.Organization.TenantId == tenantId)
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => m.JoinedAtUtc)
            .ToListAsync(ct);

    /// <summary>
    /// UIX-003-03: Persists pending change-tracked mutations for User entities
    /// already loaded by this repository (e.g. RecordLogin, IncrementSessionVersion).
    /// </summary>
    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);

    // ── Invitation and reset-token access ─────────────────────────────────────

    public Task<UserInvitation?> GetInvitationWithUserByTokenHashAsync(
        string tokenHash, CancellationToken ct = default) =>
        _db.UserInvitations
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);

    public Task<PasswordResetToken?> GetPasswordResetTokenWithUserByHashAsync(
        string tokenHash, CancellationToken ct = default) =>
        _db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public Task<List<PasswordResetToken>> GetPendingPasswordResetTokensAsync(
        Guid userId, CancellationToken ct = default) =>
        _db.PasswordResetTokens
            .Where(t => t.UserId == userId && t.Status == PasswordResetToken.Statuses.Pending)
            .ToListAsync(ct);

    public async Task AddPasswordResetTokenAsync(
        PasswordResetToken resetToken, CancellationToken ct = default)
    {
        _db.PasswordResetTokens.Add(resetToken);
        await _db.SaveChangesAsync(ct);
    }
}
