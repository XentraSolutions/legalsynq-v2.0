using Identity.Api.Endpoints;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.Tests;

public sealed class SynqLienBuyerOrganizationTests
{
    [Fact]
    public async Task CreateSynqLienBuyerOrganization_creates_lien_owner_org_idempotently()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var sourceBuyerOrgId = Guid.Parse("40000000-0000-0000-0000-000000000012");

        var firstResult = await AdminEndpointsLscc010.CreateSynqLienBuyerOrganization(
            new AdminEndpointsLscc010.CreateSynqLienBuyerOrgRequest(
                tenantId,
                sourceBuyerOrgId,
                "Capital Fund LLC",
                "buyer@capital.test"),
            db,
            CancellationToken.None);

        var firstStatus = Assert.IsAssignableFrom<IStatusCodeHttpResult>(firstResult);
        Assert.Equal(StatusCodes.Status201Created, firstStatus.StatusCode);
        Assert.True(await db.Tenants.AnyAsync(t => t.Id == tenantId));

        var org = await db.Organizations.SingleAsync();
        Assert.Equal(tenantId, org.TenantId);
        Assert.Equal(OrgType.LienOwner, org.OrgType);
        Assert.Equal("Capital Fund LLC", org.DisplayName);
        Assert.Contains(sourceBuyerOrgId.ToString("D"), org.Name);

        var secondResult = await AdminEndpointsLscc010.CreateSynqLienBuyerOrganization(
            new AdminEndpointsLscc010.CreateSynqLienBuyerOrgRequest(
                tenantId,
                sourceBuyerOrgId,
                "Capital Fund LLC",
                "buyer@capital.test"),
            db,
            CancellationToken.None);

        var secondStatus = Assert.IsAssignableFrom<IStatusCodeHttpResult>(secondResult);
        Assert.Equal(StatusCodes.Status200OK, secondStatus.StatusCode);
        Assert.Equal(1, await db.Organizations.CountAsync());
    }

    private static IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase("synqlien-buyer-org-" + Guid.CreateVersion7())
            .Options;
        return new IdentityDbContext(options);
    }
}
