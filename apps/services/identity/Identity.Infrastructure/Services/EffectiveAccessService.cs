using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class EffectiveAccessService : IEffectiveAccessService
{
    private readonly IdentityDbContext _db;
    private readonly ILogger<EffectiveAccessService> _logger;

    public EffectiveAccessService(IdentityDbContext db, ILogger<EffectiveAccessService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<EffectiveAccessResult> GetEffectiveAccessAsync(
        Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var activeEntitlements = await _db.TenantProductEntitlements
            .Where(e => e.TenantId == tenantId && e.Status == EntitlementStatus.Active)
            .Select(e => e.ProductCode)
            .ToListAsync(ct);

        if (activeEntitlements.Count == 0)
        {
            _logger.LogDebug("No active entitlements for tenant {TenantId}.", tenantId);
            return new EffectiveAccessResult([], new(), [], []);
        }

        var entitlementSet = new HashSet<string>(activeEntitlements, StringComparer.OrdinalIgnoreCase);

        var grantedAccess = await _db.UserProductAccessRecords
            .Where(a => a.TenantId == tenantId
                        && a.UserId == userId
                        && a.AccessStatus == AccessStatus.Granted)
            .Select(a => a.ProductCode)
            .ToListAsync(ct);

        var effectiveProducts = grantedAccess
            .Where(code => entitlementSet.Contains(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var activeRoles = await _db.UserRoleAssignments
            .Where(a => a.TenantId == tenantId
                        && a.UserId == userId
                        && a.AssignmentStatus == AssignmentStatus.Active)
            .ToListAsync(ct);

        var effectiveProductSet = new HashSet<string>(effectiveProducts, StringComparer.OrdinalIgnoreCase);

        var productRoles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var tenantRoles = new List<string>();

        foreach (var role in activeRoles)
        {
            if (role.ProductCode == null)
            {
                tenantRoles.Add(role.RoleCode);
                continue;
            }

            if (!effectiveProductSet.Contains(role.ProductCode))
                continue;

            if (!productRoles.TryGetValue(role.ProductCode, out var roleList))
            {
                roleList = new List<string>();
                productRoles[role.ProductCode] = roleList;
            }

            if (!roleList.Contains(role.RoleCode))
                roleList.Add(role.RoleCode);
        }

        var productRolesFlat = new List<string>();
        foreach (var (product, roles) in productRoles.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var role in roles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase))
            {
                productRolesFlat.Add($"{product}:{role}");
            }
        }

        _logger.LogDebug(
            "Effective access for user {UserId} in tenant {TenantId}: {ProductCount} products, {RoleCount} product roles, {TenantRoleCount} tenant roles.",
            userId, tenantId, effectiveProducts.Count, productRolesFlat.Count, tenantRoles.Count);

        return new EffectiveAccessResult(effectiveProducts, productRoles, productRolesFlat, tenantRoles);
    }
}
