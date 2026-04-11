using Identity.Application;
using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Domain;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class ProductRoleResolutionService : IProductRoleResolutionService
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IEnumerable<IProductRoleMapper> _mappers;
    private readonly ILogger<ProductRoleResolutionService> _logger;

    public ProductRoleResolutionService(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IEnumerable<IProductRoleMapper> mappers,
        ILogger<ProductRoleResolutionService> logger)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _mappers = mappers;
        _logger = logger;
    }

    public async Task<EffectiveAccessContext> ResolveAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        // Load scoped assignments here when the call site hasn't pre-loaded them.
        var userWithRoles = await _userRepository.GetByIdWithRolesAsync(userId, ct);
        var scopedAssignments = (userWithRoles?.ScopedRoleAssignments ?? [])
            .Where(s => s.IsActive)
            .ToList();

        return await ResolveAsync(userId, tenantId, scopedAssignments, ct);
    }

    public async Task<EffectiveAccessContext> ResolveAsync(
        Guid userId,
        Guid tenantId,
        IReadOnlyList<ScopedRoleAssignment> preloadedScopedAssignments,
        CancellationToken ct = default)
    {
        var deniedReasons = new List<string>();
        var productAccess = new List<ProductAccessEntry>();

        var enabledProductCodes = await _tenantRepository.GetEnabledProductCodesAsync(tenantId, ct);
        var enabledProductSet = new HashSet<string>(enabledProductCodes, StringComparer.OrdinalIgnoreCase);

        if (enabledProductSet.Count == 0)
        {
            _logger.LogDebug(
                "No products enabled at tenant level for tenant={TenantId}, user={UserId}.",
                tenantId, userId);

            return new EffectiveAccessContext
            {
                UserId = userId,
                TenantId = tenantId,
                ProductAccess = productAccess,
                DeniedReasons = ["No products enabled for this tenant."]
            };
        }

        var memberships = await _userRepository.GetActiveMembershipsWithProductsAsync(userId, tenantId, ct);

        if (memberships.Count == 0)
        {
            _logger.LogDebug(
                "No active org memberships for user={UserId}. No product roles can be resolved.",
                userId);

            return new EffectiveAccessContext
            {
                UserId = userId,
                TenantId = tenantId,
                ProductAccess = productAccess,
                DeniedReasons = ["User has no active organization memberships."]
            };
        }

        // Use the pre-loaded scoped assignments passed in by the caller.
        var scopedAssignments = preloadedScopedAssignments;

        var systemRoles = scopedAssignments
            .Where(s => s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global)
            .Select(s => s.Role.Name)
            .ToList();

        var mapperDict = new Dictionary<string, IProductRoleMapper>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in _mappers)
        {
            if (!mapperDict.TryAdd(m.ProductCode, m))
            {
                _logger.LogWarning(
                    "Duplicate IProductRoleMapper for product '{ProductCode}'. Using first registered mapper ({MapperType}).",
                    m.ProductCode, mapperDict[m.ProductCode].GetType().Name);
            }
        }

        foreach (var membership in memberships)
        {
            var org = membership.Organization;
            if (org is null) continue;

            var orgType = OrgTypeMapper.TryResolveCode(org.OrganizationTypeId) ?? org.OrgType;

            foreach (var orgProduct in org.OrganizationProducts.Where(op => op.IsEnabled))
            {
                var productCode = orgProduct.Product?.Code;
                if (string.IsNullOrEmpty(productCode)) continue;

                if (!enabledProductSet.Contains(productCode))
                {
                    var reason = $"Product '{productCode}' disabled at tenant level for org '{org.DisplayName ?? org.Name}'.";
                    deniedReasons.Add(reason);

                    productAccess.Add(new ProductAccessEntry
                    {
                        ProductCode = productCode,
                        OrganizationId = org.Id,
                        OrganizationName = org.DisplayName ?? org.Name,
                        OrgType = orgType,
                        IsGranted = false,
                        DenialReason = reason,
                        AccessSource = "ProductRoleResolutionService:TenantDisabled"
                    });
                    continue;
                }

                if (!ProductEligibilityConfig.IsEligible(orgType, productCode))
                {
                    var reason = $"Org type '{orgType}' not eligible for product '{productCode}'.";
                    deniedReasons.Add(reason);

                    productAccess.Add(new ProductAccessEntry
                    {
                        ProductCode = productCode,
                        OrganizationId = org.Id,
                        OrganizationName = org.DisplayName ?? org.Name,
                        OrgType = orgType,
                        IsGranted = false,
                        DenialReason = reason,
                        AccessSource = "ProductRoleResolutionService:OrgTypeIneligible"
                    });
                    continue;
                }

                var availableProductRoles = orgProduct.Product?.ProductRoles
                    ?.Where(pr => pr.IsActive)
                    .ToList() ?? [];

                if (mapperDict.TryGetValue(productCode, out var mapper))
                {
                    var context = new ProductRoleMapperContext
                    {
                        UserId = userId,
                        TenantId = tenantId,
                        Organization = org,
                        Membership = membership,
                        SystemRoles = systemRoles,
                        ScopedRoleAssignments = scopedAssignments,
                        AvailableProductRoles = availableProductRoles
                    };

                    var entries = mapper.ResolveRoles(context);
                    productAccess.AddRange(entries);
                }
                else
                {
                    var entries = ResolveDefault(
                        userId, org, orgType, productCode,
                        availableProductRoles, scopedAssignments);
                    productAccess.AddRange(entries);
                }
            }
        }

        var result = new EffectiveAccessContext
        {
            UserId = userId,
            TenantId = tenantId,
            ProductAccess = productAccess,
            DeniedReasons = deniedReasons
        };

        _logger.LogDebug(
            "Role resolution complete for user={UserId}: {GrantedCount} granted, {DeniedCount} denied across {OrgCount} org(s).",
            userId,
            productAccess.Count(p => p.IsGranted),
            productAccess.Count(p => !p.IsGranted),
            memberships.Count);

        return result;
    }

    private static IReadOnlyList<ProductAccessEntry> ResolveDefault(
        Guid userId,
        Organization org,
        string orgType,
        string productCode,
        List<ProductRole> availableProductRoles,
        IReadOnlyList<ScopedRoleAssignment> scopedAssignments)
    {
        var roles = new List<string>();

        var scopedProductRoles = scopedAssignments
            .Where(s => s.ScopeType == ScopedRoleAssignment.ScopeTypes.Product &&
                        s.OrganizationId == org.Id)
            .Select(s => s.Role.Name)
            .ToList();

        roles.AddRange(scopedProductRoles);

        foreach (var pr in availableProductRoles)
        {
            if (roles.Contains(pr.Code, StringComparer.OrdinalIgnoreCase))
                continue;

            if (pr.OrgTypeRules is not { Count: > 0 })
            {
                roles.Add(pr.Code);
                continue;
            }

            var matched = pr.OrgTypeRules.Any(r =>
            {
                if (!r.IsActive || r.OrganizationType is null) return false;
                if (org.OrganizationTypeId.HasValue)
                    return r.OrganizationTypeId == org.OrganizationTypeId.Value;
                return r.OrganizationType.Code == orgType;
            });

            if (matched)
                roles.Add(pr.Code);
        }

        if (roles.Count == 0)
        {
            return [new ProductAccessEntry
            {
                ProductCode = productCode,
                OrganizationId = org.Id,
                OrganizationName = org.DisplayName ?? org.Name,
                OrgType = orgType,
                IsGranted = false,
                DenialReason = "No roles resolved via default mapper.",
                AccessSource = "ProductRoleResolutionService:DefaultMapper"
            }];
        }

        return [new ProductAccessEntry
        {
            ProductCode = productCode,
            OrganizationId = org.Id,
            OrganizationName = org.DisplayName ?? org.Name,
            OrgType = orgType,
            EffectiveRoles = roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            IsGranted = true,
            AccessSource = "ProductRoleResolutionService:DefaultMapper"
        }];
    }
}
