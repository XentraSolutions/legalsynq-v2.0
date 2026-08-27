using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class LegacyMedicalEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public LegacyMedicalEndpointTests(LiensApiFactory factory) => _factory = factory;

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
    public async Task UpdateMedical_persists_legacy_service_and_servicing_fields()
    {
        var payload = new
        {
            id = SeedHelper.LienId.ToString(),
            caseId = SeedHelper.CaseId.ToString(),
            status = "Offered",
            purchaseDate = "06/22/2026",
            initialServiceDate = "07/07/2026",
            endServiceDate = "07/03/2026",
            note = "test",
            isBulk = "Yes",
            isServicing = "Yes",
            fundingCompanyId = string.Empty,
        };

        var updateResponse = await _client.PostAsJsonAsync(
            "/api/liens/cases/liens/update-medical",
            payload);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/liens/cases/liens/get-medical/{SeedHelper.LienId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync())!;
        body["isSuccess"]!.GetValue<bool>().Should().BeTrue();

        var data = body["data"]!;
        data["status"]!.GetValue<string>().Should().Be("Offered");
        data["purchaseDate"]!.GetValue<string>().Should().Be("06/22/2026");
        data["initialServiceDate"]!.GetValue<string>().Should().Be("07/07/2026");
        data["endServiceDate"]!.GetValue<string>().Should().Be("07/03/2026");
        data["note"]!.GetValue<string>().Should().Be("test");
        data["isBulk"]!.GetValue<string>().Should().Be("Yes");
        data["isServicing"]!.GetValue<string>().Should().Be("Yes");
        data["fundingCompanyId"]!.GetValue<string>().Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateMedical_persists_and_resolves_funding_company()
    {
        var payload = new
        {
            id = SeedHelper.LienId.ToString(),
            caseId = SeedHelper.CaseId.ToString(),
            status = "Offered",
            purchaseDate = "06/22/2026",
            initialServiceDate = "07/07/2026",
            endServiceDate = "07/03/2026",
            note = "test",
            isBulk = "Yes",
            isServicing = "Yes",
            fundingCompanyId = SeedHelper.FundingCompanyId.ToString(),
        };

        var updateResponse = await _client.PostAsJsonAsync(
            "/api/liens/cases/liens/update-medical",
            payload);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/liens/cases/liens/get-medical/{SeedHelper.LienId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync())!;
        body["isSuccess"]!.GetValue<bool>().Should().BeTrue();

        var data = body["data"]!;
        data["fundingCompanyId"]!.GetValue<string>().Should().Be(SeedHelper.FundingCompanyId.ToString());
        data["fundingCompany"]!.GetValue<string>().Should().Be("Capital Fund LLC");
    }

    [Fact]
    public async Task UpdateFacility_persists_medical_provider_and_facility_contact_metadata()
    {
        var facilityContactId = Guid.CreateVersion7();

        var payload = new
        {
            liensId = SeedHelper.LienId.ToString(),
            facilityId = SeedHelper.MedicalFacilityContactId.ToString(),
            facility = "Sunrise Clinic",
            facilityContactId = facilityContactId.ToString(),
            facilityContact = "MedicalFacility Primary Staff I",
            email = "",
            phone = "555-0101",
            medicalProviderId = SeedHelper.MedicalProviderId.ToString(),
            medicalProvider = "Dr. Anthony Ashworth, MD",
        };

        var updateResponse = await _client.PostAsJsonAsync(
            "/api/liens/cases/liens/update-facility",
            payload);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await updateResponse.Content.ReadAsStringAsync()}");

        var getResponse = await _client.GetAsync($"/api/liens/cases/liens/get-facility/{SeedHelper.LienId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await getResponse.Content.ReadAsStringAsync()}");

        var body = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync())!;
        body["isSuccess"]!.GetValue<bool>().Should().BeTrue();

        var data = body["data"]!;
        data["facilityId"]!.GetValue<string>().Should().Be(SeedHelper.MedicalFacilityContactId.ToString());
        data["facilityContactId"]!.GetValue<string>().Should().Be(facilityContactId.ToString());
        data["medicalProviderId"]!.GetValue<string>().Should().Be(SeedHelper.MedicalProviderId.ToString());
        data["phone"]!.GetValue<string>().Should().Be("555-0101");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var info = db.ServicingItems.Single(item =>
            item.LienId == SeedHelper.LienId &&
            item.TaskType == "LegacyMedicalFacilityInfo");

        info.Notes.Should().Contain($"facilityId={SeedHelper.MedicalFacilityContactId}");
        info.Notes.Should().Contain($"facilityContactId={facilityContactId}");
        info.Notes.Should().Contain($"medicalProviderId={SeedHelper.MedicalProviderId}");
        info.Notes.Should().Contain("medicalProvider=Dr. Anthony Ashworth, MD");
    }

    [Fact]
    public async Task UpdateMedical_accepts_legacy_open_status()
    {
        var payload = new
        {
            id = SeedHelper.LienId.ToString(),
            caseId = SeedHelper.CaseId.ToString(),
            status = "Open",
            purchaseDate = "07/06/2026",
            initialServiceDate = "07/07/2026",
            endServiceDate = "",
            note = "",
            isBulk = "N",
            isServicing = "N",
            fundingCompanyId = string.Empty,
        };

        var updateResponse = await _client.PostAsJsonAsync(
            "/api/liens/cases/liens/update-medical",
            payload);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await updateResponse.Content.ReadAsStringAsync()}");

        var getResponse = await _client.GetAsync($"/api/liens/cases/liens/get-medical/{SeedHelper.LienId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync())!;
        body["isSuccess"]!.GetValue<bool>().Should().BeTrue();
        body["data"]!["status"]!.GetValue<string>().Should().Be("Active");
    }

    [Fact]
    public async Task MedicalCode_create_can_be_retrieved_by_lien_id()
    {
        var code = "99213";
        var description = "Office Visit";
        var payload = new
        {
            id = (string?)null,
            liensId = SeedHelper.LienId.ToString(),
            code,
            medicareCost = "100.00",
            billingAmount = "100.00",
            purchaseAmount = "100.00",
            payee = "test payee",
            outboundCheckNumber = "chck-1000",
        };

        var createResponse = await _client.PostAsJsonAsync(
            "/api/liens/cases/liens/medicalcode",
            payload);

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/liens/cases/liens/get-medicalcode/{SeedHelper.LienId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync())!;
        body["isSuccess"]!.GetValue<bool>().Should().BeTrue();

        var data = body["data"]!.AsArray();
        var item = data.Single(item => item!["code"]!.GetValue<string>() == code)!;
        item["liensId"]!.GetValue<string>().Should().Be(SeedHelper.LienId.ToString());
        item["code"]!.GetValue<string>().Should().Be(code);
        item["description"]!.GetValue<string>().Should().Be(description);
        item["medicareCost"]!.GetValue<string>().Should().Be("100.00");
        item["billingAmount"]!.GetValue<string>().Should().Be("100.00");
        item["purchaseAmount"]!.GetValue<string>().Should().Be("100.00");
        item["payee"]!.GetValue<string>().Should().Be("test payee");
        item["outboundCheckNumber"]!.GetValue<string>().Should().Be("chck-1000");
    }

    [Fact]
    public async Task MedicalCode_update_falls_back_to_lien_and_code_when_row_id_is_stale()
    {
        var code = "45385";
        var createResponse = await _client.PostAsJsonAsync(
            "/api/liens/cases/liens/medicalcode",
            new
            {
                id = (string?)null,
                liensId = SeedHelper.LienId.ToString(),
                code,
                medicareCost = "879.00",
                billingAmount = "1000.00",
                purchaseAmount = "750.00",
                payee = "",
                outboundCheckNumber = "",
            });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await createResponse.Content.ReadAsStringAsync()}");

        var updateResponse = await _client.PostAsJsonAsync(
            "/api/liens/cases/liens/update-medicalcode",
            new
            {
                id = Guid.CreateVersion7().ToString(),
                liensId = SeedHelper.LienId.ToString(),
                code,
                medicareCost = "879.00",
                billingAmount = "1000.00",
                purchaseAmount = "1000.00",
                payee = "",
                outboundCheckNumber = "",
            });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await updateResponse.Content.ReadAsStringAsync()}");

        var getResponse = await _client.GetAsync($"/api/liens/cases/liens/get-medicalcode/{SeedHelper.LienId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await getResponse.Content.ReadAsStringAsync()}");

        var body = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync())!;
        var item = body["data"]!
            .AsArray()
            .Single(item => item!["code"]!.GetValue<string>() == code)!;
        item["purchaseAmount"]!.GetValue<string>().Should().Be("1000.00");
    }

    [Fact]
    public async Task DeleteMedicalCode_deletes_single_row_when_given_medical_code_id()
    {
        var codeA = $"A-{Guid.NewGuid():N}"[..10];
        var codeB = $"B-{Guid.NewGuid():N}"[..10];

        foreach (var code in new[] { codeA, codeB })
        {
            var createResponse = await _client.PostAsJsonAsync(
                "/api/liens/cases/liens/medicalcode",
                new
                {
                    id = (string?)null,
                    liensId = SeedHelper.LienId.ToString(),
                    code,
                    medicareCost = "100.00",
                    billingAmount = "100.00",
                    purchaseAmount = "100.00",
                    payee = "test payee",
                    outboundCheckNumber = "chck-1000",
                });

            createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var beforeDelete = JsonNode.Parse(await (await _client.GetAsync(
            $"/api/liens/cases/liens/get-medicalcode/{SeedHelper.LienId}"))
            .Content.ReadAsStringAsync())!;

        var createdRows = beforeDelete["data"]!
            .AsArray()
            .Where(item => item is not null)
            .ToList();

        var rowToDelete = createdRows.Single(item => item!["code"]!.GetValue<string>() == codeA)!;
        var rowToKeep = createdRows.Single(item => item!["code"]!.GetValue<string>() == codeB)!;

        var deleteResponse = await _client.DeleteAsync(
            $"/api/liens/cases/liens/delete-medicalcode/{rowToDelete["id"]!.GetValue<string>()}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterDeleteResponse = await _client.GetAsync(
            $"/api/liens/cases/liens/get-medicalcode/{SeedHelper.LienId}");
        afterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterDelete = JsonNode.Parse(await afterDeleteResponse.Content.ReadAsStringAsync())!;
        var remainingRows = afterDelete["data"]!
            .AsArray()
            .Where(item => item is not null)
            .ToList();

        remainingRows.Should().NotContain(item =>
            item!["id"]!.GetValue<string>() == rowToDelete["id"]!.GetValue<string>());
        remainingRows.Should().Contain(item =>
            item!["id"]!.GetValue<string>() == rowToKeep["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetMedicalDocument_returns_uploaded_lien_documents()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedHelper.LienId.ToString()), "liensId");
        form.Add(new StringContent("14"), "DocFileTypeId");
        form.Add(new StringContent("medical-doc"), "DocName");
        var file = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "medical-doc.pdf");

        var uploadResponse = await _client.PostAsync("/api/liens/cases/liens/upload/document", form);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/liens/cases/liens/get-medicaldocument/{SeedHelper.LienId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync())!;
        body["isSuccess"]!.GetValue<bool>().Should().BeTrue();
        body["message"]!.GetValue<string>().Should().Be("Successfully retrieved Medical Documents.");

        var data = body["data"]!.AsArray();
        data.Count.Should().BeGreaterThan(0);

        var item = data.Single(item =>
            item!["filename"]!.GetValue<string>() == "medical-doc")!;
        item["liensId"]!.GetValue<string>().Should().Be(SeedHelper.LienId.ToString());
        item["filename"]!.GetValue<string>().Should().Be("medical-doc");
        item["typeId"]!.GetValue<string>().Should().Be("14");
        item["url"]!.GetValue<string>().Should().StartWith("/documents/");
    }

    [Fact]
    public async Task GetAllCaseDocument_returns_case_and_lien_documents_for_case()
    {
        using var caseForm = new MultipartFormDataContent();
        caseForm.Add(new StringContent(SeedHelper.CaseId.ToString()), "caseId");
        caseForm.Add(new StringContent("14"), "DocFileTypeId");
        caseForm.Add(new StringContent("case-doc"), "DocName");
        var caseFile = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        caseFile.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        caseForm.Add(caseFile, "file", "case-doc.pdf");

        var caseUploadResponse = await _client.PostAsync("/api/liens/cases/upload/document", caseForm);
        caseUploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var lienForm = new MultipartFormDataContent();
        lienForm.Add(new StringContent(SeedHelper.LienId.ToString()), "liensId");
        lienForm.Add(new StringContent("7"), "DocFileTypeId");
        lienForm.Add(new StringContent("lien-doc"), "DocName");
        var lienFile = new ByteArrayContent("name,amount\none,1"u8.ToArray());
        lienFile.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        lienForm.Add(lienFile, "file", "lien-doc.csv");

        var lienUploadResponse = await _client.PostAsync("/api/liens/cases/liens/upload/document", lienForm);
        lienUploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/liens/cases/get-allcasedocument/{SeedHelper.CaseId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync())!;
        body["isSuccess"]!.GetValue<bool>().Should().BeTrue();
        body["message"]!.GetValue<string>().Should().Be("Successfully retrieved Documents.");

        var data = body["data"]!.AsArray();
        data.Count.Should().BeGreaterThanOrEqualTo(2);

        var caseDocument = data.Single(item =>
            item!["filename"]!.GetValue<string>() == "case-doc");
        var lienDocument = data.Single(item =>
            item!["filename"]!.GetValue<string>() == "lien-doc");

        caseDocument!["liensId"].Should().BeNull();
        caseDocument["typeId"]!.GetValue<string>().Should().Be("14");
        caseDocument["documentTypeId"]!.GetValue<string>()
            .Should().Be("10000000-0000-0000-0000-000000000005");
        lienDocument!["liensId"]!.GetValue<string>().Should().Be(SeedHelper.LienId.ToString());
        lienDocument["typeId"]!.GetValue<string>().Should().Be("7");
        lienDocument["documentTypeId"]!.GetValue<string>()
            .Should().Be("10000000-0000-0000-0000-000000000007");
    }

    [Fact]
    public async Task GetAllCaseDocument_defaults_missing_document_types_to_other()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"DOC-UNTYPED-{Guid.CreateVersion7():N}"[..36],
                "LegacyCaseDocument",
                "Imported document without type metadata",
                "Legacy import",
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                notes: "url=/documents/untyped; filename=untyped.pdf"));
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync(
            $"/api/liens/cases/get-allcasedocument/{SeedHelper.CaseId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var document = body["data"]!.AsArray().Single(item =>
            item!["filename"]!.GetValue<string>() == "untyped.pdf")!;

        document["typeId"]!.GetValue<string>()
            .Should().Be("10000000-0000-0000-0000-000000000005");
        document["documentTypeId"]!.GetValue<string>()
            .Should().Be("10000000-0000-0000-0000-000000000005");
    }

    [Theory]
    [InlineData("LegacyMedicalDocument")]
    [InlineData("LegacyLienDocument")]
    public async Task DeleteMedicalDocument_accepts_listed_legacy_document_types(string taskType)
    {
        Guid documentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var document = ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"DOC-MEDICAL-{Guid.CreateVersion7():N}"[..36],
                taskType,
                "Medical document",
                "Legacy import",
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                lienId: SeedHelper.LienId,
                notes: "url=/documents/medical; filename=medical.pdf");
            documentId = document.Id;
            db.ServicingItems.Add(document);
            await db.SaveChangesAsync();
        }

        var response = await _client.DeleteAsync($"/liens/delete-medicaldocument/{documentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        (await verifyDb.ServicingItems.FindAsync(documentId)).Should().BeNull();
    }
}
