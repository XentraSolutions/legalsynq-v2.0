using Commerce.Application.Common.Exceptions;
using Commerce.Application.Common.Time;
using Commerce.Application.Payments.Abstractions;
using Commerce.Contracts.Payments;
using Commerce.Domain.Payments;
using Commerce.Domain.Payments.Enums;
using Commerce.Domain.Subscriptions.Enums;
using Commerce.Infrastructure.Payments.Configuration;
using Commerce.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Payments.Services;

public sealed class PaymentCheckoutService : IPaymentCheckoutService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IPaymentProviderRegistry _registry;
    private readonly IPaymentProviderCustomerService _customers;
    private readonly IOptionsMonitor<PaymentProvidersOptions> _options;
    private readonly IValidator<CreateCheckoutSessionRequest> _validator;

    public PaymentCheckoutService(
        CommerceDbContext db,
        IClock clock,
        IPaymentProviderRegistry registry,
        IPaymentProviderCustomerService customers,
        IOptionsMonitor<PaymentProvidersOptions> options,
        IValidator<CreateCheckoutSessionRequest> validator)
    {
        _db = db;
        _clock = clock;
        _registry = registry;
        _customers = customers;
        _options = options;
        _validator = validator;
    }

    public async Task<CheckoutSessionResponse> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        // Currently only Stripe is implemented; the registry resolves the
        // adapter and surfaces a clean disabled/configuration error.
        var provider = PaymentProviderType.Stripe;
        var providerImpl = _registry.Get(provider);
        if (!providerImpl.IsEnabled)
            throw new PaymentProviderDisabledException(provider.ToString());

        var subscription = await _db.Subscriptions.FindAsync(new object[] { request.SubscriptionId }, ct)
            ?? throw new NotFoundException("Subscription", request.SubscriptionId.ToString());
        if (subscription.BillingAccountId != request.BillingAccountId)
            throw new InvalidRelationshipException(
                "Subscription does not belong to the specified BillingAccount.");
        if (subscription.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
            throw new InvalidRelationshipException(
                $"Subscription is in terminal state '{subscription.Status}'; cannot create a checkout session.");

        var customer = await _customers.CreateOrGetAsync(
            request.BillingAccountId, provider,
            request.CustomerEmail, request.CustomerName, ct);

        var stripeOpts = _options.CurrentValue.Stripe;
        var successUrl = request.SuccessUrl ?? stripeOpts.DefaultSuccessUrl
            ?? throw new PaymentProviderConfigurationException(provider.ToString(), "DefaultSuccessUrl");
        var cancelUrl = request.CancelUrl ?? stripeOpts.DefaultCancelUrl
            ?? throw new PaymentProviderConfigurationException(provider.ToString(), "DefaultCancelUrl");

        var lineItems = request.LineItems
            .Select(li => new ProviderCheckoutLineItem(li.ProviderPriceId, li.Quantity))
            .ToList();

        var result = await providerImpl.CreateCheckoutSessionAsync(
            new ProviderCheckoutRequest(
                request.BillingAccountId,
                request.SubscriptionId,
                customer.ProviderCustomerId,
                successUrl,
                cancelUrl,
                lineItems), ct);

        // Persist mapping. (BillingAccountId derived from subscription.)
        var mapping = await _db.PaymentProviderSubscriptions
            .FirstOrDefaultAsync(p => p.SubscriptionId == request.SubscriptionId && p.Provider == provider, ct);
        if (mapping is null)
        {
            mapping = PaymentProviderSubscription.Create(
                request.SubscriptionId, provider,
                customer.ProviderCustomerId, result.CheckoutSessionId, _clock.UtcNow);
            _db.PaymentProviderSubscriptions.Add(mapping);
        }
        else
        {
            mapping.AttachCheckoutSession(result.CheckoutSessionId, _clock.UtcNow);
        }
        await _db.SaveChangesAsync(ct);

        return new CheckoutSessionResponse(
            provider,
            result.CheckoutSessionId,
            result.CheckoutUrl,
            customer.ProviderCustomerId,
            result.ExpiresAtUtc);
    }
}
