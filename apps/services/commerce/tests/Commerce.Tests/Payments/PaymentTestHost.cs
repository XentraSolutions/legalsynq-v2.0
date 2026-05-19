using Commerce.Application.Common.Time;
using Commerce.Application.Payments.Abstractions;
using Commerce.Domain.Billing;
using Commerce.Domain.Catalog;
using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Payments.Enums;
using Commerce.Domain.Subscriptions;
using Commerce.Infrastructure.Payments;
using Commerce.Infrastructure.Payments.Configuration;
using Commerce.Infrastructure.Payments.Services;
using Commerce.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Commerce.Tests.Payments;

internal sealed class PayFixedClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
}

internal sealed class FakePaymentProvider : IPaymentProvider
{
    public PaymentProviderType ProviderType => PaymentProviderType.Stripe;
    public bool IsEnabled { get; set; } = true;
    public string NextCustomerId { get; set; } = "cus_test_123";
    public string NextCheckoutSessionId { get; set; } = "cs_test_123";
    public string NextCheckoutUrl { get; set; } = "https://checkout.stripe.test/abc";
    public bool VerifyShouldFail { get; set; }
    public Func<string, NormalizedProviderEvent>? Translator { get; set; }
    public int CustomerCreateCalls { get; private set; }
    public int CheckoutCreateCalls { get; private set; }

    public Task<ProviderCustomerResult> CreateOrGetCustomerAsync(ProviderCustomerRequest req, CancellationToken ct)
    {
        CustomerCreateCalls++;
        return Task.FromResult(new ProviderCustomerResult(NextCustomerId, req.Email, req.Name));
    }

    public Task<ProviderCheckoutResult> CreateCheckoutSessionAsync(ProviderCheckoutRequest req, CancellationToken ct)
    {
        CheckoutCreateCalls++;
        return Task.FromResult(new ProviderCheckoutResult(
            NextCheckoutSessionId, NextCheckoutUrl, null, null));
    }

    public void VerifyWebhook(ProviderWebhookPayload payload)
    {
        if (VerifyShouldFail)
            throw new Commerce.Application.Common.Exceptions.InvalidWebhookSignatureException("Stripe");
    }

    public NormalizedProviderEvent TranslateWebhookEvent(string rawBody)
        => Translator is not null
            ? Translator(rawBody)
            : new NormalizedProviderEvent(PaymentProviderType.Stripe,
                Guid.CreateVersion7().ToString("N"), "noop",
                NormalizedProviderEventKind.Unsupported,
                null, null, null, null, null, null, null, null, null, null);
}

internal sealed class PaymentTestHost : IDisposable
{
    public CommerceDbContext Db { get; }
    public PayFixedClock Clock { get; } = new();
    public FakePaymentProvider Provider { get; } = new();
    public PaymentProviderRegistry Registry { get; }
    public PaymentProviderCustomerService Customers { get; }
    public PaymentMethodReferenceService Methods { get; }
    public PaymentWebhookService Webhooks { get; }
    public PaymentCheckoutService Checkout { get; }
    public PaymentProvidersOptions Options { get; } = new()
    {
        Stripe = new StripeOptions
        {
            Enabled = true,
            SecretKey = "sk_test_xxx",
            WebhookSecret = "whsec_test",
            DefaultSuccessUrl = "https://app.test/success",
            DefaultCancelUrl = "https://app.test/cancel",
        }
    };

    public PaymentTestHost()
    {
        var opts = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"pay-tests-{Guid.CreateVersion7()}")
            .Options;
        Db = new CommerceDbContext(opts);

        Registry = new PaymentProviderRegistry(new IPaymentProvider[] { Provider });
        Customers = new PaymentProviderCustomerService(Db, Clock, Registry);
        Methods = new PaymentMethodReferenceService(Db, Clock);
        var recording = new Commerce.Infrastructure.Payments.Services.PaymentRecordingService(Db, Clock);
        var reconciliation = new Commerce.Infrastructure.Subscriptions.Services.SubscriptionReconciliationService(Db, Clock);
        Webhooks = new PaymentWebhookService(Db, Clock, Registry, recording, reconciliation);

        var monitor = new StaticOptionsMonitor<PaymentProvidersOptions>(Options);
        var validator = ResolveValidator<Contracts.Payments.CreateCheckoutSessionRequest>();
        Checkout = new PaymentCheckoutService(Db, Clock, Registry, Customers, monitor, validator);
    }

    public BillingAccount AddActiveAccount(string number = "COM-ACC-PAY01")
    {
        var account = BillingAccount.Create(number, "Acme " + Guid.CreateVersion7().ToString("N")[..6], null, "USD", Clock.UtcNow);
        account.Activate(Clock.UtcNow);
        Db.BillingAccounts.Add(account);
        Db.SaveChanges();
        return account;
    }

    public Subscription AddActiveSubscription(BillingAccount acct)
    {
        var plan = Plan.Create(null, "k-" + Guid.CreateVersion7().ToString("N")[..8], "Plan", null, BillingInterval.Monthly, null, 0, Clock.UtcNow);
        plan.Activate(Clock.UtcNow);
        Db.Plans.Add(plan);
        var price = Price.Create(plan.Id, null, null, "USD", 1999, BillingInterval.Monthly, Clock.UtcNow.AddMinutes(-5), null, Clock.UtcNow);
        price.Activate(Clock.UtcNow);
        Db.Prices.Add(price);
        Db.SaveChanges();

        var sub = Subscription.Create(
            acct.Id,
            "COM-SUB-PAY-" + Guid.CreateVersion7().ToString("N")[..8],
            Clock.UtcNow,
            Clock.UtcNow,
            Clock.UtcNow.AddMonths(1),
            null, null,
            Clock.UtcNow);
        sub.Activate(Clock.UtcNow);
        Db.Subscriptions.Add(sub);
        Db.SaveChanges();
        return sub;
    }

    private static IValidator<T> ResolveValidator<T>()
    {
        var asm = typeof(Commerce.Application.DependencyInjection).Assembly;
        var t = asm.GetTypes().First(x => !x.IsAbstract && typeof(IValidator<T>).IsAssignableFrom(x));
        return (IValidator<T>)Activator.CreateInstance(t)!;
    }

    public void Dispose() => Db.Dispose();
}

internal sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
{
    public StaticOptionsMonitor(T value) { CurrentValue = value; }
    public T CurrentValue { get; }
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
