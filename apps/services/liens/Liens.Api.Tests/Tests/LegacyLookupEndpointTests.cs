using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
    [Fact]
    public async Task MedicalStatus_returns_active_options()
    {
        var response = await _client.GetAsync("/lookup/medical/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        payload.AsArray().Any(item =>
            item is JsonObject option &&
            option["code"]?.GetValue<string>() == "Treating" &&
            option["name"]?.GetValue<string>() == "Treating").Should().BeTrue();
    }
    [Fact] public Task SettlementStatus_returns200()  => GetOk("/lookup/settlement/status");
    [Fact] public Task SettlementType_returns200()    => GetOk("/lookup/settlement/type");
    [Fact] public Task CurrentAttributes_returns200() => GetOk("/lookup/current-attributes");
    [Fact] public Task TaskStatus_returns200()        => GetOk("/lookup/task/status");
    [Fact] public Task TaskPriority_returns200()      => GetOk("/lookup/task/priority");
    [Fact] public Task ContactType_returns200()       => GetOk("/lookup/contact/type");

    [Fact]
    public async Task DocumentType_matches_curated_legacy_options_only()
    {
        var resp = await _client.GetAsync("/lookup/document/type");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        var items = payload.AsArray()
            .Select(item => (
                Code: item?["code"]?.GetValue<string>(),
                Name: item?["name"]?.GetValue<string>()))
            .ToList();

        items.Should().Equal(
            ("HicfaOrBill", "HICFA or Bill"),
            ("MedicalRecord", "Medical Record"),
            ("HIPPA", "HIPPA"),
            ("PoliceReport", "Police Report"),
            ("Other", "Other"),
            ("LienAgreement", "Lien Agreement"),
            ("Check", "Check"),
            ("AddTestQA", "Add Test QA"),
            ("BillsAndRecords", "Bills & Records"),
            ("BillsAndRecs", "Bills & Recs"),
            ("PayoffStatement", "Payoff Quote"));

        payload.AsArray()
            .Single(item => item?["code"]?.GetValue<string>() == "PayoffStatement")!["id"]!
            .GetValue<Guid>()
            .Should().Be(SeedHelper.PayoffStatementDocumentTypeId);
    }

    [Fact]
    public async Task AccidentType_matches_legacy_baseline_options_only()
    {
        var resp = await _client.GetAsync("/lookup/accident/type");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        var items = payload.AsArray()
            .Select(item => (
                Code: item?["code"]?.GetValue<string>(),
                Name: item?["name"]?.GetValue<string>()))
            .ToList();

        items.Should().Equal(
            ("DogBite", "Dog Bite"),
            ("MotorVehicleAccident", "Motor Vehicle Accident"),
            ("Other", "Other"),
            ("SlipAndFall", "Slip and Fall"),
            ("WorkersCompensation", "Workers Compensation"),
            ("MedicalMalpractice", "Medical Malpractice"));
    }

    [Fact]
    public async Task LiensStatus_matches_legacy_baseline_options_only()
    {
        var resp = await _client.GetAsync("/lookup/liens/status");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        var items = payload.AsArray()
            .Select(item => (
                Code: item?["code"]?.GetValue<string>(),
                Name: item?["name"]?.GetValue<string>()))
            .ToList();

        items.Should().Equal(
            ("Open", "Open"),
            ("Closed", "Closed"),
            ("Rejected", "Rejected"));
    }

    [Fact]
    public async Task LookupAll_includes_curated_legacy_accident_types()
    {
        var resp = await _client.GetAsync("/lookup/all");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        var items = payload["AccidentType"]!.AsArray()
            .Select(item => (
                Code: item?["code"]?.GetValue<string>(),
                Name: item?["name"]?.GetValue<string>()))
            .ToList();

        items.Should().Equal(
            ("DogBite", "Dog Bite"),
            ("MotorVehicleAccident", "Motor Vehicle Accident"),
            ("Other", "Other"),
            ("SlipAndFall", "Slip and Fall"),
            ("WorkersCompensation", "Workers Compensation"),
            ("MedicalMalpractice", "Medical Malpractice"));
    }

    [Fact]
    public async Task LookupAll_orders_contact_types_by_configured_sort_order()
    {
        var resp = await _client.GetAsync("/lookup/all");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        var items = payload[LookupCategory.ContactType]!.AsArray()
            .Select(item => (
                Code: item?["code"]?.GetValue<string>(),
                SortOrder: item?["sortOrder"]?.GetValue<int>()))
            .ToList();

        items.Should().Equal(
            (ContactType.LawFirm, 1),
            (ContactType.MedicalFacility, 2),
            (ContactType.Provider, 3),
            (ContactType.FundingCompany, 4),
            (ContactType.Lead, 5));
    }

    [Fact]
    public async Task LookupAll_includes_curated_legacy_lien_statuses()
    {
        var resp = await _client.GetAsync("/lookup/all");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        var items = payload["LienStatus"]!.AsArray()
            .Select(item => (
                Code: item?["code"]?.GetValue<string>(),
                Name: item?["name"]?.GetValue<string>()))
            .ToList();

        items.Should().Equal(
            ("Open", "Open"),
            ("Closed", "Closed"),
            ("Rejected", "Rejected"));
    }

    [Fact]
    public async Task LookupAll_includes_curated_legacy_document_types()
    {
        var resp = await _client.GetAsync("/lookup/all");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        var items = payload["DocumentCategory"]!.AsArray()
            .Select(item => (
                Code: item?["code"]?.GetValue<string>(),
                Name: item?["name"]?.GetValue<string>()))
            .ToList();

        items.Should().Equal(
            ("HicfaOrBill", "HICFA or Bill"),
            ("MedicalRecord", "Medical Record"),
            ("HIPPA", "HIPPA"),
            ("PoliceReport", "Police Report"),
            ("Other", "Other"),
            ("LienAgreement", "Lien Agreement"),
            ("Check", "Check"),
            ("AddTestQA", "Add Test QA"),
            ("BillsAndRecords", "Bills & Records"),
            ("BillsAndRecs", "Bills & Recs"),
            ("PayoffStatement", "Payoff Quote"));
    }

    [Fact]
    public async Task ModernAccidentType_category_returns_curated_legacy_options_only()
    {
        var resp = await _client.GetAsync("/api/liens/lookups/AccidentType");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        var items = payload.AsArray()
            .Select(item => (
                Code: item?["code"]?.GetValue<string>(),
                Name: item?["name"]?.GetValue<string>()))
            .ToList();

        items.Should().Equal(
            ("DogBite", "Dog Bite"),
            ("MotorVehicleAccident", "Motor Vehicle Accident"),
            ("Other", "Other"),
            ("SlipAndFall", "Slip and Fall"),
            ("WorkersCompensation", "Workers Compensation"),
            ("MedicalMalpractice", "Medical Malpractice"));
    }

    [Fact]
    public async Task ModernLienStatus_category_returns_curated_legacy_options_only()
    {
        var resp = await _client.GetAsync("/api/liens/lookups/LienStatus");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        var items = payload.AsArray()
            .Select(item => (
                Code: item?["code"]?.GetValue<string>(),
                Name: item?["name"]?.GetValue<string>()))
            .ToList();

        items.Should().Equal(
            ("Open", "Open"),
            ("Closed", "Closed"),
            ("Rejected", "Rejected"));
    }

    [Fact]
    public async Task ModernDocumentCategory_returns_curated_legacy_options_only()
    {
        var resp = await _client.GetAsync("/api/liens/lookups/DocumentCategory");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        var items = payload.AsArray()
            .Select(item => (
                Code: item?["code"]?.GetValue<string>(),
                Name: item?["name"]?.GetValue<string>()))
            .ToList();

        items.Should().Equal(
            ("HicfaOrBill", "HICFA or Bill"),
            ("MedicalRecord", "Medical Record"),
            ("HIPPA", "HIPPA"),
            ("PoliceReport", "Police Report"),
            ("Other", "Other"),
            ("LienAgreement", "Lien Agreement"),
            ("Check", "Check"),
            ("AddTestQA", "Add Test QA"),
            ("BillsAndRecords", "Bills & Records"),
            ("BillsAndRecs", "Bills & Recs"),
            ("PayoffStatement", "Payoff Quote"));
    }

    [Fact]
    public async Task CaseStatus_matches_v3_legacy_status_options_only()
    {
        var resp = await _client.GetAsync("/lookup/case/status");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        var items = payload.AsArray()
            .Select(item => (
                Code: item?["code"]?.GetValue<string>(),
                Name: item?["name"]?.GetValue<string>()))
            .ToList();

        items.Should().Equal(
            ("New", "New"),
            ("Processing", "Processing"),
            (CaseStatus.Closed, "Closed"),
            (CaseStatus.PreDemand, "Pre-demand"),
            (CaseStatus.DemandSent, "Demand Sent"),
            ("Negotiations", "Negotiations"),
            ("Litigation", "Litigation"),
            (CaseStatus.CaseSettled, "Case Settled"));
    }

    [Fact]
    public async Task ModernCaseStatus_category_returns_legacy_status_options_only()
    {
        var resp = await _client.GetAsync("/api/liens/lookups/CaseStatus");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        var items = payload.AsArray()
            .Select(item => (
                Code: item?["code"]?.GetValue<string>(),
                Name: item?["name"]?.GetValue<string>()))
            .ToList();

        items.Should().Equal(
            ("New", "New"),
            ("Processing", "Processing"),
            (CaseStatus.Closed, "Closed"),
            (CaseStatus.PreDemand, "Pre-demand"),
            (CaseStatus.DemandSent, "Demand Sent"),
            ("Negotiations", "Negotiations"),
            ("Litigation", "Litigation"),
            (CaseStatus.CaseSettled, "Case Settled"));
    }

    [Fact]
    public async Task ContactType_matches_legacy_baseline_options_only()
    {
        var resp = await _client.GetAsync("/lookup/contact/type");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        var items = payload.AsArray()
            .Select(item => (
                Code: item?["code"]?.GetValue<string>(),
                Name: item?["name"]?.GetValue<string>()))
            .ToList();

        items.Should().Equal(
            (ContactType.LawFirm, "Law Firms"),
            (ContactType.MedicalFacility, "Medical Facilities"),
            (ContactType.Provider, "Medical Providers"),
            (ContactType.FundingCompany, "Funding Companies"),
            (ContactType.Lead, "Leads"));
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
    public async Task ProcedureCodes_prefers_manual_medical_codes_that_match_medicare_codes()
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/liens/cases/manual/medical/code/create",
            new
            {
                code = "45385",
                description = "Tenant colonoscopy override",
            });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.GetAsync("/lookup/medical/procedure/codes?search=45385");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var medicalCode = payload["data"]!.AsArray()
            .Single(item => item?["code"]?.GetValue<string>() == "45385")!;
        medicalCode["description"]!
            .GetValue<string>()
            .Should()
            .Be("Tenant colonoscopy override (45385)");
    }

    [Fact]
    public async Task ProcedureCodes_includes_medicare_procedure_codes()
    {
        var resp = await _client.GetAsync("/lookup/medical/procedure/codes");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        payload["isSuccess"]!.GetValue<bool>().Should().BeTrue();
        payload["data"]!.AsArray()
            .Any(item =>
                item?["code"]?.GetValue<string>() == "45385" &&
                item?["description"]?.GetValue<string>() == "Colonoscopy, flexible; with removal by snare technique (45385)")
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task ProcedureCost_returns_empty_data_when_medicare_cost_is_not_found()
    {
        var resp = await _client.GetAsync("/lookup/medical/procedure/costs/99213");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        payload["isSuccess"]!.GetValue<bool>().Should().BeTrue();
        payload["message"]!.GetValue<string>().Should().Be("Procedure cost is not available.");
        payload["data"]!.AsArray().Should().BeEmpty();
    }

    [Fact]
    public async Task ProcedureCost_returns_medicare_cost_when_manual_code_is_not_found()
    {
        var resp = await _client.GetAsync("/lookup/medical/procedure/costs/45385");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        payload["isSuccess"]!.GetValue<bool>().Should().BeTrue();
        payload["message"]!.GetValue<string>().Should().Be("Retrieved from Medicare procedure price lookup.");

        var items = payload["data"]!.AsArray();
        items.Should().HaveCount(2);
        items.Any(item =>
            item?["facilityType"]?.GetValue<string>() == "asc" &&
            item?["cost"]?.GetValue<string>() == "703" &&
            item?["copay"]?.GetValue<string>() == "175" &&
            item?["facilityTotal"]?.GetValue<string>() == "656" &&
            item?["physicianTotal"]?.GetValue<string>() == "223" &&
            item?["total"]?.GetValue<string>() == "879")
            .Should()
            .BeTrue();
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
