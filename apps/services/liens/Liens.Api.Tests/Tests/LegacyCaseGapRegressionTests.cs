using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class LegacyCaseGapRegressionTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public LegacyCaseGapRegressionTests(LiensApiFactory factory) => _factory = factory;

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
    public async Task Create_update_getcaseinfo_and_delete_legacy_case_roundtrip()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/liens/cases/create", new
        {
            code = $"CASE-LEGACY-{Guid.CreateVersion7():N}",
            firstname = "Maria",
            lastname = "Lopez",
            dob = "01/02/1990",
            address = "123 Main St",
            city = "Austin",
            state = "TX",
            zipcode = "78701",
            dateOfLoss = "06/01/2026",
            note = "legacy create note",
            externalCaseId = "EXT-123",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await createResponse.Content.ReadAsStringAsync()}");

        var createBody = JsonNode.Parse(await createResponse.Content.ReadAsStringAsync())!;
        var caseId = Guid.Parse(createBody["data"]!["id"]!.GetValue<string>());

        var updateResponse = await _client.PatchAsJsonAsync($"/api/liens/cases/update/{caseId}", new
        {
            firstname = "Maria",
            lastname = "Rivera",
            dob = "01/02/1990",
            address = "456 Elm St",
            city = "Dallas",
            state = "TX",
            zipcode = "75001",
            dateOfLoss = "06/05/2026",
            note = "legacy updated note",
            externalCaseId = "EXT-456",
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await updateResponse.Content.ReadAsStringAsync()}");

        var infoResponse = await _client.GetAsync($"/api/liens/cases/getcaseinfo/{caseId}");
        infoResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await infoResponse.Content.ReadAsStringAsync()}");

        var infoBody = JsonNode.Parse(await infoResponse.Content.ReadAsStringAsync())!;
        var data = infoBody["data"]![0]!;
        data["firstname"]!.GetValue<string>().Should().Be("Maria");
        data["lastname"]!.GetValue<string>().Should().Be("Rivera");
        data["address"]!.GetValue<string>().Should().Be("456 Elm St");
        data["city"]!.GetValue<string>().Should().Be("Dallas");
        data["state"]!.GetValue<string>().Should().Be("TX");
        data["zipcode"]!.GetValue<string>().Should().Be("75001");
        data["externalCaseId"]!.GetValue<string>().Should().Be("EXT-456");

        var deleteResponse = await _client.DeleteAsync($"/api/liens/cases/delete/{caseId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await deleteResponse.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task Delete_legacy_case_with_only_rejected_liens_detaches_liens_then_deletes_case()
    {
        Guid caseId;
        Guid lienId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-DELETE-REJECTED-{Guid.CreateVersion7():N}"[..30],
                "Maria",
                "Lopez",
                SeedHelper.UserId);
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-DELETE-REJECTED-{Guid.CreateVersion7():N}"[..30],
                LienType.MedicalLien,
                1000m,
                SeedHelper.UserId,
                caseId: caseEntity.Id);
            lien.SetLegacyMedicalStatus("Rejected", SeedHelper.UserId);

            caseId = caseEntity.Id;
            lienId = lien.Id;
            db.Cases.Add(caseEntity);
            db.Liens.Add(lien);
            await db.SaveChangesAsync();
        }

        var deleteResponse = await _client.DeleteAsync($"/api/liens/cases/delete/{caseId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await deleteResponse.Content.ReadAsStringAsync()}");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        (await verifyDb.Cases.FindAsync(caseId)).Should().BeNull();

        var storedLien = await verifyDb.Liens.SingleAsync(l => l.Id == lienId);
        storedLien.Status.Should().Be(LienStatus.Cancelled);
        storedLien.CaseId.Should().BeNull();
    }

    [Fact]
    public async Task Case_other_metadata_roundtrips_through_legacy_routes()
    {
        var updateResponse = await _client.PostAsJsonAsync("/api/liens/cases/update-other", new
        {
            caseId = SeedHelper.CaseId,
            reductionsRate = "10",
            payment = "1500.25",
            adjustments = "100.50",
            reductionsDate = "06/20/2026",
            netProfit = "999.99",
            checkNumber = "CHK-100",
            netOutboundCheckNumber = "OUT-100",
            bulkPurchase = "Yes",
            bank = "Legacy Bank",
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await updateResponse.Content.ReadAsStringAsync()}");

        var getResponse = await _client.GetAsync($"/api/liens/cases/get-other/{SeedHelper.CaseId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await getResponse.Content.ReadAsStringAsync()}");

        var body = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync())!;
        var data = body["data"]!;
        data["reductionsRate"]!.GetValue<string>().Should().Be("10");
        data["payment"]!.GetValue<string>().Should().Be("1500.25");
        data["adjustments"]!.GetValue<string>().Should().Be("100.50");
        data["netProfit"]!.GetValue<string>().Should().Be("999.99");
        data["bank"]!.GetValue<string>().Should().Be("Legacy Bank");
    }

    [Fact]
    public async Task Payee_outbound_roundtrips_through_legacy_routes()
    {
        var updateResponse = await _client.PostAsJsonAsync("/api/liens/cases/liens/payment", new
        {
            liensId = SeedHelper.LienId,
            payee = "Legacy Payee",
            outboundCheckNumber = "OB-9001",
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await updateResponse.Content.ReadAsStringAsync()}");

        var getResponse = await _client.GetAsync($"/api/liens/cases/liens/get-payee-outbound/{SeedHelper.LienId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await getResponse.Content.ReadAsStringAsync()}");

        var body = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync())!;
        var data = body["data"]!;
        data["liensId"]!.GetValue<string>().Should().Be(SeedHelper.LienId.ToString());
        data["payee"]!.GetValue<string>().Should().Be("Legacy Payee");
        data["outboundCheckNumber"]!.GetValue<string>().Should().Be("OB-9001");
    }

    [Fact]
    public async Task Legacy_notes_routes_keep_case_notes_and_feed_notes_in_separate_streams()
    {
        var addResponse = await _client.PostAsJsonAsync("/api/liens/cases/add-note", new
        {
            caseId = SeedHelper.CaseId,
            note = "Matrix note coverage",
        });
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await addResponse.Content.ReadAsStringAsync()}");

        var detailsUpdateResponse = await _client.PatchAsJsonAsync("/api/liens/cases/details-update", new
        {
            caseId = SeedHelper.CaseId,
            notes = "Details tab note coverage",
        });
        detailsUpdateResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await detailsUpdateResponse.Content.ReadAsStringAsync()}");

        var listResponse = await _client.GetAsync($"/api/liens/cases/notes/{SeedHelper.CaseId}");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await listResponse.Content.ReadAsStringAsync()}");

        var filteredResponse = await _client.PostAsJsonAsync("/api/liens/cases/get-notes", new
        {
            caseId = SeedHelper.CaseId,
            showDeleted = "false",
            sort = "newest",
        });
        filteredResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await filteredResponse.Content.ReadAsStringAsync()}");

        var listBody = JsonNode.Parse(await listResponse.Content.ReadAsStringAsync())!;
        listBody["data"]!.AsArray().Should().Contain(item =>
            item!["note"]!.GetValue<string>() == "Details tab note coverage");
        listBody["data"]!.AsArray().Should().NotContain(item =>
            item!["note"]!.GetValue<string>() == "Matrix note coverage");
        var detailsNote = listBody["data"]!.AsArray()
            .Single(item => item!["note"]!.GetValue<string>() == "Details tab note coverage")!
            .AsObject();
        detailsNote["createdAtUtc"]!.GetValue<string>().Should().EndWith("Z");

        var filteredBody = JsonNode.Parse(await filteredResponse.Content.ReadAsStringAsync())!;
        filteredBody["data"]!.AsArray().Should().Contain(item =>
            item!["note"]!.GetValue<string>() == "Matrix note coverage");
        filteredBody["data"]!.AsArray().Should().NotContain(item =>
            item!["note"]!.GetValue<string>() == "Details tab note coverage");
        var feedNote = filteredBody["data"]!.AsArray()
            .Single(item => item!["note"]!.GetValue<string>() == "Matrix note coverage")!
            .AsObject();
        feedNote["createdAtUtc"]!.GetValue<string>().Should().EndWith("Z");
    }

    [Fact]
    public async Task Service_update_details_and_lien_status_legacy_routes_apply_changes()
    {
        var updateDetailsResponse = await _client.PatchAsJsonAsync("/service/update-details", new
        {
            caseId = SeedHelper.CaseId,
            caseStatusId = "Closed",
            isUCCFiled = "Y",
            switchedDate = "06/10/2026",
            attorney = "Legacy Counsel",
        });
        updateDetailsResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await updateDetailsResponse.Content.ReadAsStringAsync()}");

        var lienStatusResponse = await _client.PatchAsJsonAsync("/service/liens/update/status", new
        {
            caseId = SeedHelper.CaseId,
            liensId = SeedHelper.LienId,
            statusId = "Active",
        });
        lienStatusResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await lienStatusResponse.Content.ReadAsStringAsync()}");

        var bulkStatusResponse = await _client.PostAsJsonAsync("/service/update-liens-status", new
        {
            caseId = SeedHelper.CaseId,
            lienIds = SeedHelper.LienId.ToString(),
            lienStatus = "Settled",
            closedDate = "06/11/2026",
            note = "closed",
        });
        bulkStatusResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await bulkStatusResponse.Content.ReadAsStringAsync()}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var caseEntity = await db.Cases.FindAsync(SeedHelper.CaseId);
        caseEntity.Should().NotBeNull();
        caseEntity!.Status.Should().Be("Closed");
        caseEntity.Notes.Should().Contain("isUCCFiled=Y");
        caseEntity.Notes.Should().Contain("switchedDate=06/10/2026");
        caseEntity.Notes.Should().Contain("attorney=Legacy Counsel");

        var lien = await db.Liens.FindAsync(SeedHelper.LienId);
        lien.Should().NotBeNull();
        lien!.Status.Should().Be("Settled");
    }

    [Fact]
    public async Task Service_generate_csv_returns_export_payload()
    {
        var response = await _client.PostAsJsonAsync("/service/generate-csv", new
        {
            caseId = SeedHelper.CaseId,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("data")[0].GetProperty("base64").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
