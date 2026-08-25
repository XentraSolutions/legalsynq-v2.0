using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class LegacyReportEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public LegacyReportEndpointTests(LiensApiFactory factory) => _factory = factory;

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

    // ── GET /report/diy ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetSavedReports_returns200_with_seeded_report()
    {
        var resp = await _client.GetAsync("/report/diy");
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var list = await resp.Content.ReadFromJsonAsync<List<JsonElement>>();
        list.Should().NotBeNull();
        list!.Should().ContainSingle(r => r.GetProperty("id").GetGuid() == SeedHelper.ReportConfigId);
    }

    // ── POST /report/diy (run) ────────────────────────────────────────────────

    [Fact]
    public async Task RunReport_returns200_with_paginated_result()
    {
        var resp = await _client.PostAsJsonAsync("/report/diy", new
        {
            config = new { status = "Open" },
            page   = 1,
            limit  = 10,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("message").GetString().Should().Be("Liens report generated.");
        doc.RootElement.TryGetProperty("summaryTotals", out var summary).Should().BeTrue();
        summary.TryGetProperty("totalLiens", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("data", out var data).Should().BeTrue();
        data.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetProperty("page").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("limit").GetInt32().Should().Be(10);
        doc.RootElement.TryGetProperty("totalCount", out _).Should().BeTrue();

        if (data.GetArrayLength() > 0)
        {
            var row = data[0];
            row.TryGetProperty("plaintiff_first_name", out _).Should().BeTrue();
            row.TryGetProperty("plaintiff_last_name", out _).Should().BeTrue();
            row.TryGetProperty("case_id", out _).Should().BeTrue();
            row.TryGetProperty("lien_id", out _).Should().BeTrue();
            row.TryGetProperty("purchase_amt", out _).Should().BeTrue();
            row.TryGetProperty("billing_amt", out _).Should().BeTrue();
            row.TryGetProperty("case_status", out _).Should().BeTrue();
            row.TryGetProperty("date_of_loss", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task RunReport_treats_the_ui_N_bulk_sentinel_as_no_filter()
    {
        string nonBulkLienNumber;
        string bulkLienNumber;
        var prefix = $"LIEN-DIY-BULK-SCOPE-{Guid.CreateVersion7():N}"[..36];

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-DIY-NON-BULK-{Guid.CreateVersion7():N}"[..30],
                "New",
                "Plaintiff",
                SeedHelper.UserId);
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"{prefix}-N",
                LienType.MedicalLien,
                1000m,
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                isBulk: "N");
            var bulkLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"{prefix}-Y",
                LienType.MedicalLien,
                2000m,
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                isBulk: "Y");

            nonBulkLienNumber = lien.LienNumber;
            bulkLienNumber = bulkLien.LienNumber;
            db.Cases.Add(caseEntity);
            db.Liens.AddRange(lien, bulkLien);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportType = "LIENS",
            statusView = "",
            search = prefix,
            lienStatusIds = Array.Empty<string>(),
            isBulk = "N",
            columns = new[] { "case_id", "lien_id" },
            page = 1,
            limit = 50,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("totalCount").GetInt32().Should().Be(2);
        payload.RootElement.GetProperty("data").EnumerateArray()
            .Select(row => row.GetProperty("lien_id").GetString())
            .Should().BeEquivalentTo(nonBulkLienNumber, bulkLienNumber);

        var bulkOnlyResponse = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportType = "LIENS",
            statusView = "",
            search = prefix,
            lienStatusIds = Array.Empty<string>(),
            isBulk = "Y",
            columns = new[] { "case_id", "lien_id" },
            page = 1,
            limit = 50,
        });
        bulkOnlyResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await bulkOnlyResponse.Content.ReadAsStringAsync()}");

        using var bulkOnlyPayload = JsonDocument.Parse(await bulkOnlyResponse.Content.ReadAsStringAsync());
        bulkOnlyPayload.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
        bulkOnlyPayload.RootElement.GetProperty("data").EnumerateArray()
            .Single().GetProperty("lien_id").GetString().Should().Be(bulkLienNumber);

        var nonBulkOnlyResponse = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportType = "LIENS",
            statusView = "",
            search = prefix,
            lienStatusIds = Array.Empty<string>(),
            isBulk = "No",
            columns = new[] { "case_id", "lien_id" },
            page = 1,
            limit = 50,
        });
        nonBulkOnlyResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await nonBulkOnlyResponse.Content.ReadAsStringAsync()}");

        using var nonBulkOnlyPayload = JsonDocument.Parse(await nonBulkOnlyResponse.Content.ReadAsStringAsync());
        nonBulkOnlyPayload.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
        nonBulkOnlyPayload.RootElement.GetProperty("data").EnumerateArray()
            .Single().GetProperty("lien_id").GetString().Should().Be(nonBulkLienNumber);
    }

    [Fact]
    public async Task RunReport_returns_null_days_since_reduction_approval_when_no_reduction_exists()
    {
        string lienNumber;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-DIY-NO-REDUCTION-{Guid.CreateVersion7():N}"[..30],
                "NoReduction",
                "Plaintiff",
                SeedHelper.UserId);
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-DIY-NO-REDUCTION-{Guid.CreateVersion7():N}"[..30],
                LienType.MedicalLien,
                1000m,
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                isBulk: "N");

            lienNumber = lien.LienNumber;
            db.Cases.Add(caseEntity);
            db.Liens.Add(lien);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportType = "LIENS",
            search = lienNumber,
            isBulk = "N",
            columns = new[] { "lien_id", "days_since_reduction_approval" },
            page = 1,
            limit = 50,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = payload.RootElement.GetProperty("data").EnumerateArray()
            .Single(item => item.GetProperty("lien_id").GetString() == lienNumber);
        row.GetProperty("days_since_reduction_approval").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task RunReport_uses_legacy_medical_code_billing_and_purchase_amounts()
    {
        string lienNumber;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-DIY-AMOUNTS-{Guid.CreateVersion7():N}"[..30],
                "Amount",
                "Plaintiff",
                SeedHelper.UserId);
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-DIY-AMOUNTS-{Guid.CreateVersion7():N}"[..30],
                LienType.MedicalLien,
                1000m,
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                isBulk: "N");
            var medicalCode = ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LMC-DIY-AMOUNTS-{Guid.CreateVersion7():N}"[..40],
                "LegacyMedicalCode",
                "Medical code amount entry",
                "system",
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                lienId: lien.Id,
                notes: "billingAmount=600.75; purchaseAmount=275.50");

            lienNumber = lien.LienNumber;
            db.Cases.Add(caseEntity);
            db.Liens.Add(lien);
            db.ServicingItems.Add(medicalCode);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportType = "LIENS",
            isBulk = "N",
            columns = new[] { "lien_id", "purchase_amt", "billing_amt" },
            page = 1,
            limit = 50,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = payload.RootElement.GetProperty("data").EnumerateArray()
            .Single(item => item.GetProperty("lien_id").GetString() == lienNumber);
        row.GetProperty("purchase_amt").GetString().Should().Be("275.50");
        row.GetProperty("billing_amt").GetString().Should().Be("600.75");
        payload.RootElement.GetProperty("summaryTotals").GetProperty("totalPurchaseAmt").GetDecimal()
            .Should().BeGreaterThanOrEqualTo(275.50m);
        payload.RootElement.GetProperty("summaryTotals").GetProperty("totalBillingAmt").GetDecimal()
            .Should().BeGreaterThanOrEqualTo(600.75m);
    }

    [Fact]
    public async Task RunReport_populates_data_backed_legacy_columns()
    {
        string lienNumber;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseManager = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                ContactType.CaseManager,
                "Case",
                "Manager",
                SeedHelper.UserId,
                lawFirmId: SeedHelper.LawFirmId,
                organization: "Smith & Associates LLP");
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-DIY-DATA-{Guid.CreateVersion7():N}"[..30],
                "Data",
                "Plaintiff",
                SeedHelper.UserId,
                dateOfIncident: new DateOnly(2024, 6, 15),
                notes: $"lawFirmId={SeedHelper.LawFirmId}; caseManagerId={caseManager.Id}; accidentType=Motor Vehicle Accident");
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-DIY-DATA-{Guid.CreateVersion7():N}"[..30],
                LienType.MedicalLien,
                2000m,
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                facilityId: SeedHelper.FacilityId,
                incidentDate: new DateOnly(2024, 6, 15),
                isBulk: "N",
                purchaseDate: new DateOnly(2024, 6, 15));
            var reduction = LienReduction.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                lien.Id,
                new DateOnly(2025, 1, 10),
                200m,
                SeedHelper.UserId);
            var returnedPayment = SettlementPaymentDetail.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                lien.Id,
                1,
                1234.56m,
                SeedHelper.UserId,
                paymentDate: new DateOnly(2025, 2, 1));

            lien.SetLegacyMedicalStatus(LienStatus.Settled, SeedHelper.UserId);

            lienNumber = lien.LienNumber;
            db.Contacts.Add(caseManager);
            db.Cases.Add(caseEntity);
            db.Liens.Add(lien);
            db.LienReductions.Add(reduction);
            db.SettlementPaymentDetails.Add(returnedPayment);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportType = "LIENS",
            search = lienNumber,
            isBulk = "N",
            columns = new[]
            {
                "lien_id",
                "purchase_date",
                "returned_amount",
                "days_since_reduction_approval",
                "medical_facility",
                "lawfirm",
                "case_type",
                "case_manager",
            },
            page = 1,
            limit = 50,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = payload.RootElement.GetProperty("data").EnumerateArray()
            .Single(item => item.GetProperty("lien_id").GetString() == lienNumber);
        row.GetProperty("purchase_date").GetString().Should().Be("06/15/2024");
        row.GetProperty("returned_amount").GetString().Should().Be("1,234.56");
        int.Parse(row.GetProperty("days_since_reduction_approval").GetString()!)
            .Should().Be(22);
        row.GetProperty("medical_facility").GetString().Should().Be("Sunrise Clinic");
        row.GetProperty("lawfirm").GetString().Should().Be("Smith & Associates LLP");
        row.GetProperty("case_type").GetString().Should().Be("Motor Vehicle Accident");
        row.GetProperty("case_manager").GetString().Should().Be("Case Manager");
    }

    [Fact]
    public async Task RunReport_does_not_use_settlement_date_when_reduction_date_is_missing()
    {
        string lienNumber;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-DIY-REDUCTION-DAYS-{Guid.CreateVersion7():N}"[..30],
                "LegacyReduction",
                "Plaintiff",
                SeedHelper.UserId);
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-DIY-REDUCTION-DAYS-{Guid.CreateVersion7():N}"[..36],
                LienType.MedicalLien,
                300m,
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                isBulk: "N",
                purchaseDate: new DateOnly(2025, 1, 1));
            var settlement = LienSettlement.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                lien.Id,
                1,
                180m,
                SeedHelper.UserId,
                status: "Settled",
                note: "reductionAmount=20; reductionDate=; totalSettledAmount=150",
                settlementDate: new DateOnly(2025, 1, 10));
            var payment = SettlementPaymentDetail.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                lien.Id,
                1,
                150m,
                SeedHelper.UserId,
                paymentDate: new DateOnly(2025, 2, 1));

            lien.SetLegacyMedicalStatus(LienStatus.Settled, SeedHelper.UserId);
            lienNumber = lien.LienNumber;
            db.Cases.Add(caseEntity);
            db.Liens.Add(lien);
            db.LienSettlements.Add(settlement);
            db.SettlementPaymentDetails.Add(payment);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportType = "LIENS",
            statusView = "ALL",
            search = lienNumber,
            isBulk = "N",
            columns = new[]
            {
                "lien_id",
                "reduction_date",
                "days_since_reduction_approval",
            },
            page = 1,
            limit = 50,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = payload.RootElement.GetProperty("data").EnumerateArray()
            .Single(item => item.GetProperty("lien_id").GetString() == lienNumber);
        row.GetProperty("reduction_date").ValueKind.Should().Be(JsonValueKind.Null);
        row.GetProperty("days_since_reduction_approval").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task RunReport_prefers_canonical_reduction_over_legacy_settlement_metadata()
    {
        string lienNumber;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-DIY-REDUCTION-SOURCE-{Guid.CreateVersion7():N}"[..30],
                "CanonicalReduction",
                "Plaintiff",
                SeedHelper.UserId);
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-DIY-REDUCTION-SOURCE-{Guid.CreateVersion7():N}"[..36],
                LienType.MedicalLien,
                300m,
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                isBulk: "N");
            var settlement = LienSettlement.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                lien.Id,
                1,
                180m,
                SeedHelper.UserId,
                status: "Settled",
                note: "reductionAmount=20; reductionDate=2025-01-10; totalSettledAmount=150",
                settlementDate: new DateOnly(2025, 1, 10));
            var reduction = LienReduction.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                lien.Id,
                new DateOnly(2025, 1, 20),
                75m,
                SeedHelper.UserId);
            var payment = SettlementPaymentDetail.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                lien.Id,
                1,
                150m,
                SeedHelper.UserId,
                paymentDate: new DateOnly(2025, 2, 1));

            lien.SetLegacyMedicalStatus(LienStatus.Settled, SeedHelper.UserId);
            lienNumber = lien.LienNumber;
            db.Cases.Add(caseEntity);
            db.Liens.Add(lien);
            db.LienSettlements.Add(settlement);
            db.LienReductions.Add(reduction);
            db.SettlementPaymentDetails.Add(payment);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportType = "LIENS",
            statusView = "ALL",
            search = lienNumber,
            isBulk = "N",
            columns = new[]
            {
                "lien_id",
                "reduction",
                "reduction_date",
                "days_since_reduction_approval",
            },
            page = 1,
            limit = 50,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = payload.RootElement.GetProperty("data").EnumerateArray()
            .Single(item => item.GetProperty("lien_id").GetString() == lienNumber);
        row.GetProperty("reduction").GetString().Should().Be("75.00");
        row.GetProperty("reduction_date").GetString().Should().Be("01/20/2025");
        row.GetProperty("days_since_reduction_approval").GetString().Should().Be("12");
    }

    [Fact]
    public async Task RunAndExportReport_return_ucc_filed_as_yes_or_no()
    {
        var prefix = $"DIY-UCC-{Guid.CreateVersion7():N}"[..30];
        var yesLienNumber = $"{prefix}-YES";
        var noLienNumber = $"{prefix}-NO";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var yesCase = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"{prefix}-CASE-YES",
                "Ucc",
                "Filed",
                SeedHelper.UserId,
                notes: "[legacy-meta]\nisUCCFiled=true");
            var noCase = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"{prefix}-CASE-NO",
                "Ucc",
                "Not Filed",
                SeedHelper.UserId);
            var yesLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                yesLienNumber,
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                caseId: yesCase.Id,
                isBulk: "N");
            var noLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                noLienNumber,
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                caseId: noCase.Id,
                isBulk: "N");

            db.Cases.AddRange(yesCase, noCase);
            db.Liens.AddRange(yesLien, noLien);
            await db.SaveChangesAsync();
        }

        var request = new
        {
            reportType = "LIENS",
            statusView = "ALL",
            search = prefix,
            isBulk = "N",
            columns = new[] { "lien_id", "ucc_filed" },
            page = 1,
            limit = 50,
        };

        var runResponse = await _client.PostAsJsonAsync("/report/diy", request);
        runResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await runResponse.Content.ReadAsStringAsync()}");
        using var runPayload = JsonDocument.Parse(await runResponse.Content.ReadAsStringAsync());
        var rows = runPayload.RootElement.GetProperty("data").EnumerateArray()
            .ToDictionary(row => row.GetProperty("lien_id").GetString()!);
        rows[yesLienNumber].GetProperty("ucc_filed").GetString().Should().Be("Yes");
        rows[noLienNumber].GetProperty("ucc_filed").GetString().Should().Be("No");

        var exportResponse = await _client.PostAsJsonAsync("/report/diy/export", request);
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await exportResponse.Content.ReadAsStringAsync()}");
        var exportPayload = await exportResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var export = exportPayload!.RootElement.GetProperty("data").EnumerateArray().Single();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(export.GetProperty("base64").GetString()!));
        csv.Should().Contain("lien_id,ucc_filed");
        csv.Should().Contain($"{yesLienNumber},Yes");
        csv.Should().Contain($"{noLienNumber},No");
    }

    [Fact]
    public async Task RunAndExportReport_include_all_tracking_notes_and_latest_date()
    {
        string lienNumber;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-DIY-NOTES-{Guid.CreateVersion7():N}"[..30],
                "Tracking",
                "Notes",
                SeedHelper.UserId);
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-DIY-NOTES-{Guid.CreateVersion7():N}"[..30],
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                isBulk: "N");

            var olderTracking = CreateCaseNote(
                caseEntity.Id,
                SeedHelper.TenantId,
                "Older follow-up note",
                CaseNoteCategory.FollowUp,
                new DateTime(2026, 8, 10, 8, 0, 0, DateTimeKind.Utc));
            var newestTracking = CreateCaseNote(
                caseEntity.Id,
                SeedHelper.TenantId,
                "  =SUM(1,1) newest tracking note",
                CaseNoteCategory.General,
                new DateTime(2026, 8, 12, 9, 0, 0, DateTimeKind.Utc));
            var feedNote = CreateCaseNote(
                caseEntity.Id,
                SeedHelper.TenantId,
                "Feed note must be excluded",
                CaseNoteCategory.Feed,
                new DateTime(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc));
            var deletedTracking = CreateCaseNote(
                caseEntity.Id,
                SeedHelper.TenantId,
                "Deleted note must be excluded",
                CaseNoteCategory.General,
                new DateTime(2026, 8, 14, 9, 0, 0, DateTimeKind.Utc));
            deletedTracking.SoftDelete();
            var otherTenantNote = CreateCaseNote(
                caseEntity.Id,
                Guid.CreateVersion7(),
                "Other tenant note must be excluded",
                CaseNoteCategory.General,
                new DateTime(2026, 8, 15, 9, 0, 0, DateTimeKind.Utc));

            lienNumber = lien.LienNumber;
            db.Cases.Add(caseEntity);
            db.Liens.Add(lien);
            db.LienCaseNotes.AddRange(
                olderTracking,
                newestTracking,
                feedNote,
                deletedTracking,
                otherTenantNote);
            await db.SaveChangesAsync();
        }

        var request = new
        {
            reportType = "LIENS",
            search = lienNumber,
            isBulk = "N",
            columns = new[] { "lien_id", "last_case_note", "last_case_note_date" },
            page = 1,
            limit = 50,
        };

        var runResponse = await _client.PostAsJsonAsync("/report/diy", request);
        runResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await runResponse.Content.ReadAsStringAsync()}");
        using var runPayload = JsonDocument.Parse(await runResponse.Content.ReadAsStringAsync());
        var row = runPayload.RootElement.GetProperty("data").EnumerateArray()
            .Single(item => item.GetProperty("lien_id").GetString() == lienNumber);
        row.GetProperty("last_case_note").GetString()
            .Should().Be("=SUM(1,1) newest tracking note\nOlder follow-up note");
        row.GetProperty("last_case_note_date").GetString().Should().Be("08/12/2026");

        var exportResponse = await _client.PostAsJsonAsync("/report/diy/export", request);
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await exportResponse.Content.ReadAsStringAsync()}");
        var exportPayload = await exportResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var export = exportPayload!.RootElement.GetProperty("data").EnumerateArray().Single();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(export.GetProperty("base64").GetString()!));
        csv.Should().Contain("\"'=SUM(1,1) newest tracking note\nOlder follow-up note\"");
        csv.Should().NotContain("\"=SUM(1,1) newest tracking note");
        csv.Should().Contain("08/12/2026");
        csv.Should().NotContain("Feed note must be excluded");
        csv.Should().NotContain("Deleted note must be excluded");
        csv.Should().NotContain("Other tenant note must be excluded");
    }

    [Fact]
    public async Task RunAndExportReport_return_latest_eligible_feed_note_for_lien_and_combined_rows()
    {
        var prefix = $"DIY-FEED-{Guid.CreateVersion7():N}"[..30];
        var expectedDate = new DateTime(2026, 8, 14, 9, 30, 0, DateTimeKind.Utc);
        string lienNumber;
        string noNoteLienNumber;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"{prefix}-CASE",
                "Feed",
                "Selected",
                SeedHelper.UserId);
            var noNoteCase = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"{prefix}-EMPTY-CASE",
                "Feed",
                "Empty",
                SeedHelper.UserId);
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"{prefix}-LIEN",
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                isBulk: "N");
            var noNoteLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"{prefix}-EMPTY-LIEN",
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                caseId: noNoteCase.Id,
                isBulk: "N");

            var tiedLoser = CreateCaseNote(
                caseEntity.Id,
                SeedHelper.TenantId,
                "Same-time lower-id Feed note",
                CaseNoteCategory.Feed,
                expectedDate);
            SetCaseNoteProperty(tiedLoser, nameof(LienCaseNote.Id), Guid.Parse("11111111-1111-1111-1111-111111111111"));
            var tiedWinner = CreateCaseNote(
                caseEntity.Id,
                SeedHelper.TenantId,
                "=1+1 selected Feed note",
                "Feed",
                expectedDate);
            SetCaseNoteProperty(tiedWinner, nameof(LienCaseNote.Id), Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
            var deletedFeed = CreateCaseNote(
                caseEntity.Id,
                SeedHelper.TenantId,
                "Deleted Feed note",
                CaseNoteCategory.Feed,
                expectedDate.AddDays(1));
            deletedFeed.SoftDelete();
            var blankFeed = CreateCaseNote(
                caseEntity.Id,
                SeedHelper.TenantId,
                "placeholder",
                CaseNoteCategory.Feed,
                expectedDate.AddDays(2));
            SetCaseNoteProperty(blankFeed, nameof(LienCaseNote.Content), "   ");
            var generalNote = CreateCaseNote(
                caseEntity.Id,
                SeedHelper.TenantId,
                "General note",
                CaseNoteCategory.General,
                expectedDate.AddDays(3));
            var otherTenantFeed = CreateCaseNote(
                caseEntity.Id,
                Guid.CreateVersion7(),
                "Other tenant Feed note",
                CaseNoteCategory.Feed,
                expectedDate.AddDays(4));

            lienNumber = lien.LienNumber;
            noNoteLienNumber = noNoteLien.LienNumber;
            db.Cases.AddRange(caseEntity, noNoteCase);
            db.Liens.AddRange(lien, noNoteLien);
            db.LienCaseNotes.AddRange(
                tiedLoser,
                tiedWinner,
                deletedFeed,
                blankFeed,
                generalNote,
                otherTenantFeed);
            await db.SaveChangesAsync();
        }

        object Request(string reportType) => new
        {
            reportType,
            statusView = "ALL",
            search = prefix,
            isBulk = "N",
            columns = new[] { "lien_id", "notes", "notes_date" },
            page = 1,
            limit = 50,
        };

        foreach (var reportType in new[] { "LIENS", "COMBINED" })
        {
            var runResponse = await _client.PostAsJsonAsync("/report/diy", Request(reportType));
            runResponse.StatusCode.Should().Be(HttpStatusCode.OK,
                $"Body: {await runResponse.Content.ReadAsStringAsync()}");
            using var runPayload = JsonDocument.Parse(await runResponse.Content.ReadAsStringAsync());
            var rows = runPayload.RootElement.GetProperty("data").EnumerateArray()
                .ToDictionary(row => row.GetProperty("lien_id").GetString()!);
            rows[lienNumber].GetProperty("notes").GetString().Should().Be("=1+1 selected Feed note");
            rows[lienNumber].GetProperty("notes_date").GetString().Should().Be("08/14/2026");
            rows[noNoteLienNumber].GetProperty("notes").GetString().Should().BeEmpty();
            rows[noNoteLienNumber].GetProperty("notes_date").ValueKind.Should().Be(JsonValueKind.Null);
        }

        var exportResponse = await _client.PostAsJsonAsync("/report/diy/export", Request("LIENS"));
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await exportResponse.Content.ReadAsStringAsync()}");
        var exportPayload = await exportResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var export = exportPayload!.RootElement.GetProperty("data").EnumerateArray().Single();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(export.GetProperty("base64").GetString()!));
        csv.Should().StartWith("lien_id,notes,notes_date");
        csv.Should().Contain($"{lienNumber},'=1+1 selected Feed note,08/14/2026");
        csv.Should().NotContain("Same-time lower-id Feed note");
        csv.Should().NotContain("Deleted Feed note");
        csv.Should().NotContain("General note");
        csv.Should().NotContain("Other tenant Feed note");
    }

    [Fact]
    public async Task SavedCaseReport_returns_feed_note_for_unlinked_case_in_preview_and_export()
    {
        var caseNumber = $"DIY-FEED-CASE-{Guid.CreateVersion7():N}"[..32];
        Guid reportId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                caseNumber,
                "Unlinked",
                "Feed",
                SeedHelper.UserId);
            var feedNote = CreateCaseNote(
                caseEntity.Id,
                SeedHelper.TenantId,
                "Unlinked case Feed note",
                CaseNoteCategory.Feed,
                new DateTime(2026, 8, 15, 11, 0, 0, DateTimeKind.Utc));
            var savedReport = DIYReportConfig.Create(
                SeedHelper.TenantId,
                SeedHelper.UserId,
                "Saved unlinked Feed report",
                $$"""{"reportType":"CASES","search":"{{caseNumber}}","isBulk":"N","columns":["case_id","notes","notes_date"]}""",
                SeedHelper.UserId);

            reportId = savedReport.Id;
            db.Cases.Add(caseEntity);
            db.LienCaseNotes.Add(feedNote);
            db.DIYReportConfigs.Add(savedReport);
            await db.SaveChangesAsync();
        }

        var runResponse = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportId,
            page = 1,
            limit = 10,
        });
        runResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await runResponse.Content.ReadAsStringAsync()}");
        using var runPayload = JsonDocument.Parse(await runResponse.Content.ReadAsStringAsync());
        var row = runPayload.RootElement.GetProperty("data").EnumerateArray().Single();
        row.GetProperty("case_id").GetString().Should().Be(caseNumber);
        row.GetProperty("notes").GetString().Should().Be("Unlinked case Feed note");
        row.GetProperty("notes_date").GetString().Should().Be("08/15/2026");

        var exportResponse = await _client.PostAsJsonAsync("/report/diy/export", new { reportId });
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await exportResponse.Content.ReadAsStringAsync()}");
        var exportPayload = await exportResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var export = exportPayload!.RootElement.GetProperty("data").EnumerateArray().Single();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(export.GetProperty("base64").GetString()!));
        csv.Should().StartWith("case_id,notes,notes_date");
        csv.Should().Contain($"{caseNumber},Unlinked case Feed note,08/15/2026");
    }

    [Fact]
    public async Task RunReport_and_export_include_rejected_and_cancelled_liens_for_all_status_view()
    {
        var prefix = $"LIEN-DIY-EXCLUDED-{Guid.CreateVersion7():N}"[..36];
        var openLienNumber = $"{prefix}-OPEN";
        var rejectedLienNumber = $"{prefix}-REJECTED";
        var cancelledLienNumber = $"{prefix}-CANCELLED";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var openLien = Lien.Create(
                SeedHelper.TenantId, SeedHelper.OrgId, openLienNumber, LienType.MedicalLien,
                100m, SeedHelper.UserId, isBulk: "N");
            var rejectedLien = Lien.Create(
                SeedHelper.TenantId, SeedHelper.OrgId, rejectedLienNumber, LienType.MedicalLien,
                100m, SeedHelper.UserId, isBulk: "N");
            rejectedLien.SetLegacyMedicalStatus(LienStatus.Declined, SeedHelper.UserId);
            var cancelledLien = Lien.Create(
                SeedHelper.TenantId, SeedHelper.OrgId, cancelledLienNumber, LienType.MedicalLien,
                100m, SeedHelper.UserId, isBulk: "N");
            cancelledLien.SetLegacyMedicalStatus(LienStatus.Cancelled, SeedHelper.UserId);

            db.Liens.AddRange(openLien, rejectedLien, cancelledLien);
            await db.SaveChangesAsync();
        }

        var request = new
        {
            reportType = "LIENS",
            statusView = "ALL",
            search = prefix,
            isBulk = "N",
            columns = new[] { "lien_id" },
            page = 1,
            limit = 50,
        };

        var runResponse = await _client.PostAsJsonAsync("/report/diy", request);
        runResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await runResponse.Content.ReadAsStringAsync()}");

        using var runPayload = JsonDocument.Parse(await runResponse.Content.ReadAsStringAsync());
        var previewLienNumbers = runPayload.RootElement.GetProperty("data").EnumerateArray()
            .Select(row => row.GetProperty("lien_id").GetString())
            .ToList();
        previewLienNumbers.Should().BeEquivalentTo(
            openLienNumber,
            rejectedLienNumber,
            cancelledLienNumber);

        var exportResponse = await _client.PostAsJsonAsync("/report/diy/export", request);
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await exportResponse.Content.ReadAsStringAsync()}");

        var exportPayload = await exportResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var export = exportPayload!.RootElement.GetProperty("data").EnumerateArray().Single();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(export.GetProperty("base64").GetString()!));
        csv.Should().Contain(openLienNumber);
        csv.Should().Contain(rejectedLienNumber).And.Contain(cancelledLienNumber);

        var rejectedResponse = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportType = "LIENS",
            statusView = "REJECTED",
            search = prefix,
            isBulk = "N",
            columns = new[] { "lien_id" },
            page = 1,
            limit = 50,
        });
        rejectedResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await rejectedResponse.Content.ReadAsStringAsync()}");

        using var rejectedPayload = JsonDocument.Parse(await rejectedResponse.Content.ReadAsStringAsync());
        var rejectedLienNumbers = rejectedPayload.RootElement.GetProperty("data").EnumerateArray()
            .Select(row => row.GetProperty("lien_id").GetString())
            .ToList();
        rejectedLienNumbers.Should().BeEquivalentTo(rejectedLienNumber, cancelledLienNumber);
        rejectedLienNumbers.Should().NotContain(openLienNumber);
    }

    [Fact]
    public async Task RunReport_matches_legacy_settlement_amounts_and_summary_formulas()
    {
        string lienNumber;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-DIY-SETTLEMENT-{Guid.CreateVersion7():N}"[..30],
                "Settlement",
                "Parity",
                SeedHelper.UserId);
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-DIY-SETTLEMENT-{Guid.CreateVersion7():N}"[..36],
                LienType.MedicalLien,
                300m,
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                isBulk: "N",
                purchaseDate: new DateOnly(2025, 1, 1));
            var medicalCode = ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LMC-DIY-SETTLEMENT-{Guid.CreateVersion7():N}"[..40],
                "LegacyMedicalCode",
                "Medical code amount entry",
                "system",
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                lienId: lien.Id,
                notes: "purchaseAmount=100; billingAmount=300");
            var settlement = LienSettlement.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                lien.Id,
                1,
                180m,
                SeedHelper.UserId,
                status: "Settled",
                note: "reductionAmount=20; reductionDate=01/10/2025; totalSettledAmount=150",
                settlementDate: new DateOnly(2025, 2, 1));

            lien.SetLegacyMedicalStatus(LienStatus.Settled, SeedHelper.UserId);
            lienNumber = lien.LienNumber;
            db.Cases.Add(caseEntity);
            db.Liens.Add(lien);
            db.ServicingItems.Add(medicalCode);
            db.LienSettlements.Add(settlement);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportType = "LIENS",
            statusView = "ALL",
            search = lienNumber,
            isBulk = "N",
            columns = new[]
            {
                "lien_id",
                "remaining_billing_amt",
                "reduction",
                "reduction_percentage",
                "amt_to_settle",
                "returned_amount",
                "gross_profit",
                "roi",
                "settlement_date",
                "reduction_date",
            },
            page = 1,
            limit = 50,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = payload.RootElement.GetProperty("data").EnumerateArray().Single();
        row.GetProperty("lien_id").GetString().Should().Be(lienNumber);
        row.GetProperty("remaining_billing_amt").GetString().Should().Be("200.00");
        row.GetProperty("reduction").GetString().Should().Be("20.00");
        row.GetProperty("reduction_percentage").GetString().Should().Be("6.67");
        row.GetProperty("amt_to_settle").GetString().Should().Be("180.00");
        row.GetProperty("returned_amount").GetString().Should().Be("150.00");
        row.GetProperty("gross_profit").GetString().Should().Be("50.00");
        row.GetProperty("roi").GetString().Should().Be("50.00");
        row.GetProperty("settlement_date").GetString().Should().Be("02/01/2025");
        row.GetProperty("reduction_date").GetString().Should().Be("01/10/2025");

        var summary = payload.RootElement.GetProperty("summaryTotals");
        summary.GetProperty("totalPurchaseAmt").GetDecimal().Should().Be(100m);
        summary.GetProperty("totalBillingAmt").GetDecimal().Should().Be(150m);
        summary.GetProperty("grossBillingAmt").GetDecimal().Should().Be(300m);
        summary.GetProperty("totalAmtToSettle").GetDecimal().Should().Be(150m);
        summary.GetProperty("totalReturnedAmt").GetDecimal().Should().Be(150m);
        summary.GetProperty("totalGrossProfit").GetDecimal().Should().Be(50m);
        summary.GetProperty("avgRoi").GetDecimal().Should().Be(50m);

        var casesResponse = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportType = "CASES",
            statusView = "ALL",
            search = lienNumber,
            closedDateTo = "2025-02-28",
            isBulk = "N",
            columns = new[] { "case_id", "settlement_date" },
            page = 1,
            limit = 50,
        });
        casesResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await casesResponse.Content.ReadAsStringAsync()}");

        using var casesPayload = JsonDocument.Parse(await casesResponse.Content.ReadAsStringAsync());
        casesPayload.RootElement.GetProperty("data").GetArrayLength().Should().Be(1);
        casesPayload.RootElement.GetProperty("data")[0]
            .GetProperty("settlement_date").GetString().Should().Be("02/01/2025");
    }

    [Theory]
    [InlineData(LienStatus.Active, null)]
    [InlineData(LienStatus.Settled, "0.00")]
    public async Task RunReport_preserves_metadata_only_legacy_reduction_and_authoritative_blank_returned_amount(
        string lienStatus,
        string? expectedReturnedAmount)
    {
        string lienNumber;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-DIY-METADATA-SETTLEMENT-{Guid.CreateVersion7():N}"[..30],
                "Settlement",
                "Metadata",
                SeedHelper.UserId);
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-DIY-METADATA-SETTLEMENT-{Guid.CreateVersion7():N}"[..36],
                LienType.MedicalLien,
                300m,
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                isBulk: "N");
            var medicalCode = ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LMC-DIY-METADATA-SETTLEMENT-{Guid.CreateVersion7():N}"[..40],
                "LegacyMedicalCode",
                "Medical code amount entry",
                "system",
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                lienId: lien.Id,
                notes: "purchaseAmount=100; billingAmount=300");
            var settlement = LienSettlement.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                lien.Id,
                1,
                0m,
                SeedHelper.UserId,
                status: "Pending",
                note: "reductionAmount=20; reductionDate=; totalSettledAmount=");
            var payment = SettlementPaymentDetail.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                lien.Id,
                1,
                150m,
                SeedHelper.UserId,
                paymentDate: new DateOnly(2025, 2, 1));

            lien.SetLegacyMedicalStatus(lienStatus, SeedHelper.UserId);
            lienNumber = lien.LienNumber;
            db.Cases.Add(caseEntity);
            db.Liens.Add(lien);
            db.ServicingItems.Add(medicalCode);
            db.LienSettlements.Add(settlement);
            db.SettlementPaymentDetails.Add(payment);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportType = "LIENS",
            statusView = "ALL",
            search = lienNumber,
            isBulk = "N",
            columns = new[]
            {
                "lien_id",
                "reduction",
                "expected_settlement_amount",
                "amt_to_settle",
                "returned_amount",
                "settled_amt",
                "gross_profit",
            },
            page = 1,
            limit = 50,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = payload.RootElement.GetProperty("data").EnumerateArray().Single();
        row.GetProperty("lien_id").GetString().Should().Be(lienNumber);
        row.GetProperty("reduction").GetString().Should().Be("20.00");
        row.GetProperty("expected_settlement_amount").GetString().Should().Be("200.00");
        row.GetProperty("amt_to_settle").GetString().Should().Be("0.00");
        row.GetProperty("returned_amount").GetString().Should().Be(expectedReturnedAmount);
        row.GetProperty("settled_amt").GetString().Should().Be("0.00");
        row.GetProperty("gross_profit").GetString().Should().Be("-100.00");

        var summary = payload.RootElement.GetProperty("summaryTotals");
        summary.GetProperty("totalPurchaseAmt").GetDecimal().Should().Be(100m);
        summary.GetProperty("totalBillingAmt").GetDecimal().Should().Be(300m);
        summary.GetProperty("totalAmtToSettle").GetDecimal().Should().Be(300m);
        summary.GetProperty("totalReturnedAmt").GetDecimal().Should().Be(0m);
        summary.GetProperty("totalGrossProfit").GetDecimal().Should().Be(-100m);
        summary.GetProperty("avgRoi").GetDecimal().Should().Be(-100m);
    }

    [Fact]
    public async Task RunReport_applies_legacy_relationship_filters_before_paging_and_summary()
    {
        var selectedLawFirmId = Guid.CreateVersion7();
        var otherLawFirmId = Guid.CreateVersion7();
        var prefix = $"LIEN-DIY-LAWFIRM-{Guid.CreateVersion7():N}"[..34];
        string selectedLienNumber;
        string otherLienNumber;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var selectedCase = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-DIY-LAWFIRM-A-{Guid.CreateVersion7():N}"[..30],
                "Selected",
                "Plaintiff",
                SeedHelper.UserId,
                notes: $"lawFirmId={selectedLawFirmId}");
            var otherCase = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-DIY-LAWFIRM-B-{Guid.CreateVersion7():N}"[..30],
                "Other",
                "Plaintiff",
                SeedHelper.UserId,
                notes: $"lawFirmId={otherLawFirmId}");
            var selectedLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"{prefix}-A",
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                caseId: selectedCase.Id,
                isBulk: "N");
            var otherLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"{prefix}-B",
                LienType.MedicalLien,
                200m,
                SeedHelper.UserId,
                caseId: otherCase.Id,
                isBulk: "N");

            selectedLienNumber = selectedLien.LienNumber;
            otherLienNumber = otherLien.LienNumber;
            db.Cases.AddRange(selectedCase, otherCase);
            db.Liens.AddRange(selectedLien, otherLien);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportType = "LIENS",
            search = prefix,
            isBulk = "N",
            lawFirmIds = new[] { selectedLawFirmId },
            columns = new[] { "lien_id" },
            page = 1,
            limit = 1,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
        payload.RootElement.GetProperty("summaryTotals").GetProperty("totalLiens").GetInt32().Should().Be(1);
        var lienNumbers = payload.RootElement.GetProperty("data").EnumerateArray()
            .Select(row => row.GetProperty("lien_id").GetString())
            .ToList();
        lienNumbers.Should().ContainSingle().Which.Should().Be(selectedLienNumber);
        lienNumbers.Should().NotContain(otherLienNumber);
    }

    [Fact]
    public async Task RunReport_resolves_legacy_lien_status_lookup_ids_to_status_codes()
    {
        var prefix = $"LIEN-DIY-STATUS-ID-{Guid.CreateVersion7():N}"[..34];
        var draftLienNumber = $"{prefix}-DRAFT";
        var activeLienNumber = $"{prefix}-ACTIVE";
        Guid activeStatusLookupId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            activeStatusLookupId = db.LookupValues
                .First(lookup => lookup.Category == LookupCategory.LienStatus &&
                                 lookup.Code == LienStatus.Active)
                .Id;
            var draftLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                draftLienNumber,
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                isBulk: "N");
            var activeLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                activeLienNumber,
                LienType.MedicalLien,
                200m,
                SeedHelper.UserId,
                isBulk: "N");
            activeLien.SetLegacyMedicalStatus(LienStatus.Active, SeedHelper.UserId);

            db.Liens.AddRange(draftLien, activeLien);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportType = "LIENS",
            statusView = "OPEN",
            search = prefix,
            isBulk = "N",
            lienStatusIds = new[] { activeStatusLookupId },
            columns = new[] { "lien_id" },
            page = 1,
            limit = 50,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var lienNumbers = payload.RootElement.GetProperty("data").EnumerateArray()
            .Select(row => row.GetProperty("lien_id").GetString())
            .ToList();
        lienNumbers.Should().ContainSingle().Which.Should().Be(activeLienNumber);
        lienNumbers.Should().NotContain(draftLienNumber);
    }

    [Fact]
    public async Task Newly_created_case_without_liens_appears_in_case_report_preview_and_export()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/liens/cases", new
        {
            caseNumber = "",
            clientFirstName = "Report",
            clientLastName = "Visible",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await createResponse.Content.ReadAsStringAsync()}");

        using var createdCase = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var caseNumber = createdCase.RootElement.GetProperty("caseNumber").GetString();
        caseNumber.Should().MatchRegex(@"^\d{2}-\d{5}$");

        var request = new
        {
            reportType = "CASES",
            search = caseNumber,
            isBulk = "N",
            columns = new[] { "case_id", "plaintiff_first_name", "plaintiff_last_name" },
            page = 1,
            limit = 50,
        };

        var previewResponse = await _client.PostAsJsonAsync("/report/diy", request);
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await previewResponse.Content.ReadAsStringAsync()}");
        using var preview = JsonDocument.Parse(await previewResponse.Content.ReadAsStringAsync());
        preview.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
        var row = preview.RootElement.GetProperty("data").EnumerateArray().Single();
        row.GetProperty("case_id").GetString().Should().Be(caseNumber);
        row.GetProperty("plaintiff_first_name").GetString().Should().Be("Report");
        row.GetProperty("plaintiff_last_name").GetString().Should().Be("Visible");

        var exportResponse = await _client.PostAsJsonAsync("/report/diy/export", request);
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await exportResponse.Content.ReadAsStringAsync()}");
        var exportPayload = await exportResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var export = exportPayload!.RootElement.GetProperty("data").EnumerateArray().Single();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(export.GetProperty("base64").GetString()!));
        csv.Should().Contain("case_id,plaintiff_first_name,plaintiff_last_name");
        csv.Should().Contain($"{caseNumber},Report,Visible");
    }

    [Fact]
    public async Task ExportReport_returns_all_filtered_rows_even_when_preview_limit_is_one()
    {
        var prefix = $"LIEN-DIY-EXPORT-ALL-{Guid.CreateVersion7():N}"[..34];
        var firstLienNumber = $"{prefix}-A";
        var secondLienNumber = $"{prefix}-B";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Liens.AddRange(
                Lien.Create(
                    SeedHelper.TenantId,
                    SeedHelper.OrgId,
                    firstLienNumber,
                    LienType.MedicalLien,
                    100m,
                    SeedHelper.UserId,
                    isBulk: "N"),
                Lien.Create(
                    SeedHelper.TenantId,
                    SeedHelper.OrgId,
                    secondLienNumber,
                    LienType.MedicalLien,
                    200m,
                    SeedHelper.UserId,
                    isBulk: "N"));
            await db.SaveChangesAsync();
        }

        var request = new
        {
            reportType = "LIENS",
            search = prefix,
            isBulk = "N",
            columns = new[] { "lien_id" },
            page = 1,
            limit = 1,
        };

        var previewResponse = await _client.PostAsJsonAsync("/report/diy", request);
        previewResponse.EnsureSuccessStatusCode();
        using var preview = JsonDocument.Parse(await previewResponse.Content.ReadAsStringAsync());
        preview.RootElement.GetProperty("data").GetArrayLength().Should().Be(1);
        preview.RootElement.GetProperty("totalCount").GetInt32().Should().Be(2);

        var exportResponse = await _client.PostAsJsonAsync("/report/diy/export", request);
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await exportResponse.Content.ReadAsStringAsync()}");
        var exportPayload = await exportResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var export = exportPayload!.RootElement.GetProperty("data").EnumerateArray().Single();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(export.GetProperty("base64").GetString()!));
        csv.Should().Contain(firstLienNumber).And.Contain(secondLienNumber);
    }

    // ── POST /report/diy/export ───────────────────────────────────────────────

    [Fact]
    public async Task RunReport_filters_direct_legacy_payload_by_status_view()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var preDemandCase = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-PRE-{Guid.CreateVersion7():N}",
                "Pre",
                "Demand",
                SeedHelper.UserId);
            db.Cases.Add(preDemandCase);

            var preDemandLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-PRE-{Guid.CreateVersion7():N}",
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                caseId: preDemandCase.Id,
                isBulk: "No");
            db.Liens.Add(preDemandLien);

            var demandSentCase = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-DEMAND-{Guid.CreateVersion7():N}",
                "Demand",
                "Plaintiff",
                SeedHelper.UserId);
            demandSentCase.TransitionStatus(CaseStatus.DemandSent, SeedHelper.UserId);
            db.Cases.Add(demandSentCase);

            var demandSentLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-DEMAND-{Guid.CreateVersion7():N}",
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                caseId: demandSentCase.Id);
            db.Liens.Add(demandSentLien);

            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportType = "CASES",
            statusView = "PreDemand",
            lienStatusIds = Array.Empty<string>(),
            purchaseDateFrom = Array.Empty<string>(),
            purchaseDateTo = (string?)null,
            closedDateFrom = (string?)null,
            closedDateTo = (string?)null,
            isBulk = "N",
            plaintiffCaseIds = Array.Empty<string>(),
            lawFirmIds = Array.Empty<string>(),
            attorneyIds = Array.Empty<string>(),
            fundingCompanyIds = Array.Empty<string>(),
            medicalFacilityIds = Array.Empty<string>(),
            caseManagerIds = Array.Empty<string>(),
            medicalProviderIds = Array.Empty<string>(),
            columns = new[]
            {
                "plaintiff_first_name",
                "plaintiff_last_name",
                "case_id",
                "lien_id",
                "case_status",
            },
            page = "1",
            limit = "10",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        var rows = doc!.RootElement.GetProperty("data").EnumerateArray().ToList();

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(row => row.GetProperty("case_status").GetString() == "Pre-demand");
        var rowColumns = rows.Select(row => row.EnumerateObject().Select(p => p.Name).ToList()).ToList();
        rowColumns.Should().OnlyContain(columns => !columns.Contains("l_id"));
        rowColumns.Should().OnlyContain(columns => columns.SequenceEqual(new[]
        {
            "plaintiff_first_name",
            "plaintiff_last_name",
            "case_id",
            "lien_id",
            "case_status",
        }));
        doc.RootElement.GetProperty("limit").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task ExportReport_returns_case_export_compatible_base64_csv()
    {
        var request = new
        {
            viewBy = "CASE",
            reportType = "CASE",
            statusView = "",
            lienStatusIds = Array.Empty<string>(),
            purchaseDateFrom = Array.Empty<string>(),
            purchaseDateTo = (string?)null,
            closedDateFrom = (string?)null,
            closedDateTo = (string?)null,
            isBulk = "N",
            plaintiffCaseIds = Array.Empty<string>(),
            lawFirmIds = Array.Empty<string>(),
            attorneyIds = Array.Empty<string>(),
            fundingCompanyIds = Array.Empty<string>(),
            medicalFacilityIds = Array.Empty<string>(),
            caseManagerIds = Array.Empty<string>(),
            medicalProviderIds = Array.Empty<string>(),
            columns = new[]
            {
                "billing_amt",
                "case_status",
                "plaintiff_last_name",
                "plaintiff_first_name",
            },
            format = "csv",
            page = 1,
            limit = 10,
        };

        var exportResp = await _client.PostAsJsonAsync("/report/diy/export", request);
        exportResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await exportResp.Content.ReadAsStringAsync()}");

        var exportDoc = await exportResp.Content.ReadFromJsonAsync<JsonDocument>();
        exportDoc.Should().NotBeNull();

        exportDoc!.RootElement.GetProperty("isSuccess").GetBoolean()
            .Should().BeTrue();
        exportDoc.RootElement.GetProperty("message").GetString()
            .Should().Be("CSV generated successfully.");
        exportDoc.RootElement.TryGetProperty("summaryTotals", out _).Should().BeFalse();
        exportDoc.RootElement.TryGetProperty("page", out _).Should().BeFalse();
        exportDoc.RootElement.TryGetProperty("limit", out _).Should().BeFalse();
        exportDoc.RootElement.TryGetProperty("totalCount", out _).Should().BeFalse();

        var exportItems = exportDoc.RootElement.GetProperty("data").EnumerateArray().ToList();
        exportItems.Should().ContainSingle();
        var exportItem = exportItems.Single();
        exportItem.GetProperty("filename").GetString().Should().StartWith("diy_report_");
        exportItem.GetProperty("export_format").GetString().Should().Be("csv");

        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(exportItem.GetProperty("base64").GetString()!));
        csv.Should().StartWith("billing_amt,case_status,plaintiff_last_name,plaintiff_first_name");
    }

    [Fact]
    public async Task RunAndExportReport_use_latest_case_update_for_last_activity()
    {
        Guid caseId;
        Guid latestActivityId;
        string caseNumber;
        string lienNumber;
        string unlinkedCaseNumber;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-DIY-ACTIVITY-{Guid.CreateVersion7():N}"[..30],
                "Case",
                "Activity",
                SeedHelper.UserId);
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-DIY-ACTIVITY-{Guid.CreateVersion7():N}"[..30],
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                isBulk: "N");

            var caseCreated = CreateCaseNote(
                caseEntity.Id,
                SeedHelper.TenantId,
                "Case created",
                CaseNoteCategory.CaseCreated,
                new DateTime(2026, 8, 17, 16, 30, 0, DateTimeKind.Utc));
            var latestActivity = CreateCaseNote(
                caseEntity.Id,
                SeedHelper.TenantId,
                "Note updated",
                CaseNoteCategory.Internal,
                new DateTime(2026, 8, 17, 15, 0, 0, DateTimeKind.Utc));
            SetCaseNoteProperty(
                latestActivity,
                nameof(LienCaseNote.Id),
                Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
            SetCaseNoteProperty(
                latestActivity,
                nameof(LienCaseNote.UpdatedAtUtc),
                new DateTime?(new DateTime(2026, 8, 17, 16, 43, 0, DateTimeKind.Utc)));
            var tiedActivity = CreateCaseNote(
                caseEntity.Id,
                SeedHelper.TenantId,
                "Timestamp tie loser",
                CaseNoteCategory.Internal,
                new DateTime(2026, 8, 17, 14, 0, 0, DateTimeKind.Utc));
            SetCaseNoteProperty(
                tiedActivity,
                nameof(LienCaseNote.Id),
                Guid.Parse("11111111-1111-1111-1111-111111111111"));
            SetCaseNoteProperty(
                tiedActivity,
                nameof(LienCaseNote.UpdatedAtUtc),
                new DateTime?(new DateTime(2026, 8, 17, 16, 43, 0, DateTimeKind.Utc)));
            var laterTrackingNote = CreateCaseNote(
                caseEntity.Id,
                SeedHelper.TenantId,
                "Tracking note is not a case activity",
                CaseNoteCategory.General,
                new DateTime(2026, 8, 17, 17, 0, 0, DateTimeKind.Utc));
            var deletedActivity = CreateCaseNote(
                caseEntity.Id,
                SeedHelper.TenantId,
                "Deleted activity",
                CaseNoteCategory.Internal,
                new DateTime(2026, 8, 17, 18, 0, 0, DateTimeKind.Utc));
            deletedActivity.SoftDelete();
            var otherTenantActivity = CreateCaseNote(
                caseEntity.Id,
                Guid.CreateVersion7(),
                "Other tenant activity",
                CaseNoteCategory.Internal,
                new DateTime(2026, 8, 17, 19, 0, 0, DateTimeKind.Utc));
            var unlinkedCase = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-DIY-UNLINKED-{Guid.CreateVersion7():N}"[..30],
                "Unlinked",
                "Activity",
                SeedHelper.UserId);
            var unlinkedActivity = CreateCaseNote(
                unlinkedCase.Id,
                SeedHelper.TenantId,
                "Unlinked Case Activity",
                CaseNoteCategory.CaseCreated,
                new DateTime(2026, 8, 18, 17, 0, 0, DateTimeKind.Utc));

            caseId = caseEntity.Id;
            latestActivityId = latestActivity.Id;
            caseNumber = caseEntity.CaseNumber;
            lienNumber = lien.LienNumber;
            unlinkedCaseNumber = unlinkedCase.CaseNumber;
            db.Cases.AddRange(caseEntity, unlinkedCase);
            db.Liens.Add(lien);
            db.LienCaseNotes.AddRange(
                caseCreated,
                latestActivity,
                tiedActivity,
                laterTrackingNote,
                deletedActivity,
                otherTenantActivity,
                unlinkedActivity);
            await db.SaveChangesAsync();
        }

        var caseUpdatesResponse = await _client.PostAsJsonAsync("/api/liens/cases/case-updates/v3", new
        {
            CaseId = caseId,
            page = 1,
            limit = 10,
        });
        caseUpdatesResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await caseUpdatesResponse.Content.ReadAsStringAsync()}");
        using var caseUpdatesPayload = JsonDocument.Parse(await caseUpdatesResponse.Content.ReadAsStringAsync());
        var newestCaseUpdate = caseUpdatesPayload.RootElement.GetProperty("data").EnumerateArray().First();
        newestCaseUpdate.GetProperty("id").GetString().Should().Be(latestActivityId.ToString());
        newestCaseUpdate.GetProperty("description").GetString().Should().Be("Case Tracking Note Update");
        newestCaseUpdate.GetProperty("timestamp").GetString().Should().Be("08/17/2026 09:43 AM");

        foreach (var reportType in new[] { "LIENS", "COMBINED", "CASES" })
        {
            await AssertLastActivityAsync(
                reportType,
                lienNumber,
                caseNumber,
                "Case Tracking Note Update",
                "08/17/2026 09:43 AM");
        }

        await AssertLastActivityAsync(
            "CASES",
            unlinkedCaseNumber,
            unlinkedCaseNumber,
            "Unlinked Case Activity",
            "08/18/2026 10:00 AM");

        async Task AssertLastActivityAsync(
            string reportType,
            string search,
            string expectedCaseNumber,
            string expectedActivity,
            string expectedTimestamp)
        {
            var request = new
            {
                reportType,
                search,
                isBulk = "N",
                columns = new[]
                {
                    "case_id",
                    "last_case_tracking_note",
                    "last_case_tracking_date",
                },
                page = 1,
                limit = 10,
            };

            var runResponse = await _client.PostAsJsonAsync("/report/diy", request);
            runResponse.StatusCode.Should().Be(HttpStatusCode.OK,
                $"Body: {await runResponse.Content.ReadAsStringAsync()}");
            using var runPayload = JsonDocument.Parse(await runResponse.Content.ReadAsStringAsync());
            var row = runPayload.RootElement.GetProperty("data").EnumerateArray().Single();
            row.GetProperty("case_id").GetString().Should().Be(expectedCaseNumber);
            row.GetProperty("last_case_tracking_note").GetString().Should().Be(expectedActivity);
            row.GetProperty("last_case_tracking_date").GetString().Should().Be(expectedTimestamp);

            var exportResponse = await _client.PostAsJsonAsync("/report/diy/export", request);
            exportResponse.StatusCode.Should().Be(HttpStatusCode.OK,
                $"Body: {await exportResponse.Content.ReadAsStringAsync()}");
            var payload = await exportResponse.Content.ReadFromJsonAsync<JsonDocument>();
            var export = payload!.RootElement.GetProperty("data").EnumerateArray().Single();
            var csv = Encoding.UTF8.GetString(Convert.FromBase64String(export.GetProperty("base64").GetString()!));

            csv.Should().StartWith("case_id,Last Activity,Last Activity Date");
            csv.Should().Contain($"{expectedCaseNumber},{expectedActivity},{expectedTimestamp}");
            csv.Should().NotContain("Timestamp tie loser");
            csv.Should().NotContain("Deleted activity");
            csv.Should().NotContain("Other tenant activity");
        }
    }

    [Fact]
    public async Task ExportReport_honors_saved_object_column_configuration()
    {
        var response = await _client.PostAsJsonAsync("/report/diy/export", new
        {
            reportType = "LIENS",
            isBulk = "N",
            columns = new[]
            {
                new { key = "lien_id", label = "Lien ID" },
                new { key = "purchase_amt", label = "Purchase Amount" },
            },
            page = 1,
            limit = 10,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var export = payload!.RootElement.GetProperty("data").EnumerateArray().Single();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(export.GetProperty("base64").GetString()!));

        csv.Should().StartWith("lien_id,purchase_amt");
        csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).First().TrimEnd('\r')
            .Should().Be("lien_id,purchase_amt");
    }

    [Fact]
    public async Task ExportReport_uses_saved_report_columns_when_requested_by_report_id()
    {
        Guid reportId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var savedReport = DIYReportConfig.Create(
                SeedHelper.TenantId,
                SeedHelper.UserId,
                "Saved export columns",
                """{"reportType":"LIENS","isBulk":"N","columns":["lien_id","purchase_date","case_status"]}""",
                SeedHelper.UserId);
            reportId = savedReport.Id;
            db.DIYReportConfigs.Add(savedReport);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/report/diy/export", new
        {
            reportId,
            format = "csv",
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var export = payload!.RootElement.GetProperty("data").EnumerateArray().Single();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(export.GetProperty("base64").GetString()!));
        csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).First().TrimEnd('\r')
            .Should().Be("lien_id,purchase_date,case_status");
    }

    // ── POST /report/diy/save ─────────────────────────────────────────────────

    [Fact]
    public async Task SaveReport_returns201()
    {
        var resp = await _client.PostAsJsonAsync("/report/diy/save", new
        {
            name   = "My New Report",
            config = new
            {
                reportType = "LIENS",
                statusView = "ALL",
                columns = new[]
                {
                    new { key = "billing_amt", label = "Billing Amt" },
                    new { key = "case_id", label = "Case Id" },
                },
                page = 1,
                limit = 50,
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.TryGetProperty("id", out _).Should().BeTrue();
        doc.RootElement.GetProperty("reportId").GetString().Should().NotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("reportName").GetString().Should().Be("My New Report");
        doc.RootElement.GetProperty("reportType").GetString().Should().Be("LIENS");
        doc.RootElement.GetProperty("createdAt").GetString().Should().MatchRegex(@"^\d{2}/\d{2}/\d{4}$");
        doc.RootElement.GetProperty("updatedAt").GetString().Should().MatchRegex(@"^\d{2}/\d{2}/\d{4}$");
        doc.RootElement.GetProperty("columnCount").GetInt32().Should().Be(2);

        var configColumns = doc.RootElement
            .GetProperty("config")
            .GetProperty("columns")
            .EnumerateArray()
            .Select(c => c.GetProperty("key").GetString())
            .ToList();
        configColumns.Should().Equal("billing_amt", "case_id");

        var reportConfigColumns = doc.RootElement
            .GetProperty("reportConfig")
            .GetProperty("columns")
            .EnumerateArray()
            .Select(c => c.GetProperty("key").GetString())
            .ToList();
        reportConfigColumns.Should().Equal("billing_amt", "case_id");
    }

    [Fact]
    public async Task SaveReport_preserves_top_level_filter_ids_in_saved_response()
    {
        var resp = await _client.PostAsJsonAsync("/api/liens/reports/diy/save", new
        {
            name = "Saved Filters Report",
            config = new
            {
                reportType = "LIENS",
                statusView = "ALL",
                lienStatusIds = Array.Empty<string>(),
                purchaseDateFrom = "06/01/2026",
                purchaseDateTo = "06/26/2026",
                closedDateFrom = (string?)null,
                closedDateTo = (string?)null,
                isBulk = "N",
                plaintiffCaseIds = Array.Empty<string>(),
                lawFirmIds = Array.Empty<string>(),
                attorneyIds = Array.Empty<string>(),
                fundingCompanyIds = Array.Empty<string>(),
                medicalFacilityIds = Array.Empty<string>(),
                caseManagerIds = Array.Empty<string>(),
                medicalProviderIds = Array.Empty<string>(),
                columns = new[]
                {
                    new { key = "billing_amt", label = "Billing Amt" },
                    new { key = "case_id", label = "Case Id" },
                },
                page = 1,
                limit = 50,
            },
            lawFirmIds = new[] { SeedHelper.LawFirmId.ToString() },
            fundingCompanyIds = new[] { SeedHelper.FundingCompanyId.ToString() },
            medicalProviderIds = new[] { SeedHelper.MedicalProviderId.ToString() },
            plaintiffCaseIds = new[] { SeedHelper.CaseId.ToString() },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        var reportConfig = doc!.RootElement.GetProperty("reportConfig");

        reportConfig.GetProperty("lawFirmIds").EnumerateArray()
            .Select(v => v.GetString())
            .Should().Equal(SeedHelper.LawFirmId.ToString());
        reportConfig.GetProperty("fundingCompanyIds").EnumerateArray()
            .Select(v => v.GetString())
            .Should().Equal(SeedHelper.FundingCompanyId.ToString());
        reportConfig.GetProperty("medicalProviderIds").EnumerateArray()
            .Select(v => v.GetString())
            .Should().Equal(SeedHelper.MedicalProviderId.ToString());
        reportConfig.GetProperty("plaintiffCaseIds").EnumerateArray()
            .Select(v => v.GetString())
            .Should().Equal(SeedHelper.CaseId.ToString());

        var reportConfigColumns = reportConfig
            .GetProperty("columns")
            .EnumerateArray()
            .Select(c => new
            {
                key = c.GetProperty("key").GetString(),
                label = c.GetProperty("label").GetString(),
            })
            .ToList();
        reportConfigColumns.Should().ContainSingle(c => c.key == "billing_amt" && c.label == "Billing Amt");
        reportConfigColumns.Should().ContainSingle(c => c.key == "case_id" && c.label == "Case Id");
    }

    // ── DELETE /report/diy/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task DeleteReport_returns200()
    {
        // Save a fresh report to delete.
        var saveResp = await _client.PostAsJsonAsync("/report/diy/save", new
        {
            name   = "Report To Delete",
            config = new { },
        });
        saveResp.EnsureSuccessStatusCode();
        var doc  = await saveResp.Content.ReadFromJsonAsync<JsonDocument>();
        var id   = doc!.RootElement.GetProperty("id").GetGuid();

        var deleteResp = await _client.DeleteAsync($"/report/diy/{id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await deleteResp.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task GetSavedReports_legacy_saved_route_returns_enveloped_response()
    {
        var resp = await _client.GetAsync("/report/diy/saved");
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("message").GetString().Should().Be("Saved reports retrieved.");
        var data = doc.RootElement.GetProperty("data").EnumerateArray().ToList();
        data.Should().ContainSingle(r => r.GetProperty("id").GetGuid() == SeedHelper.ReportConfigId);
    }

    [Fact]
    public async Task DeleteReport_legacy_delete_alias_returns200()
    {
        var saveResp = await _client.PostAsJsonAsync("/report/diy/save", new
        {
            name = "Report To Delete Via Alias",
            config = new { },
        });
        saveResp.EnsureSuccessStatusCode();
        var doc = await saveResp.Content.ReadFromJsonAsync<JsonDocument>();
        var id = doc!.RootElement.GetProperty("id").GetGuid();

        var deleteResp = await _client.DeleteAsync($"/report/diy/delete/{id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await deleteResp.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task GetColumns_returns_legacy_column_metadata()
    {
        var resp = await _client.GetAsync("/report/diy/columns?reportType=LIENS");
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("reportType").GetString().Should().Be("LIENS");
        doc.RootElement.TryGetProperty("defaultColumn", out var defaults).Should().BeTrue();
        defaults.EnumerateArray().Select(x => x.GetString()).Should().Equal(
        [
            "plaintiff_first_name",
            "plaintiff_last_name",
            "case_id",
            "lien_id",
            "purchase_date",
            "purchase_amt",
            "billing_amt",
            "date_closed",
            "returned_amount",
            "days_since_reduction_approval",
            "medical_facility",
            "lawfirm",
            "case_type",
            "case_manager",
            "case_status",
            "date_of_loss",
        ]);
        doc.RootElement.TryGetProperty("data", out var data).Should().BeTrue();
        data.EnumerateArray().Any(x => x.GetProperty("key").GetString() == "plaintiff_first_name")
            .Should().BeTrue();
        data.EnumerateArray().Should().Contain(x =>
            x.GetProperty("key").GetString() == "days_since_purchase" &&
            x.GetProperty("label").GetString() == "Days Since Purchase");
        data.EnumerateArray().Should().Contain(x =>
            x.GetProperty("key").GetString() == "expected_settlement_amount" &&
            x.GetProperty("label").GetString() == "Expected Settlement Amount");
        data.EnumerateArray().Should().Contain(x =>
            x.GetProperty("key").GetString() == "notes" &&
            x.GetProperty("label").GetString() == "Notes" &&
            !x.GetProperty("isDefault").GetBoolean());
        data.EnumerateArray().Should().Contain(x =>
            x.GetProperty("key").GetString() == "notes_date" &&
            x.GetProperty("label").GetString() == "Notes Date" &&
            !x.GetProperty("isDefault").GetBoolean());
        var procedureColumns = doc.RootElement.GetProperty("procedureInfo")
            .EnumerateArray()
            .ToList();
        procedureColumns.Should().Contain(x => x.GetProperty("key").GetString() == "notes");
        procedureColumns.Should().Contain(x => x.GetProperty("key").GetString() == "notes_date");
        defaults.EnumerateArray().Select(x => x.GetString()).Should().NotContain("notes").And.NotContain("notes_date");
        data.EnumerateArray().Should().Contain(x =>
            x.GetProperty("key").GetString() == "days_since_reduction_approval" &&
            x.GetProperty("label").GetString() == "Days Since Reduction Approval");
        data.EnumerateArray().Should().Contain(x =>
            x.GetProperty("key").GetString() == "date_closed" &&
            x.GetProperty("label").GetString() == "Date Closed");
        data.EnumerateArray().Should().Contain(x =>
            x.GetProperty("key").GetString() == "last_case_note" &&
            x.GetProperty("label").GetString() == "Tracking Notes");
        data.EnumerateArray().Should().Contain(x =>
            x.GetProperty("key").GetString() == "last_case_note_date" &&
            x.GetProperty("label").GetString() == "Last Tracking Note Date");
        data.EnumerateArray().Should().Contain(x =>
            x.GetProperty("key").GetString() == "last_case_tracking_note" &&
            x.GetProperty("label").GetString() == "Last Activity");
        data.EnumerateArray().Should().Contain(x =>
            x.GetProperty("key").GetString() == "last_case_tracking_date" &&
            x.GetProperty("label").GetString() == "Last Activity Date");
        var caseTrackingColumns = doc.RootElement.GetProperty("caseTrackingInfo")
            .EnumerateArray()
            .ToList();
        caseTrackingColumns.Should().Contain(x =>
            x.GetProperty("key").GetString() == "last_case_tracking_note" &&
            x.GetProperty("label").GetString() == "Last Activity");
        caseTrackingColumns.Should().Contain(x =>
            x.GetProperty("key").GetString() == "last_case_tracking_date" &&
            x.GetProperty("label").GetString() == "Last Activity Date");
        data.EnumerateArray().Count(x => x.GetProperty("key").GetString() == "date_closed")
            .Should().Be(1);
        data.EnumerateArray().Should().Contain(x =>
            x.GetProperty("key").GetString() == "medical_provider" &&
            x.GetProperty("label").GetString() == "Medical Provider");
        data.EnumerateArray().Count(x => x.GetProperty("key").GetString() == "medical_facility")
            .Should().Be(1);
        data.EnumerateArray().Should().Contain(x =>
            x.GetProperty("key").GetString() == "medical_facility" &&
            x.GetProperty("label").GetString() == "Medical Facility");
        data.EnumerateArray().Should().Contain(x =>
            x.GetProperty("key").GetString() == "lawfirm_email" &&
            x.GetProperty("label").GetString() == "Lawfirm Email");
        data.EnumerateArray().Should().Contain(x =>
            x.GetProperty("key").GetString() == "date_of_loss" &&
            x.GetProperty("label").GetString() == "Date of Loss");
        data.EnumerateArray().Count(x => x.GetProperty("key").GetString() == "date_of_loss")
            .Should().Be(1);
        data.EnumerateArray().Should().Contain(x =>
            x.GetProperty("key").GetString() == "plaintiff_date_of_birth" &&
            x.GetProperty("label").GetString() == "Plaintiff Date of Birth");

        var liensInfo = doc.RootElement.GetProperty("liensInfo").EnumerateArray().ToList();
        liensInfo.Should().Contain(x => x.GetProperty("key").GetString() == "date_closed");
        liensInfo.Should().NotContain(x => x.GetProperty("key").GetString() == "medical_facility");
        liensInfo.Should().Contain(x =>
            x.GetProperty("key").GetString() == "number_of_liens" &&
            x.GetProperty("label").GetString() == "Number Of Liens");
        liensInfo.Should().NotContain(x => x.GetProperty("key").GetString() == "l_id");

        var procedureInfo = doc.RootElement.GetProperty("procedureInfo").EnumerateArray().ToList();
        procedureInfo.Should().Contain(x => x.GetProperty("key").GetString() == "medical_facility");

        var caseTrackingInfo = doc.RootElement.GetProperty("caseTrackingInfo").EnumerateArray().ToList();
        caseTrackingInfo.Should().NotContain(x => x.GetProperty("key").GetString() == "date_closed");
        caseTrackingInfo.Should().NotContain(x => x.GetProperty("key").GetString() == "date_of_loss");

        var settlementInfo = doc.RootElement.GetProperty("settlementInfo").EnumerateArray().ToList();
        settlementInfo.Should().NotContain(x => x.GetProperty("key").GetString() == "returned_amt");
        settlementInfo.Should().Contain(x =>
            x.GetProperty("key").GetString() == "returned_amount" &&
            x.GetProperty("label").GetString() == "Returned Amount");

        var caseInfo = doc.RootElement.GetProperty("caseInfo").EnumerateArray().ToList();
        caseInfo.Should().Contain(x =>
            x.GetProperty("key").GetString() == "date_of_loss" &&
            x.GetProperty("label").GetString() == "Date of Loss");
    }

    [Fact]
    public async Task RunReport_projects_new_legacy_columns_when_requested()
    {
        var resp = await _client.PostAsJsonAsync("/report/diy", new
        {
            config = new
            {
                columns = new[]
                {
                    "days_since_purchase",
                    "expected_settlement_amount",
                    "medical_provider",
                    "lawfirm_email",
                    "plaintiff_date_of_birth",
                },
            },
            page = 1,
            limit = 10,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        var rows = doc!.RootElement.GetProperty("data").EnumerateArray().ToList();
        rows.Should().NotBeEmpty();
        rows[0].TryGetProperty("days_since_purchase", out _).Should().BeTrue();
        rows[0].TryGetProperty("expected_settlement_amount", out _).Should().BeTrue();
        rows[0].TryGetProperty("medical_provider", out _).Should().BeTrue();
        rows[0].TryGetProperty("lawfirm_email", out _).Should().BeTrue();
        rows[0].TryGetProperty("plaintiff_date_of_birth", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetFilterOptions_returns_filter_choices_for_lawfirms()
    {
        var resp = await _client.PostAsJsonAsync("/report/diy/filter-options", new
        {
            filterField = "lawfirm",
            keyword = "Smith",
            limit = 10,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        var data = doc.RootElement.GetProperty("data").EnumerateArray().ToList();
        data.Should().NotBeEmpty();
        data.Should().Contain(item => item.GetProperty("id").GetString() == SeedHelper.LawFirmId.ToString());
    }

    [Fact]
    public async Task GetFilterOptions_returns_standalone_and_law_firm_case_managers()
    {
        var lawFirmCaseManager = Contact.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            ContactType.LawFirm,
            "Report",
            "Manager",
            SeedHelper.UserId,
            lawFirmId: SeedHelper.LawFirmId,
            contactSubtype: ContactSubtype.LawFirmCaseManager,
            organization: "Smith & Associates LLP");
        var standaloneCaseManager = Contact.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            ContactType.CaseManager,
            "Legacy Report",
            "Manager",
            SeedHelper.UserId);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Contacts.AddRange(lawFirmCaseManager, standaloneCaseManager);
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/report/diy/filter-options", new
        {
            filterField = "caseManager",
            keyword = "Report",
            limit = 10,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        var data = doc!.RootElement.GetProperty("data").EnumerateArray().ToList();
        data.Should().Contain(item =>
            item.GetProperty("id").GetString() == lawFirmCaseManager.Id.ToString() &&
            item.GetProperty("name").GetString() == "Report Manager");
        data.Should().Contain(item =>
            item.GetProperty("id").GetString() == standaloneCaseManager.Id.ToString() &&
            item.GetProperty("name").GetString() == "Legacy Report Manager");
    }

    [Fact]
    public async Task GetAllFilterOptions_returns_grouped_filter_payload()
    {
        var resp = await _client.GetAsync("/report/diy/all-filters?reportType=CASES&limit=10");
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("reportType").GetString().Should().Be("CASES");
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("lawfirm", out var lawFirms).Should().BeTrue();
        data.TryGetProperty("plaintiff", out var plaintiffs).Should().BeTrue();
        lawFirms.ValueKind.Should().Be(JsonValueKind.Array);
        plaintiffs.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // ── Auth enforcement ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetSavedReports_without_auth_returns_401()
    {
        var anonClient = _factory.CreateClient();
        var resp = await anonClient.GetAsync("/report/diy");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RunReport_without_auth_returns_401()
    {
        var anonClient = _factory.CreateClient();
        var resp = await anonClient.PostAsJsonAsync("/report/diy",
            new { config = new { } });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static LienCaseNote CreateCaseNote(
        Guid caseId,
        Guid tenantId,
        string content,
        string category,
        DateTime createdAtUtc)
    {
        var note = LienCaseNote.Create(
            caseId,
            tenantId,
            content,
            category,
            SeedHelper.UserId,
            "Report Author");
        typeof(LienCaseNote).GetProperty(nameof(LienCaseNote.CreatedAtUtc))!
            .SetValue(note, createdAtUtc);
        return note;
    }

    private static void SetCaseNoteProperty<T>(LienCaseNote note, string propertyName, T value) =>
        typeof(LienCaseNote).GetProperty(propertyName)!.SetValue(note, value);
}
