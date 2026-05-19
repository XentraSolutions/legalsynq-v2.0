using Commerce.Application.Common.Exceptions;
using Commerce.Application.Payments.Abstractions;
using Commerce.Contracts.Payments;
using Commerce.Domain.Payments;
using Commerce.Domain.Payments.Enums;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Payments;

public class PaymentWebhookServiceTests
{
    [Fact]
    public async Task ReceiveAsync_throws_on_bad_signature()
    {
        using var host = new PaymentTestHost();
        host.Provider.VerifyShouldFail = true;

        var act = () => host.Webhooks.ReceiveAsync(PaymentProviderType.Stripe, "{}", "sig", CancellationToken.None);
        await act.Should().ThrowAsync<InvalidWebhookSignatureException>();
    }

    [Fact]
    public async Task ReceiveAsync_returns_disabled_when_provider_off()
    {
        using var host = new PaymentTestHost();
        host.Provider.IsEnabled = false;
        var act = () => host.Webhooks.ReceiveAsync(PaymentProviderType.Stripe, "{}", "sig", CancellationToken.None);
        await act.Should().ThrowAsync<PaymentProviderDisabledException>();
    }

    [Fact]
    public async Task ReceiveAsync_processes_checkout_completed_and_marks_mapping_active()
    {
        using var host = new PaymentTestHost();
        var acct = host.AddActiveAccount();
        var sub = host.AddActiveSubscription(acct);
        await host.Checkout.CreateCheckoutSessionAsync(
            new CreateCheckoutSessionRequest(acct.Id, sub.Id,
                new[] { new CheckoutLineItem("price_test", 1) },
                null, null, "a@b.test", "B"),
            CancellationToken.None);

        host.Provider.Translator = _ => new NormalizedProviderEvent(
            PaymentProviderType.Stripe, "evt_1", "checkout.session.completed",
            NormalizedProviderEventKind.CheckoutSessionCompleted,
            "cus_test_123", "sub_remote_1", "cs_test_123", null,
            null, null, null, null, null, null);

        var result = await host.Webhooks.ReceiveAsync(PaymentProviderType.Stripe, "{}", "sig", CancellationToken.None);
        result.Status.Should().Be(PaymentProviderEventProcessingStatus.Processed);

        var mapping = host.Db.PaymentProviderSubscriptions.Single();
        mapping.Status.Should().Be(ProviderSubscriptionStatus.Active);
        mapping.ProviderSubscriptionId.Should().Be("sub_remote_1");
    }

    [Fact]
    public async Task ReceiveAsync_is_idempotent_on_duplicate_event_id()
    {
        using var host = new PaymentTestHost();
        host.Provider.Translator = _ => new NormalizedProviderEvent(
            PaymentProviderType.Stripe, "evt_dup", "x.y",
            NormalizedProviderEventKind.Unsupported,
            null, null, null, null, null, null, null, null, null, null);

        var first = await host.Webhooks.ReceiveAsync(PaymentProviderType.Stripe, "{}", "sig", CancellationToken.None);
        var second = await host.Webhooks.ReceiveAsync(PaymentProviderType.Stripe, "{}", "sig", CancellationToken.None);

        first.Status.Should().Be(PaymentProviderEventProcessingStatus.Ignored);
        second.Status.Should().Be(PaymentProviderEventProcessingStatus.Duplicate);
        host.Db.PaymentProviderEventLogs.Should().ContainSingle();
    }

    [Fact]
    public async Task ReceiveAsync_payment_method_attached_creates_method_reference()
    {
        using var host = new PaymentTestHost();
        var acct = host.AddActiveAccount();
        // Seed customer so the attach can find the BillingAccount.
        host.Db.PaymentProviderCustomers.Add(PaymentProviderCustomer.Create(
            acct.Id, PaymentProviderType.Stripe, "cus_attach", "x@y.test", "X", host.Clock.UtcNow));
        host.Db.SaveChanges();

        host.Provider.Translator = _ => new NormalizedProviderEvent(
            PaymentProviderType.Stripe, "evt_pm", "payment_method.attached",
            NormalizedProviderEventKind.PaymentMethodAttached,
            "cus_attach", null, null, "pm_card_visa",
            "visa", "4242", 12, 2030, null, null);

        var res = await host.Webhooks.ReceiveAsync(PaymentProviderType.Stripe, "{}", "sig", CancellationToken.None);
        res.Status.Should().Be(PaymentProviderEventProcessingStatus.Processed);
        var pm = host.Db.PaymentMethodReferences.Single();
        pm.BillingAccountId.Should().Be(acct.Id);
        pm.Brand.Should().Be("visa");
        pm.Last4.Should().Be("4242");
    }

    [Fact]
    public async Task ReceiveAsync_unparsable_payload_marks_failed()
    {
        using var host = new PaymentTestHost();
        host.Provider.Translator = _ => throw new System.Text.Json.JsonException("bad");
        var res = await host.Webhooks.ReceiveAsync(PaymentProviderType.Stripe, "not-json", "sig", CancellationToken.None);
        res.Status.Should().Be(PaymentProviderEventProcessingStatus.Failed);
    }

    [Fact]
    public async Task ListAsync_returns_recent_logs_filtered()
    {
        using var host = new PaymentTestHost();
        host.Provider.Translator = _ => new NormalizedProviderEvent(
            PaymentProviderType.Stripe, $"evt_{Guid.CreateVersion7():N}", "noop",
            NormalizedProviderEventKind.Unsupported,
            null, null, null, null, null, null, null, null, null, null);

        await host.Webhooks.ReceiveAsync(PaymentProviderType.Stripe, "{}", "sig", CancellationToken.None);
        await host.Webhooks.ReceiveAsync(PaymentProviderType.Stripe, "{}", "sig", CancellationToken.None);

        var all = await host.Webhooks.ListAsync(null, null, 50, CancellationToken.None);
        all.Should().HaveCount(2);

        var ignored = await host.Webhooks.ListAsync(PaymentProviderType.Stripe, PaymentProviderEventProcessingStatus.Ignored, 50, CancellationToken.None);
        ignored.Should().HaveCount(2);
    }
}
