using Commerce.Domain.Subscriptions;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Subscriptions;

public class SubscriptionNumberTests
{
    [Fact]
    public void Format_pads_to_six_digits()
    {
        SubscriptionNumber.Format(1).Should().Be("COM-SUB-000001");
        SubscriptionNumber.Format(1234567).Should().Be("COM-SUB-1234567");
    }

    [Fact]
    public void Format_rejects_zero_and_negative()
    {
        var act = () => SubscriptionNumber.Format(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("COM-SUB-000001", true, 1L)]
    [InlineData("COM-SUB-000042", true, 42L)]
    [InlineData("COM-SUB-1000000", true, 1000000L)]
    [InlineData("BAD", false, 0L)]
    [InlineData("COM-SUB-", false, 0L)]
    [InlineData("COM-SUB-abc", false, 0L)]
    [InlineData(null, false, 0L)]
    public void TryParseSequence_round_trips(string? input, bool expectedOk, long expectedSeq)
    {
        var ok = SubscriptionNumber.TryParseSequence(input, out var seq);
        ok.Should().Be(expectedOk);
        seq.Should().Be(expectedSeq);
    }
}
