using BuildingBlocks.Context;
using BuildingBlocks.Exceptions;
using CareConnect.Application.Authorization;
using CareConnect.Application.DTOs;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

public class AuthenticatedReferralScopeResolverTests
{
    [Fact]
    public void ResolveTenantId_UsesJwtTenant_WhenNoOverrideIsRequested()
    {
        var currentTenantId = Guid.CreateVersion7();
        var ctx = BuildContext(currentTenantId, Guid.CreateVersion7());

        var tenantId = AuthenticatedReferralScopeResolver.ResolveTenantId(ctx, new CreateReferralRequest(), hasVerifiedSelection: false);

        Assert.Equal(currentTenantId, tenantId);
    }

    [Fact]
    public void ResolveTenantId_UsesJwtTenant_WhenOverrideMatchesJwtTenant()
    {
        var currentTenantId = Guid.CreateVersion7();
        var ctx = BuildContext(currentTenantId, Guid.CreateVersion7());
        var request = new CreateReferralRequest
        {
            TenantId = currentTenantId,
        };

        var tenantId = AuthenticatedReferralScopeResolver.ResolveTenantId(ctx, request, hasVerifiedSelection: false);

        Assert.Equal(currentTenantId, tenantId);
    }

    [Fact]
    public void ResolveTenantId_UsesRequestedTenant_WhenSelectionWasVerified()
    {
        var currentTenantId = Guid.CreateVersion7();
        var selectedTenantId = Guid.CreateVersion7();
        var ctx = BuildContext(currentTenantId, Guid.CreateVersion7());
        var request = new CreateReferralRequest
        {
            TenantId = selectedTenantId,
        };

        var tenantId = AuthenticatedReferralScopeResolver.ResolveTenantId(ctx, request, hasVerifiedSelection: true);

        Assert.Equal(selectedTenantId, tenantId);
    }

    [Fact]
    public void ResolveTenantId_ThrowsForbidden_WhenAlternateTenantIsNotVerified()
    {
        var ctx = BuildContext(Guid.CreateVersion7(), Guid.CreateVersion7());
        var request = new CreateReferralRequest
        {
            TenantId = Guid.CreateVersion7(),
        };

        Assert.Throws<ForbiddenException>(() =>
            AuthenticatedReferralScopeResolver.ResolveTenantId(ctx, request, hasVerifiedSelection: false));
    }

    [Fact]
    public void ResolveTenantId_UsesJwtTenant_WhenSameTenantOverrideIsNotVerified()
    {
        var tenantId = Guid.CreateVersion7();
        var ctx = BuildContext(tenantId, Guid.CreateVersion7());
        var request = new CreateReferralRequest
        {
            TenantId = tenantId,
        };

        var resolvedTenantId = AuthenticatedReferralScopeResolver.ResolveTenantId(ctx, request, hasVerifiedSelection: false);

        Assert.Equal(tenantId, resolvedTenantId);
    }

    private static ICurrentRequestContext BuildContext(Guid tenantId, Guid? orgId)
    {
        var ctx = new Mock<ICurrentRequestContext>();
        ctx.SetupGet(x => x.TenantId).Returns(tenantId);
        ctx.SetupGet(x => x.OrgId).Returns(orgId);
        return ctx.Object;
    }
}
