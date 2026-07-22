using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class CaseEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public CaseEndpointTests(LiensApiFactory factory) => _factory = factory;

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

    [Fact]
    public async Task CreateCase_defaults_case_number_from_current_year_and_next_sequence()
    {
        var yearPrefix = DateTime.UtcNow.ToString("yy");

        var first = await _client.PostAsJsonAsync("/api/liens/cases", new
        {
            caseNumber = "",
            clientFirstName = "Case",
            clientLastName = "One",
        });

        first.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await first.Content.ReadAsStringAsync()}");
        var firstBody = await first.Content.ReadFromJsonAsync<CaseResponseBody>();
        firstBody!.CaseNumber.Should().Be($"{yearPrefix}-000001");

        var second = await _client.PostAsJsonAsync("/api/liens/cases", new
        {
            caseNumber = "",
            clientFirstName = "Case",
            clientLastName = "Two",
        });

        second.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await second.Content.ReadAsStringAsync()}");
        var secondBody = await second.Content.ReadFromJsonAsync<CaseResponseBody>();
        secondBody!.CaseNumber.Should().Be($"{yearPrefix}-000002");
    }

    [Fact]
    public async Task GetDashboardTotalCaseReport_returns_only_current_tenant_cases()
    {
        var otherTenantId = Guid.CreateVersion7();
        var otherCaseNumber = $"CASE-OTHER-{Guid.CreateVersion7():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Cases.Add(Case.Create(
                otherTenantId,
                Guid.CreateVersion7(),
                otherCaseNumber,
                "Other",
                "Tenant",
                Guid.CreateVersion7()));
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync(
            "/api/liens/cases/dashboard/total-case-report-export");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var rows = document!.RootElement.EnumerateArray().ToList();

        rows.Should().Contain(row =>
            row.GetProperty("caseNumber").GetString() == "CASE-TEST-001");
        rows.Should().NotContain(row =>
            row.GetProperty("caseNumber").GetString() == otherCaseNumber);
    }

    [Fact]
    public async Task GetCase_returns_summary_metadata_as_typed_fields()
    {
        var caseEntity = Case.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            $"CASE-DETAIL-{Guid.CreateVersion7():N}",
            "Marcus",
            "Delgado",
            SeedHelper.UserId,
            clientDob: new DateOnly(1995, 1, 10),
            dateOfIncident: new DateOnly(2025, 3, 14),
            notes: "accidentType=Motor Vehicle; accidentState=AZ; lawFirm=Aaron Law Group; caseManager=Aaron Law Group");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/liens/cases/{caseEntity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var body = document!.RootElement;

        body.GetProperty("accidentType").GetString().Should().Be("Motor Vehicle");
        body.GetProperty("stateOfIncident").GetString().Should().Be("AZ");
        body.GetProperty("lawFirm").GetString().Should().Be("Aaron Law Group");
        body.GetProperty("caseManager").GetString().Should().Be("Aaron Law Group");
    }

    private sealed class CaseResponseBody
    {
        public string CaseNumber { get; init; } = string.Empty;
    }
}
