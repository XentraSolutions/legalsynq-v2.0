using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class DashboardReportEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private static readonly Guid CaseManagerContactId = new("40000000-0000-0000-0000-000000000099");
    private static readonly Guid AliasedLawFirmCaseId = new("60000000-0000-0000-0000-000000000099");
    private static readonly Guid AliasedProviderLienId = new("70000000-0000-0000-0000-000000000099");
    private static readonly Guid ActiveDashboardLienId = new("70000000-0000-0000-0000-000000000100");
    private static readonly Guid CancelledDashboardLienId = new("70000000-0000-0000-0000-000000000101");
    private static readonly Guid SettledDashboardLienId = new("70000000-0000-0000-0000-000000000102");
    private static readonly Guid ForeignDashboardLienId = new("70000000-0000-0000-0000-000000000103");
    private static readonly Guid ForeignOrgId = new("30000000-0000-0000-0000-000000000099");
    private static readonly Guid CanonicalAssistantLawFirmCompanyId = new("80000000-0000-0000-0000-000000000101");
    private static readonly Guid CanonicalAssistantCaseId = new("80000000-0000-0000-0000-000000000102");

    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public DashboardReportEndpointTests(LiensApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        await SeedHelper.SeedAsync(scope.ServiceProvider);
        await SeedDashboardDataAsync(db);

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",
                JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DashboardPiechart_returns_database_aggregates_with_consistent_totals()
    {
        var response = await _client.GetAsync("/api/liens/cases/dashboard/piechart");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = payload.RootElement.GetProperty("data");

        var caseStatusTotal = data.GetProperty("caseStatus")
            .EnumerateArray()
            .Sum(item => item.GetProperty("value").GetInt32());
        var lienStatusTotal = data.GetProperty("lienStatus")
            .EnumerateArray()
            .Sum(item => item.GetProperty("value").GetInt32());

        data.GetProperty("totalCases").GetInt32().Should().Be(caseStatusTotal);
        data.GetProperty("totalLiens").GetInt32().Should().Be(lienStatusTotal);
        data.GetProperty("totalLienValue").GetDouble().Should().BeGreaterThan(0d);
    }

    [Fact]
    public async Task DashboardPiechart_matches_assistant_total_for_organization_scoped_user()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var foreignLien = Lien.Create(
            SeedHelper.TenantId,
            ForeignOrgId,
            "LIEN-DASH-FOREIGN",
            LienType.MedicalLien,
            2500m,
            SeedHelper.UserId);
        SetId(foreignLien, ForeignDashboardLienId);
        db.Liens.Add(foreignLien);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateToken(
                SeedHelper.TenantId,
                SeedHelper.UserId,
                [LiensPermissions.CaseRead, LiensPermissions.LienReadOwn],
                SeedHelper.OrgId));

        try
        {
            var dashboardResponse = await _client.GetAsync("/api/liens/cases/dashboard/piechart");
            var assistantResponse = await _client.GetAsync("/api/assistant-tools/liens/queue-summary");

            dashboardResponse.StatusCode.Should().Be(HttpStatusCode.OK, await dashboardResponse.Content.ReadAsStringAsync());
            assistantResponse.StatusCode.Should().Be(HttpStatusCode.OK, await assistantResponse.Content.ReadAsStringAsync());

            using var dashboardPayload = JsonDocument.Parse(await dashboardResponse.Content.ReadAsStringAsync());
            using var assistantPayload = JsonDocument.Parse(await assistantResponse.Content.ReadAsStringAsync());

            var dashboardTotal = dashboardPayload.RootElement
                .GetProperty("data")
                .GetProperty("totalLiens")
                .GetInt32();
            var assistantTotal = assistantPayload.RootElement
                .GetProperty("totalVisibleLiens")
                .GetInt32();

            dashboardTotal.Should().Be(assistantTotal);
            dashboardTotal.Should().BeGreaterThan(0);
        }
        finally
        {
            db.Liens.Remove(foreignLien);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task TotalCaseReportV3_returns_seeded_case_rows()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/total-case-report-export/v3",
            new { page = 1, limit = 10 });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);

        var item = payload.RootElement.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("caseNumber").GetString() == "CASE-TEST-001");
        item.GetProperty("caseNumber").GetString().Should().Be("CASE-TEST-001");
        item.GetProperty("lawFirm").GetString().Should().Be("Smith & Associates LLP");
        item.GetProperty("totalLienAmount").GetDecimal().Should().Be(9800m);
    }

    [Fact]
    public async Task AssistantCaseSearch_counts_cases_linked_to_canonical_law_firm_company()
    {
        var response = await _client.GetAsync(
            "/api/assistant-tools/cases/search?lawFirm=Canonical%20Assistant%20Law&top=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);

        var item = payload.RootElement.GetProperty("cases").EnumerateArray()
            .Single(value => value.GetProperty("caseId").GetGuid() == CanonicalAssistantCaseId);
        item.GetProperty("caseNumber").GetString().Should().Be("CASE-CANON-LAW-1");
        item.GetProperty("lawFirm").GetString().Should().Be("Canonical Assistant Law");
    }

    [Fact]
    public async Task TotalCaseReportV3_includes_new_frontend_case_without_a_lien()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/liens/cases/create", new
        {
            firstname = "Dashboard",
            lastname = "Visible",
            caseStatusId = "PreDemand",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());

        using var createdPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var caseId = createdPayload.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        var caseResponse = await _client.GetAsync($"/api/liens/cases/{caseId}");
        caseResponse.StatusCode.Should().Be(HttpStatusCode.OK, await caseResponse.Content.ReadAsStringAsync());
        using var casePayload = JsonDocument.Parse(await caseResponse.Content.ReadAsStringAsync());
        var caseNumber = casePayload.RootElement.GetProperty("caseNumber").GetString();

        var reportResponse = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/total-case-report-export/v3",
            new { page = 1, limit = 100 });
        reportResponse.StatusCode.Should().Be(HttpStatusCode.OK, await reportResponse.Content.ReadAsStringAsync());

        using var reportPayload = JsonDocument.Parse(await reportResponse.Content.ReadAsStringAsync());
        reportPayload.RootElement.GetProperty("items").EnumerateArray()
            .Should().Contain(item =>
                item.GetProperty("id").GetGuid() == caseId &&
                item.GetProperty("caseNumber").GetString() == caseNumber);
    }

    [Fact]
    public async Task TotalLienReportV3_returns_seeded_lien_rows()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/total-lien-report-export/v3",
            new { page = 1, limit = 10 });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);

        var item = payload.RootElement.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("lienNumber").GetString() == "LIEN-TEST-001");
        item.GetProperty("lienNumber").GetString().Should().Be("LIEN-TEST-001");
        item.GetProperty("caseId").GetString().Should().Be("CASE-TEST-001");
        item.GetProperty("caseRecordId").GetString().Should().Be(SeedHelper.CaseId.ToString());
        item.GetProperty("caseNumber").GetString().Should().Be("CASE-TEST-001");
        item.GetProperty("medicalProvider").GetString().Should().Be("City Medical Center");
        item.GetProperty("totalPurchaseAmount").GetDecimal().Should().Be(100m);
        item.GetProperty("totalBillingAmount").GetDecimal().Should().Be(150m);
        item.GetProperty("status").GetString().Should().Be("Open");
        item.GetProperty("purchaseDate").GetString().Should().Be("06/15/2024");
    }

    [Fact]
    public async Task TotalCaseReportV3_keeps_full_summary_when_rows_are_paged()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/total-case-report-export/v3",
            new { page = 1, limit = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        root.GetProperty("items").GetArrayLength().Should().Be(1);
        root.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(1);
        root.GetProperty("statusCounts")
            .EnumerateObject()
            .Sum(item => item.Value.GetInt32())
            .Should().Be(root.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task TotalLienReportV3_keeps_non_deleted_business_statuses_and_returns_full_result_summaries()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/total-lien-report-export/v3",
            new { page = 1, limit = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        payload.RootElement.GetProperty("totalCount").GetInt32().Should().Be(5);
        payload.RootElement.GetProperty("totalPurchaseAmount").GetDecimal().Should().Be(100m);
        payload.RootElement.GetProperty("totalBillingAmount").GetDecimal().Should().Be(150m);

        var counts = payload.RootElement.GetProperty("statusCounts");
        counts.GetProperty("Open").GetInt32().Should().Be(3);
        counts.GetProperty("Closed").GetInt32().Should().Be(1);
        counts.GetProperty("Rejected").GetInt32().Should().Be(1);

        var amounts = payload.RootElement.GetProperty("statusAmounts");
        amounts.GetProperty("Open").GetProperty("purchase").GetDecimal().Should().Be(100m);
        amounts.GetProperty("Open").GetProperty("billing").GetDecimal().Should().Be(150m);
    }

    [Fact]
    public async Task TotalLienReportV3_applies_status_filter_before_fast_summary_and_paging()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/total-lien-report-export/v3",
            new
            {
                page = 1,
                limit = 1,
                filterType = "status",
                filterId = "Closed",
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        payload.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
        payload.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        payload.RootElement.GetProperty("items")[0].GetProperty("status").GetString().Should().Be("Closed");
        payload.RootElement.GetProperty("statusCounts").GetProperty("Closed").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task TotalLienReportV3_applies_purchase_dates_before_paging_and_summaries_and_defaults_no_date_to_completed_history()
    {
        var orgId = Guid.NewGuid();
        var rangeStart = new DateOnly(2020, 6, 1);
        var rangeEnd = new DateOnly(2020, 6, 30);
        var liens = new[]
        {
            CreateDateParityLien(orgId, "LIEN-DATE-IN-START", rangeStart, LienStatus.Draft, 100m),
            CreateDateParityLien(orgId, "LIEN-DATE-IN-END", rangeEnd, LienStatus.Settled, 200m),
            CreateDateParityLien(orgId, "LIEN-DATE-PAST", rangeStart.AddDays(-1), LienStatus.Cancelled, 300m),
            CreateDateParityLien(orgId, "LIEN-DATE-FUTURE", new DateOnly(2099, 1, 1), LienStatus.Draft, 400m),
            CreateDateParityLien(orgId, "LIEN-DATE-NULL", null, LienStatus.Draft, 500m),
        };
        var servicingItems = new[]
        {
            CreateDateParityMedicalCode(orgId, liens[0], 100m, 200m),
            CreateDateParityMedicalCode(orgId, liens[1], 50m, 80m),
            CreateDateParityMedicalCode(orgId, liens[2], 700m, 900m),
            CreateDateParityMedicalCode(orgId, liens[3], 1_100m, 1_500m),
            CreateDateParityMedicalCode(orgId, liens[4], 1_300m, 1_700m),
        };

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        db.Liens.AddRange(liens);
        db.ServicingItems.AddRange(servicingItems);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId, orgId));

        try
        {
            var filteredResponse = await _client.PostAsJsonAsync(
                "/api/liens/cases/dashboard/total-lien-report-export/v3",
                new
                {
                    page = 1,
                    limit = 1,
                    startDate = rangeStart.ToString("yyyy-MM-dd"),
                    endDate = rangeEnd.ToString("yyyy-MM-dd"),
                });

            filteredResponse.StatusCode.Should().Be(HttpStatusCode.OK, await filteredResponse.Content.ReadAsStringAsync());
            using (var filteredPayload = JsonDocument.Parse(await filteredResponse.Content.ReadAsStringAsync()))
            {
                var root = filteredPayload.RootElement;
                root.GetProperty("items").GetArrayLength().Should().Be(1);
                root.GetProperty("totalCount").GetInt32().Should().Be(2);
                root.GetProperty("totalPurchaseAmount").GetDecimal().Should().Be(150m);
                root.GetProperty("totalBillingAmount").GetDecimal().Should().Be(280m);
                root.GetProperty("statusCounts").GetProperty("Open").GetInt32().Should().Be(1);
                root.GetProperty("statusCounts").GetProperty("Closed").GetInt32().Should().Be(1);
                root.GetProperty("statusAmounts").GetProperty("Open").GetProperty("purchase").GetDecimal().Should().Be(100m);
                root.GetProperty("statusAmounts").GetProperty("Closed").GetProperty("billing").GetDecimal().Should().Be(80m);
            }

            var completedHistoryResponse = await _client.PostAsJsonAsync(
                "/api/liens/cases/dashboard/total-lien-report-export/v3",
                new { page = 1, limit = 10 });

            completedHistoryResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await completedHistoryResponse.Content.ReadAsStringAsync());
            using var completedHistoryPayload = JsonDocument.Parse(await completedHistoryResponse.Content.ReadAsStringAsync());
            var completedHistoryRoot = completedHistoryPayload.RootElement;
            completedHistoryRoot.GetProperty("totalCount").GetInt32().Should().Be(3);
            completedHistoryRoot.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("lienNumber").GetString())
                .Should().BeEquivalentTo(liens.Take(3).Select(item => item.LienNumber));
            completedHistoryRoot.GetProperty("totalPurchaseAmount").GetDecimal().Should().Be(850m);
            completedHistoryRoot.GetProperty("totalBillingAmount").GetDecimal().Should().Be(1_180m);
            completedHistoryRoot.GetProperty("statusCounts").GetProperty("Open").GetInt32().Should().Be(1);
            completedHistoryRoot.GetProperty("statusCounts").GetProperty("Closed").GetInt32().Should().Be(1);
            completedHistoryRoot.GetProperty("statusCounts").GetProperty("Rejected").GetInt32().Should().Be(1);
        }
        finally
        {
            db.ServicingItems.RemoveRange(servicingItems);
            db.Liens.RemoveRange(liens);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task TotalCaseReportV3_filters_explicit_dates_by_linked_purchases_and_includes_all_cases_without_dates()
    {
        var orgId = Guid.NewGuid();
        var rangeStart = new DateOnly(2020, 6, 1);
        var rangeEnd = new DateOnly(2020, 6, 30);
        var cases = new[]
        {
            CreateDateParityCase(orgId, "CASE-DATE-IN-START", CaseStatus.PreDemand),
            CreateDateParityCase(orgId, "CASE-DATE-IN-END", CaseStatus.Closed),
            CreateDateParityCase(orgId, "CASE-DATE-PAST", CaseStatus.DemandSent),
            CreateDateParityCase(orgId, "CASE-DATE-FUTURE", CaseStatus.CaseSettled),
            CreateDateParityCase(orgId, "CASE-DATE-NULL", CaseStatus.InNegotiation),
            CreateDateParityCase(orgId, "CASE-DATE-NO-LIEN", CaseStatus.PreDemand),
        };
        var liens = new[]
        {
            CreateDateParityLien(orgId, "LIEN-CASE-IN-START", rangeStart, LienStatus.Draft, 1_000m, cases[0].Id),
            CreateDateParityLien(orgId, "LIEN-CASE-IN-END", rangeEnd, LienStatus.Draft, 2_000m, cases[1].Id),
            CreateDateParityLien(orgId, "LIEN-CASE-PAST", rangeStart.AddDays(-1), LienStatus.Draft, 3_000m, cases[2].Id),
            CreateDateParityLien(orgId, "LIEN-CASE-FUTURE", new DateOnly(2099, 1, 1), LienStatus.Draft, 4_000m, cases[3].Id),
            CreateDateParityLien(orgId, "LIEN-CASE-NULL", null, LienStatus.Draft, 5_000m, cases[4].Id),
        };

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        db.Cases.AddRange(cases);
        db.Liens.AddRange(liens);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId, orgId));

        try
        {
            var filteredResponse = await _client.PostAsJsonAsync(
                "/api/liens/cases/dashboard/total-case-report-export/v3",
                new
                {
                    page = 1,
                    limit = 10,
                    purchaseDateFrom = rangeStart.ToString("MM/dd/yyyy"),
                    purchaseDateTo = rangeEnd.ToString("MM/dd/yyyy"),
                });

            filteredResponse.StatusCode.Should().Be(HttpStatusCode.OK, await filteredResponse.Content.ReadAsStringAsync());
            using (var filteredPayload = JsonDocument.Parse(await filteredResponse.Content.ReadAsStringAsync()))
            {
                var root = filteredPayload.RootElement;
                root.GetProperty("totalCount").GetInt32().Should().Be(2);
                root.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("caseNumber").GetString())
                    .Should().BeEquivalentTo(["CASE-DATE-IN-START", "CASE-DATE-IN-END"]);
                root.GetProperty("statusCounts").GetProperty(CaseStatus.PreDemand).GetInt32().Should().Be(1);
                root.GetProperty("statusCounts").GetProperty(CaseStatus.Closed).GetInt32().Should().Be(1);
                root.GetProperty("items").EnumerateArray()
                    .Single(item => item.GetProperty("caseNumber").GetString() == "CASE-DATE-IN-START")
                    .GetProperty("totalLienAmount").GetDecimal().Should().Be(1_000m);
            }

            var allCasesResponse = await _client.PostAsJsonAsync(
                "/api/liens/cases/dashboard/total-case-report-export/v3",
                new { page = 1, limit = 10 });

            allCasesResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await allCasesResponse.Content.ReadAsStringAsync());
            using var allCasesPayload = JsonDocument.Parse(await allCasesResponse.Content.ReadAsStringAsync());
            var allCasesRoot = allCasesPayload.RootElement;
            allCasesRoot.GetProperty("totalCount").GetInt32().Should().Be(cases.Length);
            allCasesRoot.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("caseNumber").GetString())
                .Should().BeEquivalentTo(cases.Select(item => item.CaseNumber));
            allCasesRoot.GetProperty("statusCounts").GetProperty(CaseStatus.PreDemand).GetInt32().Should().Be(2);
            allCasesRoot.GetProperty("statusCounts").GetProperty(CaseStatus.Closed).GetInt32().Should().Be(1);
            allCasesRoot.GetProperty("statusCounts").GetProperty(CaseStatus.DemandSent).GetInt32().Should().Be(1);
            allCasesRoot.GetProperty("statusCounts").GetProperty(CaseStatus.CaseSettled).GetInt32().Should().Be(1);
            allCasesRoot.GetProperty("statusCounts").GetProperty(CaseStatus.InNegotiation).GetInt32().Should().Be(1);
        }
        finally
        {
            db.Liens.RemoveRange(liens);
            db.Cases.RemoveRange(cases);
            await db.SaveChangesAsync();
        }
    }

    [Theory]
    [InlineData("/api/liens/cases/dashboard/total-lien-report-export/v3")]
    [InlineData("/api/liens/cases/dashboard/total-case-report-export/v3")]
    [InlineData("/api/liens/cases/dashboard/lawfirm-case-report-export/v3")]
    [InlineData("/api/liens/cases/dashboard/medical-provider-report-export/v3")]
    public async Task Dashboard_report_v3_honors_page_and_limit(string endpoint)
    {
        var firstResponse = await _client.PostAsJsonAsync(endpoint, new { page = 1, limit = 1 });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK, await firstResponse.Content.ReadAsStringAsync());

        var secondResponse = await _client.PostAsJsonAsync(endpoint, new { page = 2, limit = 1 });
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK, await secondResponse.Content.ReadAsStringAsync());

        using var firstPayload = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        using var secondPayload = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());

        firstPayload.RootElement.GetProperty("page").GetInt32().Should().Be(1);
        firstPayload.RootElement.GetProperty("pageSize").GetInt32().Should().Be(1);
        firstPayload.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        secondPayload.RootElement.GetProperty("page").GetInt32().Should().Be(2);
        secondPayload.RootElement.GetProperty("pageSize").GetInt32().Should().Be(1);
        secondPayload.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);

        var firstIds = firstPayload.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .ToHashSet(StringComparer.Ordinal);
        var secondIds = secondPayload.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .ToHashSet(StringComparer.Ordinal);
        firstIds.Intersect(secondIds).Should().BeEmpty();
    }

    [Theory]
    [InlineData("/api/liens/cases/dashboard/total-lien-report-export/v3")]
    [InlineData("/api/liens/cases/dashboard/total-case-report-export/v3")]
    [InlineData("/api/liens/cases/dashboard/lawfirm-case-report-export/v3")]
    [InlineData("/api/liens/cases/dashboard/medical-provider-report-export/v3")]
    public async Task Dashboard_report_v3_defaults_missing_or_invalid_paging_to_page_one_and_all_rows(string endpoint)
    {
        var responses = new[]
        {
            await _client.PostAsJsonAsync(endpoint, new { }),
            await _client.PostAsJsonAsync(endpoint, new { page = 0, limit = 0 }),
        };

        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var totalCount = payload.RootElement.GetProperty("totalCount").GetInt32();
            payload.RootElement.GetProperty("page").GetInt32().Should().Be(1);
            payload.RootElement.GetProperty("pageSize").GetInt32().Should().Be(totalCount);
            payload.RootElement.GetProperty("items").GetArrayLength().Should().Be(totalCount);
        }
    }

    [Fact]
    public async Task Dashboard_report_v3_honors_limit_above_former_five_hundred_row_cap()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/total-lien-report-export/v3",
            new { page = 1, limit = 1_000 });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("page").GetInt32().Should().Be(1);
        payload.RootElement.GetProperty("pageSize").GetInt32().Should().Be(1_000);
        payload.RootElement.GetProperty("items").GetArrayLength().Should().Be(
            payload.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task TotalLienReportV3_resolves_facility_id_from_contact_facility_name()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/total-lien-report-export/v3",
            new { page = 1, limit = 10 });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = payload.RootElement.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("lienNumber").GetString() == "LIEN-TEST-001");
        item.GetProperty("facilityId").GetString().Should().Be(SeedHelper.MedicalFacilityContactId.ToString());
        item.GetProperty("facilityName").GetString().Should().Be("Sunrise Clinic");
    }

    [Fact]
    public async Task LawFirmCaseReportV3_filters_by_law_firm()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/lawfirm-case-report-export/v3",
            new
            {
                page = 1,
                limit = 10,
                filterType = "lawfirm",
                filterId = SeedHelper.LawFirmId.ToString(),
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        payload.RootElement.GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("lawFirmId").GetString() == SeedHelper.LawFirmId.ToString());
    }

    [Fact]
    public async Task MedicalProviderReportV3_filters_by_medical_provider()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/medical-provider-report-export/v3",
            new
            {
                page = 1,
                limit = 10,
                filterType = "medicalProvider",
                filterId = SeedHelper.MedicalProviderId.ToString(),
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        payload.RootElement.GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("medicalProviderId").GetString() == SeedHelper.MedicalProviderId.ToString());
    }

    [Fact]
    public async Task MedicalProviderReportV3_filters_by_purchase_date_alias_range()
    {
        var purchaseDate = new DateOnly(2024, 6, 15).ToString("MM/dd/yyyy");

        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/medical-provider-report-export/v3",
            new
            {
                page = 1,
                limit = 10,
                purchaseDateFrom = purchaseDate,
                purchaseDateTo = purchaseDate,
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        payload.RootElement.GetProperty("items")[0].GetProperty("lienNumber").GetString()
            .Should().Be("LIEN-TEST-001");
    }

    [Fact]
    public async Task MedicalProviderReportV3_pages_rows_and_preserves_full_allocation_counts()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/medical-provider-report-export/v3",
            new { page = 1, limit = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        root.GetProperty("items").GetArrayLength().Should().Be(1);
        root.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(1);
        root.GetProperty("allocationCounts").EnumerateObject()
            .Sum(property => property.Value.GetInt32())
            .Should().Be(root.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task LawFirmCaseReportV3_pages_rows_and_preserves_full_allocation_counts()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/lawfirm-case-report-export/v3",
            new { page = 1, limit = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        root.GetProperty("items").GetArrayLength().Should().Be(1);
        root.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(1);
        root.GetProperty("allocationCounts").EnumerateObject()
            .Sum(property => property.Value.GetInt32())
            .Should().Be(root.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task LawFirmCaseReportV3_filters_cases_by_any_lien_purchase_date()
    {
        var purchaseDate = new DateOnly(2024, 6, 15).ToString("MM/dd/yyyy");

        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/lawfirm-case-report-export/v3",
            new
            {
                page = 1,
                limit = 10,
                purchaseDateFrom = purchaseDate,
                purchaseDateTo = purchaseDate,
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("caseNumber").GetString() == "CASE-TEST-001");
    }

    [Fact]
    public async Task Dashboard_metrics_use_purchase_and_settlement_dates()
    {
        var deployedResponse = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/deployed",
            new { startDate = "06/15/2024", endDate = "06/15/2024" });
        deployedResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await deployedResponse.Content.ReadAsStringAsync());

        using var deployed = JsonDocument.Parse(await deployedResponse.Content.ReadAsStringAsync());
        deployed.RootElement.GetProperty("data").GetProperty("totalAmount").GetString()
            .Should().Be("100.00");

        var receivedResponse = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/cash-received",
            new { startDate = "06/20/2024", endDate = "06/20/2024" });
        receivedResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await receivedResponse.Content.ReadAsStringAsync());

        using var received = JsonDocument.Parse(await receivedResponse.Content.ReadAsStringAsync());
        received.RootElement.GetProperty("data").GetProperty("totalAmount").GetString()
            .Should().Be("250.00");
    }

    [Fact]
    public async Task Dashboard_metrics_without_date_range_include_completed_dated_history()
    {
        var deployedResponse = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/deployed",
            new { });
        deployedResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await deployedResponse.Content.ReadAsStringAsync());

        using var deployed = JsonDocument.Parse(await deployedResponse.Content.ReadAsStringAsync());
        var deployedData = deployed.RootElement.GetProperty("data");
        deployedData.GetProperty("periodStart").GetString().Should().BeEmpty();
        deployedData.GetProperty("periodEnd").GetString().Should().BeEmpty();
        deployedData.GetProperty("totalAmount").GetString().Should().Be("100.00");
        deployedData.GetProperty("totalCount").GetInt32().Should().Be(1);

        var receivedResponse = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/cash-received",
            new { });
        receivedResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await receivedResponse.Content.ReadAsStringAsync());

        using var received = JsonDocument.Parse(await receivedResponse.Content.ReadAsStringAsync());
        var receivedData = received.RootElement.GetProperty("data");
        receivedData.GetProperty("periodStart").GetString().Should().BeEmpty();
        receivedData.GetProperty("periodEnd").GetString().Should().BeEmpty();
        receivedData.GetProperty("totalAmount").GetString().Should().Be("250.00");
        receivedData.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Dashboard_csv_exports_all_matching_rows_not_only_requested_page()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/total-lien-report-export/v3",
            new { page = 1, limit = 1, isCsv = true });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(
            payload.RootElement.GetProperty("data")[0].GetProperty("base64").GetString()!));
        csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(6);
    }

    [Fact]
    public async Task LawFirmCaseReportV3_canonicalizes_law_firm_id_from_matching_label()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/lawfirm-case-report-export/v3",
            new
            {
                page = 1,
                limit = 50,
                filterType = "lawfirm",
                filterId = SeedHelper.LawFirmId.ToString(),
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("items").EnumerateArray()
            .Should().Contain(item =>
                item.GetProperty("id").GetString() == AliasedLawFirmCaseId.ToString() &&
                item.GetProperty("lawFirm").GetString() == "Smith & Associates LLP" &&
                item.GetProperty("lawFirmId").GetString() == SeedHelper.LawFirmId.ToString());
    }

    [Fact]
    public async Task MedicalProviderReportV3_canonicalizes_provider_id_from_matching_label()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/medical-provider-report-export/v3",
            new
            {
                page = 1,
                limit = 50,
                filterType = "medicalProvider",
                filterId = SeedHelper.MedicalProviderId.ToString(),
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("items").EnumerateArray()
            .Should().Contain(item =>
                item.GetProperty("id").GetString() == AliasedProviderLienId.ToString() &&
                item.GetProperty("medicalProvider").GetString() == "City Medical Center" &&
                item.GetProperty("medicalProviderId").GetString() == SeedHelper.MedicalProviderId.ToString());
    }

    [Theory]
    [InlineData("/api/liens/cases/dashboard/total-case-report-export/v3", "yes", "Case ID,Plaintiff Name,Date of Loss,Status")]
    [InlineData("/api/liens/cases/dashboard/lawfirm-case-report-export/v3", "yes", "Case ID,Plaintiff Name,Date of Loss,Law Firm")]
    [InlineData("/api/liens/cases/dashboard/medical-provider-report-export/v3", "yes", "Case ID,Plaintiff Name,Date of Loss,Medical Facility")]
    [InlineData("/api/liens/cases/dashboard/total-lien-report-export/v3", "yes", "Lien ID,Case ID,Plaintiff Name,Lien Status")]
    [InlineData("/api/liens/cases/dashboard/total-lien-report-export/v3", "true", "Lien ID,Case ID,Plaintiff Name,Lien Status")]
    public async Task DashboardReportV3_returns_base64_csv_when_is_csv_is_yes_or_true(
        string path,
        string isCsv,
        string expectedHeader)
    {
        var response = await _client.PostAsJsonAsync(path, new { page = 1, limit = 10, isCsv });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        payload.RootElement.GetProperty("message").GetString().Should().Be("CSV generated successfully.");
        payload.RootElement.TryGetProperty("items", out _).Should().BeFalse();

        var exportItems = payload.RootElement.GetProperty("data").EnumerateArray().ToList();
        exportItems.Should().ContainSingle();
        var exportItem = exportItems.Single();
        exportItem.GetProperty("filename").GetString().Should().EndWith(".csv");
        exportItem.GetProperty("export_format").GetString().Should().Be("csv");

        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(exportItem.GetProperty("base64").GetString()!));
        csv.Split('\n')[0].TrimEnd('\r').Should().Be(expectedHeader);
    }

    [Fact]
    public async Task TotalCaseReportV3_returns_base64_csv_when_is_csv_is_boolean_true()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/total-case-report-export/v3",
            new { page = 1, limit = 10, isCsv = true });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(
            payload.RootElement.GetProperty("data")[0].GetProperty("base64").GetString()!));
        csv.Split('\n')[0].TrimEnd('\r').Should().Be("Case ID,Plaintiff Name,Date of Loss,Status");
    }

    [Theory]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("no")]
    public async Task TotalCaseReportV3_returns_paginated_json_when_is_csv_is_not_true_or_yes(string isCsv)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/total-case-report-export/v3",
            new { page = 1, limit = 10, isCsv });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        payload.RootElement.TryGetProperty("data", out _).Should().BeFalse();
    }

    private static async Task SeedDashboardDataAsync(LiensDbContext db)
    {
        if (!db.Contacts.Any(c => c.Id == CaseManagerContactId))
        {
            var caseManager = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                ContactType.CaseManager,
                "Case",
                "Manager",
                SeedHelper.UserId,
                organization: "Smith & Associates LLP",
                email: "case.manager@example.com");
            SetId(caseManager, CaseManagerContactId);
            db.Contacts.Add(caseManager);
        }

        var caseEntity = await db.Cases.FindAsync(SeedHelper.CaseId);
        caseEntity.Should().NotBeNull();
        caseEntity!.Update(
            caseEntity.ClientFirstName,
            caseEntity.ClientLastName,
            SeedHelper.UserId,
            title: caseEntity.Title,
            externalReference: caseEntity.ExternalReference,
            clientDob: caseEntity.ClientDob,
            clientPhone: caseEntity.ClientPhone,
            clientEmail: caseEntity.ClientEmail,
            clientAddress: caseEntity.ClientAddress,
            dateOfIncident: caseEntity.DateOfIncident,
            insuranceCarrier: caseEntity.InsuranceCarrier,
            policyNumber: caseEntity.PolicyNumber,
            claimNumber: caseEntity.ClaimNumber,
            description: caseEntity.Description,
            notes: "lawFirmId=40000000-0000-0000-0000-000000000010; lawFirm=Smith & Associates LLP; caseManagerId=40000000-0000-0000-0000-000000000099; caseManager=Case Manager; accidentTypeId=MVA; accidentType=Motor Vehicle Accident");

        var lienEntity = await db.Liens.FindAsync(SeedHelper.LienId);
        lienEntity.Should().NotBeNull();
        lienEntity!.Update(
            lienEntity.LienType,
            lienEntity.OriginalAmount,
            SeedHelper.UserId,
            externalReference: lienEntity.ExternalReference,
            subjectFirstName: lienEntity.SubjectFirstName,
            subjectLastName: lienEntity.SubjectLastName,
            isConfidential: lienEntity.IsConfidential,
            jurisdiction: lienEntity.Jurisdiction,
            incidentDate: new DateOnly(2024, 6, 15),
            initialServiceDate: lienEntity.InitialServiceDate,
            endServiceDate: lienEntity.EndServiceDate,
            isBulk: lienEntity.IsBulk,
            isServicing: lienEntity.IsServicing,
            description: lienEntity.Description,
            notes: lienEntity.Notes,
            purchaseDate: new DateOnly(2024, 6, 15));
        lienEntity.SetFinancials(
            lienEntity.OriginalAmount,
            SeedHelper.UserId,
            currentBalance: lienEntity.CurrentBalance,
            offerPrice: lienEntity.OfferPrice,
            purchasePrice: 100m,
            payoffAmount: lienEntity.PayoffAmount);

        if (!db.ServicingItems.Any(s => s.LienId == SeedHelper.LienId && s.TaskType == "LegacyMedicalFacilityInfo"))
        {
            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "SVC-DASH-001",
                "LegacyMedicalFacilityInfo",
                "Facility info for dashboard report",
                "system",
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                lienId: SeedHelper.LienId,
                notes: $"facilityName=Sunrise Clinic; facilityContactPerson=Alice Nurse; email=alice@sunrise.com; phone=555-0101; medicalProviderId={SeedHelper.MedicalProviderId}; medicalProvider=City Medical Center"));
        }

        if (!db.ServicingItems.Any(s => s.LienId == SeedHelper.LienId && s.TaskType == "LegacyMedicalCode"))
        {
            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "SVC-DASH-002",
                "LegacyMedicalCode",
                "Medical code for dashboard report",
                "system",
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                lienId: SeedHelper.LienId,
                notes: "code=12345; medicareCost=75.00; billingAmount=150.00; purchaseAmount=100.00; payee=Health System; outboundCheckNumber=CHK-100"));
        }

        if (!db.Cases.Any(c => c.Id == AliasedLawFirmCaseId))
        {
            var aliasedCase = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "CASE-TEST-ALIAS",
                "Alias",
                "Client",
                SeedHelper.UserId,
                title: "Alias law firm case",
                dateOfIncident: new DateOnly(2024, 6, 15),
                notes: "lawFirmId=019f6aea-947f-7985-955f-cf69b056d289; lawFirm=Smith & Associates LLP; accidentTypeId=MVA; accidentType=Motor Vehicle Accident");
            SetId(aliasedCase, AliasedLawFirmCaseId);
            db.Cases.Add(aliasedCase);
        }

        if (!db.Liens.Any(l => l.Id == AliasedProviderLienId))
        {
            var aliasedLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LIEN-TEST-ALIAS",
                LienType.MedicalLien,
                2500m,
                SeedHelper.UserId,
                caseId: AliasedLawFirmCaseId,
                facilityId: SeedHelper.FacilityId,
                purchaseDate: new DateOnly(2024, 6, 14));
            SetId(aliasedLien, AliasedProviderLienId);
            db.Liens.Add(aliasedLien);
        }

        if (!db.Companies.Any(company => company.Id == CanonicalAssistantLawFirmCompanyId))
        {
            var canonicalLawFirm = Company.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                CompanyDirectoryReferenceData.LawFirmId,
                "Canonical Assistant Law",
                SeedHelper.UserId);
            SetId(canonicalLawFirm, CanonicalAssistantLawFirmCompanyId);
            db.Companies.Add(canonicalLawFirm);
        }

        if (!db.Cases.Any(c => c.Id == CanonicalAssistantCaseId))
        {
            var canonicalCase = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "CASE-CANON-LAW-1",
                "Canonical",
                "Client",
                SeedHelper.UserId,
                title: "Canonical law firm assistant case");
            SetId(canonicalCase, CanonicalAssistantCaseId);
            canonicalCase.LinkCanonicalCaseParties(CanonicalAssistantLawFirmCompanyId, null);
            db.Cases.Add(canonicalCase);
        }

        if (!db.Liens.Any(l => l.Id == ActiveDashboardLienId))
        {
            var activeLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LIEN-DASH-ACTIVE",
                LienType.MedicalLien,
                1500m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                facilityId: SeedHelper.FacilityId,
                purchaseDate: new DateOnly(2024, 6, 14));
            SetId(activeLien, ActiveDashboardLienId);
            activeLien.ListForSale(1000m, SeedHelper.UserId);
            activeLien.MarkSold(1000m, SeedHelper.OrgId, SeedHelper.UserId);
            activeLien.Activate(SeedHelper.UserId);
            db.Liens.Add(activeLien);
        }

        if (!db.Liens.Any(l => l.Id == CancelledDashboardLienId))
        {
            var cancelledLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LIEN-DASH-CANCELLED",
                LienType.MedicalLien,
                1600m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                facilityId: SeedHelper.FacilityId,
                purchaseDate: new DateOnly(2024, 6, 14));
            SetId(cancelledLien, CancelledDashboardLienId);
            cancelledLien.TransitionStatus(LienStatus.Cancelled, SeedHelper.UserId);
            db.Liens.Add(cancelledLien);
        }

        if (!db.Liens.Any(l => l.Id == SettledDashboardLienId))
        {
            var settledLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LIEN-DASH-SETTLED",
                LienType.MedicalLien,
                1700m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                facilityId: SeedHelper.FacilityId,
                purchaseDate: new DateOnly(2024, 6, 14));
            SetId(settledLien, SettledDashboardLienId);
            settledLien.ListForSale(1100m, SeedHelper.UserId);
            settledLien.MarkSold(1100m, SeedHelper.OrgId, SeedHelper.UserId);
            settledLien.Activate(SeedHelper.UserId);
            settledLien.Settle(0m, SeedHelper.UserId);
            db.Liens.Add(settledLien);
        }

        if (!db.ServicingItems.Any(s => s.LienId == AliasedProviderLienId && s.TaskType == "LegacyMedicalFacilityInfo"))
        {
            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "SVC-DASH-ALIAS-001",
                "LegacyMedicalFacilityInfo",
                "Facility info for alias dashboard report",
                "system",
                SeedHelper.UserId,
                caseId: AliasedLawFirmCaseId,
                lienId: AliasedProviderLienId,
                notes: "facilityName=Sunrise Clinic; facilityContactPerson=Alice Nurse; email=alice@sunrise.com; phone=555-0101; medicalProviderId=019f6b0e-4e90-78ea-908d-f2e94adc33b2; medicalProvider=City Medical Center"));
        }

        if (!db.LienSettlements.Any(s =>
                s.LienId == SeedHelper.LienId &&
                s.SettlementDate == new DateOnly(2024, 6, 20)))
        {
            db.LienSettlements.Add(LienSettlement.Create(
                SeedHelper.TenantId,
                SeedHelper.CaseId,
                SeedHelper.LienId,
                1,
                250m,
                SeedHelper.UserId,
                status: "Settled",
                note: "Dashboard cash received test",
                settlementDate: new DateOnly(2024, 6, 20)));
        }

        await db.SaveChangesAsync();
    }

    private static Case CreateDateParityCase(Guid orgId, string caseNumber, string status)
    {
        var caseEntity = Case.Create(
            SeedHelper.TenantId,
            orgId,
            caseNumber,
            "Date",
            "Parity",
            SeedHelper.UserId);
        if (!string.Equals(status, CaseStatus.PreDemand, StringComparison.Ordinal))
            caseEntity.TransitionStatus(status, SeedHelper.UserId);

        return caseEntity;
    }

    private static Lien CreateDateParityLien(
        Guid orgId,
        string lienNumber,
        DateOnly? purchaseDate,
        string status,
        decimal originalAmount,
        Guid? caseId = null)
    {
        var lien = Lien.Create(
            SeedHelper.TenantId,
            orgId,
            lienNumber,
            LienType.MedicalLien,
            originalAmount,
            SeedHelper.UserId,
            caseId: caseId,
            purchaseDate: purchaseDate);
        if (!string.Equals(status, LienStatus.Draft, StringComparison.Ordinal))
            lien.SetLegacyMedicalStatus(status, SeedHelper.UserId);

        return lien;
    }

    private static ServicingItem CreateDateParityMedicalCode(
        Guid orgId,
        Lien lien,
        decimal purchaseAmount,
        decimal billingAmount) =>
        ServicingItem.Create(
            SeedHelper.TenantId,
            orgId,
            $"SVC-{lien.LienNumber}",
            "LegacyMedicalCode",
            "Medical code for dashboard date parity",
            "system",
            SeedHelper.UserId,
            lienId: lien.Id,
            notes: FormattableString.Invariant(
                $"purchaseAmount={purchaseAmount:0.00}; billingAmount={billingAmount:0.00}"));

    private static void SetId<T>(T entity, Guid id) where T : class
    {
        var prop = typeof(T).GetProperty("Id",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(entity, id);
    }
}
