using System.Security.Claims;
using BuildingBlocks.Authorization;

namespace BuildingBlocks.Tests;

public class ProductRoleClaimExtensionsTests
{
    private static ClaimsPrincipal CreatePrincipal(
        IEnumerable<string>? productRoles = null,
        IEnumerable<string>? systemRoles = null)
    {
        var claims = new List<Claim>();
        foreach (var pr in productRoles ?? [])
            claims.Add(new Claim("product_roles", pr));
        foreach (var sr in systemRoles ?? [])
            claims.Add(new Claim(ClaimTypes.Role, sr));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void HasProductAccess_WithProductPrefixClaim_ReturnsTrue()
    {
        var principal = CreatePrincipal(productRoles: ["SYNQ_CARECONNECT:CARECONNECT_REFERRER"]);
        Assert.True(principal.HasProductAccess("SYNQ_CARECONNECT"));
    }

    [Fact]
    public void HasProductAccess_WithMultipleProducts_MatchesCorrectOne()
    {
        var principal = CreatePrincipal(productRoles: [
            "SYNQ_CARECONNECT:CARECONNECT_REFERRER",
            "SYNQ_FUND:SYNQFUND_FUNDER"
        ]);
        Assert.True(principal.HasProductAccess("SYNQ_CARECONNECT"));
        Assert.True(principal.HasProductAccess("SYNQ_FUND"));
        Assert.False(principal.HasProductAccess("SYNQ_LIENS"));
    }

    [Fact]
    public void HasProductAccess_WithBareRoleCode_ReturnsFalse()
    {
        var principal = CreatePrincipal(productRoles: ["CARECONNECT_REFERRER"]);
        Assert.False(principal.HasProductAccess("SYNQ_CARECONNECT"));
    }

    [Fact]
    public void HasProductAccess_WithNoProductRoles_ReturnsFalse()
    {
        var principal = CreatePrincipal();
        Assert.False(principal.HasProductAccess("SYNQ_CARECONNECT"));
    }

    [Fact]
    public void HasProductAccess_PlatformAdminBypasses()
    {
        var principal = CreatePrincipal(systemRoles: ["PlatformAdmin"]);
        Assert.True(principal.HasProductAccess("SYNQ_CARECONNECT"));
    }

    [Fact]
    public void HasProductAccess_TenantAdminBypasses()
    {
        var principal = CreatePrincipal(systemRoles: ["TenantAdmin"]);
        Assert.True(principal.HasProductAccess("SYNQ_CARECONNECT"));
    }

    [Fact]
    public void HasProductAccess_CaseInsensitive()
    {
        var principal = CreatePrincipal(productRoles: ["synq_careconnect:careconnect_referrer"]);
        Assert.True(principal.HasProductAccess("SYNQ_CARECONNECT"));
    }

    [Fact]
    public void HasProductRole_WithCorrectProductRolePair_ReturnsTrue()
    {
        var principal = CreatePrincipal(productRoles: ["SYNQ_CARECONNECT:CARECONNECT_REFERRER"]);
        Assert.True(principal.HasProductRole("SYNQ_CARECONNECT", ["CARECONNECT_REFERRER"]));
    }

    [Fact]
    public void HasProductRole_WithWrongRole_ReturnsFalse()
    {
        var principal = CreatePrincipal(productRoles: ["SYNQ_CARECONNECT:CARECONNECT_REFERRER"]);
        Assert.False(principal.HasProductRole("SYNQ_CARECONNECT", ["CARECONNECT_RECEIVER"]));
    }

    [Fact]
    public void HasProductRole_WithBareRoleCode_ReturnsFalse()
    {
        var principal = CreatePrincipal(productRoles: ["CARECONNECT_REFERRER"]);
        Assert.False(principal.HasProductRole("SYNQ_CARECONNECT", ["CARECONNECT_REFERRER"]));
    }

    [Fact]
    public void HasProductRole_WithMultipleAllowedRoles_MatchesAny()
    {
        var principal = CreatePrincipal(productRoles: ["SYNQ_CARECONNECT:CARECONNECT_RECEIVER"]);
        Assert.True(principal.HasProductRole("SYNQ_CARECONNECT",
            ["CARECONNECT_REFERRER", "CARECONNECT_RECEIVER"]));
    }

    [Fact]
    public void HasProductRole_PlatformAdminBypasses()
    {
        var principal = CreatePrincipal(systemRoles: ["PlatformAdmin"]);
        Assert.True(principal.HasProductRole("SYNQ_CARECONNECT", ["CARECONNECT_REFERRER"]));
    }

    [Fact]
    public void HasProductRole_CrossProductDoesNotMatch()
    {
        var principal = CreatePrincipal(productRoles: ["SYNQ_FUND:SYNQFUND_REFERRER"]);
        Assert.False(principal.HasProductRole("SYNQ_CARECONNECT", ["SYNQFUND_REFERRER"]));
    }

    [Fact]
    public void GetProductRoles_ReturnsAllClaims()
    {
        var roles = new[] { "SYNQ_CARECONNECT:CARECONNECT_REFERRER", "SYNQ_FUND:SYNQFUND_FUNDER" };
        var principal = CreatePrincipal(productRoles: roles);
        var result = principal.GetProductRoles();
        Assert.Equal(2, result.Count);
        Assert.Contains("SYNQ_CARECONNECT:CARECONNECT_REFERRER", result);
        Assert.Contains("SYNQ_FUND:SYNQFUND_FUNDER", result);
    }

    [Fact]
    public void IsTenantAdminOrAbove_ReturnsTrueForPlatformAdmin()
    {
        var principal = CreatePrincipal(systemRoles: ["PlatformAdmin"]);
        Assert.True(principal.IsTenantAdminOrAbove());
    }

    [Fact]
    public void IsTenantAdminOrAbove_ReturnsTrueForTenantAdmin()
    {
        var principal = CreatePrincipal(systemRoles: ["TenantAdmin"]);
        Assert.True(principal.IsTenantAdminOrAbove());
    }

    [Fact]
    public void IsTenantAdminOrAbove_ReturnsFalseForStandardUser()
    {
        var principal = CreatePrincipal(systemRoles: ["StandardUser"]);
        Assert.False(principal.IsTenantAdminOrAbove());
    }

    [Fact]
    public void IsTenantAdminOrAbove_ReturnsFalseForNoRoles()
    {
        var principal = CreatePrincipal();
        Assert.False(principal.IsTenantAdminOrAbove());
    }

    [Fact]
    public void HasProductAccess_DoesNotMatchPartialProductCode()
    {
        var principal = CreatePrincipal(productRoles: ["SYNQ_CARE:SOME_ROLE"]);
        Assert.False(principal.HasProductAccess("SYNQ_CARECONNECT"));
    }

    [Fact]
    public void HasProductRole_CaseInsensitiveMatch()
    {
        var principal = CreatePrincipal(productRoles: ["synq_fund:synqfund_referrer"]);
        Assert.True(principal.HasProductRole("SYNQ_FUND", ["SYNQFUND_REFERRER"]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NOCOLON")]
    [InlineData(":MISSING_PRODUCT")]
    [InlineData("MISSING_ROLE:")]
    public void HasProductAccess_MalformedClaim_ReturnsFalse(string claimValue)
    {
        var principal = CreatePrincipal(productRoles: [claimValue]);
        Assert.False(principal.HasProductAccess("SYNQ_CARECONNECT"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("NOCOLON")]
    [InlineData(":MISSING_PRODUCT")]
    [InlineData("MISSING_ROLE:")]
    public void HasProductRole_MalformedClaim_ReturnsFalse(string claimValue)
    {
        var principal = CreatePrincipal(productRoles: [claimValue]);
        Assert.False(principal.HasProductRole("SYNQ_CARECONNECT", ["CARECONNECT_REFERRER"]));
    }

    [Fact]
    public void HasProductAccess_MultipleColonsClaim_UsesFirstColonAsSeparator()
    {
        var principal = CreatePrincipal(productRoles: ["SYNQ_CARECONNECT:ROLE:EXTRA"]);
        Assert.True(principal.HasProductAccess("SYNQ_CARECONNECT"));
    }

    [Fact]
    public void GetProductRoles_EmptyClaimsReturnsEmpty()
    {
        var principal = CreatePrincipal();
        var result = principal.GetProductRoles();
        Assert.Empty(result);
    }

    [Fact]
    public void HasProductRole_EmptyAllowedRoles_ReturnsFalse()
    {
        var principal = CreatePrincipal(productRoles: ["SYNQ_CARECONNECT:CARECONNECT_REFERRER"]);
        Assert.False(principal.HasProductRole("SYNQ_CARECONNECT", []));
    }

    [Fact]
    public void HasProductAccess_EmptyRoleSegment_ReturnsFalse()
    {
        var principal = CreatePrincipal(productRoles: ["SYNQ_FUND:"]);
        Assert.False(principal.HasProductAccess("SYNQ_FUND"));
    }

    [Fact]
    public void HasProductAccess_WhitespaceRoleSegment_ReturnsTrue()
    {
        var principal = CreatePrincipal(productRoles: ["SYNQ_FUND: "]);
        Assert.True(principal.HasProductAccess("SYNQ_FUND"));
    }
}
