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
    public async Task RunReport_includes_a_new_non_bulk_case_and_lien_when_the_ui_submits_N()
    {
        string lienNumber;

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
                $"LIEN-DIY-NON-BULK-{Guid.CreateVersion7():N}"[..30],
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
            statusView = "",
            lienStatusIds = Array.Empty<string>(),
            isBulk = "N",
            columns = new[] { "case_id", "lien_id" },
            page = 1,
            limit = 50,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        payload.RootElement.GetProperty("data").EnumerateArray()
            .Should().Contain(row => row.GetProperty("lien_id").GetString() == lienNumber);
    }

    [Fact]
    public async Task RunReport_returns_zero_days_since_reduction_approval_when_no_reduction_exists()
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
        row.GetProperty("days_since_reduction_approval").GetString().Should().Be("0");
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
            .Should().BeGreaterThan(0);
        row.GetProperty("medical_facility").GetString().Should().Be("Sunrise Clinic");
        row.GetProperty("lawfirm").GetString().Should().Be("Smith & Associates LLP");
        row.GetProperty("case_type").GetString().Should().Be("Motor Vehicle Accident");
        row.GetProperty("case_manager").GetString().Should().Be("Case Manager");
    }

    [Fact]
    public async Task RunReport_and_export_exclude_rejected_and_cancelled_liens()
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
        previewLienNumbers.Should().ContainSingle().Which.Should().Be(openLienNumber);
        previewLienNumbers.Should().NotContain(rejectedLienNumber).And.NotContain(cancelledLienNumber);

        var exportResponse = await _client.PostAsJsonAsync("/report/diy/export", request);
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await exportResponse.Content.ReadAsStringAsync()}");

        var exportPayload = await exportResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var export = exportPayload!.RootElement.GetProperty("data").EnumerateArray().Single();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(export.GetProperty("base64").GetString()!));
        csv.Should().Contain(openLienNumber);
        csv.Should().NotContain(rejectedLienNumber).And.NotContain(cancelledLienNumber);
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
            x.GetProperty("key").GetString() == "days_since_reduction_approval" &&
            x.GetProperty("label").GetString() == "Days Since Reduction Approval");
        data.EnumerateArray().Should().Contain(x =>
            x.GetProperty("key").GetString() == "date_closed" &&
            x.GetProperty("label").GetString() == "Date Closed");
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
}
