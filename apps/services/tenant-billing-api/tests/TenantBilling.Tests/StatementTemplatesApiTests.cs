using System.Net;
using System.Net.Http.Json;
using TenantBilling.Api.Contracts;
using TenantBilling.Domain.Entities;
using Xunit;

namespace TenantBilling.Tests;

/// <summary>
/// STAT-B02 — HTTP surface for the tenant-scoped statement template
/// catalogue (<c>/api/statement-templates</c>). Mirrors the
/// invoice-template tenant test class.
/// </summary>
public class StatementTemplatesApiTests : IClassFixture<TenantBillingWebApplicationFactory>
{
    private readonly TenantBillingWebApplicationFactory _factory;
    public StatementTemplatesApiTests(TenantBillingWebApplicationFactory factory) => _factory = factory;

    private static CreateStatementTemplateRequest Sample(
        string name = "Brand A",
        string? status = null,
        bool? isDefault = null) => new()
    {
        Name = name,
        Status = status,
        IsDefault = isDefault,
        AccentColor = "#1F4FFF",
        StatementNumberPrefix = "STMT-A",
    };

    [Fact]
    public async Task RoutesRequireTenantHeader_400()
    {
        var bare = _factory.CreateClient();
        var resp = await bare.GetAsync("/api/statement-templates");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Create_AssignsTenantOwnership_AndAutoDefaultsFirstActive()
    {
        var tenant = Guid.NewGuid();
        var client = _factory.CreateClientForTenant(tenant);

        var resp = await client.PostAsJsonAsync("/api/statement-templates",
            Sample(status: StatementTemplateStatus.Active));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<StatementTemplateResponse>();
        Assert.NotNull(body);
        Assert.Equal(tenant, body!.TenantId);
        Assert.Equal(StatementTemplateStatus.Active, body.Status);
        Assert.True(body.IsDefault);
    }

    [Fact]
    public async Task TenantA_CannotSeeOrMutateTenantBsTemplates()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var ca = _factory.CreateClientForTenant(a);
        var cb = _factory.CreateClientForTenant(b);

        var aT = await (await ca.PostAsJsonAsync("/api/statement-templates",
            Sample(status: StatementTemplateStatus.Active))).Content
            .ReadFromJsonAsync<StatementTemplateResponse>();

        var bList = await cb.GetFromJsonAsync<List<StatementTemplateSummaryResponse>>(
            "/api/statement-templates");
        Assert.NotNull(bList);
        Assert.Empty(bList!);

        var bGet = await cb.GetAsync($"/api/statement-templates/{aT!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, bGet.StatusCode);

        var bPromote = await cb.PostAsync(
            $"/api/statement-templates/{aT.Id}/make-default", content: null);
        Assert.Equal(HttpStatusCode.NotFound, bPromote.StatusCode);

        var bRetire = await cb.PostAsync(
            $"/api/statement-templates/{aT.Id}/retire", content: null);
        Assert.Equal(HttpStatusCode.NotFound, bRetire.StatusCode);
    }

    [Fact]
    public async Task MakeDefault_Succeeds_AndDemotesPrior()
    {
        var tenant = Guid.NewGuid();
        var client = _factory.CreateClientForTenant(tenant);

        var a = await (await client.PostAsJsonAsync("/api/statement-templates",
            Sample("A", status: StatementTemplateStatus.Active))).Content
            .ReadFromJsonAsync<StatementTemplateResponse>();
        var b = await (await client.PostAsJsonAsync("/api/statement-templates",
            Sample("B", status: StatementTemplateStatus.Active))).Content
            .ReadFromJsonAsync<StatementTemplateResponse>();

        Assert.True(a!.IsDefault); Assert.False(b!.IsDefault);

        var promote = await client.PostAsync($"/api/statement-templates/{b.Id}/make-default", content: null);
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);
        var promoted = await promote.Content.ReadFromJsonAsync<MakeDefaultStatementTemplateResponse>();
        Assert.True(promoted!.Template.IsDefault);
        Assert.Equal(a.Id, promoted.PreviousDefaultId);

        var list = await client.GetFromJsonAsync<List<StatementTemplateSummaryResponse>>(
            "/api/statement-templates");
        Assert.Equal(2, list!.Count);
        Assert.Single(list, t => t.IsDefault && t.Id == b.Id);
    }

    [Fact]
    public async Task Update_PartialPatch_OnlyChangesSuppliedFields()
    {
        var client = _factory.CreateClientForTenant(Guid.NewGuid());
        var created = await (await client.PostAsJsonAsync("/api/statement-templates",
            Sample(status: StatementTemplateStatus.Draft))).Content
            .ReadFromJsonAsync<StatementTemplateResponse>();

        var put = await client.PutAsJsonAsync(
            $"/api/statement-templates/{created!.Id}",
            new UpdateStatementTemplateRequest { AccentColor = "#000000" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var body = await put.Content.ReadFromJsonAsync<StatementTemplateResponse>();
        Assert.Equal("#000000", body!.AccentColor);
        Assert.Equal(created.Name, body.Name);
        Assert.Equal(created.StatementNumberPrefix, body.StatementNumberPrefix);
    }

    [Fact]
    public async Task Update_Retired_400()
    {
        var client = _factory.CreateClientForTenant(Guid.NewGuid());
        var created = await (await client.PostAsJsonAsync("/api/statement-templates",
            Sample(status: StatementTemplateStatus.Active))).Content
            .ReadFromJsonAsync<StatementTemplateResponse>();
        await client.PostAsync($"/api/statement-templates/{created!.Id}/retire", content: null);

        var put = await client.PutAsJsonAsync(
            $"/api/statement-templates/{created.Id}",
            new UpdateStatementTemplateRequest { Name = "New name" });
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task MakeDefault_OnRetired_400()
    {
        // RetiredStatementTemplateCannotBeDefaultException derives
        // from InvalidOperationException (not the conflict subtype),
        // so the controller maps it to 400.
        var client = _factory.CreateClientForTenant(Guid.NewGuid());
        var created = await (await client.PostAsJsonAsync("/api/statement-templates",
            Sample(status: StatementTemplateStatus.Active))).Content
            .ReadFromJsonAsync<StatementTemplateResponse>();
        await client.PostAsync($"/api/statement-templates/{created!.Id}/retire", content: null);

        var promote = await client.PostAsync(
            $"/api/statement-templates/{created.Id}/make-default", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, promote.StatusCode);
    }

    [Fact]
    public async Task Create_BadAccentColor_400()
    {
        var client = _factory.CreateClientForTenant(Guid.NewGuid());
        var bad = Sample();
        bad.AccentColor = "not-a-color";
        var resp = await client.PostAsJsonAsync("/api/statement-templates", bad);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
