using System.Net;
using System.Net.Http.Json;
using Commerce.Contracts.Billing;
using Commerce.Contracts.Invoicing;
using Commerce.Domain.Invoicing.Enums;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Invoicing;

public class InvoicingApiTests : IClassFixture<CommerceWebApplicationFactory>
{
    private readonly CommerceWebApplicationFactory _factory;
    public InvoicingApiTests(CommerceWebApplicationFactory factory) => _factory = factory;

    private async Task<Guid> CreateActiveAccountAsync(HttpClient client)
    {
        var name = "Acme " + Guid.CreateVersion7().ToString("N")[..6];
        var resp = await client.PostAsJsonAsync("/api/commerce/billing-accounts",
            new CreateBillingAccountRequest(name, null, "USD"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await resp.Content.ReadFromJsonAsync<BillingAccountResponse>();
        var act = await client.PostAsync($"/api/commerce/billing-accounts/{created!.Id}/activate", null);
        act.StatusCode.Should().Be(HttpStatusCode.OK);
        return created.Id;
    }

    [Fact]
    public async Task Invoice_create_get_list_roundtrip()
    {
        var client = _factory.CreateClient();
        var accountId = await CreateActiveAccountAsync(client);

        var req = new CreateInvoiceRequest(accountId, "USD",
            new[] { new CreateInvoiceLineRequest("Seats", 2, 2500) });
        var create = await client.PostAsJsonAsync("/api/commerce/invoices", req);
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var inv = await create.Content.ReadFromJsonAsync<InvoiceResponse>();
        inv!.Status.Should().Be(InvoiceStatus.Open);
        inv.TotalAmountMinor.Should().Be(5000);

        var get = await client.GetAsync($"/api/commerce/invoices/{inv.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        var listAcct = await client.GetAsync($"/api/commerce/billing-accounts/{accountId}/invoices");
        listAcct.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listAcct.Content.ReadFromJsonAsync<List<InvoiceResponse>>();
        list.Should().ContainSingle(x => x.Id == inv.Id);
    }

    [Fact]
    public async Task Invoice_create_unknown_account_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/commerce/invoices",
            new CreateInvoiceRequest(Guid.CreateVersion7(), "USD",
                new[] { new CreateInvoiceLineRequest("X", 1, 100) }));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invoice_create_validation_fails_returns_400()
    {
        var client = _factory.CreateClient();
        var accountId = await CreateActiveAccountAsync(client);
        var resp = await client.PostAsJsonAsync("/api/commerce/invoices",
            new CreateInvoiceRequest(accountId, "USD", Array.Empty<CreateInvoiceLineRequest>()));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_unknown_invoice_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync($"/api/commerce/invoices/{Guid.CreateVersion7()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_invoices_returns_200()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/commerce/invoices");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_payments_returns_200()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/commerce/payments");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reprocess_unknown_event_log_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync(
            $"/api/commerce/payments/event-logs/{Guid.CreateVersion7()}/reprocess", null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
