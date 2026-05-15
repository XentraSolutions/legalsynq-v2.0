using Commerce.Domain.Payments;
using Commerce.Domain.Payments.Enums;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Payments;

public class PaymentDomainTests
{
    private static DateTime Now => new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PaymentProviderCustomer_requires_account_and_id()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderCustomer.Create(Guid.Empty, PaymentProviderType.Stripe, "cus_x", null, null, Now));
        Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderCustomer.Create(Guid.NewGuid(), PaymentProviderType.Stripe, "  ", null, null, Now));
    }

    [Fact]
    public void PaymentProviderSubscription_starts_pending_and_transitions()
    {
        var sub = PaymentProviderSubscription.Create(Guid.NewGuid(), PaymentProviderType.Stripe, "cus_1", "cs_1", Now);
        sub.Status.Should().Be(ProviderSubscriptionStatus.Pending);

        sub.MarkActive("sub_1", Now.AddMinutes(1));
        sub.Status.Should().Be(ProviderSubscriptionStatus.Active);
        sub.ProviderSubscriptionId.Should().Be("sub_1");

        sub.MarkCancelled(Now.AddMinutes(2));
        sub.Status.Should().Be(ProviderSubscriptionStatus.Cancelled);

        sub.MarkFailed(Now.AddMinutes(3));
        sub.Status.Should().Be(ProviderSubscriptionStatus.Failed);
    }

    [Fact]
    public void PaymentMethodReference_validates_safe_fields()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PaymentMethodReference.Create(Guid.NewGuid(), PaymentProviderType.Stripe, "pm_1", null, "visa", "12345", 5, 2030, Now));
        Assert.Throws<InvalidOperationException>(() =>
            PaymentMethodReference.Create(Guid.NewGuid(), PaymentProviderType.Stripe, "pm_1", null, "visa", "1234", 13, 2030, Now));
        Assert.Throws<InvalidOperationException>(() =>
            PaymentMethodReference.Create(Guid.NewGuid(), PaymentProviderType.Stripe, "pm_1", null, "visa", "1234", 5, 1900, Now));

        var pm = PaymentMethodReference.Create(Guid.NewGuid(), PaymentProviderType.Stripe, "pm_1", "cus_1", "visa", "4242", 5, 2030, Now);
        pm.IsDefault.Should().BeFalse();
        pm.MakeDefault(Now.AddMinutes(1));
        pm.IsDefault.Should().BeTrue();
        pm.DemoteDefault(Now.AddMinutes(2));
        pm.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void PaymentProviderEventLog_lifecycle()
    {
        var log = PaymentProviderEventLog.Receive(PaymentProviderType.Stripe, "evt_1", "checkout.session.completed", "{}", Now);
        log.ProcessingStatus.Should().Be(PaymentProviderEventProcessingStatus.Received);

        log.MarkProcessed(Now.AddMinutes(1));
        log.ProcessingStatus.Should().Be(PaymentProviderEventProcessingStatus.Processed);
        log.ProcessedAtUtc.Should().NotBeNull();

        var log2 = PaymentProviderEventLog.Receive(PaymentProviderType.Stripe, "evt_2", "x.y", "{}", Now);
        log2.MarkFailed("boom", Now);
        log2.ProcessingStatus.Should().Be(PaymentProviderEventProcessingStatus.Failed);
        log2.ErrorMessage.Should().Be("boom");

        Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderEventLog.Receive(PaymentProviderType.Stripe, "  ", "type", "{}", Now));
    }
}

public class PaymentManualFactoryTests
{
    private static DateTime Now => new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateManual_sets_provider_status_and_paidAt()
    {
        var paidAt = Now.AddHours(-3);
        var p = Payment.CreateManual(
            billingAccountId: Guid.NewGuid(),
            invoiceId: Guid.NewGuid(),
            subscriptionId: null,
            amountMinor: 12345,
            currency: "usd",
            paidAtUtc: paidAt,
            method: "Check",
            transactionReference: " chk-1001 ",
            recordedByLabel: "  Jamie F.  ",
            notes: "Front desk",
            nowUtc: Now);

        p.Provider.Should().Be(PaymentProviderType.Manual);
        p.Status.Should().Be(PaymentStatus.Succeeded);
        p.PaidAtUtc.Should().Be(paidAt);
        p.AmountMinor.Should().Be(12345);
        p.Currency.Should().Be("USD");
        // Manual payments deliberately leave ProviderPaymentId null so
        // that they don't collide with the unique
        // (Provider, ProviderPaymentId) index, which is reserved for
        // provider-issued ids. The admin-supplied reference is stored
        // separately on TransactionReference.
        p.ProviderPaymentId.Should().BeNull();
        p.TransactionReference.Should().Be("chk-1001");
        p.Method.Should().Be("Check");
        p.RecordedByLabel.Should().Be("Jamie F.");
        p.Notes.Should().Be("Front desk");
        p.CreatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void CreateManual_rejects_zero_or_negative_amount()
    {
        Assert.Throws<InvalidOperationException>(() => Payment.CreateManual(
            Guid.NewGuid(), null, null, 0, "USD", Now, null, null, null, null, Now));
        Assert.Throws<InvalidOperationException>(() => Payment.CreateManual(
            Guid.NewGuid(), null, null, -50, "USD", Now, null, null, null, null, Now));
    }

    [Fact]
    public void CreateManual_rejects_invalid_currency_and_default_paidAt()
    {
        Assert.Throws<InvalidOperationException>(() => Payment.CreateManual(
            Guid.NewGuid(), null, null, 100, "XX", Now, null, null, null, null, Now));
        Assert.Throws<InvalidOperationException>(() => Payment.CreateManual(
            Guid.NewGuid(), null, null, 100, "USD", default, null, null, null, null, Now));
    }

    [Fact]
    public void CreateManual_rejects_empty_billing_account()
    {
        Assert.Throws<InvalidOperationException>(() => Payment.CreateManual(
            Guid.Empty, null, null, 100, "USD", Now, null, null, null, null, Now));
    }

    [Fact]
    public void CreateManual_truncates_long_strings()
    {
        var longNotes = new string('x', 3000);
        var longLabel = new string('y', 500);
        var longRef = new string('r', 200);
        var p = Payment.CreateManual(
            Guid.NewGuid(), null, null, 100, "USD", Now,
            method: "wire", transactionReference: longRef,
            recordedByLabel: longLabel, notes: longNotes, nowUtc: Now);

        p.Notes!.Length.Should().Be(2000);
        p.RecordedByLabel!.Length.Should().Be(200);
        p.TransactionReference!.Length.Should().Be(128);
    }
}
