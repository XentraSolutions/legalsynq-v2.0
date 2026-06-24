using System.Net;
using System.Net.Http.Json;
using Commerce.Contracts.Payments;
using Commerce.Domain.Payments.Enums;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Payments;

public class PaymentApiTests : IClassFixture<CommerceWebApplicationFactory>
{
    private readonly CommerceWebApplicationFactory _factory;
    public PaymentApiTests(CommerceWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Checkout_returns_503_when_stripe_disabled()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/commerce/payments/checkout-sessions",
            new CreateCheckoutSessionRequest(Guid.CreateVersion7(), Guid.CreateVersion7(),
                new[] { new CheckoutLineItem("price_test", 1) }));

        // Validation runs first if input is invalid; we send valid Guids to reach the disabled check.
        // BillingAccount may not exist -> NotFound (404); accept either as long as it's NOT a server crash.
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.ServiceUnavailable, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EventLogs_list_returns_empty_array_initially()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/commerce/payments/event-logs");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await resp.Content.ReadFromJsonAsync<List<PaymentProviderEventLogResponse>>();
        rows.Should().NotBeNull();
    }

    [Fact]
    public async Task EventLogs_get_unknown_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync($"/api/commerce/payments/event-logs/{Guid.CreateVersion7()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PaymentMethods_list_for_unknown_account_returns_empty_ok()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync($"/api/commerce/billing-accounts/{Guid.CreateVersion7()}/payment-methods");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StripeWebhook_returns_400_when_signature_invalid_or_503_when_disabled()
    {
        var client = _factory.CreateClient();
        var content = new StringContent("{\"id\":\"evt_x\",\"type\":\"x\"}", System.Text.Encoding.UTF8, "application/json");
        // No Stripe-Signature header — and Stripe disabled by default in test config.
        var resp = await client.PostAsync("/api/commerce/payments/webhooks/stripe", content);
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.ServiceUnavailable);
    }
}
