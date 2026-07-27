using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Notifications;
using Liens.Api.Tests.Helpers;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using NPOI.HSSF.UserModel;

namespace Liens.Api.Tests.Tests;

public class SellingPortfolioEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public SellingPortfolioEndpointTests(LiensApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedHelper.SeedAsync(scope.ServiceProvider);
        scope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>().Clear();
        scope.ServiceProvider.GetRequiredService<CapturingAuditPublisher>().Clear();
        scope.ServiceProvider.GetRequiredService<CapturingPublicBuyerAccountProvisioningService>().Clear();

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",
                JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreatePortfolio_returns_created_with_lien_snapshot_and_initial_history()
    {
        var (caseId, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: "ehr-case-123",
            lienExternalId: "ehr-lien-456",
            lienNumber: $"LIEN-SELL-{Guid.NewGuid():N}");

        var request = new CreateSellingPortfolioRequest
        {
            PortfolioNumber = $"PORT-{Guid.NewGuid():N}"[..20],
            Name = "June imaging sale pool",
            Description = "Initial Las Vegas Imaging sale portfolio",
            InternalNotes = "Seller operations only",
            TargetGrouping = "Imaging",
            LienIds = [lienId],
            BuyerOrgIds = [SeedHelper.FundingCompanyId],
        };

        var response = await _client.PostAsJsonAsync("/api/liens/selling/portfolios", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<SellingPortfolioResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().NotBe(Guid.Empty);
        body.Status.Should().Be(SellingPortfolioStatus.Draft);
        body.InternalNotes.Should().Be("Seller operations only");
        body.TargetGrouping.Should().Be("Imaging");
        body.SellerOrgId.Should().Be(SeedHelper.OrgId);
        body.LienCount.Should().Be(1);
        body.OriginalAmountTotal.Should().Be(12345m);
        body.Liens.Should().ContainSingle();
        body.Liens[0].LienId.Should().Be(lienId);
        body.Liens[0].CaseId.Should().Be(caseId);
        body.Liens[0].CaseExternalId.Should().Be("ehr-case-123");
        body.Liens[0].LienExternalId.Should().Be("ehr-lien-456");
        body.Liens[0].LienLifecycleStatus.Should().Be(LienStatus.Draft);
        body.Buyers.Should().ContainSingle(b => b.BuyerOrgId == SeedHelper.FundingCompanyId);

        var historyResponse = await _client.GetAsync($"/api/liens/selling/portfolios/{body.Id}/status-history");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await historyResponse.Content.ReadFromJsonAsync<List<SellingPortfolioStatusHistoryResponse>>();
        history.Should().NotBeNull();
        history.Should().ContainSingle();
        history![0].FromStatus.Should().BeNull();
        history[0].ToStatus.Should().Be(SellingPortfolioStatus.Draft);

        var activityResponse = await _client.GetAsync($"/api/liens/selling/portfolios/{body.Id}/activity");
        activityResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var activity = await activityResponse.Content.ReadFromJsonAsync<List<SellingPortfolioActivityResponse>>();
        activity.Should().NotBeNull();
        activity.Should().ContainSingle(a => a.Action == "LIEN_SALE_PORTFOLIO_CREATED");
    }

    [Fact]
    public async Task ImportPatientDetailsReport_saves_all_rows_into_batch_upload_storage()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("June patient details"), "label");

        var fileContent = new ByteArrayContent(CreatePatientDetailsWorkbookBytes());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.ms-excel");
        form.Add(fileContent, "file", "Patient_Details_Report.xls");

        var response = await _client.PostAsync("/api/liens/selling/imports/patient-details", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        body.Should().NotBeNull();

        var root = body!.RootElement;
        var importId = root.GetProperty("id").GetGuid();
        root.GetProperty("label").GetString().Should().Be("June patient details");
        root.GetProperty("template").GetString().Should().Be("SELLING_PATIENT_DETAILS_REPORT");
        root.GetProperty("fileName").GetString().Should().Be("Patient_Details_Report.xls");
        root.GetProperty("rowCount").GetInt32().Should().Be(2);
        root.GetProperty("columnCount").GetInt32().Should().BeGreaterThan(10);

        var previewRows = root.GetProperty("previewRows").EnumerateArray().ToList();
        previewRows.Should().HaveCount(2);
        previewRows[0].GetProperty("Last Name").GetString().Should().Be("ABAD");
        previewRows[0].GetProperty("First Name").GetString().Should().Be("JACQUELINE");
        previewRows[0].GetProperty("MR#").GetString().Should().Be("2207");
        previewRows[1].GetProperty("Legal Entity").GetString().Should().Be("Las Vegas Imaging");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

        var batch = await db.BatchUploads.FindAsync(importId);
        batch.Should().NotBeNull();
        batch!.TenantId.Should().Be(SeedHelper.TenantId);
        batch.Label.Should().Be("June patient details");
        batch.Template.Should().Be("SELLING_PATIENT_DETAILS_REPORT");
        batch.Rows.Should().Be(2);

        var detailRows = db.BatchUploadDetails
            .Where(x => x.BatchUploadId == importId)
            .OrderBy(x => x.RowNumber)
            .ToList();

        detailRows.Should().HaveCount(2);
        detailRows[0].DataJson.Should().Contain("\"Last Name\":\"ABAD\"");
        detailRows[0].DataJson.Should().Contain("\"Cell Phone\":\"(702)237-1807\"");
        detailRows[1].DataJson.Should().Contain("\"State\":\"NV\"");
    }

    [Fact]
    public async Task ImportPatientDetailsReport_accepts_html_export_with_xls_extension()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("HTML patient details"), "label");

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(CreatePatientDetailsHtmlExport()));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.ms-excel");
        form.Add(fileContent, "file", "Patient_Details_Report.xls");

        var response = await _client.PostAsync("/api/liens/selling/imports/patient-details", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        body.Should().NotBeNull();

        var root = body!.RootElement;
        root.GetProperty("rowCount").GetInt32().Should().Be(2);
        root.GetProperty("previewRows")[0].GetProperty("Last Name").GetString().Should().Be("ABAD");
        root.GetProperty("previewRows")[1].GetProperty("State").GetString().Should().Be("NV");
    }

    [Fact]
    public async Task Analytics_returns_financial_aging_and_activity_metrics()
    {
        var portfolio = await CreatePortfolioAsync();

        var response = await _client.GetAsync($"/api/liens/selling/portfolios/{portfolio.Id}/analytics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SellingPortfolioAnalyticsResponse>();
        body.Should().NotBeNull();
        body!.PortfolioId.Should().Be(portfolio.Id);
        body.Financial.TotalReceivables.Should().Be(12345m);
        body.Financial.TotalOutstandingBalance.Should().Be(12345m);
        body.Financial.AverageLienBalance.Should().Be(12345m);
        body.Operational.LienCount.Should().Be(1);
        body.Operational.ActivityCount.Should().BeGreaterThan(0);
        body.AgingBuckets.Sum(b => b.LienCount).Should().Be(1);
    }

    private static byte[] CreatePatientDetailsWorkbookBytes()
    {
        var workbook = new HSSFWorkbook();
        var sheet = workbook.CreateSheet("Patient_Details_Report");

        sheet.CreateRow(0).CreateCell(0).SetCellValue("Patient Details Report");
        var header = sheet.CreateRow(11);
        var columns = new[]
        {
            "#", "Last Name", "First Name", "Middle Name", "MR#", "PC Ref#", "Gender", "DOB", "Race",
            "Ethnicity", "Language", "Sexual Orientation", "Gender Identity", "Home Phone", "Work Phone",
            "Cell Phone", "E-Mail", "Address", "City", "State", "Zip", "Country", "Legal Entity",
            "Provider", "Referring Provider"
        };

        for (var i = 0; i < columns.Length; i++)
            header.CreateCell(i).SetCellValue(columns[i]);

        var first = sheet.CreateRow(12);
        first.CreateCell(0).SetCellValue("1");
        first.CreateCell(1).SetCellValue("ABAD");
        first.CreateCell(2).SetCellValue("JACQUELINE");
        first.CreateCell(4).SetCellValue("2207");
        first.CreateCell(5).SetCellValue("5/7/2026");
        first.CreateCell(6).SetCellValue("Female");
        first.CreateCell(7).SetCellValue("01/17/2002");
        first.CreateCell(10).SetCellValue("English");
        first.CreateCell(15).SetCellValue("(702)237-1807");
        first.CreateCell(17).SetCellValue("2737 MAGNET STREET,");
        first.CreateCell(18).SetCellValue("NORTH LAS VEGAS");
        first.CreateCell(19).SetCellValue("NV");
        first.CreateCell(20).SetCellValue("89030");
        first.CreateCell(21).SetCellValue("USA");
        first.CreateCell(22).SetCellValue("Las Vegas Imaging");
        first.CreateCell(23).SetCellValue("IMAGING, LAS VEGAS");

        var second = sheet.CreateRow(13);
        second.CreateCell(0).SetCellValue("2");
        second.CreateCell(1).SetCellValue("ABEBE");
        second.CreateCell(2).SetCellValue("AMARECH");
        second.CreateCell(4).SetCellValue("1405");
        second.CreateCell(5).SetCellValue("10/26/2025");
        second.CreateCell(6).SetCellValue("Female");
        second.CreateCell(7).SetCellValue("12/07/1973");
        second.CreateCell(10).SetCellValue("English");
        second.CreateCell(13).SetCellValue("(702)465-0925");
        second.CreateCell(17).SetCellValue("5063 W DODGE RIDGE AVE ,");
        second.CreateCell(18).SetCellValue("LAS VEGAS");
        second.CreateCell(19).SetCellValue("NV");
        second.CreateCell(20).SetCellValue("89139");
        second.CreateCell(21).SetCellValue("USA");
        second.CreateCell(22).SetCellValue("Las Vegas Imaging");
        second.CreateCell(23).SetCellValue("IMAGING, LAS VEGAS");
        second.CreateCell(24).SetCellValue("LAS VEGAS SPORTS AND SPINE CEN, ALYSSA KIAT-ONG");

        using var stream = new MemoryStream();
        workbook.Write(stream, leaveOpen: true);
        return stream.ToArray();
    }

    private static string CreatePatientDetailsHtmlExport()
    {
        return """



        <html>
            <head>
                <title>Patient Details</title>
            </head>
            <body>
                <table border="1" class="f_table2">
                    <tr>
                        <td align="center" colspan="25"><b>Patient Details Report</b></td>
                    </tr>
                    <tr>
                        <td>#</td>
                        <td>Last Name</td>
                        <td>First Name</td>
                        <td>Middle Name</td>
                        <td>MR#</td>
                        <td>PC Ref#</td>
                        <td>Gender</td>
                        <td>DOB</td>
                        <td>Race</td>
                        <td>Ethnicity</td>
                        <td>Language</td>
                        <td>Sexual Orientation</td>
                        <td>Gender Identity</td>
                        <td>Home Phone</td>
                        <td>Work Phone</td>
                        <td>Cell Phone</td>
                        <td>E-Mail</td>
                        <td>Address</td>
                        <td>City</td>
                        <td>State</td>
                        <td>Zip</td>
                        <td>Country</td>
                        <td>Legal Entity</td>
                        <td>Provider</td>
                        <td>Referring Provider</td>
                    </tr>
                    <tr>
                        <td>1</td>
                        <td>ABAD</td>
                        <td>JACQUELINE</td>
                        <td></td>
                        <td>2207</td>
                        <td>5/7/2026</td>
                        <td>Female</td>
                        <td>01/17/2002</td>
                        <td></td>
                        <td></td>
                        <td>English</td>
                        <td></td>
                        <td></td>
                        <td></td>
                        <td></td>
                        <td>(702)237-1807</td>
                        <td></td>
                        <td>2737 MAGNET STREET,</td>
                        <td>NORTH LAS VEGAS</td>
                        <td>NV</td>
                        <td>89030</td>
                        <td>USA</td>
                        <td>Las Vegas Imaging</td>
                        <td>IMAGING, LAS VEGAS</td>
                        <td></td>
                    </tr>
                    <tr>
                        <td>2</td>
                        <td>ABEBE</td>
                        <td>AMARECH</td>
                        <td></td>
                        <td>1405</td>
                        <td>10/26/2025</td>
                        <td>Female</td>
                        <td>12/07/1973</td>
                        <td></td>
                        <td></td>
                        <td>English</td>
                        <td></td>
                        <td></td>
                        <td>(702)465-0925</td>
                        <td></td>
                        <td></td>
                        <td></td>
                        <td>5063 W DODGE RIDGE AVE ,</td>
                        <td>LAS VEGAS</td>
                        <td>NV</td>
                        <td>89139</td>
                        <td>USA</td>
                        <td>Las Vegas Imaging</td>
                        <td>IMAGING, LAS VEGAS</td>
                        <td></td>
                    </tr>
                </table>
            </body>
        </html>
        """;
    }

    [Fact]
    public async Task Analytics_uses_settlement_payment_details_for_payment_totals_and_exposure()
    {
        var portfolio = await CreatePortfolioAsync();
        var lien = portfolio.Liens.Single();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.LienSettlements.Add(LienSettlement.Create(
                SeedHelper.TenantId,
                lien.CaseId!.Value,
                lien.LienId,
                paymentNumber: 1,
                amount: 10000m,
                SeedHelper.UserId,
                status: "Pending"));
            db.SettlementPaymentDetails.Add(SettlementPaymentDetail.Create(
                SeedHelper.TenantId,
                lien.CaseId.Value,
                lien.LienId,
                paymentNumber: 1,
                amount: 2500m,
                SeedHelper.UserId,
                paymentDate: new DateOnly(2026, 6, 1),
                payee: "Provider"));
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/liens/selling/portfolios/{portfolio.Id}/analytics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SellingPortfolioAnalyticsResponse>();
        body.Should().NotBeNull();
        body!.Financial.PaymentTotal.Should().Be(2500m);
        body.Financial.SettlementExposure.Should().Be(7500m);
    }

    [Fact]
    public async Task Publish_endpoint_promotes_draft_to_published_and_records_activity()
    {
        var portfolio = await CreatePortfolioAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/publish",
            new TransitionSellingPortfolioStatusRequest { Notes = "Publish pool" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SellingPortfolioResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(SellingPortfolioStatus.Published);
        body.PublishedAtUtc.Should().NotBeNull();

        var history = await _client.GetFromJsonAsync<List<SellingPortfolioStatusHistoryResponse>>(
            $"/api/liens/selling/portfolios/{portfolio.Id}/status-history");
        history.Should().NotBeNull();
        history!.Should().Contain(h => h.FromStatus == SellingPortfolioStatus.Draft && h.ToStatus == SellingPortfolioStatus.ReadyForReview);
        history.Should().Contain(h => h.FromStatus == SellingPortfolioStatus.ReadyForReview && h.ToStatus == SellingPortfolioStatus.Published);

        var activity = await _client.GetFromJsonAsync<List<SellingPortfolioActivityResponse>>(
            $"/api/liens/selling/portfolios/{portfolio.Id}/activity");
        activity.Should().NotBeNull();
        activity!.Should().Contain(a => a.Action == "LIEN_SALE_PORTFOLIO_PUBLISHED");
    }

    [Fact]
    public async Task Publish_endpoint_accepts_empty_body()
    {
        var portfolio = await CreatePortfolioAsync();

        var response = await _client.PostAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/publish",
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SellingPortfolioResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(SellingPortfolioStatus.Published);
    }


    [Fact]
    public async Task Portfolio_lien_reuses_existing_case_reference_without_creating_duplicate_case()
    {
        var (caseId, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: "canonical-case",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}");

        int caseCountBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            caseCountBefore = db.Cases.Count(c => c.TenantId == SeedHelper.TenantId);
        }

        var response = await _client.PostAsJsonAsync("/api/liens/selling/portfolios",
            new CreateSellingPortfolioRequest
            {
                PortfolioNumber = $"PORT-{Guid.NewGuid():N}"[..20],
                Name = "Canonical case portfolio",
                LienIds = [lienId],
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var portfolio = (await response.Content.ReadFromJsonAsync<SellingPortfolioResponse>())!;
        portfolio.Liens.Should().ContainSingle();
        portfolio.Liens[0].CaseId.Should().Be(caseId);
        portfolio.Liens[0].CaseExternalId.Should().Be("canonical-case");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Cases.Count(c => c.TenantId == SeedHelper.TenantId).Should().Be(caseCountBefore);
        }
    }

    [Fact]
    public async Task TransitionStatus_allows_valid_transition_and_records_history()
    {
        var portfolio = await CreatePortfolioAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/status",
            new TransitionSellingPortfolioStatusRequest
            {
                Status = SellingPortfolioStatus.ReadyForReview,
                Notes = "Ready for operations review",
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SellingPortfolioResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(SellingPortfolioStatus.ReadyForReview);

        var historyResponse = await _client.GetAsync($"/api/liens/selling/portfolios/{portfolio.Id}/status-history");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<SellingPortfolioStatusHistoryResponse>>();
        history.Should().NotBeNull();
        history!.Should().Contain(h =>
            h.FromStatus == SellingPortfolioStatus.Draft &&
            h.ToStatus == SellingPortfolioStatus.ReadyForReview &&
            h.Notes == "Ready for operations review");
    }

    [Fact]
    public async Task TransitionStatus_blocks_invalid_transition()
    {
        var portfolio = await CreatePortfolioAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/status",
            new TransitionSellingPortfolioStatusRequest
            {
                Status = SellingPortfolioStatus.Accepted,
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var unchanged = await _client.GetFromJsonAsync<SellingPortfolioResponse>(
            $"/api/liens/selling/portfolios/{portfolio.Id}");
        unchanged.Should().NotBeNull();
        unchanged!.Status.Should().Be(SellingPortfolioStatus.Draft);
    }

    [Fact]
    public async Task DirectReads_deny_other_seller_org_portfolios()
    {
        var otherOrgId = Guid.Parse("30000000-0000-0000-0000-000000000099");
        var otherUserId = Guid.Parse("20000000-0000-0000-0000-000000000099");
        var portfolio = await CreatePortfolioAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",
                JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, otherUserId, otherOrgId));

        var detailResponse = await _client.GetAsync($"/api/liens/selling/portfolios/{portfolio.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var historyResponse = await _client.GetAsync($"/api/liens/selling/portfolios/{portfolio.Id}/status-history");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddLiensAndBuyers_persists_appended_children()
    {
        var portfolio = await CreatePortfolioAsync();
        var (_, secondLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}");

        var addLiensResponse = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/liens",
            new AddSellingPortfolioLiensRequest
            {
                LienIds = [secondLienId],
            });

        addLiensResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var addBuyersResponse = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/buyers",
            new AddSellingPortfolioBuyersRequest
            {
                BuyerOrgIds = [SeedHelper.FundingCompanyId],
            });

        addBuyersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reloaded = await _client.GetFromJsonAsync<SellingPortfolioResponse>(
            $"/api/liens/selling/portfolios/{portfolio.Id}");

        reloaded.Should().NotBeNull();
        reloaded!.Liens.Should().Contain(l => l.LienId == portfolio.Liens[0].LienId);
        reloaded.Liens.Should().Contain(l => l.LienId == secondLienId);
        reloaded.LienCount.Should().Be(2);
        reloaded.Buyers.Should().ContainSingle(b => b.BuyerOrgId == SeedHelper.FundingCompanyId);
    }

    [Fact]
    public async Task AddLiens_returns_partial_success_for_duplicate_ineligible_and_wrong_tenant_liens()
    {
        var portfolio = await CreatePortfolioAsync();
        var existingLienId = portfolio.Liens[0].LienId;
        var (_, eligibleLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}");
        var (_, closedLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            status: "CLOSED");
        var otherTenantLienId = await SeedOtherTenantLienAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/liens",
            new AddSellingPortfolioLiensRequest
            {
                LienIds = [existingLienId, eligibleLienId, closedLienId, otherTenantLienId],
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AddSellingPortfolioLiensResponse>();
        body.Should().NotBeNull();
        body!.RequestedCount.Should().Be(4);
        body.AddedCount.Should().Be(1);
        body.FailedCount.Should().Be(3);
        body.Results.Should().Contain(r => r.LienId == eligibleLienId && r.Success && r.Status == "added");
        body.Results.Should().Contain(r => r.LienId == existingLienId && !r.Success && r.ReasonCode == "LIEN_ALREADY_ASSIGNED" && r.Message == "Lien is already assigned to a portfolio.");
        body.Results.Should().Contain(r => r.LienId == closedLienId && !r.Success && r.ReasonCode == "LIEN_CLOSED" && r.Message == "Closed liens cannot be assigned to a portfolio.");
        body.Results.Should().Contain(r => r.LienId == otherTenantLienId && !r.Success && r.ReasonCode == "TENANT_MISMATCH" && r.Message == "Lien tenant does not match portfolio tenant.");
        body.Portfolio.Liens.Should().Contain(l => l.LienId == existingLienId);
        body.Portfolio.Liens.Should().Contain(l => l.LienId == eligibleLienId);
        body.Portfolio.Liens.Should().NotContain(l => l.LienId == closedLienId);
        body.Portfolio.Liens.Should().NotContain(l => l.LienId == otherTenantLienId);

        var reloaded = await _client.GetFromJsonAsync<SellingPortfolioResponse>(
            $"/api/liens/selling/portfolios/{portfolio.Id}");
        reloaded.Should().NotBeNull();
        reloaded!.Liens.Should().Contain(l => l.LienId == eligibleLienId);
        reloaded.Liens.Should().NotContain(l => l.LienId == closedLienId);
        reloaded.Liens.Should().NotContain(l => l.LienId == otherTenantLienId);

        using var verifyScope = _factory.Services.CreateScope();
        var audit = verifyScope.ServiceProvider.GetRequiredService<CapturingAuditPublisher>();
        audit.Events.Should().Contain(e => e.Action == "LIEN_PORTFOLIO_ELIGIBILITY_VALIDATION_FAILED");
    }

    [Fact]
    public async Task AddLiens_returns_specific_messages_for_balance_and_written_off_rules()
    {
        var portfolio = await CreatePortfolioAsync();
        var (_, zeroBalanceLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            originalAmount: 0m);
        var (_, writtenOffLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            status: "WRITTEN_OFF");

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/liens",
            new AddSellingPortfolioLiensRequest
            {
                LienIds = [zeroBalanceLienId, writtenOffLienId],
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AddSellingPortfolioLiensResponse>();
        body.Should().NotBeNull();
        body!.AddedCount.Should().Be(0);
        body.FailedCount.Should().Be(2);
        body.Results.Should().Contain(r =>
            r.LienId == zeroBalanceLienId &&
            r.ReasonCode == "BALANCE_NOT_POSITIVE" &&
            r.Message == "Lien balance must be greater than 0.");
        body.Results.Should().Contain(r =>
            r.LienId == writtenOffLienId &&
            r.ReasonCode == "LIEN_WRITTEN_OFF" &&
            r.Message == "Written-off liens cannot be assigned to a portfolio.");
    }

    [Fact]
    public async Task AddLiens_accepts_mixed_lien_ids_and_codes_and_separates_successes_from_failures()
    {
        var portfolio = await CreateEmptyPortfolioAsync();
        var firstLienNumber = $"LIEN-{Guid.NewGuid():N}";
        var (_, firstLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: firstLienNumber);
        var (_, secondLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}");

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/liens",
            new AddSellingPortfolioLiensRequest
            {
                Liens = [firstLienNumber, secondLienId.ToString(), "missing-lien-code"],
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AddSellingPortfolioLiensResponse>();
        body.Should().NotBeNull();
        body!.RequestedCount.Should().Be(3);
        body.AddedCount.Should().Be(2);
        body.FailedCount.Should().Be(1);
        body.SuccessfulAssignments.Should().HaveCount(2);
        body.SuccessfulAssignments.Should().Contain(r =>
            r.RequestedLien == firstLienNumber &&
            r.LienId == firstLienId &&
            r.LienCode == firstLienNumber);
        body.SuccessfulAssignments.Should().Contain(r =>
            r.RequestedLien == secondLienId.ToString() &&
            r.LienId == secondLienId);
        body.FailedAssignments.Should().ContainSingle(r =>
            r.RequestedLien == "missing-lien-code" &&
            r.ReasonCode == "LIEN_NOT_FOUND");
        body.Results.Should().HaveCount(3);
        body.Portfolio.LienCount.Should().Be(2);
        body.Portfolio.OriginalAmountTotal.Should().Be(24690m);
    }

    [Fact]
    public async Task RemoveLiens_returns_partial_success_and_recalculates_totals()
    {
        var (_, firstLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}");
        var (_, secondLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}");
        var (_, thirdLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}");
        var missingLienId = Guid.CreateVersion7();

        var createResponse = await _client.PostAsJsonAsync("/api/liens/selling/portfolios",
            new CreateSellingPortfolioRequest
            {
                PortfolioNumber = $"PORT-{Guid.NewGuid():N}"[..20],
                Name = "Removal test portfolio",
                LienIds = [firstLienId, secondLienId, thirdLienId],
            });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var portfolio = (await createResponse.Content.ReadFromJsonAsync<SellingPortfolioResponse>())!;
        portfolio.LienCount.Should().Be(3);
        portfolio.OriginalAmountTotal.Should().Be(37035m);

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/liens/remove",
            new RemoveSellingPortfolioLiensRequest
            {
                LienIds = [firstLienId, secondLienId, missingLienId],
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<RemoveSellingPortfolioLiensResponse>();
        body.Should().NotBeNull();
        body!.RequestedCount.Should().Be(3);
        body.RemovedCount.Should().Be(2);
        body.FailedCount.Should().Be(1);
        body.Results.Should().Contain(r => r.LienId == firstLienId && r.Success && r.Status == "removed");
        body.Results.Should().Contain(r => r.LienId == secondLienId && r.Success && r.Status == "removed");
        body.Results.Should().Contain(r => r.LienId == missingLienId && !r.Success && r.ReasonCode == "LIEN_NOT_IN_PORTFOLIO");
        body.Portfolio.LienCount.Should().Be(1);
        body.Portfolio.OriginalAmountTotal.Should().Be(12345m);
        body.Portfolio.CurrentBalanceTotal.Should().Be(12345m);
        body.Portfolio.OfferPriceTotal.Should().Be(0m);
        body.Portfolio.Liens.Should().ContainSingle(l => l.LienId == thirdLienId);

        var reloaded = await _client.GetFromJsonAsync<SellingPortfolioResponse>(
            $"/api/liens/selling/portfolios/{portfolio.Id}");
        reloaded.Should().NotBeNull();
        reloaded!.LienCount.Should().Be(1);
        reloaded.OriginalAmountTotal.Should().Be(12345m);
        reloaded.Liens.Should().ContainSingle(l => l.LienId == thirdLienId);

        using var verifyScope = _factory.Services.CreateScope();
        var audit = verifyScope.ServiceProvider.GetRequiredService<CapturingAuditPublisher>();
        var removalEvents = audit.Events.Where(e => e.Action == "LIEN_REMOVED_FROM_PORTFOLIO").ToList();
        removalEvents.Should().HaveCount(2);
        removalEvents.Should().OnlyContain(e =>
            e.EventType == "liens.selling_portfolio.lien_removed" &&
            e.TenantId == SeedHelper.TenantId &&
            e.ActorUserId == SeedHelper.UserId &&
            e.OccurredAtUtc != default);
    }

    [Fact]
    public async Task RemoveLiens_rejects_published_portfolio()
    {
        var portfolio = await CreatePortfolioAsync();
        var lienId = portfolio.Liens[0].LienId;

        var readyResponse = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/status",
            new TransitionSellingPortfolioStatusRequest { Status = SellingPortfolioStatus.ReadyForReview });
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var publishResponse = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/status",
            new TransitionSellingPortfolioStatusRequest { Status = SellingPortfolioStatus.Published });
        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/liens/remove",
            new RemoveSellingPortfolioLiensRequest { LienIds = [lienId] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<RemoveSellingPortfolioLiensResponse>();
        body.Should().NotBeNull();
        body!.RemovedCount.Should().Be(0);
        body.FailedCount.Should().Be(1);
        body.Results.Should().ContainSingle(r =>
            r.LienId == lienId &&
            !r.Success &&
            r.ReasonCode == "PORTFOLIO_NOT_EDITABLE");
        body.Portfolio.Status.Should().Be(SellingPortfolioStatus.Published);
        body.Portfolio.Liens.Should().ContainSingle(l => l.LienId == lienId);
    }

    [Fact]
    public async Task SendBuyerEmail_sends_required_subject_and_body_to_database_contact()
    {
        var buyerContactId = Guid.CreateVersion7();
        var (_, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            dateOfIncident: new DateOnly(2026, 3, 12));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var buyerContact = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.FundingCompanyId,
                ContactType.LienHolder,
                "Bailey",
                "Buyer",
                SeedHelper.UserId,
                email: "bailey.buyer@example.com");
            SetId(buyerContact, buyerContactId);
            db.Contacts.Add(buyerContact);
            await db.SaveChangesAsync();
        }

        var createResponse = await _client.PostAsJsonAsync("/api/liens/selling/portfolios",
            new CreateSellingPortfolioRequest
            {
                PortfolioNumber = $"PORT-{Guid.NewGuid():N}"[..20],
                Name = "Buyer email test portfolio",
                LienIds = [lienId],
                BuyerOrgIds = [SeedHelper.FundingCompanyId],
            });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var portfolio = (await createResponse.Content.ReadFromJsonAsync<SellingPortfolioResponse>())!;
        var lienCode = portfolio.Liens[0].LienNumber;

        var detailsUrl = $"https://app.legalsynq.test/lien/selling/{portfolio.Id}/liens/{lienId}";
        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/liens/{lienCode}/buyer-email",
            new SendLienBuyerEmailRequest
            {
                BuyerContactId = buyerContactId,
                DetailsUrl = detailsUrl,
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SendLienBuyerEmailResponse>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.BuyerName.Should().Be("Bailey Buyer");
        body.BuyerEmail.Should().Be("bailey.buyer@example.com");
        body.Subject.Should().Be($"External Client - 2026-03-12 - {lienCode}");
        body.Body.Should().Be(
            $"Hi Bailey Buyer, please find the lien details at the link below:{Environment.NewLine}{Environment.NewLine}" +
            $"{detailsUrl}{Environment.NewLine}{Environment.NewLine}" +
            "Let me know if you have any questions. Thank you.");

        using var verifyScope = _factory.Services.CreateScope();
        var publisher = verifyScope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>();
        publisher.Emails.Should().ContainSingle();
        var email = publisher.Emails[0];
        email.RecipientEmail.Should().Be("bailey.buyer@example.com");
        email.Subject.Should().Be(body.Subject);
        email.Body.Should().Be(body.Body);
        email.Metadata["lienId"].Should().Be(lienId.ToString());
        email.Metadata["buyerContactId"].Should().Be(buyerContactId.ToString());
    }

    [Fact]
    public async Task ConfirmSale_sends_new_lien_offer_email_with_real_data_and_documents()
    {
        var buyerContactId = Guid.CreateVersion7();
        var caseManagerId = Guid.CreateVersion7();
        var (_, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            dateOfIncident: new DateOnly(2026, 3, 12),
            initialServiceDate: new DateOnly(2026, 6, 1),
            caseNotes: $"caseManagerId={caseManagerId}",
            originalAmount: 3875m);

        await PrepareConfirmSaleDataAsync(
            lienId,
            buyerContactId,
            sellerEmail: "seller.operations@smithlaw.test",
            buyerEmail: "buyer.reviewer@capital.test",
            caseManagerId: caseManagerId,
            documentFileName: "signed-lien-real.pdf");

        var response = await PostConfirmSaleAsync(lienId, "confirm-sale-success");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<ConfirmSellingLienSaleResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(LienStatus.Offered);
        body.SellerStatus.Should().Be(SellingLienStatus.SubmittedForSale);
        body.AskAmount.Should().Be(2500m);
        body.OfferPrice.Should().Be(2500m);
        body.SoldAtUtc.Should().BeNull();
        body.Notification.Should().NotBeNull();
        body.Notification!.Submitted.Should().BeTrue();
        body.Notification.BuyerEmail.Should().Be("buyer.reviewer@capital.test");
        body.Notification.BuyerPortalUrl.Should().StartWith("https://app.legalsynq.test/selling/public/");
        body.Notification.BuyerPortalUrl.Should().NotContain("example.com");
        body.SellerNotification.Should().NotBeNull();
        body.SellerNotification!.Submitted.Should().BeTrue();
        body.SellerNotification.SellerEmail.Should().Be("seller.operations@smithlaw.test");
        body.SellerNotification.SellerPortalUrl.Should().StartWith("https://app.legalsynq.test/selling/public/");
        body.SellerNotification.SellerPortalUrl.Should().NotBe(body.Notification.BuyerPortalUrl);

        using var verifyScope = _factory.Services.CreateScope();
        var publisher = verifyScope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>();
        publisher.Emails.Should().HaveCount(2);
        var email = publisher.Emails.Single(captured => captured.RecipientEmail == "buyer.reviewer@capital.test");
        email.NotificationType.Should().Be(NotificationTaxonomy.Liens.Events.SellingLienSubmitted);
        email.Subject.Should().Be("New Lien Offer");
        email.RecipientEmail.Should().Be("buyer.reviewer@capital.test");
        email.Body.Should().Contain("LegalSynq");
        email.Body.Should().Contain("Awaiting Your Response");
        email.Body.Should().Contain("A medical lien has been submitted to your company for review and potential purchase.");
        email.Body.Should().Contain("View Lien for Sale");
        email.Body.Should().Contain("This Link Expires in 30 Days");
        email.Body.Should().Contain("$3,875.00");
        email.Body.Should().Contain("06/01/2026");
        email.Body.Should().Contain("Seller Operator");
        email.Body.Should().Contain("Smith & Associates LLP");
        email.Body.Should().Contain("Case Manager");
        email.Body.Should().Contain("signed-lien-real.pdf");
        email.Body.Should().NotContain("<!doctype html>");
        email.Body.Should().NotContain("<html");
        email.Body.Should().NotContain("<body");
        email.Body.Should().NotContain("John Doe");
        email.Body.Should().NotContain("Velantrix");
        email.Body.Should().NotContain("Henderson_Signed_Lien_LOP.pdf");
        email.Body.Should().NotContain("example.com");
        email.Metadata["lienId"].Should().Be(lienId.ToString());
        email.Metadata["buyerContactId"].Should().Be(buyerContactId.ToString());
        email.Options.Should().NotBeNull();
        email.Options!.IdempotencyKey.Should().Contain("confirm-sale-success");
        email.Options.TemplateKey.Should().Be(NotificationTaxonomy.Liens.Templates.SellingLienSubmittedEmail);
        email.Options.TextBody.Should().Be(email.Body);
        email.Options.HtmlBody.Should().NotBeNullOrWhiteSpace();
        email.Options.DisableClickTracking.Should().BeTrue();

        var html = email.Options.HtmlBody!;
        html.Should().StartWith("<!doctype html>");
        html.Should().Contain("Plus Jakarta Sans");
        html.Should().Contain("color-scheme\" content=\"light only\"");
        html.Should().Contain("background-color:#071b31 !important");
        html.Should().Contain("background-color:#ffffff !important");
        html.Should().Contain("bgcolor=\"#ffffff\"");
        html.Should().Contain("border-collapse:separate;border-spacing:0;background-color:#ffffff !important;");
        html.Should().Contain("border-top:1px solid #e5e5e5;border-bottom:1px solid #e5e5e5;border-left:1px solid #e5e5e5;border-top-left-radius:10px;");
        html.Should().Contain("border-top:1px solid #e5e5e5;border-bottom:1px solid #e5e5e5;border-right:1px solid #e5e5e5;border-top-right-radius:10px;");
        html.Should().Contain("border-bottom:1px solid #e5e5e5;border-left:1px solid #e5e5e5;border-bottom-left-radius:10px;");
        html.Should().Contain("border-bottom:1px solid #e5e5e5;border-right:1px solid #e5e5e5;border-bottom-right-radius:10px;");
        html.Should().NotContain("border:1px solid #e5e5e5;border-radius:10px;");
        html.Should().Contain("src=\"cid:legalsynq-brand-icon\"");
        html.Should().Contain("width:36px;padding:0 6px 0 0;vertical-align:middle;");
        html.Should().Contain("-webkit-text-fill-color:#ffffff;font-size:22px;line-height:1;font-weight:700;letter-spacing:0;\">Legal</span>");
        html.Should().Contain("-webkit-text-fill-color:#f26a2e;font-size:22px;line-height:1;font-weight:700;letter-spacing:0;\">Synq</span>");
        html.Should().Contain("src=\"cid:seller-information-icon\"");
        html.Should().Contain("src=\"cid:asset-overview-icon\"");
        html.Should().Contain("src=\"cid:supporting-documents-icon\"");
        html.Should().NotContain("<svg");
        html.Should().NotContain("class=\"email-brand-mark\"");
        html.Should().NotContain("&#10010;");
        html.Should().NotContain("&#9635;");
        html.Should().NotContain("&#9633;");
        html.Should().Contain("Awaiting Your Response");
        html.Should().Contain("View Lien for Sale");
        html.Should().Contain("This Link Expires in 30 Days");
        html.Should().Contain("$3,875.00");
        html.Should().Contain("06/01/2026");
        html.Should().Contain("Seller Operator");
        html.Should().Contain("Smith &amp; Associates LLP");
        html.Should().Contain("Case Manager");
        html.Should().Contain("signed-lien-real.pdf");
        html.Should().Contain("href=\"mailto:seller.operations@smithlaw.test\" style=\"color:#111111 !important;text-decoration:none;\"");
        html.Should().Contain("href=\"mailto:seller.operations@smithlaw.test\" style=\"color:#f26a2e !important;text-decoration:underline;\"");
        html.Should().Contain("href=\"https://app.legalsynq.test/selling/public/");
        html.Should().NotContain("John Doe");
        html.Should().NotContain("Velantrix");
        html.Should().NotContain("Henderson_Signed_Lien_LOP.pdf");
        html.Should().NotContain("example.com");

        email.Options.InlineAttachments.Should().NotBeNull();
        email.Options.InlineAttachments!.Should().HaveCount(4);
        email.Options.InlineAttachments.Should().Contain(attachment =>
            attachment.ContentId == "legalsynq-brand-icon" &&
            attachment.FileName == "legalsynq-brand-icon.png" &&
            attachment.ContentType == "image/png" &&
            !string.IsNullOrWhiteSpace(attachment.Base64Content));
        email.Options.InlineAttachments.Should().Contain(attachment =>
            attachment.ContentId == "seller-information-icon" &&
            attachment.ContentType == "image/png" &&
            !string.IsNullOrWhiteSpace(attachment.Base64Content));
        email.Options.InlineAttachments.Should().Contain(attachment =>
            attachment.ContentId == "asset-overview-icon" &&
            attachment.ContentType == "image/png" &&
            !string.IsNullOrWhiteSpace(attachment.Base64Content));
        email.Options.InlineAttachments.Should().Contain(attachment =>
            attachment.ContentId == "supporting-documents-icon" &&
            attachment.ContentType == "image/png" &&
            !string.IsNullOrWhiteSpace(attachment.Base64Content));

        var sellerEmail = publisher.Emails.Single(captured => captured.RecipientEmail == "seller.operations@smithlaw.test");
        sellerEmail.NotificationType.Should().Be(NotificationTaxonomy.Liens.Events.SellingLienSubmitted);
        sellerEmail.Subject.Should().Be("New Lien Offer");
        sellerEmail.Body.Should().Contain("LegalSynq");
        sellerEmail.Body.Should().Contain("Offered");
        sellerEmail.Body.Should().Contain("Buyer Information");
        sellerEmail.Body.Should().Contain("Buyer Reviewer");
        sellerEmail.Body.Should().Contain("Capital Fund LLC");
        sellerEmail.Body.Should().Contain("buyer.reviewer@capital.test");
        var sellerBuyerInformation = ExtractSection(sellerEmail.Body, "Buyer Information", "Asset Overview");
        sellerBuyerInformation.Should().Contain("Buyer Reviewer");
        sellerBuyerInformation.Should().Contain("Capital Fund LLC");
        sellerBuyerInformation.Should().NotContain("Email Address");
        sellerBuyerInformation.Should().NotContain("buyer.reviewer@capital.test");
        sellerEmail.Body.Should().Contain("View Lien Details");
        sellerEmail.Body.Should().Contain("$3,875.00");
        sellerEmail.Body.Should().Contain("06/01/2026");
        sellerEmail.Body.Should().Contain("signed-lien-real.pdf");
        sellerEmail.Body.Should().NotContain("Accept Lien");
        sellerEmail.Body.Should().NotContain("Decline Lien");
        sellerEmail.Body.Should().NotContain("Awaiting Your Response");
        sellerEmail.Body.Should().NotContain("Sent to Funding Company");
        sellerEmail.Metadata["audience"].Should().Be("seller");
        sellerEmail.Metadata["sellerContactId"].Should().NotBeNullOrWhiteSpace();
        sellerEmail.Metadata["buyerContactId"].Should().Be(buyerContactId.ToString());
        sellerEmail.Options.Should().NotBeNull();
        sellerEmail.Options!.IdempotencyKey.Should().Contain("confirm-sale-success");
        sellerEmail.Options.TemplateKey.Should().Be(NotificationTaxonomy.Liens.Templates.SellingLienSubmittedEmail);
        sellerEmail.Options.TextBody.Should().Be(sellerEmail.Body);
        sellerEmail.Options.HtmlBody.Should().NotBeNullOrWhiteSpace();
        sellerEmail.Options.HtmlBody.Should().Contain("Offered");
        sellerEmail.Options.HtmlBody.Should().Contain("Buyer Information");
        sellerEmail.Options.HtmlBody.Should().Contain("View Lien Details");
        sellerEmail.Options.HtmlBody.Should().Contain("Capital Fund LLC");
        sellerEmail.Options.HtmlBody.Should().Contain("buyer.reviewer@capital.test");
        var sellerBuyerInformationHtml = ExtractSection(sellerEmail.Options.HtmlBody!, "Buyer Information", "Asset Overview");
        sellerBuyerInformationHtml.Should().Contain("Buyer Reviewer");
        sellerBuyerInformationHtml.Should().Contain("Capital Fund LLC");
        sellerBuyerInformationHtml.Should().NotContain("Email Address");
        sellerBuyerInformationHtml.Should().NotContain("buyer.reviewer@capital.test");
        sellerEmail.Options.HtmlBody.Should().NotContain("Accept Lien");
        sellerEmail.Options.HtmlBody.Should().NotContain("Decline Lien");
        sellerEmail.Options.HtmlBody.Should().NotContain("Sent to Funding Company");
        sellerEmail.Options.DisableClickTracking.Should().BeTrue();
        sellerEmail.Options.InlineAttachments.Should().NotBeNull();
        sellerEmail.Options.InlineAttachments!.Should().HaveCount(4);
    }

    [Fact]
    public async Task PublicBuyerPortal_returns_temporary_portal_json_with_real_data()
    {
        var buyerContactId = Guid.CreateVersion7();
        var caseManagerId = Guid.CreateVersion7();
        var (_, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            dateOfIncident: new DateOnly(2026, 3, 12),
            initialServiceDate: new DateOnly(2026, 6, 1),
            endServiceDate: new DateOnly(2026, 6, 30),
            caseNotes: $"caseManagerId={caseManagerId}",
            lienNotes: "Medical provider lien filed after treatment and pending review.",
            originalAmount: 3875m);

        await PrepareConfirmSaleDataAsync(
            lienId,
            buyerContactId,
            sellerEmail: "seller.portal@smithlaw.test",
            buyerEmail: "buyer.portal@capital.test",
            caseManagerId: caseManagerId,
            documentFileName: "signed-lien-real.pdf");

        var confirmResponse = await PostConfirmSaleAsync(lienId, "confirm-sale-public-portal");
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await confirmResponse.Content.ReadAsStringAsync()}");

        var confirmBody = await confirmResponse.Content.ReadFromJsonAsync<ConfirmSellingLienSaleResponse>();
        var token = ExtractBuyerAccessToken(confirmBody!.Notification!.BuyerPortalUrl!);

        using var anonClient = _factory.CreateClient();
        var response = await anonClient.GetAsync($"/api/liens/selling/public/{token}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var serialized = json.GetRawText();
        json.GetProperty("audience").GetString().Should().Be("buyer");
        json.GetProperty("lien").GetProperty("lienCode").GetString().Should().StartWith("LIEN-");
        json.GetProperty("lien").GetProperty("status").GetString().Should().Be(LienStatus.Offered);
        json.GetProperty("lien").GetProperty("sellerStatus").GetString().Should().Be(SellingLienStatus.SubmittedForSale);
        json.GetProperty("lien").GetProperty("listingVisibility").GetString().Should().Be(SellingListingVisibility.Private);
        json.GetProperty("lien").GetProperty("initialServiceDate").GetString().Should().Be("2026-06-01");
        json.GetProperty("lien").GetProperty("endServiceDate").GetString().Should().Be("2026-06-30");
        json.GetProperty("lien").GetProperty("notes").GetString().Should().Be("Medical provider lien filed after treatment and pending review.");
        json.GetProperty("seller").GetProperty("name").GetString().Should().Be("Seller Operator");
        json.GetProperty("seller").GetProperty("company").GetString().Should().Be("Smith & Associates LLP");
        json.GetProperty("seller").GetProperty("email").GetString().Should().Be("seller.portal@smithlaw.test");
        json.GetProperty("buyer").GetProperty("company").GetString().Should().Be("Capital Fund LLC");
        json.GetProperty("case").GetProperty("caseManager").GetString().Should().Be("Case Manager");
        json.GetProperty("case").GetProperty("handlingLawFirm").GetString().Should().Be("Smith & Associates LLP");
        json.GetProperty("accessLink").GetProperty("expiresAtUtc").GetString().Should().NotBeNullOrWhiteSpace();

        var documents = json.GetProperty("documents").EnumerateArray().ToList();
        documents.Should().ContainSingle();
        documents[0].GetProperty("fileName").GetString().Should().Be("signed-lien-real.pdf");
        documents[0].GetProperty("category").GetString().Should().Be("Lien Document");
        documents[0].GetProperty("sizeOrType").GetString().Should().Be("PDF");

        serialized.Should().NotContain("<!doctype html>");
        serialized.Should().NotContain("<html");
        serialized.Should().NotContain("John Doe");
        serialized.Should().NotContain("Velantrix");
        serialized.Should().NotContain("Henderson_Signed_Lien_LOP.pdf");
        serialized.Should().NotContain("ApexIndustries");
        serialized.Should().NotContain("example.com");
        serialized.Should().NotContain("class=\"safari\"");
        serialized.Should().NotContain("toolbar-icons");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        db.SellingBuyerAccessLinks.Single(link => link.Token == token)
            .LastAccessedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task PublicSellerPortal_returns_read_only_json_and_rejects_buyer_actions()
    {
        var buyerContactId = Guid.CreateVersion7();
        var (_, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            initialServiceDate: new DateOnly(2026, 6, 1),
            originalAmount: 3875m);

        await PrepareConfirmSaleDataAsync(
            lienId,
            buyerContactId,
            sellerEmail: "seller.readonly@smithlaw.test",
            buyerEmail: "buyer.readonly@capital.test",
            buyerPhone: "3105551212");

        var confirmResponse = await PostConfirmSaleAsync(lienId, "confirm-sale-public-seller-view");
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await confirmResponse.Content.ReadAsStringAsync()}");

        var confirmBody = await confirmResponse.Content.ReadFromJsonAsync<ConfirmSellingLienSaleResponse>();
        var buyerToken = ExtractBuyerAccessToken(confirmBody!.Notification!.BuyerPortalUrl!);
        var sellerToken = ExtractBuyerAccessToken(confirmBody.SellerNotification!.SellerPortalUrl!);

        using var anonClient = _factory.CreateClient();
        var response = await anonClient.GetAsync($"/api/liens/selling/public/{sellerToken}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("audience").GetString().Should().Be("seller");
        json.GetProperty("buyer").GetProperty("contactName").GetString().Should().Be("Buyer Reviewer");
        json.GetProperty("buyer").GetProperty("company").GetString().Should().Be("Capital Fund LLC");
        json.GetProperty("buyer").GetProperty("email").GetString().Should().Be("buyer.readonly@capital.test");
        json.GetProperty("buyer").GetProperty("phone").GetString().Should().Be("3105551212");
        json.GetProperty("accessLink").GetProperty("responseStatus").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("lien").GetProperty("status").GetString().Should().Be(LienStatus.Offered);

        var accept = await PostPublicBuyerResponseAsync(
            sellerToken,
            "accept",
            new { notes = "seller cannot accept" },
            "seller-view-accept");
        accept.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertReadOnlyLinkErrorAsync(accept);

        var decline = await PostPublicBuyerResponseAsync(
            sellerToken,
            "decline",
            new { reason = "seller cannot decline" },
            "seller-view-decline");
        decline.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertReadOnlyLinkErrorAsync(decline);

        var activation = await PostPublicBuyerActivationAsync(
            sellerToken,
            new
            {
                companyName = "Capital Fund LLC",
                email = "buyer.readonly@capital.test",
                firstName = "Buyer",
                lastName = "Reviewer",
                password = "Password123!",
            });
        activation.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertReadOnlyLinkErrorAsync(activation);

        var buyerAccept = await PostPublicBuyerResponseAsync(
            buyerToken,
            "accept",
            new { notes = "buyer accepted while seller views read-only link" },
            "seller-view-buyer-accept");
        buyerAccept.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await buyerAccept.Content.ReadAsStringAsync()}");

        var updatedSellerResponse = await anonClient.GetAsync($"/api/liens/selling/public/{sellerToken}");
        updatedSellerResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await updatedSellerResponse.Content.ReadAsStringAsync()}");
        var updatedSellerJson = await updatedSellerResponse.Content.ReadFromJsonAsync<JsonElement>();
        updatedSellerJson.GetProperty("audience").GetString().Should().Be("seller");
        updatedSellerJson.GetProperty("accessLink").GetProperty("responseStatus").GetString()
            .Should().Be(SellingBuyerResponseStatus.Accepted);
        updatedSellerJson.GetProperty("lien").GetProperty("status").GetString().Should().Be(LienStatus.Accepted);
        updatedSellerJson.GetProperty("lien").GetProperty("sellerStatus").GetString()
            .Should().Be(SellingLienStatus.Accepted);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        db.SellingBuyerAccessLinks.Single(link => link.Token == sellerToken)
            .Purpose.Should().Be(SellingAccessLinkPurposes.ConfirmSaleSellerView);
    }

    [Fact]
    public async Task PublicSellerAndBuyerPortal_messages_round_trip_between_public_links()
    {
        var buyerContactId = Guid.CreateVersion7();
        var (_, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            initialServiceDate: new DateOnly(2026, 6, 1),
            originalAmount: 3875m);

        await PrepareConfirmSaleDataAsync(
            lienId,
            buyerContactId,
            sellerEmail: "seller.messages@smithlaw.test",
            buyerEmail: "buyer.messages@capital.test",
            buyerPhone: "3105551212");

        var confirmResponse = await PostConfirmSaleAsync(lienId, "confirm-sale-public-messages");
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await confirmResponse.Content.ReadAsStringAsync()}");

        var confirmBody = await confirmResponse.Content.ReadFromJsonAsync<ConfirmSellingLienSaleResponse>();
        var buyerToken = ExtractBuyerAccessToken(confirmBody!.Notification!.BuyerPortalUrl!);
        var sellerToken = ExtractBuyerAccessToken(confirmBody.SellerNotification!.SellerPortalUrl!);
        ClearCapturedEmails();

        using var anonClient = _factory.CreateClient();
        var buyerPost = await anonClient.PostAsJsonAsync(
            $"/api/liens/selling/public/{buyerToken}/messages",
            new { message = "Can you confirm the signed LOP is final?" });

        buyerPost.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await buyerPost.Content.ReadAsStringAsync()}");
        var buyerMessage = await buyerPost.Content.ReadFromJsonAsync<JsonElement>();
        buyerMessage.GetProperty("senderType").GetString().Should().Be("buyer");
        buyerMessage.GetProperty("senderName").GetString().Should().Be("Buyer Reviewer");
        buyerMessage.GetProperty("message").GetString().Should().Be("Can you confirm the signed LOP is final?");

        var sellerView = await anonClient.GetAsync($"/api/liens/selling/public/{sellerToken}");
        sellerView.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await sellerView.Content.ReadAsStringAsync()}");
        var sellerJson = await sellerView.Content.ReadFromJsonAsync<JsonElement>();
        var sellerMessages = sellerJson.GetProperty("messages").EnumerateArray().ToList();
        sellerMessages.Should().ContainSingle();
        sellerMessages[0].GetProperty("senderType").GetString().Should().Be("buyer");
        sellerMessages[0].GetProperty("message").GetString().Should().Be("Can you confirm the signed LOP is final?");

        using (var scope = _factory.Services.CreateScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>();
            publisher.Emails.Should().ContainSingle();
            var sellerEmail = publisher.Emails.Single();
            sellerEmail.NotificationType.Should().Be(NotificationTaxonomy.Liens.Events.OfferMessageCreated);
            sellerEmail.RecipientEmail.Should().Be("seller.messages@smithlaw.test");
            sellerEmail.Subject.Should().Be("New message on lien offer");
            sellerEmail.Body.Should().Contain("Buyer Reviewer sent a message");
            sellerEmail.Body.Should().Contain("Can you confirm the signed LOP is final?");
            sellerEmail.Body.Should().Contain($"/selling/public/{sellerToken}");
            sellerEmail.Metadata["recipientRole"].Should().Be("seller");
            sellerEmail.Metadata["senderType"].Should().Be("buyer");
            sellerEmail.Metadata["messageId"].Should().Be(buyerMessage.GetProperty("id").GetGuid().ToString());
            sellerEmail.Options.Should().NotBeNull();
            sellerEmail.Options!.DisableClickTracking.Should().BeTrue();
            sellerEmail.Options.HtmlBody.Should().Contain("View &amp; Reply");
            sellerEmail.Options.IdempotencyKey.Should().Contain(":seller");
        }

        ClearCapturedEmails();

        var sellerPost = await anonClient.PostAsJsonAsync(
            $"/api/liens/selling/public/{sellerToken}/messages",
            new { message = "The LOP is final and attached to the package." });

        sellerPost.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await sellerPost.Content.ReadAsStringAsync()}");
        var sellerMessage = await sellerPost.Content.ReadFromJsonAsync<JsonElement>();
        sellerMessage.GetProperty("senderType").GetString().Should().Be("seller");
        sellerMessage.GetProperty("senderName").GetString().Should().Be("Seller Operator");

        var buyerView = await anonClient.GetAsync($"/api/liens/selling/public/{buyerToken}");
        buyerView.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await buyerView.Content.ReadAsStringAsync()}");
        var buyerJson = await buyerView.Content.ReadFromJsonAsync<JsonElement>();
        var buyerMessages = buyerJson.GetProperty("messages").EnumerateArray().ToList();
        buyerMessages.Should().HaveCount(2);
        buyerMessages[0].GetProperty("senderType").GetString().Should().Be("buyer");
        buyerMessages[1].GetProperty("senderType").GetString().Should().Be("seller");
        buyerMessages[1].GetProperty("message").GetString().Should().Be("The LOP is final and attached to the package.");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.SellingPortalMessages.Count(message => message.TenantId == SeedHelper.TenantId && message.LienId == lienId)
                .Should().Be(2);

            var publisher = scope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>();
            publisher.Emails.Should().ContainSingle();
            var buyerEmail = publisher.Emails.Single();
            buyerEmail.NotificationType.Should().Be(NotificationTaxonomy.Liens.Events.OfferMessageCreated);
            buyerEmail.RecipientEmail.Should().Be("buyer.messages@capital.test");
            buyerEmail.Subject.Should().Be("New message on lien offer");
            buyerEmail.Body.Should().Contain("Seller Operator sent a message");
            buyerEmail.Body.Should().Contain("The LOP is final and attached to the package.");
            buyerEmail.Body.Should().Contain($"/selling/public/{buyerToken}");
            buyerEmail.Metadata["recipientRole"].Should().Be("buyer");
            buyerEmail.Metadata["senderType"].Should().Be("seller");
            buyerEmail.Metadata["messageId"].Should().Be(sellerMessage.GetProperty("id").GetGuid().ToString());
        }
    }

    [Fact]
    public async Task PublicBuyerPortal_without_documents_uses_empty_state_without_sample_files()
    {
        var buyerContactId = Guid.CreateVersion7();
        var (_, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            initialServiceDate: new DateOnly(2026, 6, 1));

        await PrepareConfirmSaleDataAsync(
            lienId,
            buyerContactId,
            sellerEmail: "seller.no-docs@smithlaw.test",
            buyerEmail: "buyer.no-docs@capital.test");

        var confirmResponse = await PostConfirmSaleAsync(lienId, "confirm-sale-public-no-docs");
        var confirmBody = await confirmResponse.Content.ReadFromJsonAsync<ConfirmSellingLienSaleResponse>();
        var token = ExtractBuyerAccessToken(confirmBody!.Notification!.BuyerPortalUrl!);

        using var anonClient = _factory.CreateClient();
        var response = await anonClient.GetAsync($"/api/liens/selling/public/{token}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("documents").EnumerateArray().Should().BeEmpty();
        var serialized = json.GetRawText();
        serialized.Should().NotContain("Henderson_Signed_Lien_LOP.pdf");
        serialized.Should().NotContain("ApexIndustries");
    }

    [Fact]
    public async Task PublicBuyerPortal_activate_account_provisions_synqlien_buyer_from_token_contact()
    {
        var (_, token) = await CreatePublicLienOfferAsync(
            "activate-account",
            buyerPhone: "3105551212");

        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<CapturingPublicBuyerAccountProvisioningService>()
                .NextResult = PublicBuyerAccountProvisioningResult.Created(
                    new Guid("20000000-0000-0000-0000-000000000201"),
                    isNew: true);
        }

        var response = await PostPublicBuyerActivationAsync(
            token,
            new
            {
                companyName = "Overridden Company",
                email = "override@capital.test",
                firstName = "Override",
                lastName = "Person",
                phone = "9999999999",
                password = "Password123!",
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("userId").GetGuid().Should().Be(new Guid("20000000-0000-0000-0000-000000000201"));
        json.GetProperty("isNew").GetBoolean().Should().BeTrue();
        json.GetProperty("loginUrl").GetString().Should().Be("/login?returnTo=%2Ffunding%2Foffered-liens&reason=synqlien-buyer-activation");

        using var verifyScope = _factory.Services.CreateScope();
        var provisioning = verifyScope.ServiceProvider.GetRequiredService<CapturingPublicBuyerAccountProvisioningService>();
        provisioning.Requests.Should().ContainSingle();
        var request = provisioning.Requests.Single();
        request.TenantId.Should().Be(SeedHelper.TenantId);
        request.BuyerOrgId.Should().Be(SeedHelper.FundingCompanyId);
        request.BuyerCompanyName.Should().Be("Capital Fund LLC");
        request.Email.Should().Be("buyer.activate-account@capital.test");
        request.FirstName.Should().Be("Buyer");
        request.LastName.Should().Be("Reviewer");
        request.Phone.Should().Be("+13105551212");
        request.Password.Should().Be("Password123!");
    }

    [Fact]
    public async Task PublicBuyerPortal_activate_account_rejects_unknown_token_without_provisioning()
    {
        var response = await PostPublicBuyerActivationAsync(
            "not-a-real-token",
            new
            {
                email = "buyer@capital.test",
                firstName = "Buyer",
                password = "Password123!",
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var verifyScope = _factory.Services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<CapturingPublicBuyerAccountProvisioningService>()
            .Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PublicBuyerPortal_accept_records_buyer_response_and_marks_lien_accepted_without_finalizing_sale()
    {
        var (lienId, token) = await CreatePublicLienOfferAsync("accept");
        ClearCapturedEmails();

        var response = await PostPublicBuyerResponseAsync(
            token,
            "accept",
            new { notes = "Accepted at ask from public portal" },
            "public-accept-response");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessLink = json.GetProperty("accessLink");
        accessLink.GetProperty("responseStatus").GetString().Should().Be(SellingBuyerResponseStatus.Accepted);
        accessLink.GetProperty("responseAmount").GetDecimal().Should().Be(2500m);
        accessLink.GetProperty("responseNotes").GetString().Should().Be("Accepted at ask from public portal");
        accessLink.GetProperty("respondedAtUtc").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("lien").GetProperty("status").GetString().Should().Be(LienStatus.Accepted);
        json.GetProperty("lien").GetProperty("sellerStatus").GetString().Should().Be(SellingLienStatus.Accepted);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var persistedLink = db.SellingBuyerAccessLinks.Single(link => link.Token == token);
        persistedLink.ResponseStatus.Should().Be(SellingBuyerResponseStatus.Accepted);
        persistedLink.ResponseAmount.Should().Be(2500m);
        persistedLink.ResponseNotes.Should().Be("Accepted at ask from public portal");
        persistedLink.ResponseIdempotencyKey.Should().Be("public-accept-response");
        persistedLink.RespondedAtUtc.Should().NotBeNull();
        persistedLink.LastAccessedAtUtc.Should().NotBeNull();

        var lien = db.Liens.Single(l => l.Id == lienId);
        lien.Status.Should().Be(LienStatus.Accepted);
        lien.SellerStatus.Should().Be(SellingLienStatus.Accepted);
        lien.SoldAtUtc.Should().BeNull();
        lien.BuyingOrgId.Should().BeNull();

        var publisher = scope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>();
        publisher.Emails.Should().HaveCount(2);
        var buyerEmail = publisher.Emails.Single(email => email.Metadata["recipientRole"] == "buyer");
        buyerEmail.NotificationType.Should().Be(NotificationTaxonomy.Liens.Events.OfferAccepted);
        buyerEmail.RecipientEmail.Should().Be("buyer.accept@capital.test");
        buyerEmail.Subject.Should().Be("Lien Offer Accepted");
        buyerEmail.Body.Should().Contain("LegalSynq");
        buyerEmail.Body.Should().Contain("accepted lien offer");
        buyerEmail.Body.Should().Contain("Capital Fund LLC");
        buyerEmail.Body.Should().Contain("Seller: Smith & Associates LLP");
        buyerEmail.Body.Should().Contain("Response notes: Accepted at ask from public portal");
        buyerEmail.Metadata["lienId"].Should().Be(lienId.ToString());
        buyerEmail.Metadata["buyerAccessLinkId"].Should().Be(persistedLink.Id.ToString());
        buyerEmail.Metadata["responseStatus"].Should().Be(SellingBuyerResponseStatus.Accepted);
        buyerEmail.Options.Should().NotBeNull();
        buyerEmail.Options!.TemplateKey.Should().BeNull();
        buyerEmail.Options.IdempotencyKey.Should().Contain(":accepted:buyer");
        buyerEmail.Options.TemplateData.Should().BeNull();
        buyerEmail.Options.BrandedRendering.Should().BeNull();
        buyerEmail.Options.TextBody.Should().Be(buyerEmail.Body);
        buyerEmail.Options.HtmlBody.Should().NotBeNullOrWhiteSpace();
        buyerEmail.Options.HtmlBody.Should().Contain("<!doctype html>");
        buyerEmail.Options.HtmlBody.Should().Contain("Lien Offer Accepted");
        buyerEmail.Options.HtmlBody.Should().Contain("Lien Number");
        buyerEmail.Options.HtmlBody.Should().Contain("Capital Fund LLC");
        AssertPublicResponseEmailBranding(buyerEmail);

        var sellerEmail = publisher.Emails.Single(email => email.Metadata["recipientRole"] == "seller");
        sellerEmail.NotificationType.Should().Be(NotificationTaxonomy.Liens.Events.OfferAccepted);
        sellerEmail.RecipientEmail.Should().Be("seller.accept@smithlaw.test");
        sellerEmail.Subject.Should().Be(buyerEmail.Subject);
        sellerEmail.Body.Should().Contain("LegalSynq");
        sellerEmail.Body.Should().Contain("Buyer Reviewer from Capital Fund LLC accepted lien offer");
        sellerEmail.Body.Should().Contain("Seller: Smith & Associates LLP");
        sellerEmail.Body.Should().Contain("Response notes: Accepted at ask from public portal");
        sellerEmail.Body.Should().NotContain("Accept Lien");
        sellerEmail.Body.Should().NotContain("Decline Lien");
        sellerEmail.Metadata["lienId"].Should().Be(lienId.ToString());
        sellerEmail.Metadata["buyerAccessLinkId"].Should().Be(persistedLink.Id.ToString());
        sellerEmail.Metadata["responseStatus"].Should().Be(SellingBuyerResponseStatus.Accepted);
        sellerEmail.Options.Should().NotBeNull();
        sellerEmail.Options!.TemplateKey.Should().BeNull();
        sellerEmail.Options.IdempotencyKey.Should().Contain(":accepted:seller");
        sellerEmail.Options.TemplateData.Should().BeNull();
        sellerEmail.Options.BrandedRendering.Should().BeNull();
        sellerEmail.Options.TextBody.Should().Be(sellerEmail.Body);
        sellerEmail.Options.HtmlBody.Should().NotBeNullOrWhiteSpace();
        sellerEmail.Options.HtmlBody.Should().Contain("<!doctype html>");
        sellerEmail.Options.HtmlBody.Should().Contain("Lien Offer Accepted");
        sellerEmail.Options.HtmlBody.Should().Contain("Buyer Reviewer");
        sellerEmail.Options.HtmlBody.Should().Contain("Smith &amp; Associates LLP");
        AssertPublicResponseEmailBranding(sellerEmail);
    }

    [Fact]
    public async Task PublicBuyerPortal_offers_alias_records_accepted_buyer_response()
    {
        var (_, token) = await CreatePublicLienOfferAsync("offers-alias");

        var response = await PostPublicBuyerResponseAsync(
            token,
            "offers",
            new { offerAmount = 999m, message = "Accepted through legacy public offer route" },
            "public-offers-alias-response");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessLink = json.GetProperty("accessLink");
        accessLink.GetProperty("responseStatus").GetString().Should().Be(SellingBuyerResponseStatus.Accepted);
        accessLink.GetProperty("responseAmount").GetDecimal().Should().Be(2500m);
        accessLink.GetProperty("responseNotes").GetString().Should().Be("Accepted through legacy public offer route");
    }

    [Fact]
    public async Task PublicBuyerPortal_decline_records_buyer_response()
    {
        var (lienId, token) = await CreatePublicLienOfferAsync("decline");
        ClearCapturedEmails();

        var response = await PostPublicBuyerResponseAsync(
            token,
            "decline",
            new { reason = "Not in buying criteria" },
            "public-decline-response");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessLink = json.GetProperty("accessLink");
        accessLink.GetProperty("responseStatus").GetString().Should().Be(SellingBuyerResponseStatus.Declined);
        accessLink.GetProperty("responseAmount").ValueKind.Should().Be(JsonValueKind.Null);
        accessLink.GetProperty("responseNotes").GetString().Should().Be("Not in buying criteria");
        accessLink.GetProperty("respondedAtUtc").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("lien").GetProperty("status").GetString().Should().Be(LienStatus.Declined);
        json.GetProperty("lien").GetProperty("sellerStatus").GetString().Should().Be(SellingLienStatus.Declined);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var persistedLink = db.SellingBuyerAccessLinks.Single(link => link.Token == token);
        persistedLink.ResponseStatus.Should().Be(SellingBuyerResponseStatus.Declined);
        persistedLink.ResponseAmount.Should().BeNull();
        persistedLink.ResponseNotes.Should().Be("Not in buying criteria");

        var lien = db.Liens.Single(l => l.Id == lienId);
        lien.Status.Should().Be(LienStatus.Declined);
        lien.SellerStatus.Should().Be(SellingLienStatus.Declined);
        lien.ClosedAtUtc.Should().NotBeNull();
        lien.WithdrawnAtUtc.Should().BeNull();
        lien.SoldAtUtc.Should().BeNull();
        lien.BuyingOrgId.Should().BeNull();

        var publisher = scope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>();
        publisher.Emails.Should().HaveCount(2);
        var buyerEmail = publisher.Emails.Single(email => email.Metadata["recipientRole"] == "buyer");
        buyerEmail.NotificationType.Should().Be(NotificationTaxonomy.Liens.Events.OfferRejected);
        buyerEmail.RecipientEmail.Should().Be("buyer.decline@capital.test");
        buyerEmail.Subject.Should().Be("Lien Offer Declined");
        buyerEmail.Body.Should().Contain("LegalSynq");
        buyerEmail.Body.Should().Contain("declined lien offer");
        buyerEmail.Body.Should().Contain("Capital Fund LLC");
        buyerEmail.Body.Should().Contain("Seller: Smith & Associates LLP");
        buyerEmail.Body.Should().Contain("Response notes: Not in buying criteria");
        buyerEmail.Metadata["lienId"].Should().Be(lienId.ToString());
        buyerEmail.Metadata["buyerAccessLinkId"].Should().Be(persistedLink.Id.ToString());
        buyerEmail.Metadata["responseStatus"].Should().Be(SellingBuyerResponseStatus.Declined);
        buyerEmail.Options.Should().NotBeNull();
        buyerEmail.Options!.TemplateKey.Should().BeNull();
        buyerEmail.Options.IdempotencyKey.Should().Contain(":declined:buyer");
        buyerEmail.Options.TemplateData.Should().BeNull();
        buyerEmail.Options.BrandedRendering.Should().BeNull();
        buyerEmail.Options.TextBody.Should().Be(buyerEmail.Body);
        buyerEmail.Options.HtmlBody.Should().NotBeNullOrWhiteSpace();
        buyerEmail.Options.HtmlBody.Should().Contain("<!doctype html>");
        buyerEmail.Options.HtmlBody.Should().Contain("Lien Offer Declined");
        buyerEmail.Options.HtmlBody.Should().Contain("Lien Number");
        buyerEmail.Options.HtmlBody.Should().Contain("Capital Fund LLC");
        AssertPublicResponseEmailBranding(buyerEmail);

        var sellerEmail = publisher.Emails.Single(email => email.Metadata["recipientRole"] == "seller");
        sellerEmail.NotificationType.Should().Be(NotificationTaxonomy.Liens.Events.OfferRejected);
        sellerEmail.RecipientEmail.Should().Be("seller.decline@smithlaw.test");
        sellerEmail.Subject.Should().Be(buyerEmail.Subject);
        sellerEmail.Body.Should().Contain("LegalSynq");
        sellerEmail.Body.Should().Contain("Buyer Reviewer from Capital Fund LLC declined lien offer");
        sellerEmail.Body.Should().Contain("Seller: Smith & Associates LLP");
        sellerEmail.Body.Should().Contain("Response notes: Not in buying criteria");
        sellerEmail.Body.Should().NotContain("Accept Lien");
        sellerEmail.Body.Should().NotContain("Decline Lien");
        sellerEmail.Metadata["lienId"].Should().Be(lienId.ToString());
        sellerEmail.Metadata["buyerAccessLinkId"].Should().Be(persistedLink.Id.ToString());
        sellerEmail.Metadata["responseStatus"].Should().Be(SellingBuyerResponseStatus.Declined);
        sellerEmail.Options.Should().NotBeNull();
        sellerEmail.Options!.TemplateKey.Should().BeNull();
        sellerEmail.Options.IdempotencyKey.Should().Contain(":declined:seller");
        sellerEmail.Options.TemplateData.Should().BeNull();
        sellerEmail.Options.BrandedRendering.Should().BeNull();
        sellerEmail.Options.TextBody.Should().Be(sellerEmail.Body);
        sellerEmail.Options.HtmlBody.Should().NotBeNullOrWhiteSpace();
        sellerEmail.Options.HtmlBody.Should().Contain("<!doctype html>");
        sellerEmail.Options.HtmlBody.Should().Contain("Lien Offer Declined");
        sellerEmail.Options.HtmlBody.Should().Contain("Buyer Reviewer");
        sellerEmail.Options.HtmlBody.Should().Contain("Smith &amp; Associates LLP");
        AssertPublicResponseEmailBranding(sellerEmail);
    }

    [Fact]
    public async Task PublicBuyerPortal_repeated_same_response_is_idempotent()
    {
        var (_, token) = await CreatePublicLienOfferAsync("idempotent");
        ClearCapturedEmails();

        var first = await PostPublicBuyerResponseAsync(
            token,
            "decline",
            new { reason = "Not this one" },
            "public-decline-idempotent");
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var firstScope = _factory.Services.CreateScope())
        {
            firstScope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>()
                .Emails.Should().HaveCount(2);
        }

        var second = await PostPublicBuyerResponseAsync(
            token,
            "decline",
            new { reason = "Different duplicate reason" },
            "public-decline-idempotent-replay");

        second.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await second.Content.ReadAsStringAsync()}");
        var json = await second.Content.ReadFromJsonAsync<JsonElement>();
        var accessLink = json.GetProperty("accessLink");
        accessLink.GetProperty("responseStatus").GetString().Should().Be(SellingBuyerResponseStatus.Declined);
        accessLink.GetProperty("responseNotes").GetString().Should().Be("Not this one");
        json.GetProperty("lien").GetProperty("status").GetString().Should().Be(LienStatus.Declined);
        json.GetProperty("lien").GetProperty("sellerStatus").GetString().Should().Be(SellingLienStatus.Declined);

        using var verifyScope = _factory.Services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>()
            .Emails.Should().HaveCount(2);
    }

    [Fact]
    public async Task PublicBuyerPortal_repeated_same_response_retries_failed_notifications()
    {
        var (_, token) = await CreatePublicLienOfferAsync("retry-response-notifications");
        ClearCapturedEmails();

        using (var failingScope = _factory.Services.CreateScope())
        {
            failingScope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>()
                .FailEmailSends = true;
        }

        var first = await PostPublicBuyerResponseAsync(
            token,
            "decline",
            new { reason = "Temporarily not buying this lien" },
            "public-decline-notification-fails");
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var failedScope = _factory.Services.CreateScope())
        {
            var publisher = failedScope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>();
            publisher.Emails.Should().BeEmpty();
            publisher.FailEmailSends = false;
        }

        var second = await PostPublicBuyerResponseAsync(
            token,
            "decline",
            new { reason = "Different duplicate reason" },
            "public-decline-notification-retry");

        second.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await second.Content.ReadAsStringAsync()}");
        var json = await second.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("accessLink").GetProperty("responseStatus").GetString()
            .Should().Be(SellingBuyerResponseStatus.Declined);
        json.GetProperty("accessLink").GetProperty("responseNotes").GetString()
            .Should().Be("Temporarily not buying this lien");

        using var verifyScope = _factory.Services.CreateScope();
        var notifications = verifyScope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>().Emails;
        notifications.Should().HaveCount(2);
        notifications.Should().ContainSingle(email =>
            email.Metadata["recipientRole"] == "buyer" &&
            email.NotificationType == NotificationTaxonomy.Liens.Events.OfferRejected);
        notifications.Should().ContainSingle(email =>
            email.Metadata["recipientRole"] == "seller" &&
            email.NotificationType == NotificationTaxonomy.Liens.Events.OfferRejected);
        notifications.Should().OnlyContain(email =>
            email.Body.Contains("Response notes: Temporarily not buying this lien", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PublicBuyerPortal_opposite_response_after_recorded_response_conflicts()
    {
        var (_, token) = await CreatePublicLienOfferAsync("conflict");

        var first = await PostPublicBuyerResponseAsync(
            token,
            "accept",
            new { },
            "public-accept-before-conflict");
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await PostPublicBuyerResponseAsync(
            token,
            "decline",
            new { reason = "Changed mind" },
            "public-decline-conflict");

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var json = await second.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetProperty("code").GetString().Should().Be("response-conflict");
    }

    [Fact]
    public async Task PublicBuyerPortal_response_rejects_unknown_token_without_authentication()
    {
        var response = await PostPublicBuyerResponseAsync(
            "not-a-real-token",
            "accept",
            new { },
            "public-accept-unknown");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetProperty("code").GetString().Should().Be("not-found");
    }

    [Fact]
    public async Task PublicBuyerPortal_response_rejects_expired_token()
    {
        var (_, token) = await CreatePublicLienOfferAsync("expired");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var accessLink = db.SellingBuyerAccessLinks.Single(link => link.Token == token);
            SetDateTimeProperty(accessLink, nameof(SellingBuyerAccessLink.ExpiresAtUtc), DateTime.UtcNow.AddMinutes(-1));
            await db.SaveChangesAsync();
        }

        var response = await PostPublicBuyerResponseAsync(
            token,
            "accept",
            new { },
            "public-accept-expired");

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetProperty("code").GetString().Should().Be("expired");
    }

    [Fact]
    public async Task PublicBuyerPortal_response_rejects_revoked_token()
    {
        var (_, token) = await CreatePublicLienOfferAsync("revoked");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var accessLink = db.SellingBuyerAccessLinks.Single(link => link.Token == token);
            accessLink.Revoke(SeedHelper.UserId);
            await db.SaveChangesAsync();
        }

        var response = await PostPublicBuyerResponseAsync(
            token,
            "decline",
            new { reason = "No longer interested" },
            "public-decline-revoked");

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetProperty("code").GetString().Should().Be("revoked");
    }

    [Fact]
    public async Task PublicBuyerPortal_rejects_unknown_token_without_authentication()
    {
        using var anonClient = _factory.CreateClient();
        var response = await anonClient.GetAsync("/api/liens/selling/public/not-a-real-token");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetProperty("code").GetString().Should().Be("not-found");
        json.GetProperty("error").GetProperty("title").GetString().Should().Be("Lien offer link unavailable");
        json.GetProperty("error").GetProperty("message").GetString().Should().Be("The secure link could not be found.");
        json.GetRawText().Should().NotContain("example.com");
    }

    [Fact]
    public async Task ConfirmSale_without_notification_transitions_lien_without_sending_email()
    {
        var (_, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            originalAmount: 4000m);

        await PrepareConfirmSaleDataAsync(
            lienId,
            Guid.CreateVersion7(),
            sellerEmail: null,
            buyerEmail: "unused.buyer@capital.test");

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/liens/{lienId}/confirm-sale",
            new ConfirmSellingLienSaleRequest
            {
                ConfirmationAccepted = true,
                SendBuyerNotification = false,
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<ConfirmSellingLienSaleResponse>();
        body!.Status.Should().Be(LienStatus.Offered);
        body.SellerStatus.Should().Be(SellingLienStatus.SubmittedForSale);
        body.Notification.Should().BeNull();

        using var verifyScope = _factory.Services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>()
            .Emails.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmSale_rejects_notification_when_buyer_contact_email_is_missing()
    {
        var buyerContactId = Guid.CreateVersion7();
        var (_, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            initialServiceDate: new DateOnly(2026, 6, 1));

        await PrepareConfirmSaleDataAsync(
            lienId,
            buyerContactId,
            sellerEmail: "seller.operations@smithlaw.test",
            buyerEmail: null);

        var response = await PostConfirmSaleAsync(lienId, "confirm-sale-missing-buyer-email");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        db.Liens.Single(l => l.Id == lienId).Status.Should().Be(LienStatus.Draft);
        verifyScope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>()
            .Emails.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmSale_rejects_notification_when_seller_email_is_missing()
    {
        var buyerContactId = Guid.CreateVersion7();
        var (_, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            initialServiceDate: new DateOnly(2026, 6, 1));

        await PrepareConfirmSaleDataAsync(
            lienId,
            buyerContactId,
            sellerEmail: null,
            buyerEmail: "buyer.reviewer@capital.test");

        var response = await PostConfirmSaleAsync(lienId, "confirm-sale-missing-seller-email");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        db.Liens.Single(l => l.Id == lienId).Status.Should().Be(LienStatus.Draft);
        verifyScope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>()
            .Emails.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmSale_replay_with_same_idempotency_key_does_not_send_duplicate_email()
    {
        var buyerContactId = Guid.CreateVersion7();
        var (_, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            initialServiceDate: new DateOnly(2026, 6, 1));

        await PrepareConfirmSaleDataAsync(
            lienId,
            buyerContactId,
            sellerEmail: "seller.operations@smithlaw.test",
            buyerEmail: "buyer.reviewer@capital.test");

        var first = await PostConfirmSaleAsync(lienId, "confirm-sale-replay");
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await PostConfirmSaleAsync(lienId, "confirm-sale-replay");
        second.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await second.Content.ReadAsStringAsync()}");
        var secondBody = await second.Content.ReadFromJsonAsync<ConfirmSellingLienSaleResponse>();
        secondBody!.Notification!.Submitted.Should().BeTrue();

        using var verifyScope = _factory.Services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>()
            .Emails.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConfirmSale_notification_failure_does_not_roll_back_lien_transition()
    {
        var buyerContactId = Guid.CreateVersion7();
        var (_, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            initialServiceDate: new DateOnly(2026, 6, 1));

        await PrepareConfirmSaleDataAsync(
            lienId,
            buyerContactId,
            sellerEmail: "seller.operations@smithlaw.test",
            buyerEmail: "buyer.reviewer@capital.test");

        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>().FailEmailSends = true;
        }

        var response = await PostConfirmSaleAsync(lienId, "confirm-sale-notification-failure");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<ConfirmSellingLienSaleResponse>();
        body!.Status.Should().Be(LienStatus.Offered);
        body.Notification!.Submitted.Should().BeFalse();
        body.Notification.NotificationStatus.Should().Be("failed");
        body.SellerNotification.Should().NotBeNull();
        body.SellerNotification!.Submitted.Should().BeFalse();
        body.SellerNotification.NotificationStatus.Should().Be("skipped");
        body.SellerNotification.FailureMessage.Should().Contain("buyer notification was not submitted");

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        db.Liens.Single(l => l.Id == lienId).Status.Should().Be(LienStatus.Offered);
        verifyScope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>()
            .Emails.Should().BeEmpty();
    }

    private async Task<SellingPortfolioResponse> CreatePortfolioAsync()
    {
        var (_, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}");

        var response = await _client.PostAsJsonAsync("/api/liens/selling/portfolios",
            new CreateSellingPortfolioRequest
            {
                PortfolioNumber = $"PORT-{Guid.NewGuid():N}"[..20],
                Name = "Transition test portfolio",
                LienIds = [lienId],
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<SellingPortfolioResponse>())!;
    }

    private Task<HttpResponseMessage> PostConfirmSaleAsync(Guid lienId, string idempotencyKey)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/liens/selling/liens/{lienId}/confirm-sale");
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        message.Content = JsonContent.Create(new ConfirmSellingLienSaleRequest
        {
            ConfirmationAccepted = true,
            SendBuyerNotification = true,
        });

        return _client.SendAsync(message);
    }

    private static async Task AssertReadOnlyLinkErrorAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetProperty("code").GetString().Should().Be("read-only-link");
        json.GetProperty("error").GetProperty("title").GetString().Should().Be("Lien offer is read-only");
    }

    private void ClearCapturedEmails()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>().Clear();
    }

    private static void AssertPublicResponseEmailBranding(CapturedEmail email)
    {
        email.Body.Should().NotContain("This notification does not finalize");
        email.Options.Should().NotBeNull();
        email.Options!.HtmlBody.Should().NotContain("This notification does not finalize");
        email.Options.HtmlBody.Should().Contain("src=\"cid:legalsynq-brand-icon\"");
        email.Options.InlineAttachments.Should().NotBeNull();
        email.Options.InlineAttachments!.Should().ContainSingle(attachment =>
            attachment.ContentId == "legalsynq-brand-icon" &&
            attachment.FileName == "legalsynq-brand-icon.png" &&
            attachment.ContentType == "image/png");
    }

    private static string ExtractSection(string value, string start, string end)
    {
        var startIndex = value.IndexOf(start, StringComparison.Ordinal);
        startIndex.Should().BeGreaterThanOrEqualTo(0);
        startIndex += start.Length;

        var endIndex = value.IndexOf(end, startIndex, StringComparison.Ordinal);
        endIndex.Should().BeGreaterThanOrEqualTo(0);

        return value[startIndex..endIndex];
    }

    private async Task<(Guid LienId, string Token)> CreatePublicLienOfferAsync(
        string scenario,
        string? buyerPhone = null)
    {
        var buyerContactId = Guid.CreateVersion7();
        var (_, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            initialServiceDate: new DateOnly(2026, 6, 1),
            originalAmount: 3875m);

        await PrepareConfirmSaleDataAsync(
            lienId,
            buyerContactId,
            sellerEmail: $"seller.{scenario}@smithlaw.test",
            buyerEmail: $"buyer.{scenario}@capital.test",
            buyerPhone: buyerPhone);

        var confirmResponse = await PostConfirmSaleAsync(
            lienId,
            $"confirm-sale-public-response-{scenario}-{Guid.NewGuid():N}");
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await confirmResponse.Content.ReadAsStringAsync()}");

        var confirmBody = await confirmResponse.Content.ReadFromJsonAsync<ConfirmSellingLienSaleResponse>();
        return (lienId, ExtractBuyerAccessToken(confirmBody!.Notification!.BuyerPortalUrl!));
    }

    private async Task<HttpResponseMessage> PostPublicBuyerResponseAsync(
        string token,
        string action,
        object body,
        string idempotencyKey)
    {
        using var anonClient = _factory.CreateClient();
        var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/liens/selling/public/{token}/{action}");
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        message.Content = JsonContent.Create(body);
        return await anonClient.SendAsync(message);
    }

    private async Task<HttpResponseMessage> PostPublicBuyerActivationAsync(
        string token,
        object body)
    {
        using var anonClient = _factory.CreateClient();
        return await anonClient.PostAsJsonAsync(
            $"/api/liens/selling/public/{token}/activate-account",
            body);
    }

    private async Task PrepareConfirmSaleDataAsync(
        Guid lienId,
        Guid buyerContactId,
        string? sellerEmail,
        string? buyerEmail,
        Guid? caseManagerId = null,
        string? documentFileName = null,
        string? buyerPhone = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

        if (sellerEmail is null)
        {
            foreach (var contact in db.Contacts.Where(c =>
                         c.TenantId == SeedHelper.TenantId &&
                         c.OrgId == SeedHelper.OrgId &&
                         c.Email != null &&
                         c.IsActive))
            {
                contact.Deactivate(SeedHelper.UserId);
            }
        }
        else
        {
            foreach (var contact in db.Contacts.Where(c =>
                         c.TenantId == SeedHelper.TenantId &&
                         c.OrgId == SeedHelper.OrgId &&
                         c.Email != null &&
                         c.IsActive))
            {
                contact.Deactivate(SeedHelper.UserId);
            }

            db.Contacts.Add(Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                ContactType.LawFirm,
                "Seller",
                "Operator",
                SeedHelper.UserId,
                organization: "Smith & Associates LLP",
                email: sellerEmail));
        }

        var buyerContact = Contact.Create(
            SeedHelper.TenantId,
            SeedHelper.FundingCompanyId,
            ContactType.LienHolder,
            "Buyer",
            "Reviewer",
            SeedHelper.UserId,
            organization: "Capital Fund LLC",
            email: buyerEmail,
            phone: buyerPhone);
        SetId(buyerContact, buyerContactId);
        db.Contacts.Add(buyerContact);

        if (caseManagerId.HasValue)
        {
            var caseManager = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                ContactType.InternalUser,
                "Case",
                "Manager",
                SeedHelper.UserId,
                organization: "Smith & Associates LLP",
                email: "case.manager@smithlaw.test");
            SetId(caseManager, caseManagerId.Value);
            db.Contacts.Add(caseManager);
        }

        var lien = db.Liens.Single(l => l.Id == lienId);
        lien.UpdateSellingAnalyticsFields(
            SeedHelper.UserId,
            sellerStatus: SellingLienStatus.PreparedForSale,
            listingVisibility: SellingListingVisibility.Private,
            fundingCompanyId: SeedHelper.FundingCompanyId,
            fundingCompanyContactId: buyerContactId,
            askAmount: 2500m);

        if (!string.IsNullOrWhiteSpace(documentFileName))
        {
            var documentId = Guid.CreateVersion7();
            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"DOC-{Guid.CreateVersion7():N}"[..36],
                "LegacyLienDocument",
                $"Lien document uploaded: {Path.GetFileNameWithoutExtension(documentFileName)}",
                "Seller Operator",
                SeedHelper.UserId,
                lienId: lienId,
                notes: $"documentId={documentId}; url=/documents/{documentId}; filename={Path.GetFileNameWithoutExtension(documentFileName)}; originalFileName={documentFileName}"));
        }

        await db.SaveChangesAsync();
    }

    private async Task<SellingPortfolioResponse> CreateEmptyPortfolioAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/liens/selling/portfolios",
            new CreateSellingPortfolioRequest
            {
                PortfolioNumber = $"PORT-{Guid.NewGuid():N}"[..20],
                Name = "Empty test portfolio",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<SellingPortfolioResponse>())!;
    }

    private async Task<(Guid CaseId, Guid LienId)> SeedExternalCaseAndLienAsync(
        string caseExternalId,
        string lienExternalId,
        string lienNumber,
        DateOnly? dateOfIncident = null,
        DateOnly? initialServiceDate = null,
        DateOnly? endServiceDate = null,
        string? caseNotes = null,
        string? lienNotes = null,
        string? status = null,
        decimal originalAmount = 12345m)
    {
        var caseId = Guid.CreateVersion7();
        var lienId = Guid.CreateVersion7();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

        var caseEntity = Case.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            $"CASE-{Guid.NewGuid():N}"[..20],
            "External",
            "Client",
            SeedHelper.UserId,
            externalReference: caseExternalId,
            dateOfIncident: dateOfIncident,
            notes: caseNotes);

        SetId(caseEntity, caseId);
        db.Cases.Add(caseEntity);

        var lien = Lien.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            lienNumber,
            LienType.MedicalLien,
            originalAmount,
            SeedHelper.UserId,
            externalReference: lienExternalId,
            caseId: caseId,
            initialServiceDate: initialServiceDate,
            endServiceDate: endServiceDate,
            notes: lienNotes);

        if (status == LienStatus.Sold)
        {
            lien.ListForSale(100m, SeedHelper.UserId);
            lien.MarkSold(90m, SeedHelper.FundingCompanyId, SeedHelper.UserId);
        }
        else if (!string.IsNullOrWhiteSpace(status))
        {
            SetStringProperty(lien, "Status", status);
        }

        SetId(lien, lienId);
        db.Liens.Add(lien);

        await db.SaveChangesAsync();
        return (caseId, lienId);
    }

    private async Task<Guid> SeedOtherTenantLienAsync()
    {
        var otherTenantId = Guid.Parse("10000000-0000-0000-0000-000000000099");
        var otherOrgId = Guid.Parse("30000000-0000-0000-0000-000000000099");
        var caseId = Guid.CreateVersion7();
        var lienId = Guid.CreateVersion7();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

        var caseEntity = Case.Create(
            otherTenantId,
            otherOrgId,
            $"CASE-{Guid.NewGuid():N}"[..20],
            "Other",
            "Tenant",
            SeedHelper.UserId);
        SetId(caseEntity, caseId);
        db.Cases.Add(caseEntity);

        var lien = Lien.Create(
            otherTenantId,
            otherOrgId,
            $"LIEN-{Guid.NewGuid():N}",
            LienType.MedicalLien,
            4000m,
            SeedHelper.UserId,
            caseId: caseId);
        SetId(lien, lienId);
        db.Liens.Add(lien);

        await db.SaveChangesAsync();
        return lienId;
    }

    private static void SetId<T>(T entity, Guid id) where T : class
    {
        var prop = typeof(T).GetProperty("Id",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(entity, id);
    }

    private static void SetStringProperty<T>(T entity, string propertyName, string value) where T : class
    {
        var prop = typeof(T).GetProperty(propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(entity, value);
    }

    private static void SetDateTimeProperty<T>(T entity, string propertyName, DateTime value) where T : class
    {
        var prop = typeof(T).GetProperty(propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(entity, value);
    }

    private static string ExtractBuyerAccessToken(string buyerPortalUrl)
    {
        var uri = new Uri(buyerPortalUrl, UriKind.Absolute);
        return Uri.UnescapeDataString(uri.Segments.Last().Trim('/'));
    }
}
