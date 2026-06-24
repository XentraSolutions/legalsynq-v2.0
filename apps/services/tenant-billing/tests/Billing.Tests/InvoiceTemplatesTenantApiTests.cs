using System.Net;
using System.Net.Http.Json;
using Billing.Api.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Services;
using Xunit;

namespace Billing.Tests;

/// <summary>
/// HTTP surface for the Tenant Billing template catalogue. Tenant
/// routes require X-Tenant-Id and are scoped to the calling tenant.
/// </summary>
public class InvoiceTemplatesTenantApiTests : IClassFixture<BillingWebApplicationFactory>
{
    private readonly BillingWebApplicationFactory _factory;
    public InvoiceTemplatesTenantApiTests(BillingWebApplicationFactory factory) => _factory = factory;

    private static CreateInvoiceTemplateRequest SampleRequest(
        string? status = null,
        bool? isDefault = null,
        int? defaultDueDays = 30) => new()
    {
        Name = "Tenant brand",
        Status = status,
        IsDefault = isDefault,
        AccentColor = "#10B981",
        DefaultDueDays = defaultDueDays,
        InvoiceNumberPrefix = "INV",
    };

    [Fact]
    public async Task Tenant_RoutesRequireHeader_400()
    {
        var bare = _factory.CreateClient(); // NO header
        var resp = await bare.GetAsync("/api/invoice-templates/tenant");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Create_AssignsTenantOwnership()
    {
        var tenantId = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenantId);

        var resp = await client.PostAsJsonAsync("/api/invoice-templates/tenant",
            SampleRequest(status: InvoiceTemplateStatus.Active));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<InvoiceTemplateResponse>();
        Assert.NotNull(body);
        Assert.Equal(InvoiceTemplateOwnerType.Tenant, body!.OwnerType);
        Assert.Equal(tenantId, body.BillingAccountId);
        Assert.True(body.IsDefault); // first active in scope
    }

    [Fact]
    public async Task TenantA_CannotSeeTenantBsTemplates()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var clientA = _factory.CreateClientForTenant(tenantA);
        var clientB = _factory.CreateClientForTenant(tenantB);

        var aT = await (await clientA.PostAsJsonAsync("/api/invoice-templates/tenant",
            SampleRequest(status: InvoiceTemplateStatus.Active))).Content.ReadFromJsonAsync<InvoiceTemplateResponse>();

        // B's list is empty.
        var bList = await clientB.GetFromJsonAsync<List<InvoiceTemplateSummaryResponse>>(
            "/api/invoice-templates/tenant");
        Assert.NotNull(bList);
        Assert.Empty(bList!);

        // B cannot fetch A's template by id.
        var bGet = await clientB.GetAsync($"/api/invoice-templates/tenant/{aT!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, bGet.StatusCode);

        // B cannot promote A's template either.
        var bPromote = await clientB.PostAsync(
            $"/api/invoice-templates/tenant/{aT.Id}/make-default", content: null);
        Assert.Equal(HttpStatusCode.NotFound, bPromote.StatusCode);
    }

    [Fact]
    public async Task Update_PartialPatch_OnlyChangesSuppliedFields()
    {
        var client = _factory.CreateClientForTenant(Guid.CreateVersion7());
        var created = await (await client.PostAsJsonAsync("/api/invoice-templates/tenant",
            SampleRequest(status: InvoiceTemplateStatus.Draft))).Content.ReadFromJsonAsync<InvoiceTemplateResponse>();

        var put = await client.PutAsJsonAsync(
            $"/api/invoice-templates/tenant/{created!.Id}",
            new UpdateInvoiceTemplateRequest { AccentColor = "#000000" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var body = await put.Content.ReadFromJsonAsync<InvoiceTemplateResponse>();
        Assert.Equal("#000000", body!.AccentColor);
        Assert.Equal(created.Name, body.Name); // unchanged
        Assert.Equal(created.DefaultDueDays, body.DefaultDueDays); // unchanged
    }

    [Fact]
    public async Task Update_Retired_400()
    {
        var client = _factory.CreateClientForTenant(Guid.CreateVersion7());
        var created = await (await client.PostAsJsonAsync("/api/invoice-templates/tenant",
            SampleRequest(status: InvoiceTemplateStatus.Active))).Content.ReadFromJsonAsync<InvoiceTemplateResponse>();
        await client.PostAsync($"/api/invoice-templates/tenant/{created!.Id}/retire", content: null);

        var put = await client.PutAsJsonAsync(
            $"/api/invoice-templates/tenant/{created.Id}",
            new UpdateInvoiceTemplateRequest { Name = "New name" });
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }
}
