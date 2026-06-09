using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TenantBilling.Api.Contracts;
using Xunit;

namespace TenantBilling.Tests.Api;

public class BillingApiTests : IClassFixture<TenantBillingWebApplicationFactory>
{
    private readonly TenantBillingWebApplicationFactory _factory;

    public BillingApiTests(TenantBillingWebApplicationFactory factory) => _factory = factory;

    private static readonly DateTime IssueDate = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DueDate = IssueDate.AddDays(30);

    private static async Task<CustomerResponse> CreateCustomerAsync(HttpClient client)
    {
        var req = new CreateCustomerRequest
        {
            Name = "Acme " + Guid.CreateVersion7().ToString("N")[..6],
            Email = $"acme+{Guid.CreateVersion7():N}@example.com",
        };
        var resp = await client.PostAsJsonAsync("/api/customers", req);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<CustomerResponse>();
        body.Should().NotBeNull();
        return body!;
    }

    private static CreateInvoiceRequest BuildInvoiceRequest(
        Guid customerId, string invoiceNumber,
        decimal unitPrice = 100m, int quantity = 2, decimal taxAmount = 0m)
        => new()
        {
            CustomerId = customerId,
            InvoiceNumber = invoiceNumber,
            IssueDate = IssueDate,
            DueDate = DueDate,
            Currency = "USD",
            TaxAmount = taxAmount,
            Lines = new List<CreateInvoiceLineRequest>
            {
                new() { Description = "Widget", Quantity = quantity, UnitPrice = unitPrice }
            }
        };

    [Fact]
    public async Task Customer_invoice_payment_happy_path()
    {
        var tenantId = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenantId);

        // 1. Create customer
        var customer = await CreateCustomerAsync(client);
        customer.TenantId.Should().Be(tenantId);

        // 2. Create invoice for that customer
        var invoiceNumber = "INV-HAPPY-" + Guid.CreateVersion7().ToString("N")[..6];
        var invReq = BuildInvoiceRequest(customer.Id, invoiceNumber, unitPrice: 25m, quantity: 4, taxAmount: 5m);
        var invResp = await client.PostAsJsonAsync("/api/invoices", invReq);
        invResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var invoice = await invResp.Content.ReadFromJsonAsync<InvoiceResponse>();
        invoice.Should().NotBeNull();
        invoice!.TenantId.Should().Be(tenantId);
        invoice.CustomerId.Should().Be(customer.Id);
        invoice.Status.Should().Be("Draft");
        invoice.Subtotal.Should().Be(100m);
        invoice.TaxAmount.Should().Be(5m);
        invoice.TotalAmount.Should().Be(105m);
        invoice.Lines.Should().HaveCount(1);

        // GET the invoice back
        var getInv = await client.GetAsync($"/api/invoices/{invoice.Id}");
        getInv.StatusCode.Should().Be(HttpStatusCode.OK);
        (await getInv.Content.ReadFromJsonAsync<InvoiceResponse>())!.Id.Should().Be(invoice.Id);

        // 3. Issue the invoice — TBS-B03 returns the slim IssueInvoiceResponse
        // shape; we deserialize as InvoiceResponse to keep this assertion
        // resilient since the slim shape is a subset.
        var issueResp = await client.PostAsync($"/api/invoices/{invoice.Id}/issue", content: null);
        issueResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var issued = await issueResp.Content.ReadFromJsonAsync<IssueInvoiceResponse>();
        issued.Should().NotBeNull();
        issued!.Status.Should().Be("Issued");
        issued.IssuedAt.Should().NotBeNull();

        // 4. Record payment for that invoice. The new shape returns
        //    RecordPaymentResponse: payment + post-payment invoice summary.
        var payReq = new CreatePaymentRequest
        {
            InvoiceId = invoice.Id,
            Amount = 105m,
            Currency = "USD",
            Method = "card",
            TransactionReference = "ref-1",
            Notes = "happy path"
        };
        var payResp = await client.PostAsJsonAsync("/api/payments", payReq);
        payResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var recorded = await payResp.Content.ReadFromJsonAsync<RecordPaymentResponse>();
        recorded.Should().NotBeNull();
        recorded!.Payment.TenantId.Should().Be(tenantId);
        recorded.Payment.InvoiceId.Should().Be(invoice.Id);
        recorded.Payment.Amount.Should().Be(105m);
        recorded.Payment.Status.Should().Be("Recorded");
        recorded.Payment.Notes.Should().Be("happy path");
        recorded.InvoiceSummary.InvoiceTotal.Should().Be(105m);
        recorded.InvoiceSummary.TotalPaid.Should().Be(105m);
        recorded.InvoiceSummary.BalanceDue.Should().Be(0m);
        recorded.InvoiceSummary.InvoiceStatus.Should().Be("Paid");

        // GET the payment back
        var getPay = await client.GetAsync($"/api/payments/{recorded.Payment.Id}");
        getPay.StatusCode.Should().Be(HttpStatusCode.OK);

        // GET /api/invoices/{id}/payment-summary
        var summaryResp = await client.GetAsync($"/api/invoices/{invoice.Id}/payment-summary");
        summaryResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await summaryResp.Content.ReadFromJsonAsync<InvoicePaymentSummaryResponse>();
        summary.Should().NotBeNull();
        summary!.TotalPaid.Should().Be(105m);
        summary.BalanceDue.Should().Be(0m);
        summary.InvoiceStatus.Should().Be("Paid");

