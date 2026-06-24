using Commerce.Infrastructure.Integration.HostAdapters;
using Xunit;

namespace Commerce.Tests.Integration;

/// <summary>
/// LS-INT-01 — unit tests for the LegalSynq identity integration adapters in Commerce.
/// These tests validate configuration and role helpers without spinning up a full
/// ASP.NET pipeline.
/// </summary>
public class LegalSynqCommerceIdentityTests
{
    // ── LegalSynqIdentityOptions ────────────────────────────────────────────

    [Fact]
    public void DefaultOptions_HasExpectedDefaults()
    {
        var opts = new LegalSynqIdentityOptions();

        Assert.False(opts.Enabled);
        Assert.Equal("legalsynq-identity", opts.Issuer);
        Assert.Equal("legalsynq-platform", opts.Audience);
        Assert.Equal(string.Empty, opts.SigningKey);
        Assert.Equal("legalsynq", opts.HostPlatformKey);
    }

    [Fact]
    public void SectionName_MatchesConfigPath()
    {
        Assert.Equal("LegalSynq:Identity", LegalSynqIdentityOptions.SectionName);
    }

    // ── LegalSynqPlatformRoles ──────────────────────────────────────────────

    [Theory]
    [InlineData(LegalSynqPlatformRoles.PlatformAdmin, true)]
    [InlineData(LegalSynqPlatformRoles.TenantAdmin, true)]
    [InlineData(LegalSynqPlatformRoles.BillingManager, true)]
    [InlineData(LegalSynqPlatformRoles.InternalService, true)]
    [InlineData(LegalSynqPlatformRoles.BillingReadOnly, false)]
    [InlineData(LegalSynqPlatformRoles.SupportAgent, false)]
    [InlineData("SomeOtherRole", false)]
    public void HasBillingWrite_ReturnsExpected(string role, bool expected)
    {
        var result = LegalSynqPlatformRoles.HasBillingWrite([role]);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(LegalSynqPlatformRoles.PlatformAdmin, true)]
    [InlineData(LegalSynqPlatformRoles.TenantAdmin, true)]
    [InlineData(LegalSynqPlatformRoles.BillingManager, true)]
    [InlineData(LegalSynqPlatformRoles.BillingReadOnly, true)]
    [InlineData(LegalSynqPlatformRoles.SupportAgent, true)]
    [InlineData(LegalSynqPlatformRoles.InternalService, true)]
    [InlineData("Unknown", false)]
    public void HasBillingRead_ReturnsExpected(string role, bool expected)
    {
        var result = LegalSynqPlatformRoles.HasBillingRead([role]);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void HasBillingWrite_EmptyCollection_ReturnsFalse()
    {
        Assert.False(LegalSynqPlatformRoles.HasBillingWrite([]));
    }

    [Fact]
    public void HasBillingWrite_MultipleRoles_OnlyNeedOneMatch()
    {
        var roles = new[] { "SomeUnknownRole", LegalSynqPlatformRoles.BillingManager };
        Assert.True(LegalSynqPlatformRoles.HasBillingWrite(roles));
    }

    // ── Standalone guard: options are disabled by default ──────────────────

    [Fact]
    public void IdentityOptions_Enabled_DefaultFalse_GuaranteesStandaloneMode()
    {
        var opts = new LegalSynqIdentityOptions();
        Assert.False(opts.Enabled,
            "LegalSynq:Identity:Enabled must default to false to preserve standalone behavior on deploy.");
    }
}
