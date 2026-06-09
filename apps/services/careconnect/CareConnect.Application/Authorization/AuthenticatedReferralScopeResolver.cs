using BuildingBlocks.Context;
using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;

namespace CareConnect.Application.Authorization;

public static class AuthenticatedReferralScopeResolver
{
    public static Guid ResolveTenantId(
        ICurrentRequestContext ctx,
        CreateReferralRequest request,
        bool hasVerifiedSelection)
    {
        var currentTenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
        var requestedTenantId = request.TenantId ?? currentTenantId;

        if (requestedTenantId == currentTenantId)
            return currentTenantId;

        if (!hasVerifiedSelection)
            throw new ForbiddenException();

        return requestedTenantId;
    }
}
