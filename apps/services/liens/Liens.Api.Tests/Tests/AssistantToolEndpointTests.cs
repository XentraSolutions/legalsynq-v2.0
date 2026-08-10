using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public sealed class AssistantToolEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public AssistantToolEndpointTests(LiensApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedHelper.SeedAsync(scope.ServiceProvider);

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetLien_routes_return_latest_recorded_reduction_amount()
    {
        var routes = new[]
        {
            $"/api/assistant-tools/liens/{SeedHelper.LienId}",
            "/api/assistant-tools/liens/by-number/LIEN-TEST-001",
        };

        foreach (var route in routes)
        {
            var response = await _client.GetAsync(route);

            response.StatusCode.Should().Be(
                HttpStatusCode.OK,
                $"Body: {await response.Content.ReadAsStringAsync()}");

            var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
            document!.RootElement
                .GetProperty("lien")
                .GetProperty("reductionAmount")
                .GetDecimal()
                .Should()
                .Be(500m);
        }
    }

    [Fact]
    public async Task SearchCases_filters_law_firm_before_pagination_and_returns_full_count()
    {
        var otherOrgId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var foreignOrgId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            for (var index = 1; index <= 12; index++)
            {
                db.Cases.Add(Case.Create(
                    SeedHelper.TenantId,
                    SeedHelper.OrgId,
                    $"CASE-SMITH-{index:000}",
                    "Smith",
                    $"Client {index:000}",
                    SeedHelper.UserId));
            }

            db.Contacts.Add(Contact.Create(
                SeedHelper.TenantId,
                otherOrgId,
                ContactType.LawFirm,
                "Rival",
                "Counsel",
                SeedHelper.UserId,
                organization: "Rival Counsel LLP"));

            db.Contacts.Add(Contact.Create(
                foreignTenantId,
                foreignOrgId,
                ContactType.LawFirm,
                "Smith",
                "Foreign",
                SeedHelper.UserId,
                organization: "Smith & Associates LLP"));
            db.Cases.Add(Case.Create(
                foreignTenantId,
                foreignOrgId,
                "CASE-FOREIGN-001",
                "Foreign",
                "Client",
                SeedHelper.UserId));

            // These are created last so an unfiltered first page contains no Smith cases.
            for (var index = 1; index <= 12; index++)
            {
                db.Cases.Add(Case.Create(
                    SeedHelper.TenantId,
                    otherOrgId,
                    $"CASE-RIVAL-{index:000}",
                    "Rival",
                    $"Client {index:000}",
                    SeedHelper.UserId));
            }

            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync(
            "/api/assistant-tools/cases/search?lawFirm=smith%20%26%20ASSOCIATES&top=3");

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var root = document!.RootElement;
        root.GetProperty("totalCount").GetInt32().Should().Be(13);

        var items = root.GetProperty("cases").EnumerateArray().ToList();
        items.Should().HaveCount(3);
        items.Should().OnlyContain(item =>
            item.GetProperty("lawFirm").GetString() == "Smith & Associates LLP");
    }

    [Fact]
    public async Task SearchCases_returns_zero_for_unknown_law_firm()
    {
        var response = await _client.GetAsync(
            "/api/assistant-tools/cases/search?lawFirm=Unknown%20Firm&top=3");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var root = document!.RootElement;
        root.GetProperty("totalCount").GetInt32().Should().Be(0);
        root.GetProperty("cases").GetArrayLength().Should().Be(0);
    }
}
