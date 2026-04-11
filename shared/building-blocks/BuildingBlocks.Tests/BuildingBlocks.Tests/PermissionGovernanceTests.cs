using System.Security.Claims;
using BuildingBlocks.Authorization;
using Identity.Domain;

namespace BuildingBlocks.Tests;

public class PermissionGovernanceTests
{
    private static ClaimsPrincipal CreatePrincipal(
        IEnumerable<string>? permissions = null,
        IEnumerable<string>? systemRoles = null,
        bool authenticated = true)
    {
        var claims = new List<Claim>();
        foreach (var perm in permissions ?? [])
            claims.Add(new Claim("permissions", perm));
        foreach (var sr in systemRoles ?? [])
            claims.Add(new Claim(ClaimTypes.Role, sr));

        var identity = authenticated
            ? new ClaimsIdentity(claims, "TestAuth")
            : new ClaimsIdentity(claims);
        return new ClaimsPrincipal(identity);
    }

    // ── Naming Convention Validation ─────────────────────────────────────────

    [Theory]
    [InlineData("referral:create")]
    [InlineData("application:evaluate")]
    [InlineData("provider:view")]
    [InlineData("lien:create")]
    [InlineData("a:b")]
    [InlineData("domain1:action2")]
    [InlineData("multi:segment:code")]
    public void IsValidCode_ValidCodes_ReturnsTrue(string code)
    {
        Assert.True(Capability.IsValidCode(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Referral:Create")]
    [InlineData("REFERRAL:CREATE")]
    [InlineData("referral_create")]
    [InlineData("referral-create")]
    [InlineData(":create")]
    [InlineData("referral:")]
    [InlineData("referral::create")]
    [InlineData("1referral:create")]
    [InlineData("referral :create")]
    [InlineData("referral: create")]
    public void IsValidCode_InvalidCodes_ReturnsFalse(string code)
    {
        Assert.False(Capability.IsValidCode(code));
    }

    [Fact]
    public void IsValidCode_NullCode_ReturnsFalse()
    {
        Assert.False(Capability.IsValidCode(null!));
    }

    // ── Capability.Create ─────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidCode_SetsAllFields()
    {
        var productId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var cap = Capability.Create(productId, "referral:create", "Create Referral",
            "Allows creating referrals", "Referral", creatorId);

        Assert.Equal(productId, cap.ProductId);
        Assert.Equal("referral:create", cap.Code);
        Assert.Equal("Create Referral", cap.Name);
        Assert.Equal("Allows creating referrals", cap.Description);
        Assert.Equal("Referral", cap.Category);
        Assert.True(cap.IsActive);
        Assert.Equal(creatorId, cap.CreatedBy);
        Assert.NotEqual(Guid.Empty, cap.Id);
    }

    [Fact]
    public void Create_NormalizesCodeToLower()
    {
        var cap = Capability.Create(Guid.NewGuid(), "  Referral:Create  ", "Test");
        Assert.Equal("referral:create", cap.Code);
    }

    [Fact]
    public void Create_InvalidCode_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Capability.Create(Guid.NewGuid(), "INVALID_CODE", "Test"));
    }

    [Fact]
    public void Create_EmptyCode_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Capability.Create(Guid.NewGuid(), "", "Test"));
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Capability.Create(Guid.NewGuid(), "test:code", ""));
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var cap = Capability.Create(Guid.NewGuid(), "test:code", "  My Name  ",
            "  Desc  ", "  Cat  ");

        Assert.Equal("My Name", cap.Name);
        Assert.Equal("Desc", cap.Description);
        Assert.Equal("Cat", cap.Category);
    }

    // ── Capability.Update ─────────────────────────────────────────────────────

    [Fact]
    public void Update_ChangesNameDescriptionCategory()
    {
        var cap = Capability.Create(Guid.NewGuid(), "test:code", "Old Name");
        var updaterId = Guid.NewGuid();

        cap.Update("New Name", "New Desc", "New Category", updaterId);

        Assert.Equal("New Name", cap.Name);
        Assert.Equal("New Desc", cap.Description);
        Assert.Equal("New Category", cap.Category);
        Assert.Equal(updaterId, cap.UpdatedBy);
        Assert.NotNull(cap.UpdatedAtUtc);
    }

    [Fact]
    public void Update_EmptyName_Throws()
    {
        var cap = Capability.Create(Guid.NewGuid(), "test:code", "Name");
        Assert.Throws<ArgumentException>(() => cap.Update("", null, null));
    }

    // ── Capability.Deactivate / Activate ──────────────────────────────────────

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var cap = Capability.Create(Guid.NewGuid(), "test:code", "Test");
        Assert.True(cap.IsActive);

        var updaterId = Guid.NewGuid();
        cap.Deactivate(updaterId);

        Assert.False(cap.IsActive);
        Assert.Equal(updaterId, cap.UpdatedBy);
        Assert.NotNull(cap.UpdatedAtUtc);
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var cap = Capability.Create(Guid.NewGuid(), "test:code", "Test");
        cap.Deactivate();
        Assert.False(cap.IsActive);

        var updaterId = Guid.NewGuid();
        cap.Activate(updaterId);

        Assert.True(cap.IsActive);
        Assert.Equal(updaterId, cap.UpdatedBy);
    }

    // ── HasPermission claim check ─────────────────────────────────────────────

    [Fact]
    public void HasPermission_WithMatchingClaim_ReturnsTrue()
    {
        var principal = CreatePrincipal(permissions: ["SYNQ_FUND.application:create"]);
        Assert.True(principal.HasPermission("SYNQ_FUND.application:create"));
    }

    [Fact]
    public void HasPermission_WithoutMatchingClaim_ReturnsFalse()
    {
        var principal = CreatePrincipal(permissions: ["SYNQ_FUND.application:create"]);
        Assert.False(principal.HasPermission("SYNQ_FUND.application:approve"));
    }

    [Fact]
    public void HasPermission_EmptyPermissions_ReturnsFalse()
    {
        var principal = CreatePrincipal(permissions: []);
        Assert.False(principal.HasPermission("SYNQ_FUND.application:create"));
    }

    [Fact]
    public void HasPermission_CaseInsensitive()
    {
        var principal = CreatePrincipal(permissions: ["SYNQ_FUND.application:create"]);
        Assert.True(principal.HasPermission("synq_fund.application:create"));
    }

    [Fact]
    public void HasPermission_MultiplePermissions_MatchesCorrect()
    {
        var principal = CreatePrincipal(permissions: [
            "SYNQ_FUND.application:create",
            "SYNQ_FUND.application:approve",
            "SYNQ_CARECONNECT.referral:create",
        ]);
        Assert.True(principal.HasPermission("SYNQ_FUND.application:create"));
        Assert.True(principal.HasPermission("SYNQ_FUND.application:approve"));
        Assert.True(principal.HasPermission("SYNQ_CARECONNECT.referral:create"));
        Assert.False(principal.HasPermission("SYNQ_FUND.application:decline"));
    }

    // ── Cross-product permission blocking ─────────────────────────────────────

    [Fact]
    public void HasPermission_CrossProduct_DoesNotLeak()
    {
        var principal = CreatePrincipal(permissions: [
            "SYNQ_FUND.application:create",
        ]);
        Assert.False(principal.HasPermission("SYNQ_CARECONNECT.application:create"));
        Assert.False(principal.HasPermission("SYNQ_LIEN.application:create"));
    }

    // ── Admin bypass ──────────────────────────────────────────────────────────

    [Fact]
    public void IsTenantAdminOrAbove_WithPlatformAdmin_ReturnsTrue()
    {
        var principal = CreatePrincipal(systemRoles: ["PlatformAdmin"]);
        Assert.True(principal.IsTenantAdminOrAbove());
    }

    [Fact]
    public void IsTenantAdminOrAbove_WithTenantAdmin_ReturnsTrue()
    {
        var principal = CreatePrincipal(systemRoles: ["TenantAdmin"]);
        Assert.True(principal.IsTenantAdminOrAbove());
    }

    [Fact]
    public void IsTenantAdminOrAbove_WithRegularUser_ReturnsFalse()
    {
        var principal = CreatePrincipal(permissions: ["SYNQ_FUND.application:create"]);
        Assert.False(principal.IsTenantAdminOrAbove());
    }
}
