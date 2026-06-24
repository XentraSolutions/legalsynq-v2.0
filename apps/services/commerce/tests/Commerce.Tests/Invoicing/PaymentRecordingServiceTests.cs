using Commerce.Application.Payments.Abstractions;
using Commerce.Contracts.Invoicing;
using Commerce.Domain.Invoicing.Enums;
using Commerce.Domain.Payments;
using Commerce.Domain.Payments.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Commerce.Tests.Invoicing;

public class PaymentRecordingServiceTests
{
    private static NormalizedProviderEvent MakeEvent(
        string eventId, NormalizedProviderEventKind kind,
        Guid? billingAccountId = null,
        long? amount = 1000, string currency = "USD",
        string? providerInvoiceId = null,
        string? providerPaymentIntentId = null,
        string? failureCode = null, string? failureMessage = null)
        => new(
            PaymentProviderType.Stripe,
            eventId,
            "evt." + kind,
            kind,
            ProviderCustomerId: null,
            ProviderSubscriptionId: null,
            ProviderCheckoutSessionId: null,
            ProviderPaymentMethodId: null,
            PaymentMethodBrand: null,
            PaymentMethodLast4: null,
            PaymentMethodExpMonth: null,
            PaymentMethodExpYear: null,
            BillingAccountId: billingAccountId,
            SubscriptionId: null,
            ProviderPaymentIntentId: providerPaymentIntentId,
            ProviderInvoiceId: providerInvoiceId,
            AmountMinor: amount,
            Currency: currency,
            FailureCode: failureCode,
            FailureMessage: failureMessage,
            OccurredAtUtc: null,
            ProviderSubscriptionStatus: null);

    [Fact]
    public async Task RecordFromEvent_succeeded_creates_payment_and_attempt()
    {
        using var host = new InvoicingTestHost();
        var account = host.AddActiveAccount();

        var ev = MakeEvent("evt_1", NormalizedProviderEventKind.PaymentIntentSucceeded,
            billingAccountId: account.Id, providerPaymentIntentId: "pi_1");
        var result = await host.Recording.RecordFromEventAsync(ev, true, default);
        await host.Db.SaveChangesAsync();

        result.Should().NotBeNull();
        result!.PaymentStatus.Should().Be(PaymentStatus.Succeeded);
        var payments = await host.Db.Payments.AsNoTracking().ToListAsync();
        payments.Should().HaveCount(1);
        payments[0].ProviderPaymentId.Should().Be("pi_1");
        var attempts = await host.Db.PaymentAttempts.AsNoTracking().ToListAsync();
        attempts.Should().HaveCount(1);
        attempts[0].Status.Should().Be(PaymentAttemptStatus.Succeeded);
    }

    [Fact]
    public async Task RecordFromEvent_failed_records_attempt_with_error_details()
    {
        using var host = new InvoicingTestHost();
        var account = host.AddActiveAccount();
        var ev = MakeEvent("evt_2", NormalizedProviderEventKind.PaymentIntentFailed,
            billingAccountId: account.Id, providerPaymentIntentId: "pi_2",
            failureCode: "card_declined", failureMessage: "Your card was declined");

        var result = await host.Recording.RecordFromEventAsync(ev, false, default);
        await host.Db.SaveChangesAsync();

        result.Should().NotBeNull();
        result!.PaymentStatus.Should().Be(PaymentStatus.Failed);
        var attempts = await host.Db.PaymentAttempts.AsNoTracking().ToListAsync();
        attempts[0].ErrorCode.Should().Be("card_declined");
        attempts[0].ErrorMessage.Should().Be("Your card was declined");
    }

    [Fact]
    public async Task RecordFromEvent_idempotent_on_provider_event_id()
    {
        using var host = new InvoicingTestHost();
        var account = host.AddActiveAccount();

        var ev = MakeEvent("evt_dup", NormalizedProviderEventKind.PaymentIntentSucceeded,
            billingAccountId: account.Id, providerPaymentIntentId: "pi_dup");
        await host.Recording.RecordFromEventAsync(ev, true, default);
        await host.Db.SaveChangesAsync();
        var second = await host.Recording.RecordFromEventAsync(ev, true, default);
        await host.Db.SaveChangesAsync();

        second.Should().NotBeNull();
        var attempts = await host.Db.PaymentAttempts.AsNoTracking().ToListAsync();
        attempts.Should().HaveCount(1);
        var payments = await host.Db.Payments.AsNoTracking().ToListAsync();
        payments.Should().HaveCount(1);
    }

    [Fact]
    public async Task RecordFromEvent_attaches_invoice_when_provider_invoice_matches()
    {
        using var host = new InvoicingTestHost();
        var account = host.AddActiveAccount();
        var inv = await host.Invoices.CreateAsync(new CreateInvoiceRequest(
            account.Id, "USD",
            new[] { new CreateInvoiceLineRequest("X", 1, 1000) }), default);

        var dbInvoice = await host.Db.Invoices.FirstAsync(i => i.Id == inv.Id);
        dbInvoice.AttachProviderInvoice(PaymentProviderType.Stripe, "in_provider_1", host.Clock.UtcNow);
        await host.Db.SaveChangesAsync();

        var ev = MakeEvent("evt_inv1", NormalizedProviderEventKind.InvoicePaymentSucceeded,
            billingAccountId: account.Id, providerPaymentIntentId: "pi_inv_1",
            providerInvoiceId: "in_provider_1", amount: 1000);
        var result = await host.Recording.RecordFromEventAsync(ev, true, default);
        await host.Db.SaveChangesAsync();

        result!.MatchedInvoice.Should().BeTrue();
        var refreshed = await host.Db.Invoices.AsNoTracking().FirstAsync(i => i.Id == inv.Id);
        refreshed.Status.Should().Be(InvoiceStatus.Paid);
        refreshed.AmountPaidMinor.Should().Be(1000);
    }

    [Fact]
    public async Task RecordFromEvent_returns_null_when_no_billing_account_resolvable()
    {
        using var host = new InvoicingTestHost();
        var ev = MakeEvent("evt_orphan", NormalizedProviderEventKind.PaymentIntentSucceeded,
            billingAccountId: null);
        var result = await host.Recording.RecordFromEventAsync(ev, true, default);
        result.Should().BeNull();
    }
}
