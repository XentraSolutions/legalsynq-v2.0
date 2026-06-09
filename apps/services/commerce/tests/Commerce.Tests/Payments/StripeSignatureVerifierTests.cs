using Commerce.Application.Common.Exceptions;
using Commerce.Infrastructure.Payments.Stripe;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Payments;

public class StripeSignatureVerifierTests
{
    private const string Secret = "whsec_unit_test";
    private static readonly DateTime Now = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
    private const string Body = "{\"id\":\"evt_1\",\"type\":\"checkout.session.completed\"}";

    [Fact]
    public void Verify_accepts_freshly_signed_payload()
    {
        var sig = StripeSignatureVerifier.SignForTesting(Body, Secret, Now);
        var act = () => StripeSignatureVerifier.Verify(Body, sig, Secret, 300, Now);
        act.Should().NotThrow();
    }

    [Fact]
    public void Verify_rejects_tampered_body()
    {
        var sig = StripeSignatureVerifier.SignForTesting(Body, Secret, Now);
        var act = () => StripeSignatureVerifier.Verify(Body + "x", sig, Secret, 300, Now);
        act.Should().Throw<InvalidWebhookSignatureException>();
    }

    [Fact]
    public void Verify_rejects_wrong_secret()
    {
        var sig = StripeSignatureVerifier.SignForTesting(Body, Secret, Now);
        var act = () => StripeSignatureVerifier.Verify(Body, sig, "whsec_other", 300, Now);
        act.Should().Throw<InvalidWebhookSignatureException>();
    }

    [Fact]
    public void Verify_rejects_stale_timestamp()
    {
        var oldTs = Now.AddMinutes(-30);
        var sig = StripeSignatureVerifier.SignForTesting(Body, Secret, oldTs);
        var act = () => StripeSignatureVerifier.Verify(Body, sig, Secret, 300, Now);
        act.Should().Throw<InvalidWebhookSignatureException>();
    }

    [Fact]
    public void Verify_rejects_missing_header()
    {
        var act = () => StripeSignatureVerifier.Verify(Body, null, Secret, 300, Now);
        act.Should().Throw<InvalidWebhookSignatureException>();
    }

    [Fact]
    public void Verify_rejects_malformed_header()
    {
        var act = () => StripeSignatureVerifier.Verify(Body, "garbage", Secret, 300, Now);
        act.Should().Throw<InvalidWebhookSignatureException>();
    }

    [Fact]
    public void Verify_throws_configuration_when_secret_missing()
    {
        var sig = StripeSignatureVerifier.SignForTesting(Body, Secret, Now);
        var act = () => StripeSignatureVerifier.Verify(Body, sig, null, 300, Now);
        act.Should().Throw<PaymentProviderConfigurationException>();
    }
}
