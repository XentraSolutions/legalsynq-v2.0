using System.Net;
using System.Net.Http.Json;
using Commerce.Contracts.Billing;
using Commerce.Contracts.Catalog;
using Commerce.Contracts.Subscriptions;
using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Subscriptions.Enums;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Subscriptions;

public class SubscriptionApiTests : IClassFixture<CommerceWebApplicationFactory>
{
    private readonly CommerceWebApplicationFactory _factory;
    public SubscriptionApiTests(CommerceWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Full_subscription_lifecycle_via_api()
    {
        var client = _factory.CreateClient();

        // 1. Create + activate billing account
        var acctResp = await client.PostAsJsonAsync("/api/commerce/billing-accounts",
            new CreateBillingAccountRequest("Acme " + Guid.CreateVersion7().ToString("N")[..6], null, "USD"));
        acctResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var account = (await acctResp.Content.ReadFromJsonAsync<BillingAccountResponse>())!;
        (await client.PostAsync($"/api/commerce/billing-accounts/{account.Id}/activate", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Create + activate plan
        var planResp = await client.PostAsJsonAsync("/api/commerce/catalog/plans",
            new CreatePlanRequest(null, "api-plan-" + Guid.CreateVersion7().ToString("N")[..8], "Api Plan",
                null, BillingInterval.Monthly, null, 0));
        planResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var plan = (await planResp.Content.ReadFromJsonAsync<PlanResponse>())!;
        (await client.PostAsync($"/api/commerce/catalog/plans/{plan.Id}/activate", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Create + activate price
        var priceResp = await client.PostAsJsonAsync("/api/commerce/catalog/prices",
            new CreatePriceRequest(plan.Id, null, null, "USD", 1999, BillingInterval.Monthly,
                DateTime.UtcNow.AddMinutes(-1), null));
        priceResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var price = (await priceResp.Content.ReadFromJsonAsync<PriceResponse>())!;
        (await client.PostAsync($"/api/commerce/catalog/prices/{price.Id}/activate", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Create subscription
        var createResp = await client.PostAsJsonAsync("/api/commerce/subscriptions",
            new CreateSubscriptionRequest(account.Id, plan.Id, price.Id, 1));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var sub = (await createResp.Content.ReadFromJsonAsync<SubscriptionResponse>())!;
        sub.SubscriptionNumber.Should().StartWith("COM-SUB-");
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.Items.Should().HaveCount(1);

        // 5. Suspend → reactivate → cancel
        (await client.PostAsync($"/api/commerce/subscriptions/{sub.Id}/suspend", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/commerce/subscriptions/{sub.Id}/reactivate", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var cancel = await client.PostAsJsonAsync($"/api/commerce/subscriptions/{sub.Id}/cancel",
            new CancelSubscriptionRequest(false, "done"));
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = (await cancel.Content.ReadFromJsonAsync<SubscriptionResponse>())!;
        cancelled.Status.Should().Be(SubscriptionStatus.Cancelled);

        // 6. Changes endpoint
        var changes = await client.GetAsync($"/api/commerce/subscriptions/{sub.Id}/changes");
        changes.StatusCode.Should().Be(HttpStatusCode.OK);
        var changeList = (await changes.Content.ReadFromJsonAsync<List<SubscriptionChangeResponse>>())!;
        changeList.Should().Contain(c => c.ChangeType == SubscriptionChangeType.Cancelled);
    }

    [Fact]
    public async Task Get_unknown_subscription_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync($"/api/commerce/subscriptions/{Guid.CreateVersion7()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Validation_error_returns_400()
    {
        var client = _factory.CreateClient();
        var bad = await client.PostAsJsonAsync("/api/commerce/subscriptions",
            new CreateSubscriptionRequest(Guid.Empty, Guid.Empty, Guid.Empty, 0));
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
