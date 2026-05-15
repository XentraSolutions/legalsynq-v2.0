using System.Net;
using System.Net.Http.Json;
using Commerce.Contracts.AccountStanding;
using Commerce.Contracts.Billing;
using Commerce.Domain.AccountStanding.Enums;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.AccountStanding;

public class AccountStandingApiTests : IClassFixture<CommerceWebApplicationFactory>
{
    private readonly CommerceWebApplicationFactory _factory;
    public AccountStandingApiTests(CommerceWebApplicationFactory factory) => _factory = factory;

    private async Task<Guid> CreateActiveAccountAsync(HttpClient client)
    {
        var name = "Acme " + Guid.NewGuid().ToString("N")[..6];
        var resp = await client.PostAsJsonAsync("/api/commerce/billing-accounts",
            new CreateBillingAccountRequest(name, null, "USD"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await resp.Content.ReadFromJsonAsync<BillingAccountResponse>();
        await client.PostAsync($"/api/commerce/billing-accounts/{created!.Id}/activate", null);
        return created.Id;
    }

    [Fact]
    public async Task Evaluate_then_get_returns_GoodStanding_with_no_invoices()
    {
        var client = _factory.CreateClient();
        var accountId = await CreateActiveAccountAsync(client);

        var eval = await client.PostAsync(
            $"/api/commerce/billing-accounts/{accountId}/account-standing/evaluate", null);
        eval.StatusCode.Should().Be(HttpStatusCode.OK);
        var evaluated = await eval.Content.ReadFromJsonAsync<AccountStandingResponse>();
        evaluated!.BillingAccountId.Should().Be(accountId);
        evaluated.Status.Should().Be(AccountStandingStatus.Good);

        var get = await client.GetAsync(
            $"/api/commerce/billing-accounts/{accountId}/account-standing");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_for_unknown_account_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync(
            $"/api/commerce/billing-accounts/{Guid.NewGuid()}/account-standing");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Evaluate_for_unknown_account_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync(
            $"/api/commerce/billing-accounts/{Guid.NewGuid()}/account-standing/evaluate", null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
