using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
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

    [Fact]
    public async Task ContactType_includes_funding_company_and_lead()
    {
        var resp = await _client.GetAsync("/lookup/contact/type");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        var codes = payload.AsArray()
            .Select(item => item?["code"]?.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

        codes.Should().Contain("FundingCompany");
        codes.Should().Contain("Lead");
    }

    [Fact]
    public async Task ProcedureCodes_includes_manual_medical_codes()
    {
        var resp = await _client.GetAsync("/lookup/medical/procedure/codes");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        payload["isSuccess"]!.GetValue<bool>().Should().BeTrue();
        payload["data"]!.AsArray()
            .Any(item =>
                item?["code"]?.GetValue<string>() == "MANUAL-001" &&
                item?["description"]?.GetValue<string>() == "Manual Procedure (MANUAL-001)")
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task ProcedureCost_returns404_when_external_cost_source_is_not_configured()
    {
        var resp = await _client.GetAsync("/lookup/medical/procedure/costs/99213");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        payload["isSuccess"]!.GetValue<bool>().Should().BeFalse();
        payload["message"]!.GetValue<string>().Should().Be("Unable to get procedure cost.");
    }

    [Fact]
    public async Task ProcedureCost_returns_manual_medical_cost_before_lookup_value()
    {
        var resp = await _client.GetAsync("/lookup/medical/procedure/costs/MANUAL-001");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        payload["isSuccess"]!.GetValue<bool>().Should().BeTrue();
        payload["message"]!.GetValue<string>().Should().Be("Retrieved from manual medical codes.");

        var item = payload["data"]!.AsArray().Should().ContainSingle().Subject!;
        item["code"]!.GetValue<string>().Should().Be("MANUAL-001");
        item["description"]!.GetValue<string>().Should().Be("Manual Procedure");
        item["facilityType"]!.GetValue<string>().Should().Be("ASC");
        item["cost"]!.GetValue<string>().Should().Be("100");
        item["copay"]!.GetValue<string>().Should().Be("10");
        item["facilityTotal"]!.GetValue<string>().Should().Be("70");
        item["physicianTotal"]!.GetValue<string>().Should().Be("30");
        item["total"]!.GetValue<string>().Should().Be("110");
    }

    [Fact] public Task LookupAll_returns200()         => GetOk("/lookup/all");
    [Fact] public Task LookupContact_returns200()     => GetOk("/lookup/contact");

    [Fact]
    public async Task LookupContactLawfirm_returns200()
        => await GetOk("/lookup/contact/lawfirm");

    [Fact]
    public async Task LookupContactLawfirm_excludes_case_manager_subtype_contacts()
    {
        Guid lawFirmCaseManagerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseManagerContact = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                ContactType.LawFirm,
                "Case",
                "Manager",
                SeedHelper.UserId,
                lawFirmId: SeedHelper.LawFirmId,
                contactSubtype: ContactSubtype.LawFirmCaseManager,
                organization: "Smith & Associates LLP");
            lawFirmCaseManagerId = caseManagerContact.Id;
            db.Contacts.Add(caseManagerContact);
            await db.SaveChangesAsync();
        }

        var resp = await _client.GetAsync("/lookup/contact/lawfirm");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!.AsArray();
        payload.Any(item => item?["id"]?.GetValue<Guid>() == lawFirmCaseManagerId).Should().BeFalse();
    }

    [Fact]
    public async Task LookupContactMedicalProvider_returns200()
        => await GetOk("/lookup/contact/medical-provider");

    [Fact]
    public async Task LookupContactFundingCompany_returns200()
        => await GetOk("/lookup/contact/funding-company");

    [Fact]
    public async Task LookupContactFundingCompany_includes_new_funding_company_contact_type()
    {
        var fundingCompanyId = Guid.CreateVersion7();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var contact = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                ContactType.FundingCompany,
                "Future",
                "Capital",
                SeedHelper.UserId,
                organization: "Future Capital");
            typeof(Contact).GetProperty("Id")!.SetValue(contact, fundingCompanyId);
            db.Contacts.Add(contact);
            await db.SaveChangesAsync();
        }

        var resp = await _client.GetAsync("/lookup/contact/funding-company");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!.AsArray();
        payload.Any(item => item?["id"]?.GetValue<Guid>() == fundingCompanyId).Should().BeTrue();
    }

    [Fact]
    public async Task LookupContactLawfirmRole_returns200()
        => await GetOk("/lookup/contact/lawfirm/role");

    [Fact]
    public async Task LookupContactLawfirmRole_returns_law_firm_subtype_options()
    {
        var resp = await _client.GetAsync("/lookup/contact/lawfirm/role");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        var roles = payload.AsArray()
            .Select(item => (
                Code: item?["code"]?.GetValue<string>(),
                Name: item?["name"]?.GetValue<string>()))
            .ToList();

        roles.Should().Contain((Liens.Domain.Enums.ContactSubtype.LawFirmCaseManager, "Case Manager"));
        roles.Should().Contain((Liens.Domain.Enums.ContactSubtype.LawFirmAttorney, "Attorney"));
        roles.Should().Contain((Liens.Domain.Enums.ContactSubtype.LawFirmOther, "Other"));
    }

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
