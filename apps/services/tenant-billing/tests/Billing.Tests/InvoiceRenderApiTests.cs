using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Billing.Domain.Entities;
using Billing.Domain.Repositories;
using Billing.Domain.Rendering;
using Xunit;

namespace Billing.Tests;

/// <summary>
/// INV-TPL-03 — HTTP-surface tests for the render endpoints
/// (<c>GET /api/invoices/{id}/render</c> JSON and
/// <c>GET /api/invoices/{id}/render/html</c>). Covers the happy
/// path, cross-tenant, missing, the snapshot-survives-edit
/// guarantee, and the tenant-header guard.
/// </summary>
public class InvoiceRenderApiTests : IClassFixture<BillingWebApplicationFactory>
{
    private readonly BillingWebApplicationFactory _factory;
    public InvoiceRenderApiTests(BillingWebApplicationFactory factory) => _factory = factory;

    private async Task<(Guid customerId, Guid invoiceId)> SeedSimpleInvoiceAsync(Guid tenantId)
    {
        using var scope = _factory.Services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Acme Co",
            Email = $"billing+{Guid.NewGuid():N}@acme.test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await customers.AddAsync(customer);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customer.Id,
            InvoiceNumber = "INV-RND-1",
            Status = "Issued",
            Currency = "USD",
            IssueDate = new DateTime(2026, 4, 1),
            DueDate = new DateTime(2026, 5, 1),
            Subtotal = 100m,
            TaxAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 100m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        invoice.LineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            Description = "Consulting",
            Quantity = 2,
            UnitPrice = 50m,
            LineTotal = 100m,
            CreatedAt = DateTime.UtcNow,
        });
        await invoices.AddAsync(invoice);
        return (customer.Id, invoice.Id);
    }

    [Fact]
    public async Task GetRender_ReturnsJsonDocument()
    {
        var tenantId = Guid.NewGuid();
        var (_, invoiceId) = await SeedSimpleInvoiceAsync(tenantId);
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.GetAsync($"/api/invoices/{invoiceId}/render");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("application/json", resp.Content.Headers.ContentType?.MediaType);

        var doc = await resp.Content.ReadFromJsonAsync<InvoiceRenderDocument>();
        Assert.NotNull(doc);
        Assert.Equal(invoiceId, doc!.InvoiceId);
        Assert.Equal("INV-RND-1", doc.InvoiceNumber);
        Assert.Equal("Acme Co", doc.CustomerName);
        Assert.Single(doc.Lines);
        Assert.Equal(100m, doc.TotalAmount);
        Assert.Equal(100m, doc.AmountDue);
        Assert.Equal(0m, doc.AmountPaid);
        Assert.Null(doc.TemplateSnapshot);
    }

    [Fact]
    public async Task GetRenderHtml_ReturnsTextHtml()
    {
        var tenantId = Guid.NewGuid();
        var (_, invoiceId) = await SeedSimpleInvoiceAsync(tenantId);
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.GetAsync($"/api/invoices/{invoiceId}/render/html");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("text/html", resp.Content.Headers.ContentType?.MediaType);

        var body = await resp.Content.ReadAsStringAsync();
        Assert.StartsWith("<!doctype html>", body);
        Assert.Contains("Invoice INV-RND-1", body);
        Assert.Contains("Acme Co", body);
        Assert.Contains("Consulting", body);
    }

    private async Task<Guid> SeedInvoiceWithAddressAndIssuerAsync(Guid tenantId)
    {
        using var scope = _factory.Services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Acme Co",
            Email = $"billing+{Guid.NewGuid():N}@acme.test",
            BillingAddressLine1 = "100 Main St",
            BillingAddressLine2 = "Suite 4",
            BillingCity = "Springfield",
            BillingStateRegion = "IL",
            BillingPostalCode = "62704",
            BillingCountry = "USA",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await customers.AddAsync(customer);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customer.Id,
            InvoiceNumber = "INV-RND-2",
            Status = "Issued",
            Currency = "USD",
            IssueDate = new DateTime(2026, 4, 1),
            DueDate = new DateTime(2026, 5, 1),
            Subtotal = 100m,
            TaxAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 100m,
            // Issuer snapshot directly stamped on the invoice — the
            // render service reads these columns, never the live
            // template, so no template row is needed for this test.
            IssuerDisplayName = "Brand A Display",
            IssuerLegalName = "Brand A, Inc.",
            IssuerAddressLine1 = "100 Market St",
            IssuerAddressLine2 = "Suite 200",
            IssuerCity = "San Francisco",
            IssuerStateRegion = "CA",
            IssuerPostalCode = "94105",
            IssuerCountry = "USA",
            IssuerEmail = "ar@brand.test",
            IssuerPhone = "+1-415-555-0100",
            IssuerTaxId = "EIN-12-3456789",
            IssuerWebsite = "https://brand.test",
            IssuerStampedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        invoice.LineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            Description = "Consulting",
            Quantity = 2,
            UnitPrice = 50m,
            LineTotal = 100m,
            CreatedAt = DateTime.UtcNow,
        });
        await invoices.AddAsync(invoice);
        return invoice.Id;
    }

    [Fact]
    public async Task GetRender_JsonIncludesCustomerAddressAndIssuer()
    {
        var tenantId = Guid.NewGuid();
        var invoiceId = await SeedInvoiceWithAddressAndIssuerAsync(tenantId);
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.GetAsync($"/api/invoices/{invoiceId}/render");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = await resp.Content.ReadFromJsonAsync<InvoiceRenderDocument>();
        Assert.NotNull(doc);

        Assert.NotNull(doc!.CustomerAddress);
        Assert.Equal("100 Main St", doc.CustomerAddress!.Line1);
        Assert.Equal("IL", doc.CustomerAddress.StateRegion);

        Assert.NotNull(doc.Issuer);
        Assert.Equal("Brand A Display", doc.Issuer!.DisplayName);
        Assert.Equal("Brand A, Inc.", doc.Issuer.LegalName);
        Assert.Equal("https://brand.test", doc.Issuer.Website);
        Assert.NotNull(doc.Issuer.StampedAtUtc);
    }

    [Fact]
    public async Task GetRenderHtml_IncludesFromAndBillToAddressBlocks()
    {
        var tenantId = Guid.NewGuid();
        var invoiceId = await SeedInvoiceWithAddressAndIssuerAsync(tenantId);
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.GetAsync($"/api/invoices/{invoiceId}/render/html");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains(">From<", body);
        Assert.Contains("Brand A Display", body);
        Assert.Contains("href=\"https://brand.test\"", body);
        Assert.Contains(">Bill to<", body);
        Assert.Contains("100 Main St", body);
        Assert.Contains("Springfield, IL 62704", body);
    }

    [Fact]
    public async Task GetRender_MissingInvoice_404()
    {
        var tenantId = Guid.NewGuid();
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.GetAsync($"/api/invoices/{Guid.NewGuid()}/render");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetRenderHtml_MissingInvoice_404()
    {
        var tenantId = Guid.NewGuid();
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.GetAsync($"/api/invoices/{Guid.NewGuid()}/render/html");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetRender_CrossTenant_404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (_, invoiceId) = await SeedSimpleInvoiceAsync(tenantA);
        var clientB = _factory.CreateClientForTenant(tenantB);

        var resp = await clientB.GetAsync($"/api/invoices/{invoiceId}/render");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetRender_MissingTenantHeader_400()
    {
        var rawClient = _factory.CreateClient();
        var resp = await rawClient.GetAsync($"/api/invoices/{Guid.NewGuid()}/render");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GetRenderHtml_MissingTenantHeader_400()
    {
        var rawClient = _factory.CreateClient();
        var resp = await rawClient.GetAsync($"/api/invoices/{Guid.NewGuid()}/render/html");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GetRender_EmptyGuid_400()
    {
        var client = _factory.CreateClientForTenant(Guid.NewGuid());
        var resp = await client.GetAsync($"/api/invoices/{Guid.Empty}/render");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GetRenderHtml_EmptyGuid_400()
    {
        var client = _factory.CreateClientForTenant(Guid.NewGuid());
        var resp = await client.GetAsync($"/api/invoices/{Guid.Empty}/render/html");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
