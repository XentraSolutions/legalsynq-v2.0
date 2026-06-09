using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Billing.Api.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Repositories;
using Billing.Domain.Services;
using Xunit;

namespace Billing.Tests;

/// <summary>
/// INV-TPL-01 hook into invoice creation: when the request omits
/// DueDate, the controller resolves it via
/// <see cref="IInvoiceTemplateSelectionService"/> using the tenant's
/// active default template's <c>DefaultDueDays</c>.
/// </summary>
public class InvoiceCreationDefaultDueDaysApiTests : IClassFixture<BillingWebApplicationFactory>
{
    private readonly BillingWebApplicationFactory _factory;
    public InvoiceCreationDefaultDueDaysApiTests(BillingWebApplicationFactory factory) => _factory = factory;

    private async Task<Guid> SeedCustomerAsync(Guid tenantId)
    {
        using var scope = _factory.Services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var c = new Customer
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Name = "Acme",
            Email = "billing@acme.test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await customers.AddAsync(c);
        return c.Id;
    }

    private static CreateInvoiceRequest BuildInvoiceRequest(Guid customerId, DateTime issueDate, DateTime? dueDate)
        => new()
        {
            CustomerId = customerId,
            IssueDate = issueDate,
            DueDate = dueDate,
            Currency = "USD",
            TaxAmount = 0m,
            Lines = new()
            {
                new CreateInvoiceLineRequest { Description = "Consulting", Quantity = 1, UnitPrice = 100m }
            }
        };

    [Fact]
    public async Task Create_OmittedDueDate_NoTemplate_400()
    {
        var tenantId = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenantId);
        var customerId = await SeedCustomerAsync(tenantId);

        var resp = await client.PostAsJsonAsync("/api/invoices",
            BuildInvoiceRequest(customerId, DateTime.UtcNow.Date, dueDate: null));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Create_OmittedDueDate_AppliesTemplateDefault()
    {
        var tenantId = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenantId);
        var customerId = await SeedCustomerAsync(tenantId);

        // Seed an Active default template with DefaultDueDays = 14.
        var templateResp = await client.PostAsJsonAsync(
            "/api/invoice-templates/tenant",
            new CreateInvoiceTemplateRequest
            {
                Name = "Default tenant brand",
                Status = InvoiceTemplateStatus.Active,
                IsDefault = true,
                DefaultDueDays = 14,
            });
        Assert.Equal(HttpStatusCode.Created, templateResp.StatusCode);

        var issueDate = new DateTime(2026, 4, 1);
        var resp = await client.PostAsJsonAsync("/api/invoices",
            BuildInvoiceRequest(customerId, issueDate, dueDate: null));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.NotNull(body);
        Assert.Equal(issueDate.AddDays(14), body!.DueDate);
    }

    [Fact]
    public async Task Create_ProvidedDueDate_OverridesTemplateDefault()
    {
        var tenantId = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenantId);
        var customerId = await SeedCustomerAsync(tenantId);

        await client.PostAsJsonAsync("/api/invoice-templates/tenant",
            new CreateInvoiceTemplateRequest
            {
                Name = "Default",
                Status = InvoiceTemplateStatus.Active,
                IsDefault = true,
                DefaultDueDays = 14,
            });

        var issueDate = new DateTime(2026, 4, 1);
        var explicitDue = new DateTime(2026, 6, 1);
        var resp = await client.PostAsJsonAsync("/api/invoices",
            BuildInvoiceRequest(customerId, issueDate, dueDate: explicitDue));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal(explicitDue, body!.DueDate);
    }

    [Fact]
    public async Task Create_TenantA_DoesNotApply_TenantBsDefault()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var clientA = _factory.CreateClientForTenant(tenantA);
        var clientB = _factory.CreateClientForTenant(tenantB);
        var customerB = await SeedCustomerAsync(tenantB);

        // Only tenant A configures a default.
        await clientA.PostAsJsonAsync("/api/invoice-templates/tenant",
            new CreateInvoiceTemplateRequest
            {
                Name = "A default",
                Status = InvoiceTemplateStatus.Active,
                IsDefault = true,
                DefaultDueDays = 14,
            });

        // Tenant B creates an invoice without DueDate. There is no
        // default in B's scope, so the response must be 400 — A's
        // default must NOT leak across tenants.
        var resp = await clientB.PostAsJsonAsync("/api/invoices",
            BuildInvoiceRequest(customerB, DateTime.UtcNow.Date, dueDate: null));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
