using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Billing.Domain.Entities;
using Billing.Domain.Repositories;
using Billing.Domain.Services;
using Xunit;

namespace Billing.Tests;

/// <summary>
/// TBS-B05: HTTP surface for mark-overdue (single + batch). Covers the
/// happy paths plus the 400 / 404 mappings demanded by the spec.
/// </summary>
public class InvoicesMarkOverdueApiTests : IClassFixture<BillingWebApplicationFactory>
{
    private readonly BillingWebApplicationFactory _factory;

    public InvoicesMarkOverdueApiTests(BillingWebApplicationFactory factory) => _factory = factory;

    private async Task<(Guid tenantId, Guid customerId, Guid invoiceId)> SeedAsync(
        string status, DateTime dueDate)
    {
        var tenantId = Guid.CreateVersion7();
        using var scope = _factory.Services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();

        var customer = new Customer
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Name = "Acme",
            Email = "billing@acme.test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await customers.AddAsync(customer);

        var inv = new Invoice
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CustomerId = customer.Id,
            InvoiceNumber = $"INV-2026-{Random.Shared.Next(100000, 999999)}",
            IssueDate = DateTime.UtcNow.AddDays(-30),
            DueDate = dueDate,
            Status = status,
            Subtotal = 100m,
            TaxAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 100m,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await invoices.AddAsync(inv);
        return (tenantId, customer.Id, inv.Id);
    }

    [Fact]
    public async Task POST_mark_overdue_returns_200_and_lifecycle_response_for_eligible_invoice()
    {
        var (tenantId, _, invoiceId) = await SeedAsync(InvoiceStatus.Issued, DateTime.UtcNow.AddDays(-3));
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.PostAsync($"/api/invoices/{invoiceId}/mark-overdue", content: null);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(invoiceId, body.GetProperty("id").GetGuid());
        Assert.Equal(InvoiceStatus.Issued,  body.GetProperty("previousStatus").GetString());
        Assert.Equal(InvoiceStatus.Overdue, body.GetProperty("currentStatus").GetString());
    }

    [Fact]
    public async Task POST_mark_overdue_returns_400_for_future_due_invoice()
    {
        var (tenantId, _, invoiceId) = await SeedAsync(InvoiceStatus.Issued, DateTime.UtcNow.AddDays(7));
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.PostAsync($"/api/invoices/{invoiceId}/mark-overdue", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task POST_mark_overdue_returns_400_when_status_disallowed()
    {
        // Paid → Overdue is structurally rejected by the lifecycle engine.
        var (tenantId, _, invoiceId) = await SeedAsync(InvoiceStatus.Paid, DateTime.UtcNow.AddDays(-3));
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.PostAsync($"/api/invoices/{invoiceId}/mark-overdue", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task POST_mark_overdue_returns_404_for_cross_tenant_invoice()
    {
        var (ownerTenant, _, invoiceId) = await SeedAsync(InvoiceStatus.Issued, DateTime.UtcNow.AddDays(-3));
        // Different tenant making the request — must appear as not found.
        var otherTenant = Guid.CreateVersion7();
        Assert.NotEqual(ownerTenant, otherTenant);
        var client = _factory.CreateClientForTenant(otherTenant);

        var resp = await client.PostAsync($"/api/invoices/{invoiceId}/mark-overdue", content: null);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task POST_mark_overdue_returns_404_when_invoice_missing()
    {
        var tenantId = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.PostAsync($"/api/invoices/{Guid.CreateVersion7()}/mark-overdue", content: null);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task POST_mark_overdue_batch_returns_count_for_calling_tenant()
    {
        var (tenantId, customerId, _) = await SeedAsync(InvoiceStatus.Issued, DateTime.UtcNow.AddDays(-5));
        // Seed a second eligible + one ineligible for the same tenant.
        using (var scope = _factory.Services.CreateScope())
        {
            var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
            await invoices.AddAsync(new Invoice
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                CustomerId = customerId,
                InvoiceNumber = $"INV-2026-{Random.Shared.Next(100000, 999999)}",
                IssueDate = DateTime.UtcNow.AddDays(-30),
                DueDate = DateTime.UtcNow.AddDays(-1),
                Status = InvoiceStatus.PartiallyPaid,
                Subtotal = 50m, TaxAmount = 0m, DiscountAmount = 0m, TotalAmount = 50m,
                Currency = "USD",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await invoices.AddAsync(new Invoice
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                CustomerId = customerId,
                InvoiceNumber = $"INV-2026-{Random.Shared.Next(100000, 999999)}",
                IssueDate = DateTime.UtcNow.AddDays(-30),
                DueDate = DateTime.UtcNow.AddDays(7),
                Status = InvoiceStatus.Issued,
                Subtotal = 75m, TaxAmount = 0m, DiscountAmount = 0m, TotalAmount = 75m,
                Currency = "USD",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
        }

        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.PostAsync("/api/invoices/mark-overdue?take=50", content: null);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("updatedCount").GetInt32());
        Assert.Equal(0, body.GetProperty("failedCount").GetInt32());
    }
}
