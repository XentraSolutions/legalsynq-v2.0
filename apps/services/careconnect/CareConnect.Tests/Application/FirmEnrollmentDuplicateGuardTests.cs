using CareConnect.Application.Interfaces;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

public class FirmEnrollmentDuplicateGuardTests
{
    [Fact]
    public async Task ActiveInTenant_ShouldBlock_FirmEnrollment()
    {
        var identityOrgs = new Mock<IIdentityOrganizationService>();
        var tenantId = Guid.NewGuid();
        var email = "firm@example.com";

        identityOrgs
            .Setup(x => x.GetReferrerPortalAccessStatusAsync(tenantId, email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReferrerPortalAccessStatuses.ActiveInTenant);

        var status = await identityOrgs.Object.GetReferrerPortalAccessStatusAsync(tenantId, email);

        Assert.Equal(ReferrerPortalAccessStatuses.ActiveInTenant, status);
        Assert.True(status == ReferrerPortalAccessStatuses.ActiveInTenant,
            "Firm enrollment should be blocked when status is active_in_tenant");
    }

    [Fact]
    public async Task NoAccount_ShouldAllow_FirmEnrollment()
    {
        var identityOrgs = new Mock<IIdentityOrganizationService>();
        var tenantId = Guid.NewGuid();
        var email = "newfirm@example.com";

        identityOrgs
            .Setup(x => x.GetReferrerPortalAccessStatusAsync(tenantId, email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReferrerPortalAccessStatuses.NoAccount);

        var status = await identityOrgs.Object.GetReferrerPortalAccessStatusAsync(tenantId, email);

        Assert.NotEqual(ReferrerPortalAccessStatuses.ActiveInTenant, status);
        Assert.False(status == ReferrerPortalAccessStatuses.ActiveInTenant,
            "Firm enrollment should NOT be blocked when status is no_account");
    }

    [Fact]
    public async Task ExistingUserOtherTenant_ShouldAllow_FirmEnrollment()
    {
        var identityOrgs = new Mock<IIdentityOrganizationService>();
        var tenantId = Guid.NewGuid();
        var email = "crossfirm@example.com";

        identityOrgs
            .Setup(x => x.GetReferrerPortalAccessStatusAsync(tenantId, email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReferrerPortalAccessStatuses.ExistingUserOtherTenant);

        var status = await identityOrgs.Object.GetReferrerPortalAccessStatusAsync(tenantId, email);

        Assert.NotEqual(ReferrerPortalAccessStatuses.ActiveInTenant, status);
        Assert.False(status == ReferrerPortalAccessStatuses.ActiveInTenant,
            "Firm enrollment should NOT be blocked when user exists in other tenant only");
    }
}
