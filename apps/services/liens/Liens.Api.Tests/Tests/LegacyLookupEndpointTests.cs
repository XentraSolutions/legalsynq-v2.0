using System.Net;
using System.Net.Http.Headers;
using Liens.Api.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class LegacyLookupEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public LegacyLookupEndpointTests(LiensApiFactory factory) => _factory = factory;

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

    // ── Happy-path tests ──────────────────────────────────────────────────────

    [Fact] public Task States_returns200()            => GetOk("/lookup/states");
    [Fact] public Task DocumentType_returns200()      => GetOk("/lookup/document/type");
    [Fact] public Task AccidentType_returns200()      => GetOk("/lookup/accident/type");
    [Fact] public Task LiensStatus_returns200()       => GetOk("/lookup/liens/status");
    [Fact] public Task CaseStatus_returns200()        => GetOk("/lookup/case/status");
    [Fact] public Task MedicalStatus_returns200()     => GetOk("/lookup/medical/status");
    [Fact] public Task SettlementStatus_returns200()  => GetOk("/lookup/settlement/status");
    [Fact] public Task SettlementType_returns200()    => GetOk("/lookup/settlement/type");
    [Fact] public Task CurrentAttributes_returns200() => GetOk("/lookup/current-attributes");
    [Fact] public Task TaskStatus_returns200()        => GetOk("/lookup/task/status");
    [Fact] public Task TaskPriority_returns200()      => GetOk("/lookup/task/priority");
    [Fact] public Task ContactType_returns200()       => GetOk("/lookup/contact/type");
    [Fact] public Task ProcedureCodes_returns200()    => GetOk("/lookup/medical/procedure/codes");

    [Fact]
    public async Task ProcedureCost_returns200_for_seeded_code()
        => await GetOk("/lookup/medical/procedure/costs/99213");

    [Fact] public Task LookupAll_returns200()         => GetOk("/lookup/all");
    [Fact] public Task LookupContact_returns200()     => GetOk("/lookup/contact");

    [Fact]
    public async Task LookupContactLawfirm_returns200()
        => await GetOk("/lookup/contact/lawfirm");

    [Fact]
    public async Task LookupContactMedicalProvider_returns200()
        => await GetOk("/lookup/contact/medical-provider");

    [Fact]
    public async Task LookupContactFundingCompany_returns200()
        => await GetOk("/lookup/contact/funding-company");

    [Fact]
    public async Task LookupContactLawfirmRole_returns200()
        => await GetOk("/lookup/contact/lawfirm/role");

    [Fact]
    public async Task LookupBackupCaseManager_returns200()
        => await GetOk($"/lookup/backupcasemanager/{SeedHelper.LawFirmId}");

    [Fact]
    public async Task LookupCaseManager_returns200()
        => await GetOk($"/lookup/casemanager/{SeedHelper.LawFirmId}");

    [Fact]
    public async Task LookupContactsByRoleId_returns200()
        => await GetOk($"/lookup/contacts/{SeedHelper.LawFirmId}");

    [Fact] public Task LookupUserList_returns200()    => GetOk("/lookup/user-list");
    [Fact] public Task LookupFacility_returns200()    => GetOk("/lookup/facility");

    [Fact]
    public async Task LookupContactPerson_returns200()
        => await GetOk($"/lookup/contactperson/{SeedHelper.FacilityId}");

    // ── Auth enforcement ──────────────────────────────────────────────────────

    [Fact]
    public async Task States_without_auth_returns_401()
    {
        var anonClient = _factory.CreateClient();
        var resp = await anonClient.GetAsync("/lookup/states");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LookupAll_without_auth_returns_401()
    {
        var anonClient = _factory.CreateClient();
        var resp = await anonClient.GetAsync("/lookup/all");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task GetOk(string path)
    {
        var resp = await _client.GetAsync(path);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"GET {path} should return 200");
    }
}
