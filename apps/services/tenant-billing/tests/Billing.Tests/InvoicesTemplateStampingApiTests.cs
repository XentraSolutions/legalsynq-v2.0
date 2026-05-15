using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Billing.Api.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Repositories;
using Xunit;

namespace Billing.Tests;

/// <summary>
/// INV-TPL-02: HTTP-surface tests for the create-invoice template
/// stamping behaviour. Exercises the explicit-id path, the implicit
/// tenant-default fallback, the historical-snapshot guarantee, and
/// the cross-tenant 400.
/// </summary>
public class InvoicesTemplateStampingApiTests : IClassFixture<BillingWebApplicationFactory>
{
    private readonly BillingWebApplicationFactory _factory;
    public InvoicesTemplateStampingApiTests(BillingWebApplicationFactory factory) => _factory = factory;

    private async Task<Guid> SeedCustomerAsync(Guid tenantId)
    {
        using var scope = _factory.Services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var c = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Acme",
            Email = $"billing+{Guid.NewGuid():N}@acme.test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await customers.AddAsync(c);
        return c.Id;
    }

    private static CreateInvoiceTemplateRequest TemplatePayload(string name, bool isDefault = false) => new()
    {
        Name = name,
        Status = InvoiceTemplateStatus.Active,
        IsDefault = isDefault,
        AccentColor = "#10B981",
        HeaderText = $"Header {name}",
        FooterText = $"Footer {name}",
        DefaultDueDays = 30,
    };

    private static CreateInvoiceRequest InvoicePayload(Guid customerId, Guid? templateId = null, DateTime? dueDate = null)
        => new()
        {
            CustomerId = customerId,
            IssueDate = new DateTime(2026, 4, 1),
            DueDate = dueDate ?? new DateTime(2026, 5, 1),
            Currency = "USD",
            TaxAmount = 0m,
            InvoiceTemplateId = templateId,
            Lines = new()
            {
                new CreateInvoiceLineRequest { Description = "Consulting", Quantity = 1, UnitPrice = 100m }
            }
        };

    private static async Task<InvoiceTemplateResponse> CreateTemplateAsync(
        HttpClient client, string name, bool isDefault = false)
    {
        var resp = await client.PostAsJsonAsync(
            "/api/invoice-templates/tenant", TemplatePayload(name, isDefault));
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<InvoiceTemplateResponse>();
        Assert.NotNull(body);
        return body!;
    }

    [Fact]
    public async Task Create_WithExplicitTemplateId_ResponseCarriesSnapshot()
    {
        var tenantId = Guid.NewGuid();
        var client = _factory.CreateClientForTenant(tenantId);
        var customerId = await SeedCustomerAsync(tenantId);
        var tpl = await CreateTemplateAsync(client, "Brand A");

        var resp = await client.PostAsJsonAsync("/api/invoices",
            InvoicePayload(customerId, templateId: tpl.Id));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body!.TemplateSnapshot);
        Assert.Equal(tpl.Id, body.TemplateSnapshot!.Id);
        Assert.Equal("Brand A", body.TemplateSnapshot.Name);
        Assert.Equal("#10B981", body.TemplateSnapshot.AccentColor);
        Assert.Equal("Header Brand A", body.TemplateSnapshot.HeaderText);
        Assert.NotNull(body.TemplateSnapshot.StampedAtUtc);
    }

    [Fact]
    public async Task Create_NoExplicitId_FallsBackToTenantDefault()
    {
        var tenantId = Guid.NewGuid();
        var client = _factory.CreateClientForTenant(tenantId);
        var customerId = await SeedCustomerAsync(tenantId);
        var tpl = await CreateTemplateAsync(client, "Default", isDefault: true);

        var resp = await client.PostAsJsonAsync("/api/invoices",
            InvoicePayload(customerId, templateId: null));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.NotNull(body!.TemplateSnapshot);
        Assert.Equal(tpl.Id, body.TemplateSnapshot!.Id);
        Assert.Equal("Default", body.TemplateSnapshot.Name);
    }

    [Fact]
    public async Task Create_NoExplicitId_NoDefault_StillSucceedsUnstamped()
    {
        var tenantId = Guid.NewGuid();
        var client = _factory.CreateClientForTenant(tenantId);
        var customerId = await SeedCustomerAsync(tenantId);

        // No template configured, but DueDate is supplied so the
        // INV-TPL-01 fallback isn't required → 201 with a null
        // TemplateSnapshot.
        var resp = await client.PostAsJsonAsync("/api/invoices",
            InvoicePayload(customerId, templateId: null));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Null(body!.TemplateSnapshot);
    }

    [Fact]
    public async Task Create_ExplicitId_OtherTenant_400()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var clientA = _factory.CreateClientForTenant(tenantA);
        var clientB = _factory.CreateClientForTenant(tenantB);
        var customerB = await SeedCustomerAsync(tenantB);

        // A creates a template; B tries to stamp it.
        var aTpl = await CreateTemplateAsync(clientA, "A's brand");

        var resp = await clientB.PostAsJsonAsync("/api/invoices",
            InvoicePayload(customerB, templateId: aTpl.Id));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Create_ExplicitId_Retired_400()
    {
        var tenantId = Guid.NewGuid();
        var client = _factory.CreateClientForTenant(tenantId);
        var customerId = await SeedCustomerAsync(tenantId);
        var tpl = await CreateTemplateAsync(client, "ToRetire");

        // Retire the template (POST … /retire on the standard
        // template surface).
        var retire = await client.PostAsync(
            $"/api/invoice-templates/tenant/{tpl.Id}/retire", content: null);
        retire.EnsureSuccessStatusCode();

        var resp = await client.PostAsJsonAsync("/api/invoices",
            InvoicePayload(customerId, templateId: tpl.Id));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Snapshot_SurvivesSubsequentTemplateEdit()
    {
        var tenantId = Guid.NewGuid();
        var client = _factory.CreateClientForTenant(tenantId);
        var customerId = await SeedCustomerAsync(tenantId);
        var tpl = await CreateTemplateAsync(client, "Original");

        var createResp = await client.PostAsJsonAsync("/api/invoices",
            InvoicePayload(customerId, templateId: tpl.Id));
        var created = await createResp.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.NotNull(created!.TemplateSnapshot);

        // Mutate the template (rename + recolour).
        var update = await client.PutAsJsonAsync(
            $"/api/invoice-templates/tenant/{tpl.Id}",
            new UpdateInvoiceTemplateRequest
            {
                Name = "Renamed",
                AccentColor = "#FF0000",
            });
        update.EnsureSuccessStatusCode();

        // Reload the invoice — snapshot must reflect the ORIGINAL
        // template state, not the post-edit state.
        var reload = await client.GetFromJsonAsync<InvoiceResponse>($"/api/invoices/{created.Id}");
        Assert.NotNull(reload!.TemplateSnapshot);
        Assert.Equal("Original", reload.TemplateSnapshot!.Name);
        Assert.Equal("#10B981", reload.TemplateSnapshot.AccentColor);
    }
}
