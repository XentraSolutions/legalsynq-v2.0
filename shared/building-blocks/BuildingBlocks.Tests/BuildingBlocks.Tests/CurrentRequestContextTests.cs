using System.Security.Claims;
using BuildingBlocks.Context;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Tests;

public sealed class CurrentRequestContextTests
{
    [Fact]
    public void Roles_IncludeRoleClaimsWhenJwtUsesRoleClaimType()
    {
        var context = CreateContext(
            new Claim("role", "TenantAdmin"),
            new Claim("role", "PlatformAdmin"));

        Assert.Contains("TenantAdmin", context.Roles);
        Assert.Contains("PlatformAdmin", context.Roles);
    }

    [Fact]
    public void IsPlatformAdmin_RecognizesRoleClaimType()
    {
        var context = CreateContext(new Claim("role", "PlatformAdmin"));

        Assert.True(context.IsPlatformAdmin);
    }

    [Fact]
    public void Roles_DeduplicateMixedRoleClaimTypes()
    {
        var context = CreateContext(
            new Claim("role", "TenantAdmin"),
            new Claim(ClaimTypes.Role, "TenantAdmin"));

        Assert.Single(context.Roles);
        Assert.Equal("TenantAdmin", Assert.Single(context.Roles));
    }

    private static CurrentRequestContext CreateContext(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"))
        };

        return new CurrentRequestContext(new HttpContextAccessor { HttpContext = httpContext });
    }
}
