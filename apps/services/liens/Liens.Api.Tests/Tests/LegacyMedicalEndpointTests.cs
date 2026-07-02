using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Liens.Api.Tests.Helpers;
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
    public async Task MedicalCode_create_can_be_retrieved_by_lien_id()
    {
        var payload = new
        {
            id = (string?)null,
            liensId = SeedHelper.LienId.ToString(),
            code = "12345",
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
        data.Should().ContainSingle();

        var item = data[0]!;
        item["liensId"]!.GetValue<string>().Should().Be(SeedHelper.LienId.ToString());
        item["code"]!.GetValue<string>().Should().Be("12345");
        item["medicareCost"]!.GetValue<string>().Should().Be("100.00");
        item["billingAmount"]!.GetValue<string>().Should().Be("100.00");
        item["purchaseAmount"]!.GetValue<string>().Should().Be("100.00");
        item["payee"]!.GetValue<string>().Should().Be("test payee");
        item["outboundCheckNumber"]!.GetValue<string>().Should().Be("chck-1000");
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
        lienDocument!["liensId"]!.GetValue<string>().Should().Be(SeedHelper.LienId.ToString());
    }
}
