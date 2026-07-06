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
    public async Task GetMedicalFacilities_returns200()
    {
        var resp = await _client.GetAsync("/contact/medical-facility/");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetModernMedicalFacilities_returns200()
    {
        var resp = await _client.GetAsync("/api/liens/contacts/medical-facilities");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetModernContactsList_returns200()
    {
        var resp = await _client.GetAsync("/api/liens/contacts?pageSize=100");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetModernContactsList_filters_law_firm_subcontacts_by_query()
    {
        var attorneyResp = await _client.PostAsJsonAsync("/api/liens/contacts", new
        {
            contactType = "LawFirm",
            contactSubtype = "Attorney",
            lawFirmId = SeedHelper.LawFirmId,
            firstName = "Avery",
            lastName = "Attorney",
        });
        attorneyResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var caseManagerResp = await _client.PostAsJsonAsync("/api/liens/contacts", new
        {
            contactType = "LawFirm",
            contactSubtype = "CaseManager",
            lawFirmId = SeedHelper.LawFirmId,
            firstName = "Casey",
            lastName = "Manager",
        });
        caseManagerResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var resp = await _client.GetAsync($"/api/liens/contacts?LawFirmId={SeedHelper.LawFirmId}&Type=Attorney&pageSize=100");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<PaginatedContactResponseDto>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        body.Items.Should().ContainSingle(x =>
            x.LawFirmId == SeedHelper.LawFirmId &&
            x.ContactSubtype == "Attorney" &&
            x.FirstName == "Avery" &&
            x.LastName == "Attorney");
        body.Items.Should().NotContain(x =>
            x.LawFirmId == SeedHelper.LawFirmId &&
            x.ContactSubtype == "CaseManager" &&
            x.FirstName == "Casey" &&
            x.LastName == "Manager");
    }

    [Fact]
    public async Task GetModernContactsList_filters_facility_subcontacts_by_query()
    {
        var facilityResp = await _client.PostAsJsonAsync("/api/liens/contacts", new
        {
            contactType = "Facility",
            contactSubtype = "FacilityContactPerson",
            facilityId = SeedHelper.FacilityId,
            firstName = "Taylor",
            lastName = "Staff",
        });
        facilityResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var unrelatedResp = await _client.PostAsJsonAsync("/api/liens/contacts", new
        {
            contactType = "LawFirm",
            contactSubtype = "Attorney",
            lawFirmId = SeedHelper.LawFirmId,
            firstName = "Robin",
            lastName = "Counsel",
        });
        unrelatedResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var resp = await _client.GetAsync($"/api/liens/contacts?facilityId={SeedHelper.FacilityId}&Type=FacilityContactPerson&pageSize=100");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<PaginatedContactResponseDto>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        body.Items.Should().Contain(x =>
            x.FacilityId == SeedHelper.FacilityId &&
            x.ContactSubtype == "FacilityContactPerson" &&
            x.FirstName == "Taylor" &&
            x.LastName == "Staff");
        body.Items.Should().NotContain(x =>
            x.FirstName == "Robin" &&
            x.LastName == "Counsel");
    }

    [Fact]
    public async Task GetModernContactsList_filters_facility_subcontacts_by_parent_facility_contact_id()
    {
        var facilityResp = await _client.PostAsJsonAsync("/api/liens/contacts", new
        {
            contactType = "Facility",
            contactSubtype = "FacilityContactPerson",
            facilityId = SeedHelper.MedicalFacilityContactId,
            firstName = "Jordan",
            lastName = "Staff",
        });
        facilityResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var resp = await _client.GetAsync($"/api/liens/contacts?facilityId={SeedHelper.MedicalFacilityContactId}&type=FacilityContactPerson&pageSize=100");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<PaginatedContactResponseDto>();
        body.Should().NotBeNull();
        body!.Items.Should().Contain(x =>
            x.ContactSubtype == "FacilityContactPerson" &&
            x.FirstName == "Jordan" &&
            x.LastName == "Staff");
    }

    [Fact]
    public async Task GetModernContactsList_accepts_facility_contact_person_in_contact_type_query()
    {
        var facilityResp = await _client.PostAsJsonAsync("/api/liens/contacts", new
        {
            contactType = "Facility",
            contactSubtype = "FacilityContactPerson",
            facilityId = SeedHelper.MedicalFacilityContactId,
            firstName = "Morgan",
            lastName = "Alias",
        });
        facilityResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var resp = await _client.GetAsync($"/api/liens/contacts?facilityId={SeedHelper.MedicalFacilityContactId}&contactType=FacilityContactPerson&pageSize=100");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<PaginatedContactResponseDto>();
        body.Should().NotBeNull();
        body!.Items.Should().Contain(x =>
            x.ContactSubtype == "FacilityContactPerson" &&
            x.FirstName == "Morgan" &&
            x.LastName == "Alias");
    }

    [Fact]
    public async Task GetModernContactsList_accepts_parent_facility_type_query()
    {
        var parentFacilityResp = await _client.PostAsJsonAsync("/api/liens/contacts", new
        {
            contactType = "Facility",
            facilityId = SeedHelper.FacilityId,
            firstName = "Parent",
            lastName = "Facility",
        });
        parentFacilityResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var staffResp = await _client.PostAsJsonAsync("/api/liens/contacts", new
        {
            contactType = "Facility",
            contactSubtype = "FacilityContactPerson",
            facilityId = SeedHelper.FacilityId,
            firstName = "Nested",
            lastName = "Staff",
        });
        staffResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var resp = await _client.GetAsync($"/api/liens/contacts?facilityId={SeedHelper.FacilityId}&Type=Facility&pageSize=100");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<PaginatedContactResponseDto>();
        body.Should().NotBeNull();
        body!.Items.Should().ContainSingle(x =>
            x.ContactSubtype == null &&
            x.FirstName == "Parent" &&
            x.LastName == "Facility");
        body.Items.Should().NotContain(x =>
            x.ContactSubtype == "FacilityContactPerson" &&
            x.FirstName == "Nested" &&
            x.LastName == "Staff");
    }

    [Fact]
    public async Task GetFundingCompanies_returns200()
    {
        var resp = await _client.GetAsync("/contact/funding-company/");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetModernFundingCompanies_returns200()
    {
        var resp = await _client.GetAsync("/api/liens/contacts/funding-companies");
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
    public async Task SearchMedicalFacilitiesV3_returns200()
    {
        var resp = await _client.PostAsJsonAsync("/contact/medical-facility/v3",
            new { page = 1, limit = 10 });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SearchModernMedicalFacilities_returns200()
    {
        var resp = await _client.PostAsJsonAsync("/api/liens/contacts/medical-facilities/search",
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
    public async Task SearchModernFundingCompanies_returns200()
    {
        var resp = await _client.PostAsJsonAsync("/api/liens/contacts/funding-companies/search",
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
            contactSubtype = "Attorney",
            lawFirmId    = SeedHelper.LawFirmId,
            firstName    = "New",
            lastName     = "Firm",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await resp.Content.ReadFromJsonAsync<ContactResponseDto>();
        body.Should().NotBeNull();
        body!.LawFirmId.Should().Be(SeedHelper.LawFirmId);
        body.ContactSubtype.Should().Be("Attorney");
        body.Organization.Should().Contain("Smith & Associates");
    }

    [Fact]
    public async Task CreateFundingCompanyContact_returns201()
    {
        var resp = await _client.PostAsJsonAsync("/api/liens/contacts", new
        {
            contactType  = "FundingCompany",
            firstName    = "Capital",
            lastName     = "Partner",
            organization = "Capital Partner Funding LLC",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateMedicalFacilityContact_returns201()
    {
        var resp = await _client.PostAsJsonAsync("/api/liens/contacts", new
        {
            contactType  = "MedicalFacility",
            firstName    = "Facility",
            lastName     = "Coordinator",
            organization = "Northside Medical Facility",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateFacilityContactPerson_with_standalone_facility_contact_id_links_and_returns201()
    {
        var resp = await _client.PostAsJsonAsync("/api/liens/contacts", new
        {
            contactType = "Facility",
            contactSubtype = "FacilityContactPerson",
            facilityId = SeedHelper.MedicalFacilityContactId,
            firstName = "Legacy",
            lastName = "Staff",
            email = "legacy.staff@example.com",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await resp.Content.ReadFromJsonAsync<ContactResponseDto>();
        created.Should().NotBeNull();
        created!.FacilityId.Should().NotBeNull();

        var parentResp = await _client.GetAsync($"/api/liens/contacts/{SeedHelper.MedicalFacilityContactId}");
        parentResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var parent = await parentResp.Content.ReadFromJsonAsync<ContactResponseDto>();
        parent.Should().NotBeNull();
        parent!.FacilityId.Should().Be(created.FacilityId);
    }

    [Fact]
    public async Task CreateProvider_with_law_firm_subtype_returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/liens/contacts", new
        {
            contactType = "Provider",
            contactSubtype = "Attorney",
            firstName = "Invalid",
            lastName = "Subtype",
            organization = "City Medical Center",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateLawFirmSubtype_without_law_firm_id_returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/liens/contacts", new
        {
            contactType = "LawFirm",
            contactSubtype = "Attorney",
            firstName = "Missing",
            lastName = "Parent",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
    public async Task UpdateLawFirmSubtypeContact_returns200()
    {
        var createResp = await _client.PostAsJsonAsync("/api/liens/contacts", new
        {
            contactType = "LawFirm",
            contactSubtype = "Attorney",
            lawFirmId = SeedHelper.LawFirmId,
            firstName = "Alex",
            lastName = "Smith",
            title = "Associate",
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResp.Content.ReadFromJsonAsync<ContactResponseDto>();
        created.Should().NotBeNull();

        var updateResp = await _client.PutAsJsonAsync($"/api/liens/contacts/{created!.Id}", new
        {
            contactType = "LawFirm",
            contactSubtype = "CaseManager",
            lawFirmId = SeedHelper.LawFirmId,
            firstName = "Alex",
            lastName = "Smith",
            title = "Partner",
        });

        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await updateResp.Content.ReadFromJsonAsync<ContactResponseDto>();
        updated.Should().NotBeNull();
        updated!.LawFirmId.Should().Be(SeedHelper.LawFirmId);
        updated.ContactSubtype.Should().Be("CaseManager");
        updated.Title.Should().Be("Partner");
        updated.Organization.Should().Contain("Smith & Associates");
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

    [Fact]
    public async Task Deleted_contact_is_excluded_from_default_modern_listing()
    {
        var createResp = await _client.PostAsJsonAsync("/contact/create", new
        {
            contactType = "Lead",
            firstName = "Inactive",
            lastName = "Hidden",
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResp.Content.ReadFromJsonAsync<IdResponse>();
        created.Should().NotBeNull();

        var deleteResp = await _client.DeleteAsync($"/contact/delete/{created!.Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResp = await _client.GetAsync("/api/liens/contacts?pageSize=100");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await listResp.Content.ReadFromJsonAsync<PaginatedContactResponseDto>();
        body.Should().NotBeNull();
        body!.Items.Should().NotContain(x => x.Id == created.Id);
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
    private sealed record ContactResponseDto(Guid Id, Guid? LawFirmId, Guid? FacilityId, string? ContactSubtype, string? Organization, string? Title, string? FirstName, string? LastName);
    private sealed record PaginatedContactResponseDto(List<ContactResponseDto> Items, int TotalCount);
}
