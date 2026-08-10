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
    public async Task ServiceCase_v3_returns_settlement_and_financial_fields()
    {
        var caseNumber = $"CASE-SVC-METRICS-{Guid.CreateVersion7():N}"[..30];

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                caseNumber,
                "Settlement",
                "Plaintiff",
                SeedHelper.UserId);
            var firstLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-SVC-METRICS-A-{Guid.CreateVersion7():N}"[..30],
                LienType.MedicalLien,
                1_000m,
                SeedHelper.UserId,
                caseId: caseEntity.Id);
            var secondLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-SVC-METRICS-B-{Guid.CreateVersion7():N}"[..30],
                LienType.MedicalLien,
                400m,
                SeedHelper.UserId,
                caseId: caseEntity.Id);
            var firstMedicalCode = ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LMC-SVC-METRICS-A-{Guid.CreateVersion7():N}"[..40],
                "LegacyMedicalCode",
                "First medical code amount",
                "system",
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                lienId: firstLien.Id,
                notes: "billingAmount=600.75; purchaseAmount=275.50");
            var secondMedicalCode = ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LMC-SVC-METRICS-B-{Guid.CreateVersion7():N}"[..40],
                "LegacyMedicalCode",
                "Second medical code amount",
                "system",
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                lienId: firstLien.Id,
                notes: "billingAmount=99.25; purchaseAmount=24.50");
            var firstSettlement = LienSettlement.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                firstLien.Id,
                1,
                0m,
                SeedHelper.UserId,
                "full_payment",
                note: "legacySettlementId=123; totalSettledAmount=180",
                settlementDate: new DateOnly(2025, 4, 1));
            var secondSettlement = LienSettlement.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                secondLien.Id,
                1,
                20m,
                SeedHelper.UserId,
                "full_payment",
                settlementDate: new DateOnly(2025, 4, 2));
            var payment = SettlementPaymentDetail.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                secondLien.Id,
                1,
                20m,
                SeedHelper.UserId,
                new DateOnly(2025, 4, 2),
                "Test Payor",
                "CHK-SVC-METRICS",
                "[legacy-meta]\nnetProfit=0.00; type=by_attorney; status=full_payment");

            db.Cases.Add(caseEntity);
            db.Liens.AddRange(firstLien, secondLien);
            db.ServicingItems.AddRange(firstMedicalCode, secondMedicalCode);
            db.LienSettlements.AddRange(firstSettlement, secondSettlement);
            db.SettlementPaymentDetails.Add(payment);
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

        item["settlementStatus"]!.GetValue<string>().Should().Be("Full Payment");
        item["settlementDate"]!.GetValue<string>().Should().Be("04/02/2025");
        item["settlementAmount"]!.GetValue<decimal>().Should().Be(200m);
        item["billingAmount"]!.GetValue<decimal>().Should().Be(1_100m);
        item["purchaseAmount"]!.GetValue<decimal>().Should().Be(300m);
    }

    [Fact]
    public async Task ServiceCase_v3_uses_payment_amount_when_no_settlement_exists()
    {
        var caseNumber = $"CASE-SVC-PAYMENT-{Guid.CreateVersion7():N}"[..30];

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                caseNumber,
                "Payment",
                "Fallback",
                SeedHelper.UserId);
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-SVC-PAYMENT-{Guid.CreateVersion7():N}"[..30],
                LienType.MedicalLien,
                3_295m,
                SeedHelper.UserId,
                caseId: caseEntity.Id);
            var payment = SettlementPaymentDetail.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                lien.Id,
                1,
                750m,
                SeedHelper.UserId,
                new DateOnly(2026, 8, 6),
                "Test Payor",
                "CHK-SVC-PAYMENT",
                "[legacy-meta]\nnetProfit=0.00; type=by_attorney; status=full_payment");

            db.Cases.Add(caseEntity);
            db.Liens.Add(lien);
            db.SettlementPaymentDetails.Add(payment);
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

        item["settlementStatus"]!.GetValue<string>().Should().Be("Full Payment");
        item["settlementDate"]!.GetValue<string>().Should().NotBeEmpty();
        item["settlementAmount"]!.GetValue<decimal>().Should().Be(750m);
    }

    [Fact]
    public async Task ServiceCase_v3_returns_empty_success_payload_when_no_cases_match()
    {
        var response = await _client.PostAsJsonAsync("/service/case/v3", new
        {
            keyword = $"NO-MATCH-{Guid.CreateVersion7():N}",
            page = 1,
            limit = 10,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        body["isSuccess"]!.GetValue<bool>().Should().BeTrue();
        body["data"]!.AsArray().Should().BeEmpty();
        body["page"]!.GetValue<int>().Should().Be(1);
        body["limit"]!.GetValue<int>().Should().Be(10);
        body["totalCount"]!.GetValue<int>().Should().Be(0);
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
    public async Task Global_and_v3_searches_rank_reversed_fuzzy_plaintiff_names()
    {
        var caseNumber = $"CASE-FUZZY-{Guid.CreateVersion7():N}"[..40];
        var lienNumber = $"LIEN-FUZZY-{Guid.CreateVersion7():N}"[..40];

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                caseNumber,
                "Jude",
                "Hannah",
                SeedHelper.UserId);
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                lienNumber,
                LienType.MedicalLien,
                1000m,
                SeedHelper.UserId,
                caseId: caseEntity.Id);

            db.Cases.Add(caseEntity);
            db.Liens.Add(lien);
            await db.SaveChangesAsync();
        }

        const string keyword = "Hanna Jud";

        var globalResponse = await _client.PostAsJsonAsync("/api/liens/cases/global-search", new
        {
            query = keyword,
            page = 1,
            limit = 20,
        });
        globalResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await globalResponse.Content.ReadAsStringAsync()}");

        var global = JsonNode.Parse(await globalResponse.Content.ReadAsStringAsync())!;
        global["cases"]!["items"]!.AsArray().Should().Contain(item =>
            item!["caseNumber"]!.GetValue<string>() == caseNumber);
        global["liens"]!["items"]!.AsArray().Should().Contain(item =>
            item!["lienNumber"]!.GetValue<string>() == lienNumber);

        var caseResponse = await _client.PostAsJsonAsync("/api/liens/cases/v3", new
        {
            keyword,
            page = 1,
            limit = 20,
        });
        caseResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await caseResponse.Content.ReadAsStringAsync()}");
        var caseBody = JsonNode.Parse(await caseResponse.Content.ReadAsStringAsync())!;
        caseBody["data"]!.AsArray().Should().Contain(item =>
            item!["caseNumber"]!.GetValue<string>() == caseNumber);

        var lienResponse = await _client.PostAsJsonAsync("/api/liens/cases/liens/v3", new
        {
            keyword,
            page = 1,
            limit = 20,
        });
        lienResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await lienResponse.Content.ReadAsStringAsync()}");
        var lienBody = JsonNode.Parse(await lienResponse.Content.ReadAsStringAsync())!;
        lienBody["items"]!.AsArray().Should().Contain(item =>
            item!["lienNumber"]!.GetValue<string>() == lienNumber);

        var serviceCaseResponse = await _client.PostAsJsonAsync("/service/case/v3", new
        {
            keyword,
            page = 1,
            limit = 20,
        });
        serviceCaseResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await serviceCaseResponse.Content.ReadAsStringAsync()}");
        var serviceCase = JsonNode.Parse(await serviceCaseResponse.Content.ReadAsStringAsync())!;
        serviceCase["data"]!.AsArray().Should().Contain(item =>
            item!["caseCode"]!.GetValue<string>() == caseNumber);

        var serviceLienResponse = await _client.PostAsJsonAsync("/service/liens/v3", new
        {
            keyword,
            page = 1,
            limit = 20,
        });
        serviceLienResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await serviceLienResponse.Content.ReadAsStringAsync()}");
        var serviceLien = JsonNode.Parse(await serviceLienResponse.Content.ReadAsStringAsync())!;
        serviceLien["data"]!.AsArray().Should().Contain(item =>
            item!["lienCode"]!.GetValue<string>() == lienNumber);
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
    public async Task ServiceSettlementHistory_v3_returns_lien_code_for_history_items()
    {
        var historyResponse = await _client.PostAsJsonAsync("/service/settlement/history/v3", new
        {
            caseId = SeedHelper.CaseId,
            page = 1,
            limit = 10,
        });
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await historyResponse.Content.ReadAsStringAsync()}");

        var body = JsonNode.Parse(await historyResponse.Content.ReadAsStringAsync())!;
        var hasExpectedItem = body["data"]!.AsArray().Any(item =>
        {
            var lienId = item?["lienId"]?.GetValue<string>();
            var lienCode = item?["lienCode"]?.GetValue<string>();
            var updatedBy = item?["updatedBy"]?.GetValue<string>();
            return lienId == "LIEN-TEST-001"
                && lienCode == "LIEN-TEST-001"
                && updatedBy == "Demo User";
        });

        hasExpectedItem.Should().BeTrue();
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
    public async Task LegacyCaseNotes_route_keeps_details_update_notes_as_history()
    {
        const string firstDetailsNote = "First details update note";
        const string secondDetailsNote = "Second details update note";
        var firstUpdateResponse = await _client.PatchAsJsonAsync("/api/liens/cases/details-update", new
        {
            caseId = SeedHelper.CaseId,
            notes = firstDetailsNote,
        });
        firstUpdateResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await firstUpdateResponse.Content.ReadAsStringAsync()}");

        var secondUpdateResponse = await _client.PatchAsJsonAsync("/api/liens/cases/details-update", new
        {
            caseId = SeedHelper.CaseId,
            notes = secondDetailsNote,
        });
        secondUpdateResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await secondUpdateResponse.Content.ReadAsStringAsync()}");

        var response = await _client.GetAsync($"/api/liens/cases/notes/{SeedHelper.CaseId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var data = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["data"]!.AsArray();
        data.Should().Contain(item => item!["note"]!.GetValue<string>() == firstDetailsNote);
        data.Should().Contain(item => item!["note"]!.GetValue<string>() == secondDetailsNote);
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

    [Fact]
    public async Task LegacyDashboardMetric_routes_keep_purchase_and_filtered_settlement_date_history()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var datedLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LIEN-DATED-DASHBOARD",
                LienType.MedicalLien,
                750m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                purchaseDate: new DateOnly(2025, 2, 1));

            db.Liens.Add(datedLien);
            db.LienSettlements.Add(LienSettlement.Create(
                SeedHelper.TenantId,
                SeedHelper.CaseId,
                datedLien.Id,
                2,
                750m,
                SeedHelper.UserId,
                settlementDate: new DateOnly(2025, 2, 1)));
            await db.SaveChangesAsync();
        }

        var deployedResponse = await _client.PostAsJsonAsync("/api/liens/cases/dashboard/deployed", new
        {
            page = 1,
            limit = 1000,
        });
        deployedResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await deployedResponse.Content.ReadAsStringAsync()}");

        var deployed = JsonNode.Parse(await deployedResponse.Content.ReadAsStringAsync())!["data"]!;
        deployed["periodStart"]!.GetValue<string>().Should().BeEmpty();
        deployed["periodEnd"]!.GetValue<string>().Should().BeEmpty();
        deployed["totalCount"]!.GetValue<int>().Should().Be(1);

        var cashReceivedResponse = await _client.PostAsJsonAsync("/api/liens/cases/dashboard/cash-received", new
        {
            startDate = "02/01/2025",
            endDate = "02/01/2025",
        });
        cashReceivedResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await cashReceivedResponse.Content.ReadAsStringAsync()}");

        var cashReceived = JsonNode.Parse(await cashReceivedResponse.Content.ReadAsStringAsync())!["data"]!;
        cashReceived["periodStart"]!.GetValue<string>().Should().Be("02/01/2025");
        cashReceived["periodEnd"]!.GetValue<string>().Should().Be("02/01/2025");
        cashReceived["totalAmount"]!.GetValue<string>().Should().Be("750.00");
        cashReceived["totalCount"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    public async Task LegacyDashboardDeployed_uses_diy_purchase_precedence_for_dated_liens()
    {
        var otherTenantId = Guid.CreateVersion7();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var medicalCodeLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LIEN-DASHBOARD-PURCHASE-MEDICAL",
                LienType.MedicalLien,
                1_000m,
                SeedHelper.UserId,
                purchaseDate: new DateOnly(2025, 2, 1));
            medicalCodeLien.SetFinancials(1_000m, SeedHelper.UserId, purchasePrice: 100m);

            var fallbackLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LIEN-DASHBOARD-PURCHASE-FALLBACK",
                LienType.MedicalLien,
                500m,
                SeedHelper.UserId,
                purchaseDate: new DateOnly(2025, 2, 1));
            fallbackLien.SetFinancials(500m, SeedHelper.UserId, purchasePrice: 300m);

            var outsideRangeLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LIEN-DASHBOARD-PURCHASE-OUTSIDE",
                LienType.MedicalLien,
                600m,
                SeedHelper.UserId,
                purchaseDate: new DateOnly(2025, 1, 31));
            outsideRangeLien.SetFinancials(600m, SeedHelper.UserId, purchasePrice: 400m);

            var otherTenantLien = Lien.Create(
                otherTenantId,
                SeedHelper.OrgId,
                "LIEN-DASHBOARD-PURCHASE-OTHER",
                LienType.MedicalLien,
                10_000m,
                SeedHelper.UserId,
                purchaseDate: new DateOnly(2025, 2, 1));
            otherTenantLien.SetFinancials(10_000m, SeedHelper.UserId, purchasePrice: 10_000m);

            db.Liens.AddRange(medicalCodeLien, fallbackLien, outsideRangeLien, otherTenantLien);
            db.ServicingItems.AddRange(
                ServicingItem.Create(
                    SeedHelper.TenantId,
                    SeedHelper.OrgId,
                    "LMC-DASHBOARD-PURCHASE-1",
                    "LegacyMedicalCode",
                    "First purchase amount",
                    "system",
                    SeedHelper.UserId,
                    lienId: medicalCodeLien.Id,
                    notes: "billingAmount=1,500; purchaseAmount=250"),
                ServicingItem.Create(
                    SeedHelper.TenantId,
                    SeedHelper.OrgId,
                    "LMC-DASHBOARD-PURCHASE-2",
                    "LegacyMedicalCode",
                    "Second purchase amount",
                    "system",
                    SeedHelper.UserId,
                    lienId: medicalCodeLien.Id,
                    notes: "billingAmount=500; purchaseAmount=50"),
                ServicingItem.Create(
                    SeedHelper.TenantId,
                    SeedHelper.OrgId,
                    "LMC-DASHBOARD-PURCHASE-OUTSIDE",
                    "LegacyMedicalCode",
                    "Outside-range purchase amount",
                    "system",
                    SeedHelper.UserId,
                    lienId: outsideRangeLien.Id,
                    notes: "billingAmount=600; purchaseAmount=500"),
                ServicingItem.Create(
                    otherTenantId,
                    SeedHelper.OrgId,
                    "LMC-DASHBOARD-PURCHASE-OTHER",
                    "LegacyMedicalCode",
                    "Other-tenant purchase amount",
                    "system",
                    SeedHelper.UserId,
                    lienId: otherTenantLien.Id,
                    notes: "billingAmount=10,000; purchaseAmount=10,000"));
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/liens/cases/dashboard/deployed", new
        {
            startDate = "02/01/2025",
            endDate = "02/01/2025",
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var deployed = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["data"]!;
        deployed["periodStart"]!.GetValue<string>().Should().Be("02/01/2025");
        deployed["periodEnd"]!.GetValue<string>().Should().Be("02/01/2025");
        deployed["totalAmount"]!.GetValue<string>().Should().Be("600.00");
        deployed["totalCount"]!.GetValue<int>().Should().Be(2);
    }

    [Fact]
    public async Task LegacyDashboardCashReceived_without_date_range_matches_diy_returned_amount_precedence()
    {
        var otherTenantId = Guid.CreateVersion7();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var metadataLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LIEN-DASHBOARD-METADATA",
                LienType.MedicalLien,
                1_000m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId);
            metadataLien.SetFinancials(1_000m, SeedHelper.UserId, payoffAmount: 900m);

            var payoffLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LIEN-DASHBOARD-PAYOFF",
                LienType.MedicalLien,
                500m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId);
            payoffLien.SetFinancials(500m, SeedHelper.UserId, payoffAmount: 220m);

            var paymentLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LIEN-DASHBOARD-PAYMENT",
                LienType.MedicalLien,
                600m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId);

            var otherTenantLien = Lien.Create(
                otherTenantId,
                SeedHelper.OrgId,
                "LIEN-DASHBOARD-OTHER-TENANT",
                LienType.MedicalLien,
                10_000m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId);

            var deletedMetadata = LienSettlement.Create(
                SeedHelper.TenantId,
                SeedHelper.CaseId,
                paymentLien.Id,
                2,
                700m,
                SeedHelper.UserId,
                note: "legacySettlementId=deleted; totalSettledAmount=700");
            deletedMetadata.SoftDelete(SeedHelper.UserId);

            var deletedPayment = SettlementPaymentDetail.Create(
                SeedHelper.TenantId,
                SeedHelper.CaseId,
                paymentLien.Id,
                3,
                900m,
                SeedHelper.UserId);
            deletedPayment.SoftDelete(SeedHelper.UserId);

            db.Liens.AddRange(metadataLien, payoffLien, paymentLien, otherTenantLien);
            db.LienSettlements.AddRange(
                LienSettlement.Create(
                    SeedHelper.TenantId,
                    SeedHelper.CaseId,
                    metadataLien.Id,
                    1,
                    50m,
                    SeedHelper.UserId,
                    note: "legacySettlementId=1; totalSettledAmount=180"),
                LienSettlement.Create(
                    SeedHelper.TenantId,
                    SeedHelper.CaseId,
                    metadataLien.Id,
                    2,
                    25m,
                    SeedHelper.UserId,
                    note: "legacySettlementId=2; totalSettledAmount=20"),
                LienSettlement.Create(
                    SeedHelper.TenantId,
                    SeedHelper.CaseId,
                    payoffLien.Id,
                    1,
                    60m,
                    SeedHelper.UserId),
                LienSettlement.Create(
                    SeedHelper.TenantId,
                    SeedHelper.CaseId,
                    paymentLien.Id,
                    1,
                    500m,
                    SeedHelper.UserId),
                deletedMetadata);
            db.SettlementPaymentDetails.AddRange(
                SettlementPaymentDetail.Create(
                    SeedHelper.TenantId,
                    SeedHelper.CaseId,
                    metadataLien.Id,
                    1,
                    1_000m,
                    SeedHelper.UserId),
                SettlementPaymentDetail.Create(
                    SeedHelper.TenantId,
                    SeedHelper.CaseId,
                    payoffLien.Id,
                    1,
                    800m,
                    SeedHelper.UserId),
                SettlementPaymentDetail.Create(
                    SeedHelper.TenantId,
                    SeedHelper.CaseId,
                    paymentLien.Id,
                    1,
                    300m,
                    SeedHelper.UserId),
                SettlementPaymentDetail.Create(
                    SeedHelper.TenantId,
                    SeedHelper.CaseId,
                    paymentLien.Id,
                    2,
                    45m,
                    SeedHelper.UserId),
                deletedPayment,
                SettlementPaymentDetail.Create(
                    otherTenantId,
                    SeedHelper.CaseId,
                    otherTenantLien.Id,
                    1,
                    10_000m,
                    SeedHelper.UserId));
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/liens/cases/dashboard/cash-received", new
        {
            page = 1,
            limit = 1000,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var cashReceived = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["data"]!;
        cashReceived["periodStart"]!.GetValue<string>().Should().BeEmpty();
        cashReceived["periodEnd"]!.GetValue<string>().Should().BeEmpty();
        cashReceived["totalAmount"]!.GetValue<string>().Should().Be("5265.00");
        cashReceived["totalCount"]!.GetValue<int>().Should().Be(4);
    }
}
