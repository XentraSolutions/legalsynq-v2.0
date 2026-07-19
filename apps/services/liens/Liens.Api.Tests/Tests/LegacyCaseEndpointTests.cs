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

public class LegacyCaseEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public LegacyCaseEndpointTests(LiensApiFactory factory) => _factory = factory;

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
    public async Task GetCasesV3_accepts_comma_separated_status_codes()
    {
        var resp = await _client.PostAsJsonAsync("/api/liens/cases/v3", new
        {
            keyword = "",
            page = 1,
            limit = 20,
            sortBy = "",
            sortDirection = "",
            statusId = "PreDemand,DemandSent",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task GetCasesV3_filters_by_law_firm_accident_type_and_case_manager()
    {
        var accidentTypeId = $"ACC-{Guid.CreateVersion7():N}";
        var caseManagerId = Guid.CreateVersion7();
        var otherOrgId = Guid.CreateVersion7();
        var otherManagerId = Guid.CreateVersion7();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-FILTER-MATCH-{Guid.CreateVersion7():N}",
                "Filter",
                "Match",
                SeedHelper.UserId,
                notes: $"accidentTypeId={accidentTypeId}; caseManagerId={caseManagerId}"));

            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                otherOrgId,
                $"CASE-FILTER-OTHER-ORG-{Guid.CreateVersion7():N}",
                "Filter",
                "OtherOrg",
                SeedHelper.UserId,
                notes: $"accidentTypeId={accidentTypeId}; caseManagerId={caseManagerId}"));

            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-FILTER-OTHER-META-{Guid.CreateVersion7():N}",
                "Filter",
                "OtherMeta",
                SeedHelper.UserId,
                notes: $"accidentTypeId={accidentTypeId}-OTHER; caseManagerId={otherManagerId}"));

            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/v3", new
        {
            keyword = "",
            page = 1,
            limit = 20,
            sortBy = "",
            sortDirection = "",
            statusId = "PreDemand,DemandSent",
            lawFirmId = SeedHelper.OrgId.ToString(),
            accidentTypeId,
            caseManagerId = caseManagerId.ToString(),
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetCasesV3_returns_law_firm_case_manager_and_accident_type_display_values()
    {
        var caseManagerId = Guid.CreateVersion7();
        var caseNumber = $"CASE-V3-DISPLAY-{Guid.CreateVersion7():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var caseManager = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                ContactType.CaseManager,
                "John",
                "Doe",
                SeedHelper.UserId);
            typeof(Contact).GetProperty(nameof(Contact.Id))!.SetValue(caseManager, caseManagerId);

            db.Contacts.Add(caseManager);
            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                caseNumber,
                "Display",
                "Match",
                SeedHelper.UserId,
                notes: $"lawFirmId={SeedHelper.LawFirmId}; accidentTypeId=MVA; accidentType=Motor Vehicle Accident; caseManagerId={caseManagerId}"));

            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/v3", new
        {
            keyword = caseNumber,
            page = 1,
            limit = 20,
            sortBy = "",
            sortDirection = "",
            statusId = "PreDemand,DemandSent",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        var item = doc!.RootElement.GetProperty("data").EnumerateArray().Single();

        item.GetProperty("lawFirmId").GetString().Should().Be(SeedHelper.LawFirmId.ToString());
        item.GetProperty("lawFirm").GetString().Should().Be("Smith & Associates LLP");
        item.GetProperty("caseManagerId").GetString().Should().Be(caseManagerId.ToString());
        item.GetProperty("caseManager").GetString().Should().Be("John Doe");
        item.GetProperty("accidentTypeId").GetString().Should().Be("MVA");
        item.GetProperty("accidentType").GetString().Should().Be("Motor Vehicle Accident");
    }

    [Fact]
    public async Task GetCasesV3_returns_law_firm_subcontact_case_manager_display_value()
    {
        var caseManagerId = Guid.CreateVersion7();
        var caseNumber = $"CASE-V3-LF-CM-{Guid.CreateVersion7():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var caseManager = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                ContactType.LawFirm,
                "Jamie",
                "Manager",
                SeedHelper.UserId,
                lawFirmId: SeedHelper.LawFirmId,
                contactSubtype: ContactSubtype.LawFirmCaseManager,
                organization: "Smith & Associates LLP");
            typeof(Contact).GetProperty(nameof(Contact.Id))!.SetValue(caseManager, caseManagerId);

            db.Contacts.Add(caseManager);
            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                caseNumber,
                "Display",
                "Subcontact",
                SeedHelper.UserId,
                notes: $"lawFirmId={SeedHelper.LawFirmId}; accidentTypeId=MVA; accidentType=Motor Vehicle Accident; caseManagerId={caseManagerId}"));

            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/v3", new
        {
            keyword = caseNumber,
            page = 1,
            limit = 20,
            sortBy = "",
            sortDirection = "",
            statusId = "PreDemand,DemandSent",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        var item = doc!.RootElement.GetProperty("data").EnumerateArray().Single();

        item.GetProperty("caseManagerId").GetString().Should().Be(caseManagerId.ToString());
        item.GetProperty("caseManager").GetString().Should().Be("Jamie Manager");
    }

    [Fact]
    public async Task GetLawFirmV3_returns_cases_when_filtered_by_law_firm_contact_id()
    {
        var resp = await _client.PostAsJsonAsync("/api/liens/cases/law/v3", new
        {
            lawFirmId = SeedHelper.LawFirmId,
            page = 1,
            limit = 10,
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        body.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Should()
            .Contain(SeedHelper.CaseId);
    }

    [Fact]
    public async Task GetMedicalV3_returns_cases_when_linked_by_medical_provider_contact_id()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LMFI-MED-{Guid.CreateVersion7():N}",
                "LegacyMedicalFacilityInfo",
                "Legacy medical facility information",
                "system",
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                lienId: SeedHelper.LienId,
                notes: $"medicalProviderId={SeedHelper.MedicalProviderId}"));
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/medical/v3", new
        {
            medicalId = SeedHelper.MedicalProviderId,
            page = 1,
            limit = 10,
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        body.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Should()
            .Contain(SeedHelper.CaseId);
    }

    [Fact]
    public async Task GetMedicalFacilityV3_returns_cases_when_filtered_by_medical_facility_contact_id()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LMFI-FAC-{Guid.CreateVersion7():N}",
                "LegacyMedicalFacilityInfo",
                "Legacy medical facility information",
                "system",
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                lienId: SeedHelper.LienId,
                notes: $"facilityId={SeedHelper.MedicalFacilityContactId}"));
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/medical/facility/v3", new
        {
            facilityId = SeedHelper.MedicalFacilityContactId,
            page = 1,
            limit = 10,
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        body.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Should()
            .Contain(SeedHelper.CaseId);
    }

    [Fact]
    public async Task GetFundingV3_returns_cases_when_linked_by_funding_company_contact_id()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Liens.Add(Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-FUND-{Guid.CreateVersion7():N}",
                "MedicalLien",
                1250m,
                SeedHelper.UserId,
                externalReference: SeedHelper.FundingCompanyId.ToString(),
                caseId: SeedHelper.CaseId));
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/funding/v3", new
        {
            fundingCompanyId = SeedHelper.FundingCompanyId,
            page = 1,
            limit = 10,
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        body.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Should()
            .Contain(SeedHelper.CaseId);
    }

    [Fact]
    public async Task GetLeadsV3_returns_cases_when_linked_by_lead_contact_id()
    {
        Guid leadCaseId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-LEAD-{Guid.CreateVersion7():N}",
                "Lead",
                "Matched",
                SeedHelper.UserId,
                notes: $"leadId={SeedHelper.LeadContactId}");
            leadCaseId = caseEntity.Id;
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/leads/v3", new
        {
            leadId = SeedHelper.LeadContactId,
            page = 1,
            limit = 10,
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        body.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Should()
            .Contain(leadCaseId);
    }

    [Fact]
    public async Task DetailsUpdate_persists_extended_tracking_flags_and_get_case_by_id_returns_them()
    {
        var patchResp = await _client.PatchAsJsonAsync("/api/liens/cases/details-update", new
        {
            caseId = SeedHelper.CaseId,
            currentStatus = "PreDemand",
            currentMedicalStatus = "Treating",
            caseType = "Motor Vehicle Accident",
            stateOfIncident = "CA",
            trackingFollowUp = "07/16/2026",
            dateOfLoss = "06/15/2024",
            leadId = SeedHelper.LeadContactId.ToString(),
            shareCase = "true",
            minorComp = "false",
            caseDropped = "false",
            childSupportLiens = "true",
            isUccFiled = "false",
        });

        patchResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await patchResp.Content.ReadAsStringAsync()}");

        var getResp = await _client.GetAsync($"/api/liens/cases/{SeedHelper.CaseId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await getResp.Content.ReadAsStringAsync()}");

        var body = await getResp.Content.ReadFromJsonAsync<JsonDocument>();
        body.Should().NotBeNull();

        var root = body!.RootElement;
        root.GetProperty("shareCase").GetString().Should().Be("Yes");
        root.GetProperty("minorComp").GetString().Should().Be("No");
        root.GetProperty("caseDropped").GetString().Should().Be("No");
        root.GetProperty("childSupportLiens").GetString().Should().Be("Yes");
        root.GetProperty("isUccFiled").GetString().Should().Be("No");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var caseEntity = await db.Cases.FindAsync(SeedHelper.CaseId);
        caseEntity.Should().NotBeNull();
        caseEntity!.Notes.Should().Contain("shareCase=Yes");
        caseEntity.Notes.Should().Contain("minorComp=No");
        caseEntity.Notes.Should().Contain("caseDropped=No");
        caseEntity.Notes.Should().Contain("childSupportLiens=Yes");
        caseEntity.Notes.Should().Contain("isUccFiled=No");
    }

    [Theory]
    [InlineData("Litigation(Pending)")]
    [InlineData("Litigation(Open)")]
    [InlineData("Litigation(Closed)")]
    public async Task DetailsUpdate_accepts_legacy_litigation_status_variants(string currentStatus)
    {
        Guid caseId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-LIT-{Guid.CreateVersion7():N}",
                "Legacy",
                "Litigation",
                SeedHelper.UserId);
            caseId = caseEntity.Id;
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
        }

        var patchResp = await _client.PatchAsJsonAsync("/api/liens/cases/details-update", new
        {
            caseId,
            currentStatus,
            currentMedicalStatus = "Treating",
            caseType = "Motor Vehicle Accident",
            stateOfIncident = "CA",
        });

        patchResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await patchResp.Content.ReadAsStringAsync()}");

        var getResp = await _client.GetAsync($"/api/liens/cases/{caseId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await getResp.Content.ReadAsStringAsync()}");

        var body = await getResp.Content.ReadFromJsonAsync<JsonDocument>();
        body.Should().NotBeNull();
        body!.RootElement.GetProperty("status").GetString()
            .Should().Be(CaseStatus.InNegotiation);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var updatedCase = await verifyDb.Cases.FindAsync(caseId);
        updatedCase.Should().NotBeNull();
        updatedCase!.Status.Should().Be(CaseStatus.InNegotiation);
    }

    [Fact]
    public async Task GetCaseById_returns_default_false_flags_when_metadata_is_missing()
    {
        Guid caseId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-FLAGS-{Guid.CreateVersion7():N}",
                "Default",
                "Flags",
                SeedHelper.UserId);
            caseId = caseEntity.Id;
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
        }

        var resp = await _client.GetAsync($"/api/liens/cases/{caseId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body.Should().NotBeNull();

        var root = body!.RootElement;
        root.GetProperty("shareCase").GetString().Should().Be("false");
        root.GetProperty("minorComp").GetString().Should().Be("false");
        root.GetProperty("caseDropped").GetString().Should().Be("false");
        root.GetProperty("childSupportLiens").GetString().Should().Be("false");
        root.GetProperty("isUccFiled").GetString().Should().Be("false");
    }

    [Fact]
    public async Task UploadDocument_rejects_disallowed_file_extension()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedHelper.CaseId.ToString()), "caseId");
        form.Add(new ByteArrayContent("hello"u8.ToArray()), "file", "payload.exe");

        var resp = await _client.PostAsync("/api/liens/cases/upload/document", form);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"Body: {await resp.Content.ReadAsStringAsync()}");
        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("message").GetString()
            .Should().Contain("File type not allowed");
    }

    [Fact]
    public async Task UploadLienDocument_rejects_non_form_payload_with_actionable_message()
    {
        var resp = await _client.PostAsJsonAsync("/api/liens/cases/liens/upload/document", new
        {
            liensId = SeedHelper.LienId,
            DocFileTypeId = "14",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"Body: {await resp.Content.ReadAsStringAsync()}");
        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("message").GetString()
            .Should().Be("Content-Type must be multipart/form-data.");
    }

    [Fact]
    public async Task UploadDocument_uploads_case_document_and_records_legacy_metadata()
    {
        using var scope = _factory.Services.CreateScope();
        var uploadClient = scope.ServiceProvider.GetRequiredService<CapturingLegacyDocumentUploadClient>();
        uploadClient.Clear();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedHelper.CaseId.ToString()), "caseId");
        form.Add(new StringContent("14"), "DocFileTypeId");
        form.Add(new StringContent("payoff-quote"), "DocName");
        form.Add(new StringContent("Payoff Statement"), "DocDescription");
        var file = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "payoff-quote.pdf");

        var resp = await _client.PostAsync("/api/liens/cases/upload/document", form);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        uploadClient.Uploads.Should().ContainSingle();
        var upload = uploadClient.Uploads.Single();
        upload.ReferenceId.Should().Be(SeedHelper.CaseId);
        upload.ReferenceType.Should().Be("Case");
        upload.Title.Should().Be("payoff-quote");

        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var item = db.ServicingItems.Single(i =>
            i.TaskType == "LegacyCaseDocument" &&
            i.Notes != null &&
            i.Notes.Contains(upload.DocumentId.ToString()));
        item.CaseId.Should().Be(SeedHelper.CaseId);
        item.Notes.Should().Contain("typeId=14");
        item.Notes.Should().Contain(upload.DocumentId.ToString());

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("data").GetProperty("url").GetString()
            .Should().Be($"/documents/{upload.DocumentId}");
    }

    [Fact]
    public async Task PayoffQuote_accepts_legacy_misspelled_route()
    {
        using var scope = _factory.Services.CreateScope();
        var uploadClient = scope.ServiceProvider.GetRequiredService<CapturingLegacyDocumentUploadClient>();
        uploadClient.Clear();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedHelper.CaseId.ToString()), "caseId");
        form.Add(new StringContent("14"), "DocFileTypeId");
        form.Add(new StringContent("payoff-quote"), "DocName");
        var file = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "payoff-quote.pdf");

        var uploadResp = await _client.PostAsync("/api/liens/cases/upload/document", form);
        uploadResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await uploadResp.Content.ReadAsStringAsync()}");

        var resp = await _client.GetAsync($"/api/liens/cases/payoff-qoute/{SeedHelper.CaseId}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");
        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("url").GetString()
            .Should().Be($"/documents/{uploadClient.Uploads.Single().DocumentId}");
    }

    [Fact]
    public async Task PayoffQuote_finds_document_uploaded_with_payoff_statement_lookup_type()
    {
        using var scope = _factory.Services.CreateScope();
        var uploadClient = scope.ServiceProvider.GetRequiredService<CapturingLegacyDocumentUploadClient>();
        uploadClient.Clear();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedHelper.CaseId.ToString()), "caseId");
        form.Add(new StringContent(SeedHelper.PayoffStatementDocumentTypeId.ToString()), "DocFileTypeId");
        form.Add(new StringContent("payoff-quote"), "DocName");
        var file = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "payoff-quote.pdf");

        var uploadResp = await _client.PostAsync("/api/liens/cases/upload/document", form);
        uploadResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await uploadResp.Content.ReadAsStringAsync()}");

        var resp = await _client.GetAsync($"/api/liens/cases/payoff-qoute/{SeedHelper.CaseId}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");
        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("url").GetString()
            .Should().Be($"/documents/{uploadClient.Uploads.Single().DocumentId}");
    }

    [Fact]
    public async Task PayoffQuote_returns_ok_with_empty_url_when_document_missing()
    {
        Guid caseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-NODOC-{Guid.NewGuid():N}"[..20],
                "No",
                "Document",
                SeedHelper.UserId);
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
            caseId = caseEntity.Id;
        }

        var resp = await _client.GetAsync($"/api/liens/cases/payoff-qoute/{caseId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("url").GetString().Should().BeEmpty();
    }

    [Fact]
    public async Task CaseUpdatesV3_returns_ok_with_empty_data_when_case_has_no_notes()
    {
        Guid caseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-NONOTES-{Guid.NewGuid():N}"[..20],
                "No",
                "Notes",
                SeedHelper.UserId);
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
            caseId = caseEntity.Id;
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/case-updates/v3", new
        {
            CaseId = caseId,
            page = 1,
            limit = 10,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("data").EnumerateArray().Should().BeEmpty();
        body.RootElement.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task UploadLienDocument_uploads_lien_document_and_records_legacy_metadata()
    {
        using var scope = _factory.Services.CreateScope();
        var uploadClient = scope.ServiceProvider.GetRequiredService<CapturingLegacyDocumentUploadClient>();
        uploadClient.Clear();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedHelper.LienId.ToString()), "liensId");
        form.Add(new StringContent(Guid.Parse("00000000-0000-0000-0000-0000000000A1").ToString()), "DocFileTypeId");
        var file = new ByteArrayContent("name,amount\none,1"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "lien-upload.csv");

        var resp = await _client.PostAsync("/api/liens/cases/liens/upload/document", form);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        uploadClient.Uploads.Should().ContainSingle();
        var upload = uploadClient.Uploads.Single();
        upload.ReferenceId.Should().Be(SeedHelper.LienId);
        upload.ReferenceType.Should().Be("Lien");
        upload.Title.Should().Be("lien-upload");

        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var item = db.ServicingItems.Single(i =>
            i.TaskType == "LegacyLienDocument" &&
            i.Notes != null &&
            i.Notes.Contains(upload.DocumentId.ToString()));
        item.LienId.Should().Be(SeedHelper.LienId);
        item.Notes.Should().Contain(upload.DocumentId.ToString());
    }
}