        // GET /api/invoices/{id}/payments
        var listResp = await client.GetAsync($"/api/invoices/{invoice.Id}/payments");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var paymentsList = await listResp.Content.ReadFromJsonAsync<List<PaymentResponse>>();
        paymentsList.Should().NotBeNull();
        paymentsList!.Should().HaveCount(1);
        paymentsList![0].Id.Should().Be(recorded.Payment.Id);
    }

    [Fact]
    public async Task Create_invoice_with_no_lines_returns_400_from_model_validation()
    {
        var tenantId = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenantId);
        var customer = await CreateCustomerAsync(client);

        var invReq = new CreateInvoiceRequest
        {
            CustomerId = customer.Id,
            InvoiceNumber = "INV-NOLINES-" + Guid.CreateVersion7().ToString("N")[..6],
            IssueDate = IssueDate,
            DueDate = DueDate,
            Currency = "USD",
            Lines = new List<CreateInvoiceLineRequest>()
        };

        var resp = await client.PostAsJsonAsync("/api/invoices", invReq);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_invoice_for_customer_in_other_tenant_returns_400()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var clientA = _factory.CreateClientForTenant(tenantA);
        var clientB = _factory.CreateClientForTenant(tenantB);

        // Customer belongs to tenantA, request goes through tenantB's client.
        var customer = await CreateCustomerAsync(clientA);
        var invReq = BuildInvoiceRequest(customer.Id,
            "INV-CROSS-" + Guid.CreateVersion7().ToString("N")[..6]);

        var resp = await clientB.PostAsJsonAsync("/api/invoices", invReq);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_invoice_with_due_date_before_issue_date_returns_400()
    {
        var tenantId = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenantId);
        var customer = await CreateCustomerAsync(client);

        var invReq = BuildInvoiceRequest(customer.Id,
            "INV-DATES-" + Guid.CreateVersion7().ToString("N")[..6]);
        invReq.DueDate = invReq.IssueDate.AddDays(-1);

        var resp = await client.PostAsJsonAsync("/api/invoices", invReq);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_duplicate_invoice_number_for_same_tenant_returns_409()
    {
        var tenantId = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenantId);
        var customer = await CreateCustomerAsync(client);

        var number = "INV-DUP-" + Guid.CreateVersion7().ToString("N")[..6];
        var first = await client.PostAsJsonAsync("/api/invoices",
            BuildInvoiceRequest(customer.Id, number));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/invoices",
            BuildInvoiceRequest(customer.Id, number));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_payment_for_invoice_in_other_tenant_returns_400()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var clientA = _factory.CreateClientForTenant(tenantA);
        var clientB = _factory.CreateClientForTenant(tenantB);

        var customer = await CreateCustomerAsync(clientA);
        var invResp = await clientA.PostAsJsonAsync("/api/invoices",
            BuildInvoiceRequest(customer.Id,
                "INV-PAYCROSS-" + Guid.CreateVersion7().ToString("N")[..6]));
        invResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var invoice = (await invResp.Content.ReadFromJsonAsync<InvoiceResponse>())!;

        // tenantB tries to pay tenantA's invoice; should fail at the
        // tenant ownership check inside the payment service.
        var payReq = new CreatePaymentRequest
        {
            InvoiceId = invoice.Id,
            Amount = 10m,
            Currency = "USD",
            Method = "card",
        };
        var payResp = await clientB.PostAsJsonAsync("/api/payments", payReq);
        payResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_payment_with_zero_amount_returns_400_from_model_validation()
    {
        var tenantId = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenantId);
        var customer = await CreateCustomerAsync(client);
        var invResp = await client.PostAsJsonAsync("/api/invoices",
            BuildInvoiceRequest(customer.Id,
                "INV-ZEROPAY-" + Guid.CreateVersion7().ToString("N")[..6]));
        var invoice = (await invResp.Content.ReadFromJsonAsync<InvoiceResponse>())!;

        var payReq = new CreatePaymentRequest
        {
            InvoiceId = invoice.Id,
            Amount = 0m,
            Currency = "USD",
            Method = "card",
        };
        var resp = await client.PostAsJsonAsync("/api/payments", payReq);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_unknown_invoice_returns_404()
    {
        var client = _factory.CreateClientForTenant(Guid.CreateVersion7());
        var resp = await client.GetAsync($"/api/invoices/{Guid.CreateVersion7()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_unknown_payment_returns_404()
    {
        var client = _factory.CreateClientForTenant(Guid.CreateVersion7());
        var resp = await client.GetAsync($"/api/payments/{Guid.CreateVersion7()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Health_endpoint_returns_200()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_invoice_payments_for_other_tenants_invoice_returns_404()
    {
        // The invoice id is in the URL path, so a foreign-tenant request must
        // look exactly like a missing invoice (404). It must not surface 403,
        // an empty 200, or any indicator that the invoice exists for another
        // tenant — that would be a cross-tenant existence leak.
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var clientA = _factory.CreateClientForTenant(tenantA);
        var clientB = _factory.CreateClientForTenant(tenantB);

        var customer = await CreateCustomerAsync(clientA);
        var invResp = await clientA.PostAsJsonAsync("/api/invoices",
            BuildInvoiceRequest(customer.Id,
                "INV-XPAYS-" + Guid.CreateVersion7().ToString("N")[..6]));
        var invoice = (await invResp.Content.ReadFromJsonAsync<InvoiceResponse>())!;

        var crossPayments = await clientB.GetAsync($"/api/invoices/{invoice.Id}/payments");
        crossPayments.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var crossSummary = await clientB.GetAsync($"/api/invoices/{invoice.Id}/payment-summary");
        crossSummary.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Sanity: the owning tenant gets 200 from both endpoints.
        var ownPayments = await clientA.GetAsync($"/api/invoices/{invoice.Id}/payments");
        ownPayments.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownSummary = await clientA.GetAsync($"/api/invoices/{invoice.Id}/payment-summary");
        ownSummary.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
