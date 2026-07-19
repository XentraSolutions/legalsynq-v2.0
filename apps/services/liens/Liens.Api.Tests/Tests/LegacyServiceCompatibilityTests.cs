using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class LegacyServiceCompatibilityTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public LegacyServiceCompatibilityTests(LiensApiFactory factory) => _factory = factory;

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
    public async Task ServiceCase_routes_return_seeded_case_data()
    {
        var getResponse = await _client.GetAsync("/service/case");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await getResponse.Content.ReadAsStringAsync()}");

        var postResponse = await _client.PostAsJsonAsync("/service/case/v3", new
        {
            page = 1,
            limit = 10,
        });
        postResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await postResponse.Content.ReadAsStringAsync()}");

        var getBody = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync())!;
        getBody["isSuccess"]!.GetValue<bool>().Should().BeTrue();
        getBody["data"]!.AsArray().Should().Contain(item =>
            item!["caseId"]!.GetValue<string>() == SeedHelper.CaseId.ToString());

        var postBody = JsonNode.Parse(await postResponse.Content.ReadAsStringAsync())!;
        postBody["isSuccess"]!.GetValue<bool>().Should().BeTrue();
        postBody["data"]!.AsArray().Should().Contain(item =>
            item!["caseId"]!.GetValue<string>() == SeedHelper.CaseId.ToString());
    }

    [Fact]
    public async Task ServiceCase_v3_returns_case_manager_fields_when_available()
    {
        var caseManagerId = Guid.CreateVersion7();
        var caseNumber = $"CASE-SVC-V3-{Guid.CreateVersion7():N}";

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
                "Legacy",
                "Service",
                SeedHelper.UserId,
                notes: $"lawFirmId={SeedHelper.LawFirmId}; caseManagerId={caseManagerId}"));

            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/service/case/v3", new
        {
            keyword = caseNumber,
            page = 1,
            limit = 10,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var item = body["data"]!.AsArray().Single(node =>
            node!["caseCode"]!.GetValue<string>() == caseNumber)!;

        item["caseManagerId"]!.GetValue<string>().Should().Be(caseManagerId.ToString());
        item["caseManager"]!.GetValue<string>().Should().Be("John Doe");
        item["lawFirmId"]!.GetValue<string>().Should().Be(SeedHelper.LawFirmId.ToString());
        item["lawfirm"]!.GetValue<string>().Should().Be("Smith & Associates LLP");
    }

    [Fact]
    public async Task ServiceLien_routes_return_seeded_lien_data()
    {
        var listResponse = await _client.GetAsync($"/service/all-liens/{SeedHelper.CaseId}");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await listResponse.Content.ReadAsStringAsync()}");

        var searchResponse = await _client.PostAsJsonAsync("/service/liens/v3", new
        {
            caseId = SeedHelper.CaseId,
            page = 1,
            limit = 10,
        });
        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await searchResponse.Content.ReadAsStringAsync()}");

        var listBody = JsonNode.Parse(await listResponse.Content.ReadAsStringAsync())!;
        listBody["data"]!.AsArray().Should().Contain(item =>
            item!["liensId"]!.GetValue<string>() == SeedHelper.LienId.ToString());

        var searchBody = JsonNode.Parse(await searchResponse.Content.ReadAsStringAsync())!;
        searchBody["data"]!.AsArray().Should().Contain(item =>
            item!["liensId"]!.GetValue<string>() == SeedHelper.LienId.ToString());
    }

    [Fact]
    public async Task ServiceSettlementCompatibility_routes_return_data()
    {
        var historyResponse = await _client.PostAsJsonAsync("/service/settlement/history/v3", new
        {
            caseId = SeedHelper.CaseId,
            page = 1,
            limit = 10,
        });
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await historyResponse.Content.ReadAsStringAsync()}");

        var paymentsResponse = await _client.GetAsync($"/service/liens/settlement/payment-details/{SeedHelper.CaseId}");
        paymentsResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await paymentsResponse.Content.ReadAsStringAsync()}");

        var settlementResponse = await _client.GetAsync($"/service/liens/settlement-details/{SeedHelper.CaseId}");
        settlementResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await settlementResponse.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task ServiceDeletePayment_post_route_deletes_payment()
    {
        var createResponse = await _client.PostAsJsonAsync("/service/liens/settlement/payment", new
        {
            caseId = SeedHelper.CaseId,
            lienId = SeedHelper.LienId,
            paymentNumber = 77,
            amount = 123m,
            paymentDate = "2025-04-16",
            payee = "Delete Me",
            checkNumber = "CHK-DEL",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await createResponse.Content.ReadAsStringAsync()}");

        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var paymentId = createBody!.RootElement.GetProperty("id").GetGuid();

        var deleteResponse = await _client.PostAsJsonAsync("/service/delete-payment", new
        {
            caseId = SeedHelper.CaseId,
            paymentId,
        });
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await deleteResponse.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task LegacyTask_routes_support_create_get_and_delete()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/liens/cases/task/create", new
        {
            caseId = SeedHelper.CaseId,
            title = "Legacy follow-up",
            description = "Call counsel",
            dueDate = "06/30/2026",
            priority = "Normal",
            status = "Open",
            assignedTo = "qa@test.local",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await createResponse.Content.ReadAsStringAsync()}");

        var listResponse = await _client.GetAsync($"/api/liens/cases/get-task/{SeedHelper.CaseId}");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await listResponse.Content.ReadAsStringAsync()}");

        var listBody = JsonNode.Parse(await listResponse.Content.ReadAsStringAsync())!;
        var task = listBody["data"]!.AsArray().Single(item =>
            item!["title"]!.GetValue<string>() == "Legacy follow-up")!;
        var taskId = Guid.Parse(task["taskId"]!.GetValue<string>());

        var deleteResponse = await _client.DeleteAsync($"/api/liens/cases/task/delete/{taskId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await deleteResponse.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task LegacyCaseNote_routes_support_add_and_delete()
    {
        var addResponse = await _client.PostAsJsonAsync("/api/liens/cases/add-note", new
        {
            caseId = SeedHelper.CaseId,
            note = "Legacy case note",
        });
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await addResponse.Content.ReadAsStringAsync()}");

        Guid noteId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            noteId = db.LienCaseNotes.Single(n => n.CaseId == SeedHelper.CaseId && n.Content == "Legacy case note").Id;
        }

        var deleteResponse = await _client.PostAsJsonAsync("/api/liens/cases/delete-note", new
        {
            noteId,
        });
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await deleteResponse.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task LegacyCaseDocument_route_returns_uploaded_case_documents()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedHelper.CaseId.ToString()), "caseId");
        form.Add(new StringContent("14"), "DocFileTypeId");
        form.Add(new StringContent("legacy-case-doc"), "DocName");
        var file = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "legacy-case-doc.pdf");

        var uploadResponse = await _client.PostAsync("/api/liens/cases/upload/document", form);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await uploadResponse.Content.ReadAsStringAsync()}");

        var getResponse = await _client.GetAsync($"/api/liens/cases/get-casedocument/{SeedHelper.CaseId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await getResponse.Content.ReadAsStringAsync()}");

        var body = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync())!;
        body["data"]!.AsArray().Should().Contain(item =>
            item!["filename"]!.GetValue<string>() == "legacy-case-doc");
    }

    [Fact]
    public async Task LegacyDashboardMetric_routes_return_200()
    {
        var deployedResponse = await _client.PostAsJsonAsync("/api/liens/cases/dashboard/deployed", new
        {
            startDate = "01/01/2024",
            endDate = "12/31/2026",
        });
        deployedResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await deployedResponse.Content.ReadAsStringAsync()}");

        var cashReceivedResponse = await _client.PostAsJsonAsync("/api/liens/cases/dashboard/cash-received", new
        {
            startDate = "01/01/2024",
            endDate = "12/31/2026",
        });
        cashReceivedResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await cashReceivedResponse.Content.ReadAsStringAsync()}");
    }
}
