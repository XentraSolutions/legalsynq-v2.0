using BuildingBlocks.Authorization;
using CareConnect.Domain;
using Xunit;

namespace CareConnect.Tests.Domain;

public class ReferralWorkflowRulesTests
{
    // ── Canonical transitions ─────────────────────────────────────────────────
    [Theory]
    [InlineData("New",      "Accepted",  true)]
    [InlineData("New",      "Declined",  true)]
    [InlineData("New",      "Cancelled", true)]
    [InlineData("New",      "Scheduled", false)]
    [InlineData("Accepted", "Scheduled", true)]
    [InlineData("Accepted", "Declined",  true)]
    [InlineData("Accepted", "Cancelled", true)]
    [InlineData("Accepted", "New",       false)]
    [InlineData("Scheduled","Completed", true)]
    [InlineData("Scheduled","Cancelled", true)]
    [InlineData("Scheduled","Accepted",  false)]
    [InlineData("Completed","Cancelled", false)]
    [InlineData("Declined", "Accepted",  false)]
    [InlineData("Cancelled","New",       false)]
    public void IsValidTransition_CanonicalStatuses_ReturnsExpected(string from, string to, bool expected)
    {
        Assert.Equal(expected, ReferralWorkflowRules.IsValidTransition(from, to));
    }

    // ── Legacy transitions (old data rows that haven't been migrated yet) ─────
    [Theory]
    [InlineData("Received",  "Accepted",  true)]
    [InlineData("Received",  "Declined",  true)]
    [InlineData("Received",  "Cancelled", true)]
    [InlineData("Contacted", "Accepted",  true)]
    [InlineData("Contacted", "Scheduled", true)]
    public void IsValidTransition_LegacyStatuses_AllowsTransitionToCanonical(string from, string to, bool expected)
    {
        Assert.Equal(expected, ReferralWorkflowRules.IsValidTransition(from, to));
    }

    // ── Terminal states ───────────────────────────────────────────────────────
    [Theory]
    [InlineData("Completed", true)]
    [InlineData("Declined",  true)]
    [InlineData("Cancelled", true)]
    [InlineData("New",       false)]
    [InlineData("Accepted",  false)]
    [InlineData("Scheduled", false)]
    public void IsTerminal_ReturnsExpected(string status, bool expected)
    {
        Assert.Equal(expected, ReferralWorkflowRules.IsTerminal(status));
    }

    // ── RequiredCapabilityFor maps to the correct code ────────────────────────
    [Theory]
    [InlineData("Accepted",  CapabilityCodes.ReferralAccept)]
    [InlineData("Declined",  CapabilityCodes.ReferralDecline)]
    [InlineData("Cancelled", CapabilityCodes.ReferralCancel)]
    [InlineData("Scheduled", CapabilityCodes.ReferralUpdateStatus)]
    [InlineData("Completed", CapabilityCodes.ReferralUpdateStatus)]
    public void RequiredCapabilityFor_ReturnsExpected(string toStatus, string expectedCap)
    {
        Assert.Equal(expectedCap, ReferralWorkflowRules.RequiredCapabilityFor(toStatus));
    }

    // ── ValidStatuses.All contains canonical values ───────────────────────────
    [Fact]
    public void ValidStatuses_All_ContainsCanonicalValues()
    {
        var all = Referral.ValidStatuses.All;
        Assert.Contains("New",       all);
        Assert.Contains("Accepted",  all);
        Assert.Contains("Scheduled", all);
        Assert.Contains("Completed", all);
        Assert.Contains("Declined",  all);
        Assert.Contains("Cancelled", all);

        // Legacy values must NOT be in canonical All list
        Assert.DoesNotContain("Received",  all);
        Assert.DoesNotContain("Contacted", all);
    }

    // ── Legacy.Normalize maps old values correctly ────────────────────────────
    [Theory]
    [InlineData("Received",  "Accepted")]
    [InlineData("Contacted", "Accepted")]
    [InlineData("New",       "New")]
    [InlineData("Accepted",  "Accepted")]
    [InlineData("Declined",  "Declined")]
    public void Legacy_Normalize_MapsExpected(string input, string expected)
    {
        Assert.Equal(expected, Referral.ValidStatuses.Legacy.Normalize(input));
    }

    // ── ValidateTransition throws for invalid transitions ─────────────────────
    [Fact]
    public void ValidateTransition_InvalidTransition_Throws()
    {
        Assert.Throws<BuildingBlocks.Exceptions.ValidationException>(
            () => ReferralWorkflowRules.ValidateTransition("Completed", "New"));
    }

    [Fact]
    public void ValidateTransition_SameStatus_DoesNotThrow()
    {
        var ex = Record.Exception(
            () => ReferralWorkflowRules.ValidateTransition("Accepted", "Accepted"));
        Assert.Null(ex);
    }
}
