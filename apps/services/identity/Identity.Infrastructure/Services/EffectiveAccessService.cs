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
            return new EffectiveAccessResult([], new(), [], [], [], []);
        }

        var entitlementSet = new HashSet<string>(activeEntitlements, StringComparer.OrdinalIgnoreCase);

        var directProducts = await _db.UserProductAccessRecords
            .Where(a => a.TenantId == tenantId && a.UserId == userId && a.AccessStatus == AccessStatus.Granted)
            .Select(a => a.ProductCode)
            .ToListAsync(ct);

        var activeGroupIds = await _db.AccessGroupMemberships
            .Where(m => m.TenantId == tenantId && m.UserId == userId && m.MembershipStatus == MembershipStatus.Active)
            .Select(m => m.GroupId)
            .ToListAsync(ct);

        var activeGroups = activeGroupIds.Count > 0
            ? await _db.AccessGroups
                .Where(g => activeGroupIds.Contains(g.Id) && g.TenantId == tenantId && g.Status == GroupStatus.Active)
                .ToDictionaryAsync(g => g.Id, g => g.Name, ct)
            : new Dictionary<Guid, string>();

        var validGroupIds = activeGroups.Keys.ToList();

        var inheritedProducts = validGroupIds.Count > 0
            ? await _db.GroupProductAccessRecords
                .Where(a => a.TenantId == tenantId && validGroupIds.Contains(a.GroupId) && a.AccessStatus == AccessStatus.Granted)
                .Select(a => new { a.ProductCode, a.GroupId })
                .ToListAsync(ct)
            : [];

        var productSources = new List<EffectiveProductEntry>();
        var effectiveProductSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var code in directProducts)
        {
            if (entitlementSet.Contains(code) && effectiveProductSet.Add(code))
                productSources.Add(new EffectiveProductEntry(code, "Direct"));
        }

        foreach (var ip in inheritedProducts)
        {
            if (!entitlementSet.Contains(ip.ProductCode)) continue;
            if (effectiveProductSet.Add(ip.ProductCode))
            {
                activeGroups.TryGetValue(ip.GroupId, out var gn);
                productSources.Add(new EffectiveProductEntry(ip.ProductCode, "Inherited", ip.GroupId, gn));
            }
        }

        var effectiveProducts = effectiveProductSet
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var directRoles = await _db.UserRoleAssignments
            .Where(a => a.TenantId == tenantId && a.UserId == userId && a.AssignmentStatus == AssignmentStatus.Active)
            .ToListAsync(ct);

        var inheritedRoles = validGroupIds.Count > 0
            ? await _db.GroupRoleAssignments
                .Where(a => a.TenantId == tenantId && validGroupIds.Contains(a.GroupId) && a.AssignmentStatus == AssignmentStatus.Active)
                .ToListAsync(ct)
            : [];

        var productRoles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var tenantRoles = new List<string>();
        var roleSources = new List<EffectiveRoleEntry>();
        var seenRoleKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddRole(string roleCode, string? productCode, string source, Guid? groupId, string? groupName)
        {
            var key = $"{productCode ?? "__TENANT__"}:{roleCode}";
            if (!seenRoleKeys.Add(key)) return;

            roleSources.Add(new EffectiveRoleEntry(roleCode, productCode, source, groupId, groupName));

            if (productCode == null)
            {
                tenantRoles.Add(roleCode);
                return;
            }

            if (!effectiveProductSet.Contains(productCode)) return;

            if (!productRoles.TryGetValue(productCode, out var roleList))
            {
                roleList = new List<string>();
                productRoles[productCode] = roleList;
            }
            roleList.Add(roleCode);
        }

        foreach (var r in directRoles)
            AddRole(r.RoleCode, r.ProductCode, "Direct", null, null);

        foreach (var r in inheritedRoles)
        {
            activeGroups.TryGetValue(r.GroupId, out var gn);
            AddRole(r.RoleCode, r.ProductCode, "Inherited", r.GroupId, gn);
        }

        var productRolesFlat = new List<string>();
        foreach (var (product, roles) in productRoles.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var role in roles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase))
                productRolesFlat.Add($"{product}:{role}");
        }

        _logger.LogDebug(
            "Effective access for user {UserId} in tenant {TenantId}: {ProductCount} products ({DirectCount} direct, {InheritedCount} inherited), {RoleCount} product roles, {TenantRoleCount} tenant roles.",
            userId, tenantId, effectiveProducts.Count,
            productSources.Count(s => s.Source == "Direct"),
            productSources.Count(s => s.Source == "Inherited"),
            productRolesFlat.Count, tenantRoles.Count);

        return new EffectiveAccessResult(effectiveProducts, productRoles, productRolesFlat, tenantRoles, productSources, roleSources);
    }
}
