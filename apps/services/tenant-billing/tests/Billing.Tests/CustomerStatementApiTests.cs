using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Billing.Domain.Entities;
using Billing.Domain.Repositories;
using Billing.Domain.Statements;
using Xunit;

namespace Billing.Tests;

/// <summary>
/// STAT-B01 — HTTP-surface tests for the customer statement endpoints
/// (<c>GET /api/statements/customers/{id}</c> JSON,
/// <c>GET /api/statements/customers/{id}/render/html</c>, and
/// <c>GET /api/statements/customers/{id}/monthly</c>). Covers the
/// happy path, escaping, cross-tenant + missing customer 404s,
/// validation 400s, and the tenant-header guard.
/// </summary>
public class CustomerStatementApiTests : IClassFixture<BillingWebApplicationFactory>
{
    private readonly BillingWebApplicationFactory _factory;
    public CustomerStatementApiTests(BillingWebApplicationFactory factory) => _factory = factory;

    private async Task<(Guid customerId, Guid invoiceId)> SeedAsync(
        Guid tenantId, string customerName = "Acme Co", string currency = "USD")
    {
        using var scope = _factory.Services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
        var payments = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

        var customer = new Customer
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Name = customerName,
            Email = $"billing+{Guid.CreateVersion7():N}@acme.test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await customers.AddAsync(customer);

        var invoice = new Invoice
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CustomerId = customer.Id,
            InvoiceNumber = $"INV-STMT-{Guid.CreateVersion7().ToString("N")[..8]}",
            Status = InvoiceStatus.Issued,
            Currency = currency,
            IssueDate = new DateTime(2026, 04, 10),
            DueDate = new DateTime(2026, 05, 10),
            Subtotal = 200m,
            TotalAmount = 200m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await invoices.AddAsync(invoice);

        var payment = new Payment
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            InvoiceId = invoice.Id,
            Amount = 50m,
            Currency = currency,
            Method = "Card",
            Status = "Recorded",
            PaidAt = new DateTime(2026, 04, 15),
            CreatedAt = DateTime.UtcNow,
        };
        await payments.AddAsync(payment);

