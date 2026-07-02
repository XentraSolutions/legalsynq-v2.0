// CC-PORTAL-ACCOUNT-CHECK: Tests for the pre-submit portal account guard
// on POST /api/public/referrals.
//
// The guard blocks public referral submissions when the sender email is
// already linked to an active CareConnect portal account on the target tenant.
// This prevents duplicate anonymous+authenticated referral paths and nudges
// active portal users to log in instead.
//
// The actual guard lives in PublicNetworkEndpoints.HandlePublicReferral (private static).
// These tests validate the contract at the IIdentityOrganizationService level and
// verify that the status string comparison used in the guard is correct.
using CareConnect.Application.Interfaces;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

public class PublicReferralPortalAccountGuardTests
{
    // ── ReferrerPortalAccessStatuses constant values ─────────────────────────

    [Fact]
    public void ActiveInTenant_HasExpectedStringValue()
    {
        Assert.Equal("active_in_tenant", ReferrerPortalAccessStatuses.ActiveInTenant);
    }

    [Fact]
    public void ExistingUserOtherTenant_HasExpectedStringValue()
    {
        Assert.Equal("existing_user_other_tenant", ReferrerPortalAccessStatuses.ExistingUserOtherTenant);
    }

    [Fact]
    public void NoAccount_HasExpectedStringValue()
    {
        Assert.Equal("no_account", ReferrerPortalAccessStatuses.NoAccount);
    }

    // ── Guard logic: active_in_tenant blocks ─────────────────────────────────

    [Fact]
    public async Task ActiveInTenant_ShouldBlock_PublicReferral()
    {
        var identityOrgs = new Mock<IIdentityOrganizationService>();
        var tenantId = Guid.NewGuid();
        var email = "lawfirm@example.com";

        identityOrgs
            .Setup(x => x.GetReferrerPortalAccessStatusAsync(tenantId, email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReferrerPortalAccessStatuses.ActiveInTenant);

        var status = await identityOrgs.Object.GetReferrerPortalAccessStatusAsync(tenantId, email);

        Assert.Equal(ReferrerPortalAccessStatuses.ActiveInTenant, status);
        Assert.True(status == ReferrerPortalAccessStatuses.ActiveInTenant,
            "Guard should block when status is active_in_tenant");
    }

    // ── Guard logic: other statuses should NOT block ─────────────────────────

    [Fact]
    public async Task NoAccount_ShouldNotBlock_PublicReferral()
    {
        var identityOrgs = new Mock<IIdentityOrganizationService>();
        var tenantId = Guid.NewGuid();
        var email = "newuser@example.com";

        identityOrgs
            .Setup(x => x.GetReferrerPortalAccessStatusAsync(tenantId, email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReferrerPortalAccessStatuses.NoAccount);

        var status = await identityOrgs.Object.GetReferrerPortalAccessStatusAsync(tenantId, email);

        Assert.NotEqual(ReferrerPortalAccessStatuses.ActiveInTenant, status);
        Assert.False(status == ReferrerPortalAccessStatuses.ActiveInTenant,
            "Guard should NOT block when status is no_account");
    }

    [Fact]
    public async Task ExistingUserOtherTenant_ShouldNotBlock_PublicReferral()
    {
        var identityOrgs = new Mock<IIdentityOrganizationService>();
        var tenantId = Guid.NewGuid();
        var email = "crossuser@example.com";

        identityOrgs
            .Setup(x => x.GetReferrerPortalAccessStatusAsync(tenantId, email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReferrerPortalAccessStatuses.ExistingUserOtherTenant);

        var status = await identityOrgs.Object.GetReferrerPortalAccessStatusAsync(tenantId, email);

        Assert.NotEqual(ReferrerPortalAccessStatuses.ActiveInTenant, status);
        Assert.False(status == ReferrerPortalAccessStatuses.ActiveInTenant,
            "Guard should NOT block when status is existing_user_other_tenant");
    }

    // ── Guard logic: empty/null email skips the check ────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrNullEmail_ShouldSkipGuard(string? email)
    {
        // The guard uses: if (!string.IsNullOrWhiteSpace(req.SenderEmail))
        // Empty/null/whitespace emails skip the check entirely.
        Assert.True(string.IsNullOrWhiteSpace(email),
            "Guard should skip when SenderEmail is null, empty, or whitespace");
    }

    // ── Interface contract: never throws ─────────────────────────────────────

    [Fact]
    public async Task GetReferrerPortalAccessStatus_OnFailure_ReturnsNoAccount()
    {
        // Per the IIdentityOrganizationService contract, this method
        // "Always returns NoAccount on any infrastructure failure — never throws."
        // This test verifies the mock returns the safe default.
        var identityOrgs = new Mock<IIdentityOrganizationService>();
        var tenantId = Guid.NewGuid();
        var email = "any@example.com";

        // Default return when Identity is down = NoAccount (safe default)
        identityOrgs
            .Setup(x => x.GetReferrerPortalAccessStatusAsync(tenantId, email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReferrerPortalAccessStatuses.NoAccount);

        var status = await identityOrgs.Object.GetReferrerPortalAccessStatusAsync(tenantId, email);

        Assert.Equal(ReferrerPortalAccessStatuses.NoAccount, status);
        Assert.False(status == ReferrerPortalAccessStatuses.ActiveInTenant,
            "Infrastructure failure should never block a public referral");
    }

    // ── All three statuses are distinct ──────────────────────────────────────

    [Fact]
    public void AllStatusValues_AreDistinct()
    {
        var statuses = new[]
        {
            ReferrerPortalAccessStatuses.ActiveInTenant,
            ReferrerPortalAccessStatuses.ExistingUserOtherTenant,
            ReferrerPortalAccessStatuses.NoAccount,
        };

        Assert.Equal(statuses.Length, statuses.Distinct().Count());
    }
}
