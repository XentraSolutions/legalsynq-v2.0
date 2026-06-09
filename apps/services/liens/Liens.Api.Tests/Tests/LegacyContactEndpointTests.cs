using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Liens.Api.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class LegacyContactEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public LegacyContactEndpointTests(LiensApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedHelper.SeedAsync(scope.ServiceProvider);

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",
                JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── GET list routes ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetLawFirms_returns200()
    {
        var resp = await _client.GetAsync("/contact/lawfirm/");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetLawFirm_by_id_returns200()
    {
        var resp = await _client.GetAsync($"/contact/lawfirm/{SeedHelper.LawFirmId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMedicalProviders_returns200()
    {
        var resp = await _client.GetAsync("/contact/medical-provider/");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetFundingCompanies_returns200()
    {
        var resp = await _client.GetAsync("/contact/funding-company/");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetLeads_returns200()
    {
        var resp = await _client.GetAsync("/contact/leads/");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetLawFirmRole_returns200()
    {
        var resp = await _client.GetAsync("/contact/lawfirm/role/");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── POST v3 search routes ─────────────────────────────────────────────────

    [Fact]
    public async Task SearchLawFirmsV3_returns200()
    {
        var resp = await _client.PostAsJsonAsync("/contact/lawfirm/v3",
            new { page = 1, limit = 10 });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SearchMedicalProvidersV3_returns200()
    {
        var resp = await _client.PostAsJsonAsync("/contact/medical-provider/v3",
            new { page = 1, limit = 10 });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SearchMedicalProvidersV3_with_orgId_returns200()
    {
        var resp = await _client.PostAsJsonAsync(
            $"/contact/medical-provider/v3/{SeedHelper.OrgId}",
            new { page = 1, limit = 10 });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SearchFundingCompaniesV3_returns200()
    {
        var resp = await _client.PostAsJsonAsync("/contact/funding-company/v3",
            new { page = 1, limit = 10 });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SearchLeadsV3_returns200()
    {
        var resp = await _client.PostAsJsonAsync("/contact/leads/v3",
            new { page = 1, limit = 10 });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── CREATE / UPDATE / DELETE ──────────────────────────────────────────────

    [Fact]
    public async Task CreateContact_returns201()
    {
        var resp = await _client.PostAsJsonAsync("/contact/create", new
        {
            contactType  = "LawFirm",
            firstName    = "New",
            lastName     = "Firm",
            organization = "New Law Firm LLC",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdateContact_returns200()
    {
        var resp = await _client.PostAsJsonAsync("/contact/update", new
        {
            id           = SeedHelper.LawFirmId,
            contactType  = "LawFirm",
            firstName    = "Smith",
            lastName     = "Updated",
            organization = "Smith & Associates Updated LLP",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteContact_returns200()
    {
        // Create a throwaway contact to delete.
        var createResp = await _client.PostAsJsonAsync("/contact/create", new
        {
            contactType = "Lead",
            firstName   = "Delete",
            lastName    = "Me",
        });
        var body = await createResp.Content.ReadFromJsonAsync<IdResponse>();
        body.Should().NotBeNull();

        var deleteResp = await _client.DeleteAsync($"/contact/delete/{body!.Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── CSV exports ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateContactCsv_returns200()
    {
        var resp = await _client.PostAsJsonAsync("/contact/generate-csv", new
        {
            contactType = "LawFirm",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GenerateFacilityCsv_returns200()
    {
        var resp = await _client.PostAsJsonAsync("/contact/generate-facility-csv", new
        {
            tenantId = SeedHelper.TenantId,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Auth enforcement ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetLawFirms_without_auth_returns_401()
    {
        var anonClient = _factory.CreateClient();
        var resp = await anonClient.GetAsync("/contact/lawfirm/");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateContact_without_auth_returns_401()
    {
        var anonClient = _factory.CreateClient();
        var resp = await anonClient.PostAsJsonAsync("/contact/create",
            new { contactType = "LawFirm", firstName = "X", lastName = "Y" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Helper DTO for parsing created entity ID.
    private sealed record IdResponse(Guid Id);
}
