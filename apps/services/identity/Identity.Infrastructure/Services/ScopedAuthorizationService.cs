using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

/// <summary>
/// DB-backed implementation of IScopedAuthorizationService.
///
/// Phase I: provides real scope-aware authorization checks against
/// ScopedRoleAssignments.  GLOBAL scope always satisfies narrower scope checks
/// (a global admin can always act within any org or product).
/// </summary>
public sealed class ScopedAuthorizationService : IScopedAuthorizationService
{
    private readonly IdentityDbContext _db;

    public ScopedAuthorizationService(IdentityDbContext db) => _db = db;

    public Task<bool> HasOrganizationRoleAsync(
        Guid   userId,
        string roleName,
        Guid   organizationId,
        CancellationToken ct = default)
        => _db.ScopedRoleAssignments
            .AnyAsync(s =>
                s.UserId   == userId &&
                s.IsActive &&
                s.Role.Name == roleName &&
                (s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global ||
                 (s.ScopeType == ScopedRoleAssignment.ScopeTypes.Organization &&
                  s.OrganizationId == organizationId)),
                ct);

    public Task<bool> HasProductRoleAsync(
        Guid   userId,
        string roleName,
        Guid   productId,
        CancellationToken ct = default)
        => _db.ScopedRoleAssignments
            .AnyAsync(s =>
                s.UserId   == userId &&
                s.IsActive &&
                s.Role.Name == roleName &&
                (s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global ||
                 (s.ScopeType == ScopedRoleAssignment.ScopeTypes.Product &&
                  s.ProductId == productId)),
                ct);

    public async Task<ScopedRoleSummaryResponse> GetScopedRoleSummaryAsync(
        Guid   userId,
        CancellationToken ct = default)
    {
        var rows = await _db.ScopedRoleAssignments
            .Include(s => s.Role)
            .Where(s => s.UserId == userId && s.IsActive)
            .OrderBy(s => s.ScopeType)
            .ThenBy(s => s.AssignedAtUtc)
            .ToListAsync(ct);

        var entries = rows
            .Select(s => new ScopedRoleEntry(
                s.Id,
                s.Role.Name,
                s.ScopeType,
                s.OrganizationId,
                s.ProductId,
                s.OrganizationRelationshipId,
                s.TenantId))
            .ToList();

        return new ScopedRoleSummaryResponse(userId, entries.Count, entries);
    }
}
