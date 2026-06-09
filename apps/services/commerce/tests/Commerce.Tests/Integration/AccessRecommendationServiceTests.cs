using Commerce.Contracts.Integration;
using Commerce.Domain.AccountStanding.Enums;
using Commerce.Domain.Billing.Enums;
using Commerce.Infrastructure.Integration.Services;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Integration;

public class AccessRecommendationServiceTests
{
    [Theory]
    [InlineData(AccountStandingStatus.Good,        true,  AccessRecommendation.Allow)]
    [InlineData(AccountStandingStatus.Good,        false, AccessRecommendation.ReadOnly)]
    [InlineData(AccountStandingStatus.Trialing,    true,  AccessRecommendation.Allow)]
    [InlineData(AccountStandingStatus.GracePeriod, true,  AccessRecommendation.GraceLimited)]
    [InlineData(AccountStandingStatus.PastDue,     true,  AccessRecommendation.ReadOnly)]
    [InlineData(AccountStandingStatus.Suspended,   true,  AccessRecommendation.Block)]
    [InlineData(AccountStandingStatus.Closed,      true,  AccessRecommendation.Block)]
    [InlineData(AccountStandingStatus.Cancelled,   false, AccessRecommendation.ReadOnly)]
    public void Maps_standing_to_expected_recommendation(
        AccountStandingStatus standing,
        bool hasActiveOrTrialing,
        AccessRecommendation expected)
    {
        var (rec, _) = CommerceAccessRecommendationService.ComputeRecommendation(
            billingAccountStatus: BillingAccountStatus.Active,
            standingStatus: standing,
            standingReason: null,
            hasActiveOrTrialing: hasActiveOrTrialing);
        rec.Should().Be(expected);
    }

    [Fact]
    public void No_standing_record_yields_unknown()
    {
        var (rec, reason) = CommerceAccessRecommendationService.ComputeRecommendation(
            billingAccountStatus: BillingAccountStatus.Active,
            standingStatus: null,
            standingReason: null,
            hasActiveOrTrialing: false);
        rec.Should().Be(AccessRecommendation.Unknown);
        reason.Should().Contain("No account-standing");
    }

    [Theory]
    [InlineData(BillingAccountStatus.Suspended)]
    [InlineData(BillingAccountStatus.Closed)]
    public void Billing_account_state_overrides_standing(BillingAccountStatus status)
    {
        var (rec, _) = CommerceAccessRecommendationService.ComputeRecommendation(
            billingAccountStatus: status,
            standingStatus: AccountStandingStatus.Good,
            standingReason: null,
            hasActiveOrTrialing: true);
        rec.Should().Be(AccessRecommendation.Block);
    }

    [Fact]
    public async Task End_to_end_returns_response_for_seeded_account()
    {
        using var host = new IntegrationTestHost();
        var seed = host.SeedAccount(standing: AccountStandingStatus.GracePeriod);

        var rec = await host.Recommendation.GetForBillingAccountAsync(seed.BillingAccountId, default);

        rec.Should().NotBeNull();
        rec!.Recommendation.Should().Be(AccessRecommendation.GraceLimited);
        rec.HostPlatformKey.Should().Be(seed.HostPlatformKey);
        rec.AccountStandingStatus.Should().Be(nameof(AccountStandingStatus.GracePeriod));
        rec.HasActiveOrTrialingSubscription.Should().BeTrue();
    }

    [Fact]
    public async Task End_to_end_returns_null_for_missing_account()
    {
        using var host = new IntegrationTestHost();
        var rec = await host.Recommendation.GetForBillingAccountAsync(Guid.CreateVersion7(), default);
        rec.Should().BeNull();
    }
}