        return (customer.Id, invoice.Id);
    }

    [Fact]
    public async Task Json_ReturnsDocumentWithExpectedTotals()
    {
        var tenantId = Guid.CreateVersion7();
        var (customerId, _) = await SeedAsync(tenantId);
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.GetAsync(
            $"/api/statements/customers/{customerId}?from=2026-04-01&to=2026-04-30");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = await resp.Content.ReadFromJsonAsync<CustomerStatementDocument>();
        Assert.NotNull(doc);
        Assert.Equal(customerId, doc!.CustomerId);
        Assert.Equal(200m, doc.TotalInvoiced);
        Assert.Equal(50m, doc.TotalPaid);
        Assert.Equal(150m, doc.ClosingBalance);
        Assert.Equal(150m, doc.OutstandingBalance);
        Assert.Single(doc.OutstandingInvoices);
        Assert.Equal(2, doc.Transactions.Count);
    }

    [Fact]
    public async Task Html_ReturnsTextHtml_ContainingCustomerName_AndPeriod()
    {
        var tenantId = Guid.CreateVersion7();
        var (customerId, _) = await SeedAsync(tenantId, customerName: "Globex Industries");
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.GetAsync(
            $"/api/statements/customers/{customerId}/render/html?from=2026-04-01&to=2026-04-30");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.StartsWith("text/html", resp.Content.Headers.ContentType?.MediaType ?? "");
        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Globex Industries", html);
        Assert.Contains("2026-04-01", html);
        Assert.Contains("2026-04-30", html);
        Assert.Contains("Outstanding", html);
        // Ensure no <script> tags are present in the rendered output.
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Html_EscapesUnsafeCustomerName()
    {
        var tenantId = Guid.CreateVersion7();
        var (customerId, _) = await SeedAsync(tenantId,
            customerName: "<script>alert('x')</script>");
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.GetAsync(
            $"/api/statements/customers/{customerId}/render/html?from=2026-04-01&to=2026-04-30");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var html = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<script>alert", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;alert", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingCustomer_Returns404()
    {
        var tenantId = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.GetAsync(
            $"/api/statements/customers/{Guid.CreateVersion7()}?from=2026-04-01&to=2026-04-30");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task CrossTenantCustomer_Returns404()
    {
        var ownerTenant = Guid.CreateVersion7();
        var otherTenant = Guid.CreateVersion7();
        var (customerId, _) = await SeedAsync(ownerTenant);

        var client = _factory.CreateClientForTenant(otherTenant);
        var resp = await client.GetAsync(
            $"/api/statements/customers/{customerId}?from=2026-04-01&to=2026-04-30");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task InvalidDateRange_Returns400()
    {
        var tenantId = Guid.CreateVersion7();
        var (customerId, _) = await SeedAsync(tenantId);
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.GetAsync(
            $"/api/statements/customers/{customerId}?from=2026-04-30&to=2026-04-01");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task RangeOver366Days_Returns400()
    {
        var tenantId = Guid.CreateVersion7();
        var (customerId, _) = await SeedAsync(tenantId);
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.GetAsync(
            $"/api/statements/customers/{customerId}?from=2024-01-01&to=2025-06-01");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task MissingDateParameters_Returns400()
    {
        var tenantId = Guid.CreateVersion7();
        var (customerId, _) = await SeedAsync(tenantId);
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.GetAsync($"/api/statements/customers/{customerId}");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task MissingTenantHeader_Returns400()
    {
        var tenantId = Guid.CreateVersion7();
        var (customerId, _) = await SeedAsync(tenantId);
        var client = _factory.CreateClient(); // no tenant header set

        var resp = await client.GetAsync(
            $"/api/statements/customers/{customerId}?from=2026-04-01&to=2026-04-30");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Monthly_Returns200_WithCorrectPeriodBoundaries()
    {
        var tenantId = Guid.CreateVersion7();
        var (customerId, _) = await SeedAsync(tenantId);
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.GetAsync(
            $"/api/statements/customers/{customerId}/monthly?year=2026&month=4");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = await resp.Content.ReadFromJsonAsync<CustomerStatementDocument>();
        Assert.NotNull(doc);
        Assert.Equal(new DateTime(2026, 04, 01), doc!.PeriodStartDate);
        Assert.Equal(new DateTime(2026, 04, 30), doc.PeriodEndDate);
        Assert.Equal(200m, doc.TotalInvoiced);
        Assert.Equal(50m, doc.TotalPaid);
    }

    [Fact]
    public async Task Monthly_InvalidMonth_Returns400()
    {
        var tenantId = Guid.CreateVersion7();
        var (customerId, _) = await SeedAsync(tenantId);
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.GetAsync(
            $"/api/statements/customers/{customerId}/monthly?year=2026&month=13");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task MultipleCurrencies_ForSameCustomer_Return400ProblemDetails()
    {
        var tenantId = Guid.CreateVersion7();
        var (customerId, _) = await SeedAsync(tenantId, currency: "USD");

        // Add a second invoice in a different currency for the same
        // customer so the service trips its multi-currency guard.
        using (var scope = _factory.Services.CreateScope())
        {
            var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
            await invoices.AddAsync(new Invoice
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                CustomerId = customerId,
                InvoiceNumber = $"INV-EUR-{Guid.CreateVersion7().ToString("N")[..8]}",
                Status = InvoiceStatus.Issued,
                Currency = "EUR",
                IssueDate = new DateTime(2026, 04, 12),
                DueDate = new DateTime(2026, 05, 12),
                Subtotal = 75m,
                TotalAmount = 75m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        var client = _factory.CreateClientForTenant(tenantId);
        var resp = await client.GetAsync(
            $"/api/statements/customers/{customerId}?from=2026-04-01&to=2026-04-30");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("application/problem+json", resp.Content.Headers.ContentType?.MediaType ?? "");
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("multiple currencies", body, StringComparison.OrdinalIgnoreCase);
    }
}
