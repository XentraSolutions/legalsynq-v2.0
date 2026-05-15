using Commerce.Contracts.Integration;
using Commerce.Infrastructure.Integration.TenantBilling;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Integration.TenantBilling;

/// <summary>
/// TB-INT-01 — mapping is pure and lives outside DI; cover the full
/// status × recommendation matrix here so the publisher tests can stay
/// focused on transport behaviour.
/// </summary>
public class TenantBillingEntitlementMapperTests
{
    [Theory]
    [InlineData("Good",        "Enabled")]
    [InlineData("Trialing",    "Enabled")]
    [InlineData("GracePeriod", "Enabled")]
    [InlineData("PastDue",     "Enabled")]
    [InlineData("Suspended",   "Suspended")]
    [InlineData("Cancelled",   "Disabled")]
    [InlineData("Closed",      "Disabled")]
    [InlineData("",            "Unknown")]
    [InlineData("WeirdNew",    "Unknown")]
    public void MapEntitlementStatus_returns_expected(string standing, string expected)
        => TenantBillingEntitlementMapper.MapEntitlementStatus(standing).Should().Be(expected);

    [Fact]
    public void MapEntitlementStatus_null_returns_unknown()
        => TenantBillingEntitlementMapper.MapEntitlementStatus(null).Should().Be("Unknown");

    [Theory]
    [InlineData(AccessRecommendation.Allow,        "Allow")]
    [InlineData(AccessRecommendation.ReadOnly,     "ReadOnly")]
    [InlineData(AccessRecommendation.GraceLimited, "GraceLimited")]
    [InlineData(AccessRecommendation.Block,        "Block")]
    [InlineData(AccessRecommendation.Unknown,      "Unknown")]
    public void MapAccessRecommendation_passes_enum_through_as_string(
        AccessRecommendation rec, string expected)
        => TenantBillingEntitlementMapper.MapAccessRecommendation(rec).Should().Be(expected);

    [Fact]
    public void Map_populates_all_required_fields_and_raw_json()
    {
        var ba = Guid.NewGuid();
        var generatedAt = new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc);
        var graceEnd = generatedAt.AddDays(7);
        var planId = Guid.NewGuid();
        var subId = Guid.NewGuid();
        var snapshot = new CommerceEntitlementSnapshot(
            BillingAccountId: ba,
            AccountNumber: "ACC-1",
            DisplayName: "Acme Co",
            HostPlatformKey: "host-x",
            ExternalTenantId: Guid.NewGuid().ToString(),
            AccountStandingStatus: "GracePeriod",
            AccountStandingReason: "card-declined",
            AccountStandingGracePeriodEndsAtUtc: graceEnd,
            AccessRecommendation: AccessRecommendation.GraceLimited,
            Products: new[] { new EntitlementProductRef(Guid.NewGuid(), "PROD-1", "Prod 1") },
            Plans: new[] { new EntitlementPlanRef(planId, "PLAN-A", "Plan A", null, "PROD-1") },
            Subscriptions: new[]
            {
                new EntitlementSubscriptionRef(
                    subId, "SUB-1", "Active",
                    generatedAt, generatedAt.AddDays(30), null, false,
                    new[] { new EntitlementSubscriptionItemRef(Guid.NewGuid(), planId, "PLAN-A", 1) }),
            },
            Limits: Array.Empty<EntitlementFeatureLimit>(),
            GeneratedAtUtc: generatedAt);

        var dto = TenantBillingEntitlementMapper.Map(snapshot);

        dto.BillingAccountId.Should().Be(ba);
        dto.SourceSystem.Should().Be("commerce");
        dto.EntitlementStatus.Should().Be("Enabled");
        dto.AccessRecommendation.Should().Be("GraceLimited");
        dto.SourceSnapshotId.Should().Be(generatedAt.ToString("O"));
        dto.SourceSubscriptionId.Should().Be(subId.ToString());
        dto.SourcePlanKey.Should().Be("PLAN-A");
        dto.SourceProductKey.Should().Be("PROD-1");
        dto.Reason.Should().Be("card-declined");
        dto.EffectiveFromUtc.Should().Be(generatedAt);
        dto.EffectiveToUtc.Should().Be(graceEnd);
        dto.RawSnapshotJson.Should().NotBeNullOrEmpty();
        dto.RawSnapshotJson!.Should().Contain("\"billingAccountId\"")
            .And.Contain(ba.ToString());
    }

    [Fact]
    public void Map_handles_empty_subscriptions_and_plans()
    {
        var snapshot = MinimalSnapshot(standing: "Good", rec: AccessRecommendation.Allow);
        var dto = TenantBillingEntitlementMapper.Map(snapshot);
        dto.SourceSubscriptionId.Should().BeNull();
        dto.SourcePlanKey.Should().BeNull();
        dto.SourceProductKey.Should().BeNull();
        dto.EntitlementStatus.Should().Be("Enabled");
        dto.AccessRecommendation.Should().Be("Allow");
    }

    [Fact]
    public void Map_truncates_long_reason()
    {
        var longReason = new string('x', 2000);
        var snapshot = MinimalSnapshot("PastDue", AccessRecommendation.ReadOnly, reason: longReason);
        var dto = TenantBillingEntitlementMapper.Map(snapshot);
        dto.Reason!.Length.Should().Be(1000);
    }

    private static CommerceEntitlementSnapshot MinimalSnapshot(
        string standing,
        AccessRecommendation rec,
        string? reason = null)
        => new(
            BillingAccountId: Guid.NewGuid(),
            AccountNumber: "ACC",
            DisplayName: "n",
            HostPlatformKey: null,
            ExternalTenantId: null,
            AccountStandingStatus: standing,
            AccountStandingReason: reason,
            AccountStandingGracePeriodEndsAtUtc: null,
            AccessRecommendation: rec,
            Products: Array.Empty<EntitlementProductRef>(),
            Plans: Array.Empty<EntitlementPlanRef>(),
            Subscriptions: Array.Empty<EntitlementSubscriptionRef>(),
            Limits: Array.Empty<EntitlementFeatureLimit>(),
            GeneratedAtUtc: DateTime.UtcNow);
}
