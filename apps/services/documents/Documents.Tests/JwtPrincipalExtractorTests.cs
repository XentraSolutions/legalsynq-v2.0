using System.Security.Claims;
using BuildingBlocks.Authentication.ServiceTokens;
using Documents.Infrastructure.Auth;
using Xunit;

namespace Documents.Tests;

public class JwtPrincipalExtractorTests
{
    [Fact]
    public void Extract_UsesActorClaim_WhenSubjectIsServicePrincipal()
    {
        var actorUserId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "service:careconnect"),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(ServiceTokenAuthenticationDefaults.ActorClaim, $"user:{actorUserId}"),
            new Claim(ClaimTypes.Role, "service"),
        ], "test"));

        var result = JwtPrincipalExtractor.Extract(principal);

        Assert.Equal(actorUserId, result.UserId);
        Assert.Equal(tenantId, result.TenantId);
    }

    [Fact]
    public void Extract_FallsBackToEmptyGuid_WhenServicePrincipalHasNoActor()
    {
        var tenantId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "service:careconnect"),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(ClaimTypes.Role, "service"),
        ], "test"));

        var result = JwtPrincipalExtractor.Extract(principal);

        Assert.Equal(Guid.Empty, result.UserId);
        Assert.Equal(tenantId, result.TenantId);
    }

    [Fact]
    public void Extract_IncludesPlatformProductRoles()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", userId.ToString()),
            new Claim("tenantId", tenantId.ToString()),
            new Claim("product_roles", "SYNQ_LIENS:SYNQLIEN_SELLER"),
        ], "test"));

        var result = JwtPrincipalExtractor.Extract(principal);

        Assert.Contains("SYNQ_LIENS:SYNQLIEN_SELLER", result.ProductRoles);
    }
}
