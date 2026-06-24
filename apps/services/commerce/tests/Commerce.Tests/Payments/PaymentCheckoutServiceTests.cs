using Commerce.Application.Common.Exceptions;
using Commerce.Contracts.Payments;
using Commerce.Domain.Payments.Enums;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Payments;

public class PaymentCheckoutServiceTests
{
    private static readonly IReadOnlyList<CheckoutLineItem> Items =
        new[] { new CheckoutLineItem("price_test_123", 1) };

    [Fact]
    public async Task CreateCheckoutSession_creates_customer_and_mapping()
    {
        using var host = new PaymentTestHost();
        var acct = host.AddActiveAccount();
        var sub = host.AddActiveSubscription(acct);

        var resp = await host.Checkout.CreateCheckoutSessionAsync(
            new CreateCheckoutSessionRequest(acct.Id, sub.Id, Items,
                "https://app.test/ok", "https://app.test/no",
                "buyer@example.com", "Buyer"),
            CancellationToken.None);

        resp.Provider.Should().Be(PaymentProviderType.Stripe);
        resp.CheckoutSessionId.Should().Be("cs_test_123");
        resp.CheckoutUrl.Should().Be("https://checkout.stripe.test/abc");
        resp.ProviderCustomerId.Should().Be("cus_test_123");

        host.Provider.CustomerCreateCalls.Should().Be(1);
        host.Provider.CheckoutCreateCalls.Should().Be(1);

        host.Db.PaymentProviderCustomers.Should().ContainSingle();
        host.Db.PaymentProviderSubscriptions.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateCheckoutSession_reuses_existing_customer_and_updates_session()
    {
        using var host = new PaymentTestHost();
        var acct = host.AddActiveAccount();
        var sub = host.AddActiveSubscription(acct);

        var req = new CreateCheckoutSessionRequest(acct.Id, sub.Id, Items, null, null, "x@y.test", "Z");
        await host.Checkout.CreateCheckoutSessionAsync(req, CancellationToken.None);

        host.Provider.NextCheckoutSessionId = "cs_test_456";
        var second = await host.Checkout.CreateCheckoutSessionAsync(req, CancellationToken.None);

        second.CheckoutSessionId.Should().Be("cs_test_456");
        host.Provider.CustomerCreateCalls.Should().Be(1, "customer is reused");
        host.Db.PaymentProviderCustomers.Should().ContainSingle();
        host.Db.PaymentProviderSubscriptions.Should().ContainSingle(s => s.ProviderCheckoutSessionId == "cs_test_456");
    }

    [Fact]
    public async Task CreateCheckoutSession_throws_when_provider_disabled()
    {
        using var host = new PaymentTestHost();
        host.Provider.IsEnabled = false;
        var acct = host.AddActiveAccount();
        var sub = host.AddActiveSubscription(acct);

        var act = () => host.Checkout.CreateCheckoutSessionAsync(
            new CreateCheckoutSessionRequest(acct.Id, sub.Id, Items), CancellationToken.None);
        await act.Should().ThrowAsync<PaymentProviderDisabledException>();
    }

    [Fact]
    public async Task CreateCheckoutSession_rejects_subscription_belonging_to_other_account()
    {
        using var host = new PaymentTestHost();
        var a1 = host.AddActiveAccount("COM-ACC-PAY02");
        var a2 = host.AddActiveAccount("COM-ACC-PAY03");
        var sub = host.AddActiveSubscription(a1);

        var act = () => host.Checkout.CreateCheckoutSessionAsync(
            new CreateCheckoutSessionRequest(a2.Id, sub.Id, Items), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidRelationshipException>();
    }

    [Fact]
    public async Task CreateCheckoutSession_validates_input()
    {
        using var host = new PaymentTestHost();
        var act = () => host.Checkout.CreateCheckoutSessionAsync(
            new CreateCheckoutSessionRequest(Guid.Empty, Guid.Empty, Items), CancellationToken.None);
        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }
}
