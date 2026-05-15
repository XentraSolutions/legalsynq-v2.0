using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TenantBilling.Api.Contracts;
using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Repositories;
using Xunit;

namespace TenantBilling.Tests;

/// <summary>
/// STAT-B02 — HTTP-surface tests for the persisted statement
/// endpoints (<c>POST .../generate</c>, monthly/generate, history,
/// render/html, void).
/// </summary>
public class StatementsPersistenceApiTests : IClassFixture<TenantBillingWebApplicationFactory>
{
    private readonly TenantBillingWebApplicationFactory _factory;
    public StatementsPersistenceApiTests(TenantBillingWebApplicationFactory factory) => _factory = factory;

    private async Task<Guid> SeedCustomerWithInvoiceAsync(Guid tenant, string customerName = "Acme Co")
    {
        using var scope = _factory.Services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            Name = customerName,
            Email = $"x+{Guid.NewGuid():N}@acme.test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await customers.AddAsync(customer);

        await invoices.AddAsync(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            CustomerId = customer.Id,
            InvoiceNumber = $"INV-{Guid.NewGuid().ToString("N")[..8]}",
            Status = InvoiceStatus.Issued,
            Currency = "USD",
            IssueDate = new DateTime(2026, 4, 10),
            DueDate = new DateTime(2026, 5, 10),
            Subtotal = 200m,
            TotalAmount = 200m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        return customer.Id;
    }

    [Fact]
    public async Task GenerateMonthly_201_PersistsAndAssignsNumber()
    {
        var tenant = Guid.NewGuid();
        var cid = await SeedCustomerWithInvoiceAsync(tenant);
        var client = _factory.CreateClientForTenant(tenant);

        var resp = await client.PostAsJsonAsync(
            $"/api/statements/customers/{cid}/monthly/generate",
            new GenerateMonthlyStatementRequest { Year = 2026, Month = 4 });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<CustomerStatementResponse>();
        Assert.NotNull(body);
        Assert.Equal("STMT-2026-000001", body!.StatementNumber);
        Assert.Equal(CustomerStatementStatus.Generated, body.Status);
        Assert.Equal(200m, body.TotalInvoiced);
        Assert.False(string.IsNullOrEmpty(body.StatementSnapshotJson));
        Assert.False(body.HasHtmlSnapshot);

        // Returned Location header points at /api/statements/history/{id}
        Assert.NotNull(resp.Headers.Location);
        Assert.Contains($"/api/statements/history/{body.Id}", resp.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Generate_WithRenderHtml_ProducesHtmlSnapshot()
    {
        var tenant = Guid.NewGuid();
        var cid = await SeedCustomerWithInvoiceAsync(tenant, "Globex Industries");
        var client = _factory.CreateClientForTenant(tenant);

        var resp = await client.PostAsJsonAsync(
            $"/api/statements/customers/{cid}/generate",
            new GenerateStatementRequest
            {
                PeriodStart = new DateTime(2026, 4, 1),
                PeriodEnd = new DateTime(2026, 4, 30),
                RenderHtml = true,
            });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<CustomerStatementResponse>();
        Assert.True(body!.HasHtmlSnapshot);

        var html = await client.GetAsync($"/api/statements/history/{body.Id}/render/html");
        Assert.Equal(HttpStatusCode.OK, html.StatusCode);
        Assert.Contains("Globex Industries", await html.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Generate_DraftTemplate_400()
    {
        var tenant = Guid.NewGuid();
        var cid = await SeedCustomerWithInvoiceAsync(tenant);
        var client = _factory.CreateClientForTenant(tenant);

        var draft = await (await client.PostAsJsonAsync("/api/statement-templates",
            new CreateStatementTemplateRequest { Name = "Draft" })).Content
            .ReadFromJsonAsync<StatementTemplateResponse>();

        var resp = await client.PostAsJsonAsync(
            $"/api/statements/customers/{cid}/monthly/generate",
            new GenerateMonthlyStatementRequest { Year = 2026, Month = 4, TemplateId = draft!.Id });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Generate_UnknownCustomer_404()
    {
        var tenant = Guid.NewGuid();
        var client = _factory.CreateClientForTenant(tenant);

        var resp = await client.PostAsJsonAsync(
            $"/api/statements/customers/{Guid.NewGuid()}/monthly/generate",
            new GenerateMonthlyStatementRequest { Year = 2026, Month = 4 });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Generate_CrossTenantCustomer_404()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var cid = await SeedCustomerWithInvoiceAsync(owner);
        var client = _factory.CreateClientForTenant(other);

        var resp = await client.PostAsJsonAsync(
            $"/api/statements/customers/{cid}/monthly/generate",
            new GenerateMonthlyStatementRequest { Year = 2026, Month = 4 });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task History_ListsOnlyOwnedSnapshots()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var cid = await SeedCustomerWithInvoiceAsync(owner);
        var ownerClient = _factory.CreateClientForTenant(owner);
        var otherClient = _factory.CreateClientForTenant(other);

        var first = await ownerClient.PostAsJsonAsync(
            $"/api/statements/customers/{cid}/monthly/generate",
            new GenerateMonthlyStatementRequest { Year = 2026, Month = 4 });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<CustomerStatementResponse>();

        // Owner sees one entry.
        var ownerList = await ownerClient.GetFromJsonAsync<List<CustomerStatementSummaryResponse>>(
            $"/api/statements/customers/{cid}/history");
        Assert.NotNull(ownerList);
        Assert.Single(ownerList!);

        // Other tenant sees nothing under the same customer id (cross-tenant 404 path).
        var otherList = await otherClient.GetAsync($"/api/statements/customers/{cid}/history");
        Assert.Equal(HttpStatusCode.OK, otherList.StatusCode);
        var otherItems = await otherList.Content.ReadFromJsonAsync<List<CustomerStatementSummaryResponse>>();
        Assert.Empty(otherItems!);

        // Other tenant cannot fetch by snapshot id.
        var otherGet = await otherClient.GetAsync($"/api/statements/history/{firstBody!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, otherGet.StatusCode);
    }

    [Fact]
    public async Task RenderHistoryHtml_LazyRehydrates_WhenNoCachedHtml()
    {
        var tenant = Guid.NewGuid();
        var cid = await SeedCustomerWithInvoiceAsync(tenant, "Lazy Co");
        var client = _factory.CreateClientForTenant(tenant);

        var generated = await (await client.PostAsJsonAsync(
            $"/api/statements/customers/{cid}/monthly/generate",
            new GenerateMonthlyStatementRequest { Year = 2026, Month = 4, RenderHtml = false }))
            .Content.ReadFromJsonAsync<CustomerStatementResponse>();
        Assert.False(generated!.HasHtmlSnapshot);

        var html = await client.GetAsync($"/api/statements/history/{generated.Id}/render/html");
        Assert.Equal(HttpStatusCode.OK, html.StatusCode);
        var content = await html.Content.ReadAsStringAsync();
        Assert.Contains("Lazy Co", content);
    }

    [Fact]
    public async Task Void_IsIdempotent_AndCrossTenantIsolated()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var cid = await SeedCustomerWithInvoiceAsync(owner);
        var ownerClient = _factory.CreateClientForTenant(owner);
        var otherClient = _factory.CreateClientForTenant(other);

        var generated = await (await ownerClient.PostAsJsonAsync(
            $"/api/statements/customers/{cid}/monthly/generate",
            new GenerateMonthlyStatementRequest { Year = 2026, Month = 4 }))
            .Content.ReadFromJsonAsync<CustomerStatementResponse>();

        // Cross-tenant void → 404.
        var crossVoid = await otherClient.PostAsJsonAsync(
            $"/api/statements/history/{generated!.Id}/void",
            new VoidStatementRequest { Reason = "noop" });
        Assert.Equal(HttpStatusCode.NotFound, crossVoid.StatusCode);

        var v1 = await ownerClient.PostAsJsonAsync(
            $"/api/statements/history/{generated.Id}/void",
            new VoidStatementRequest { Reason = "duplicate" });
        Assert.Equal(HttpStatusCode.OK, v1.StatusCode);
        var b1 = await v1.Content.ReadFromJsonAsync<CustomerStatementResponse>();
        Assert.Equal(CustomerStatementStatus.Voided, b1!.Status);
        Assert.Equal("duplicate", b1.VoidReason);

        var v2 = await ownerClient.PostAsJsonAsync(
            $"/api/statements/history/{generated.Id}/void",
            new VoidStatementRequest { Reason = "ignored" });
        Assert.Equal(HttpStatusCode.OK, v2.StatusCode);
        var b2 = await v2.Content.ReadFromJsonAsync<CustomerStatementResponse>();
        Assert.Equal("duplicate", b2!.VoidReason); // first reason wins
        Assert.Equal(b1.VoidedAtUtc, b2.VoidedAtUtc);
    }

    [Fact]
    public async Task Generate_StampsDefaultTemplate()
    {
        var tenant = Guid.NewGuid();
        var cid = await SeedCustomerWithInvoiceAsync(tenant);
        var client = _factory.CreateClientForTenant(tenant);

        var t = await (await client.PostAsJsonAsync("/api/statement-templates",
            new CreateStatementTemplateRequest
            {
                Name = "Default",
                Status = StatementTemplateStatus.Active,
            })).Content.ReadFromJsonAsync<StatementTemplateResponse>();

        var resp = await client.PostAsJsonAsync(
            $"/api/statements/customers/{cid}/monthly/generate",
            new GenerateMonthlyStatementRequest { Year = 2026, Month = 4 });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<CustomerStatementResponse>();
        Assert.Equal(t!.Id, body!.TemplateId);
        Assert.False(string.IsNullOrEmpty(body.TemplateSnapshotJson));
    }
}
