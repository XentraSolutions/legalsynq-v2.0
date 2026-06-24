using System.Net;
using System.Net.Http.Json;
using Billing.Api.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Services;
using Xunit;

namespace Billing.Tests;

/// <summary>
/// HTTP surface for the Platform Billing template catalogue. Platform
/// routes do NOT require X-Tenant-Id, so each test uses the bare
/// HttpClient (no header).
///
/// NOTE: the Platform scope is global (no tenant key), so any state
/// created by one platform test would leak into the next under a
/// shared <see cref="IClassFixture{T}"/>. We therefore stand up a
/// fresh <see cref="BillingWebApplicationFactory"/> (and its
/// per-instance InMemory database) per test method. The cost is a
/// couple of seconds per test which is acceptable for the small
/// number of platform-scoped tests we run.
/// </summary>
public class InvoiceTemplatesPlatformApiTests : IDisposable
{
    private readonly BillingWebApplicationFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    private static CreateInvoiceTemplateRequest SampleRequest(
        string? status = null,
        bool? isDefault = null,
        int? defaultDueDays = null) => new()
    {
        Name = "Platform brand",
        Description = "Used when platform invoices a tenant",
        Status = status,
        IsDefault = isDefault,
        AccentColor = "#1F4FFF",
        DefaultDueDays = defaultDueDays,
        InvoiceNumberPrefix = "PLAT",
        DisplayBillingAddress = true,
        DisplayPaymentInstructions = true,
        DisplayTerms = true,
    };

    [Fact]
    public async Task Create_NoHeaderRequired_201()
    {
        var client = _factory.CreateClient(); // NO X-Tenant-Id

        var resp = await client.PostAsJsonAsync(
            "/api/invoice-templates/platform",
            SampleRequest(status: InvoiceTemplateStatus.Active, defaultDueDays: 30));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<InvoiceTemplateResponse>();
        Assert.NotNull(body);
        Assert.Equal(InvoiceTemplateOwnerType.Platform, body!.OwnerType);
        Assert.Null(body.BillingAccountId);
        // First Active template in scope auto-becomes default.
        Assert.True(body.IsDefault);
    }

    [Fact]
    public async Task Create_InvalidColor_400()
    {
        var client = _factory.CreateClient();
        var bad = SampleRequest(status: InvoiceTemplateStatus.Draft);
        bad.AccentColor = "blue";

        var resp = await client.PostAsJsonAsync("/api/invoice-templates/platform", bad);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task List_AndGet_RoundTrip()
    {
        var client = _factory.CreateClient();
        var create = await client.PostAsJsonAsync("/api/invoice-templates/platform", SampleRequest());
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<InvoiceTemplateResponse>();

        var list = await client.GetFromJsonAsync<List<InvoiceTemplateSummaryResponse>>("/api/invoice-templates/platform");
        Assert.NotNull(list);
        Assert.Contains(list!, t => t.Id == created!.Id);

        var single = await client.GetFromJsonAsync<InvoiceTemplateResponse>(
            $"/api/invoice-templates/platform/{created!.Id}");
        Assert.NotNull(single);
        Assert.Equal(created.Id, single!.Id);
    }

    [Fact]
    public async Task GetMissing_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync($"/api/invoice-templates/platform/{Guid.CreateVersion7()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Activate_Then_MakeDefault_Promotes()
    {
        var client = _factory.CreateClient();
        var first = await (await client.PostAsJsonAsync("/api/invoice-templates/platform",
            SampleRequest(status: InvoiceTemplateStatus.Active))).Content.ReadFromJsonAsync<InvoiceTemplateResponse>();

        var second = await (await client.PostAsJsonAsync("/api/invoice-templates/platform",
            SampleRequest(status: InvoiceTemplateStatus.Active))).Content.ReadFromJsonAsync<InvoiceTemplateResponse>();

        Assert.True(first!.IsDefault);
        Assert.False(second!.IsDefault);

        var promote = await client.PostAsync(
            $"/api/invoice-templates/platform/{second.Id}/make-default", content: null);
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);

        var body = await promote.Content.ReadFromJsonAsync<MakeDefaultTemplateResponse>();
        Assert.NotNull(body);
        Assert.Equal(second.Id, body!.Template.Id);
        Assert.True(body.Template.IsDefault);
        Assert.Equal(first.Id, body.PreviousDefaultTemplateId);

        // The previous default should no longer report as default.
        var firstAfter = await client.GetFromJsonAsync<InvoiceTemplateResponse>(
            $"/api/invoice-templates/platform/{first.Id}");
        Assert.False(firstAfter!.IsDefault);

        // GET .../default returns the new default.
        var def = await client.GetFromJsonAsync<InvoiceTemplateResponse>(
            "/api/invoice-templates/platform/default");
        Assert.Equal(second.Id, def!.Id);
    }

    [Fact]
    public async Task Retire_OnDefault_ClearsDefault()
    {
        var client = _factory.CreateClient();
        var t = await (await client.PostAsJsonAsync("/api/invoice-templates/platform",
            SampleRequest(status: InvoiceTemplateStatus.Active))).Content.ReadFromJsonAsync<InvoiceTemplateResponse>();

        var retire = await client.PostAsync(
            $"/api/invoice-templates/platform/{t!.Id}/retire", content: null);
        Assert.Equal(HttpStatusCode.OK, retire.StatusCode);

        var body = await retire.Content.ReadFromJsonAsync<InvoiceTemplateResponse>();
        Assert.Equal(InvoiceTemplateStatus.Retired, body!.Status);
        Assert.False(body.IsDefault);

        var def = await client.GetAsync("/api/invoice-templates/platform/default");
        Assert.Equal(HttpStatusCode.NotFound, def.StatusCode);
    }

    [Fact]
    public async Task MakeDefault_OnRetired_400()
    {
        var client = _factory.CreateClient();
        var t = await (await client.PostAsJsonAsync("/api/invoice-templates/platform",
            SampleRequest(status: InvoiceTemplateStatus.Active))).Content.ReadFromJsonAsync<InvoiceTemplateResponse>();
        await client.PostAsync($"/api/invoice-templates/platform/{t!.Id}/retire", content: null);

        var resp = await client.PostAsync(
            $"/api/invoice-templates/platform/{t.Id}/make-default", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
