using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Domain;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class CareConnectRoleMapper : IProductRoleMapper
{
    private readonly ILogger<CareConnectRoleMapper> _logger;

    public string ProductCode => ProductCodes.SynqCareConnect;

    public CareConnectRoleMapper(ILogger<CareConnectRoleMapper> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<ProductAccessEntry> ResolveRoles(ProductRoleMapperContext context)
    {
        var org = context.Organization;
        var orgType = OrgTypeMapper.TryResolveCode(org.OrganizationTypeId) ?? org.OrgType;

        if (!ProductEligibilityConfig.IsEligible(orgType, ProductCode))
        {
            _logger.LogDebug(
                "CareConnect denied for user={UserId} org={OrgId}: org type {OrgType} not eligible.",
                context.UserId, org.Id, orgType);

            return [new ProductAccessEntry
            {
                ProductCode = ProductCode,
                OrganizationId = org.Id,
                OrganizationName = org.DisplayName ?? org.Name,
                OrgType = orgType,
                IsGranted = false,
                DenialReason = $"Organization type '{orgType}' is not eligible for CareConnect.",
                AccessSource = "CareConnectRoleMapper:Eligibility"
            }];
        }

        var resolvedRoles = new List<string>();
        var accessSource = "CareConnectRoleMapper";

        var scopedProductRoles = context.ScopedRoleAssignments
            .Where(s => s.IsActive &&
                        s.ScopeType == ScopedRoleAssignment.ScopeTypes.Product &&
                        s.OrganizationId == org.Id)
            .Select(s => s.Role.Name)
            .ToList();

        if (scopedProductRoles.Count > 0)
        {
            resolvedRoles.AddRange(scopedProductRoles);
            accessSource = "CareConnectRoleMapper:ScopedRoleAssignment";
        }

        var dbEligibleRoles = ResolveFromProductOrganizationTypeRules(context.AvailableProductRoles, org);
        foreach (var role in dbEligibleRoles)
        {
            if (!resolvedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                resolvedRoles.Add(role);
        }

        if (dbEligibleRoles.Count > 0 && scopedProductRoles.Count == 0)
            accessSource = "CareConnectRoleMapper:OrgTypeRule";

        if (resolvedRoles.Count == 0)
        {
            var fallbackRoles = ResolveFallbackByOrgType(orgType);
            resolvedRoles.AddRange(fallbackRoles);
            if (fallbackRoles.Count > 0)
                accessSource = "CareConnectRoleMapper:OrgTypeFallback";
        }

        if (resolvedRoles.Count == 0)
        {
            _logger.LogDebug(
                "CareConnect: no roles resolved for user={UserId} org={OrgId} orgType={OrgType}. " +
                "No scoped assignment, no DB rules matched, no fallback rules.",
                context.UserId, org.Id, orgType);

            return [new ProductAccessEntry
            {
                ProductCode = ProductCode,
                OrganizationId = org.Id,
                OrganizationName = org.DisplayName ?? org.Name,
                OrgType = orgType,
                IsGranted = false,
                DenialReason = "No CareConnect roles resolved for this organization context.",
                AccessSource = "CareConnectRoleMapper:NoRoles"
            }];
        }

        _logger.LogDebug(
            "CareConnect roles resolved for user={UserId} org={OrgId}: [{Roles}] via {Source}.",
            context.UserId, org.Id, string.Join(", ", resolvedRoles), accessSource);

        return [new ProductAccessEntry
        {
            ProductCode = ProductCode,
            OrganizationId = org.Id,
            OrganizationName = org.DisplayName ?? org.Name,
            OrgType = orgType,
            EffectiveRoles = resolvedRoles.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            IsGranted = true,
            AccessSource = accessSource
        }];
    }

    private static IReadOnlyList<string> ResolveFromProductOrganizationTypeRules(
        IReadOnlyList<ProductRole> availableProductRoles,
        Organization org)
    {
        var roles = new List<string>();

        foreach (var pr in availableProductRoles.Where(pr => pr.IsActive))
        {
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
                return r.OrganizationType.Code == org.OrgType;
            });

            if (matched)
                roles.Add(pr.Code);
        }

        return roles;
    }

    private static IReadOnlyList<string> ResolveFallbackByOrgType(string orgType)
    {
        return orgType switch
        {
            OrgType.Provider => ["CARECONNECT_RECEIVER"],
            OrgType.LawFirm => ["CARECONNECT_REFERRER"],
            OrgType.Internal => ["CARECONNECT_ADMIN"],
            _ => []
        };
    }
}
