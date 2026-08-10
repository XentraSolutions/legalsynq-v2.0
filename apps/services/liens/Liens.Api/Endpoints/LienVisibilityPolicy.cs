using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;

namespace Liens.Api.Endpoints;

internal static class LienVisibilityPolicy
{
    internal static LienVisibilityScope Resolve(ICurrentRequestContext context)
    {
        if (context.IsPlatformAdmin ||
            context.Roles.Contains(Roles.TenantAdmin, StringComparer.OrdinalIgnoreCase) ||
            HasPermission(context, LiensPermissions.LienRead))
        {
            return LienVisibilityScope.All;
        }

        var canReadOwn = HasPermission(context, LiensPermissions.LienReadOwn);
        var canBrowse = HasPermission(context, LiensPermissions.LienBrowse);
        var canReadHeld = HasPermission(context, LiensPermissions.LienReadHeld);

        if ((!canReadOwn && !canBrowse && !canReadHeld) ||
            context.OrgId is not { } orgId ||
            orgId == Guid.Empty)
        {
            return LienVisibilityScope.None;
        }

        return new LienVisibilityScope(
            CanReadAnyLien: true,
            OrgId: orgId,
            IncludeSellerOrg: canReadOwn,
            IncludeBuyerOrg: canReadHeld,
            IncludeHolderOrg: canReadHeld,
            IncludeMarketplace: canBrowse);
    }

    internal static IQueryable<Lien> Apply(IQueryable<Lien> query, LienVisibilityScope visibility)
    {
        if (!visibility.CanReadAnyLien)
            return query.Where(_ => false);

        if (visibility.OrgId is not { } orgId)
            return query;

        return query.Where(lien =>
            (visibility.IncludeSellerOrg && (lien.OrgId == orgId || lien.SellingOrgId == orgId)) ||
            (visibility.IncludeBuyerOrg && lien.BuyingOrgId == orgId) ||
            (visibility.IncludeHolderOrg && lien.HoldingOrgId == orgId) ||
            (visibility.IncludeMarketplace &&
                (lien.Status == LienStatus.Offered || lien.Status == LienStatus.UnderReview)));
    }

    private static bool HasPermission(ICurrentRequestContext context, string permission)
        => context.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}

internal sealed record LienVisibilityScope(
    bool CanReadAnyLien,
    Guid? OrgId,
    bool IncludeSellerOrg,
    bool IncludeBuyerOrg,
    bool IncludeHolderOrg,
    bool IncludeMarketplace)
{
    public static LienVisibilityScope All { get; } = new(true, null, true, true, true, true);
    public static LienVisibilityScope None { get; } = new(false, null, false, false, false, false);
}
