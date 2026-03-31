using BuildingBlocks.Authorization;
using CareConnect.Infrastructure.Services;
using Xunit;

namespace CareConnect.Tests.Authorization;

public class CareConnectCapabilityServiceTests
{
    private readonly CareConnectCapabilityService _sut = new();

    [Theory]
    [InlineData(ProductRoleCodes.CareConnectReferrer, CapabilityCodes.ReferralCreate,    true)]
    [InlineData(ProductRoleCodes.CareConnectReferrer, CapabilityCodes.ReferralReadOwn,   true)]
    [InlineData(ProductRoleCodes.CareConnectReferrer, CapabilityCodes.ReferralCancel,    true)]
    [InlineData(ProductRoleCodes.CareConnectReferrer, CapabilityCodes.ProviderSearch,    true)]
    [InlineData(ProductRoleCodes.CareConnectReferrer, CapabilityCodes.ProviderMap,       true)]
    [InlineData(ProductRoleCodes.CareConnectReferrer, CapabilityCodes.DashboardRead,     true)]
    // Referrer must NOT have receiver capabilities
    [InlineData(ProductRoleCodes.CareConnectReferrer, CapabilityCodes.ReferralAccept,    false)]
    [InlineData(ProductRoleCodes.CareConnectReferrer, CapabilityCodes.ReferralDecline,   false)]
    [InlineData(ProductRoleCodes.CareConnectReferrer, CapabilityCodes.ScheduleManage,    false)]
    [InlineData(ProductRoleCodes.CareConnectReferrer, CapabilityCodes.ProviderManage,    false)]
    // Receiver capabilities
    [InlineData(ProductRoleCodes.CareConnectReceiver, CapabilityCodes.ReferralAccept,    true)]
    [InlineData(ProductRoleCodes.CareConnectReceiver, CapabilityCodes.ReferralDecline,   true)]
    [InlineData(ProductRoleCodes.CareConnectReceiver, CapabilityCodes.ReferralReadAddressed, true)]
    [InlineData(ProductRoleCodes.CareConnectReceiver, CapabilityCodes.ScheduleManage,    true)]
    [InlineData(ProductRoleCodes.CareConnectReceiver, CapabilityCodes.AppointmentManage, true)]
    // Receiver must NOT have referrer-only capabilities
    [InlineData(ProductRoleCodes.CareConnectReceiver, CapabilityCodes.ReferralCreate,    false)]
    [InlineData(ProductRoleCodes.CareConnectReceiver, CapabilityCodes.ProviderManage,    false)]
    public async Task HasCapabilityAsync_MatchesExpectedMapping(string roleCode, string capability, bool expected)
    {
        var result = await _sut.HasCapabilityAsync(new[] { roleCode }, capability);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task HasCapabilityAsync_EmptyRoles_ReturnsFalse()
    {
        var result = await _sut.HasCapabilityAsync(Array.Empty<string>(), CapabilityCodes.ReferralCreate);
        Assert.False(result);
    }

    [Fact]
    public async Task HasCapabilityAsync_MultipleRoles_UnionOfCapabilities()
    {
        var roles = new[] { ProductRoleCodes.CareConnectReferrer, ProductRoleCodes.CareConnectReceiver };
        Assert.True(await _sut.HasCapabilityAsync(roles, CapabilityCodes.ReferralCreate));
        Assert.True(await _sut.HasCapabilityAsync(roles, CapabilityCodes.ReferralAccept));
    }

    [Fact]
    public async Task HasCapabilityAsync_UnknownRole_ReturnsFalse()
    {
        var result = await _sut.HasCapabilityAsync(new[] { "UNKNOWN_ROLE" }, CapabilityCodes.ReferralCreate);
        Assert.False(result);
    }

    [Fact]
    public async Task GetCapabilitiesAsync_Referrer_ReturnsExpectedSet()
    {
        var caps = await _sut.GetCapabilitiesAsync(new[] { ProductRoleCodes.CareConnectReferrer });
        Assert.Contains(CapabilityCodes.ReferralCreate, caps);
        Assert.Contains(CapabilityCodes.ProviderSearch, caps);
        Assert.DoesNotContain(CapabilityCodes.ReferralAccept, caps);
        Assert.DoesNotContain(CapabilityCodes.ScheduleManage, caps);
    }
}
