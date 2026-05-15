using System.Net;
using System.Net.Http.Json;
using Commerce.Contracts.Billing;
using Commerce.Contracts.Invoicing;
using Commerce.Contracts.Payments;
using Commerce.Domain.Invoicing.Enums;
using Commerce.Domain.Payments.Enums;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Payments;

public class ManualPaymentApiTests : IClassFixture<CommerceWebApplicationFactory>
{
    private readonly CommerceWebApplicationFactory _factory;
    public ManualPaymentApiTests(CommerceWebApplicationFactory factory) => _factory = factory;

    private static async Task<Guid> CreateActiveAccountAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/commerce/billing-accounts",
            new CreateBillingAccountRequest("Acme " + Guid.NewGuid().ToString("N")[..6], null, "USD"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await resp.Content.ReadFromJsonAsync<BillingAccountResponse>();
        var act = await client.PostAsync($"/api/commerce/billing-accounts/{created!.Id}/activate", null);
        act.StatusCode.Should().Be(HttpStatusCode.OK);
        return created.Id;
    }

    private static async Task<InvoiceResponse> CreateOpenInvoiceAsync(
        HttpClient client, Guid accountId, long lineAmountMinor = 5000, int qty = 2)
    {
        var resp = await client.PostAsJsonAsync("/api/commerce/invoices",
            new CreateInvoiceRequest(accountId, "USD",
                new[] { new CreateInvoiceLineRequest("Seats", qty, lineAmountMinor) }));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<InvoiceResponse>())!;
    }

    [Fact]
    public async Task Records_manual_payment_against_invoice_201()
    {
        var client = _factory.CreateClient();
        var acctId = await CreateActiveAccountAsync(client);
        var inv = await CreateOpenInvoiceAsync(client, acctId);

        var resp = await client.PostAsJsonAsync(
            $"/api/commerce/invoices/{inv.Id}/manual-payments",
            new RecordManualPaymentRequest(
                AmountMinor: 6000,
                PaidAtUtc: DateTime.UtcNow,
                Method: "check",
                TransactionReference: "chk-9000",
                RecordedByLabel: "Reception",
                Notes: "Front desk"));

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var payment = await resp.Content.ReadFromJsonAsync<PaymentResponse>();
        payment!.Provider.Should().Be(PaymentProviderType.Manual);
        payment.Status.Should().Be(PaymentStatus.Succeeded);
        payment.AmountMinor.Should().Be(6000);
        payment.InvoiceId.Should().Be(inv.Id);
        payment.Method.Should().Be("check");
        payment.RecordedByLabel.Should().Be("Reception");
        payment.Notes.Should().Be("Front desk");
        payment.TransactionReference.Should().Be("chk-9000");
        payment.ProviderPaymentId.Should().BeNull();

        // Verify the invoice balance was updated.
        var get = await client.GetAsync($"/api/commerce/invoices/{inv.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await get.Content.ReadFromJsonAsync<InvoiceResponse>();
        updated!.AmountPaidMinor.Should().Be(6000);
        updated.AmountDueMinor.Should().Be(4000);
        updated.Status.Should().Be(InvoiceStatus.Open);
    }

    [Fact]
    public async Task Full_payment_marks_invoice_paid()
    {
        var client = _factory.CreateClient();
        var acctId = await CreateActiveAccountAsync(client);
        var inv = await CreateOpenInvoiceAsync(client, acctId);

        var resp = await client.PostAsJsonAsync(
            $"/api/commerce/invoices/{inv.Id}/manual-payments",
            new RecordManualPaymentRequest(10_000, DateTime.UtcNow, "wire"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var get = await client.GetAsync($"/api/commerce/invoices/{inv.Id}");
        var updated = await get.Content.ReadFromJsonAsync<InvoiceResponse>();
        updated!.Status.Should().Be(InvoiceStatus.Paid);
        updated.AmountDueMinor.Should().Be(0);
        updated.PaidAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Overpayment_returns_422()
    {
        var client = _factory.CreateClient();
        var acctId = await CreateActiveAccountAsync(client);
        var inv = await CreateOpenInvoiceAsync(client, acctId);

        var resp = await client.PostAsJsonAsync(
            $"/api/commerce/invoices/{inv.Id}/manual-payments",
            new RecordManualPaymentRequest(99_999, DateTime.UtcNow, "cash"));
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Already_paid_invoice_returns_409()
    {
        var client = _factory.CreateClient();
        var acctId = await CreateActiveAccountAsync(client);
        var inv = await CreateOpenInvoiceAsync(client, acctId);

        var first = await client.PostAsJsonAsync(
            $"/api/commerce/invoices/{inv.Id}/manual-payments",
            new RecordManualPaymentRequest(10_000, DateTime.UtcNow, "wire"));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(
            $"/api/commerce/invoices/{inv.Id}/manual-payments",
            new RecordManualPaymentRequest(1, DateTime.UtcNow, "cash"));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Unknown_invoice_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync(
            $"/api/commerce/invoices/{Guid.NewGuid()}/manual-payments",
            new RecordManualPaymentRequest(100, DateTime.UtcNow, "cash"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Validation_errors_return_400()
    {
        var client = _factory.CreateClient();
        var acctId = await CreateActiveAccountAsync(client);
        var inv = await CreateOpenInvoiceAsync(client, acctId);

        // Zero amount.
        var r1 = await client.PostAsJsonAsync(
            $"/api/commerce/invoices/{inv.Id}/manual-payments",
            new RecordManualPaymentRequest(0, DateTime.UtcNow, "cash"));
        r1.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Bad method.
        var r2 = await client.PostAsJsonAsync(
            $"/api/commerce/invoices/{inv.Id}/manual-payments",
            new RecordManualPaymentRequest(100, DateTime.UtcNow, "bitcoin"));
        r2.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Default PaidAtUtc.
        var r3 = await client.PostAsJsonAsync(
            $"/api/commerce/invoices/{inv.Id}/manual-payments",
            new RecordManualPaymentRequest(100, default, "cash"));
        r3.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Manual_payment_appears_in_payment_listings()
    {
        var client = _factory.CreateClient();
        var acctId = await CreateActiveAccountAsync(client);
        var inv = await CreateOpenInvoiceAsync(client, acctId);

        var post = await client.PostAsJsonAsync(
            $"/api/commerce/invoices/{inv.Id}/manual-payments",
            new RecordManualPaymentRequest(2500, DateTime.UtcNow, "ach"));
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        var payment = await post.Content.ReadFromJsonAsync<PaymentResponse>();

        var byAcct = await client.GetAsync($"/api/commerce/billing-accounts/{acctId}/payments");
        byAcct.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await byAcct.Content.ReadFromJsonAsync<List<PaymentResponse>>();
        rows.Should().Contain(p => p.Id == payment!.Id && p.Provider == PaymentProviderType.Manual);
    }
}
