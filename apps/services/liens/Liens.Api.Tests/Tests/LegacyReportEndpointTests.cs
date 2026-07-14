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
    public async Task ExportReport_returns200_with_base64_data()
    {
        var resp = await _client.PostAsJsonAsync("/report/diy/export", new
        {
            config = new { status = "Open" },
            page   = 1,
            limit  = 10,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.TryGetProperty("data", out var dataProp).Should().BeTrue();
        dataProp.GetString().Should().NotBeNull();
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
        defaults.EnumerateArray().Select(x => x.GetString()).Should().Contain("billing_amt");
        doc.RootElement.TryGetProperty("data", out var data).Should().BeTrue();
        data.EnumerateArray().Any(x => x.GetProperty("key").GetString() == "plaintiff_first_name")
            .Should().BeTrue();
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
