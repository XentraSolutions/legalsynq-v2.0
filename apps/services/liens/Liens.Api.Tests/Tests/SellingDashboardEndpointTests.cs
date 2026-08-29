using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Application.DTOs;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class SellingDashboardEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public SellingDashboardEndpointTests(LiensApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedHelper.SeedAsync(scope.ServiceProvider);

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Dashboard_returns_summary_for_all_filtered_rows_and_pending_tab_items()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var otherSellerOrgId = Guid.CreateVersion7();
        var pending = CreateDashboardLien(fundingCompanyId, SellingLienStatus.Pending, 1_000m, 800m, 700m);

        await SeedDashboardLiensAsync(db =>
        {
            db.Liens.Add(pending);
            db.Liens.Add(CreateDashboardLien(fundingCompanyId, SellingLienStatus.Internal, 2_000m, 1_600m, 0m));
            db.Liens.Add(CreateDashboardLien(
                fundingCompanyId,
                SellingLienStatus.Sold,
                3_000m,
                2_400m,
                0m,
                purchasePrice: 2_500m,
                soldAtUtc: DateTime.UtcNow.AddDays(-1)));
            db.Liens.Add(CreateDashboardLien(
                fundingCompanyId,
                SellingLienStatus.SubmittedForSale,
                10_000m,
                8_000m,
                7_000m));
            db.Liens.Add(CreateDashboardLien(
                fundingCompanyId,
                SellingLienStatus.Pending,
                9_000m,
                7_200m,
                0m,
                sellerOrgId: otherSellerOrgId));
        });

        var response = await _client.GetAsync(
            $"/api/liens/selling/dashboard?tab=pending&fundingCompanyId={fundingCompanyId}&sortBy=askAmount&sortDirection=desc");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<SellingDashboardResponse>();
        body.Should().NotBeNull();
        body!.Summary.TotalPortfolioValue.Should().Be(16_000m);
        body.Summary.TotalPending.Should().Be(11_000m);
        body.Summary.TotalInternal.Should().Be(2_000m);
        body.Summary.TotalSold.Should().Be(3_000m);
        body.Summary.PendingCount.Should().Be(2);
        body.Summary.InternalCount.Should().Be(1);
        body.Summary.SoldCount.Should().Be(1);
        body.TotalCount.Should().Be(2);
        body.Items.Should().HaveCount(2);
        body.Items.Should().Contain(item =>
            item.LienId == pending.Id &&
            item.HighestBidAmount == 700m &&
            item.Status == SellingLienStatus.Pending);
        body.Items.Should().Contain(item => item.Status == SellingLienStatus.SubmittedForSale);
    }

    [Fact]
    public async Task Dashboard_sold_tab_search_includes_accepted_liens_as_sold()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var accepted = CreateAcceptedDashboardLien(fundingCompanyId);
        await SeedDashboardLiensAsync(db => db.Liens.Add(accepted));

        var response = await _client.GetAsync(
            $"/api/liens/selling/dashboard?tab=sold&search={Uri.EscapeDataString(accepted.LienNumber)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<SellingDashboardResponse>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().Be(1);
        body.Summary.SoldCount.Should().Be(1);
        body.Summary.TotalSold.Should().Be(accepted.OriginalAmount);
        body.Items.Should().ContainSingle();
        body.Items[0].LienId.Should().Be(accepted.Id);
        body.Items[0].Status.Should().Be(SellingLienStatus.Sold);
    }

    [Fact]
    public async Task Pending_tab_keeps_approval_stage_liens_visible_with_current_status()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var approval = CreateDashboardLien(fundingCompanyId, SellingLienStatus.Approval, 1_000m, 800m, 0m);
        var submitted = CreateDashboardLien(fundingCompanyId, SellingLienStatus.SubmittedForSale, 2_000m, 1_600m, 0m);
        var prepared = CreateDashboardLien(fundingCompanyId, SellingLienStatus.PreparedForSale, 3_000m, 2_400m, 0m);

        await SeedDashboardLiensAsync(db => db.Liens.AddRange(approval, submitted, prepared));

        var response = await _client.GetAsync(
            $"/api/liens/selling/liens?tab=pending&fundingCompanyId={fundingCompanyId}&sortBy=askAmount&sortDirection=asc");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<SellingLienListResponse>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().Be(3);
        body.Items.Select(item => item.Status).Should().Equal(
            SellingLienStatus.Approval,
            SellingLienStatus.SubmittedForSale,
            SellingLienStatus.PreparedForSale);
        var approvalItem = body.Items.Single(item => item.LienId == approval.Id);
        approvalItem.CreatedAtUtc.Should().NotBe(default);
        approvalItem.CreateDate.Should().Be(approvalItem.CreatedAtUtc);
    }

    [Fact]
    public async Task Lien_list_defaults_to_all_tab_when_tab_is_omitted()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var caseEntity = Case.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            $"CASE-ALL-{Guid.NewGuid():N}"[..32],
            "All",
            "Tabs",
            SeedHelper.UserId);
        var pending = CreateDashboardLien(fundingCompanyId, SellingLienStatus.Pending, 1_000m, 800m, 0m, caseId: caseEntity.Id);
        var internalLien = CreateDashboardLien(fundingCompanyId, SellingLienStatus.Internal, 2_000m, 1_600m, 0m, caseId: caseEntity.Id);
        var sold = CreateDashboardLien(fundingCompanyId, SellingLienStatus.Sold, 3_000m, 2_400m, 0m, caseId: caseEntity.Id);

        await SeedDashboardLiensAsync(db => db.AddRange(caseEntity, pending, internalLien, sold));

        var response = await _client.GetAsync($"/api/liens/selling/liens?caseId={caseEntity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<SellingLienListResponse>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().Be(3);
        body.Items.Select(item => item.LienId).Should().BeEquivalentTo([pending.Id, internalLien.Id, sold.Id]);
    }

    [Fact]
    public async Task Archived_tab_returns_archived_liens_while_active_tabs_exclude_them()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var active = CreateDashboardLien(fundingCompanyId, SellingLienStatus.Pending, 1_000m, 800m, 0m);
        var archived = CreateDashboardLien(fundingCompanyId, SellingLienStatus.Archived, 2_000m, 1_600m, 0m);
        archived.UpdateSellingAnalyticsFields(
            SeedHelper.UserId,
            sellerStatus: SellingLienStatus.Archived,
            archivedAtUtc: DateTime.UtcNow.AddDays(-1),
            archivedReason: "duplicate");

        await SeedDashboardLiensAsync(db => db.Liens.AddRange(active, archived));

        var activeResponse = await _client.GetAsync(
            $"/api/liens/selling/liens?tab=pending&fundingCompanyId={fundingCompanyId}");
        activeResponse.StatusCode.Should().Be(HttpStatusCode.OK, await activeResponse.Content.ReadAsStringAsync());
        var activeBody = await activeResponse.Content.ReadFromJsonAsync<SellingLienListResponse>();
        activeBody.Should().NotBeNull();
        activeBody!.Items.Should().ContainSingle(item => item.LienId == active.Id);

        var archivedResponse = await _client.GetAsync(
            $"/api/liens/selling/liens?tab=archived&fundingCompanyId={fundingCompanyId}");
        archivedResponse.StatusCode.Should().Be(HttpStatusCode.OK, await archivedResponse.Content.ReadAsStringAsync());
        var archivedBody = await archivedResponse.Content.ReadFromJsonAsync<SellingLienListResponse>();
        archivedBody.Should().NotBeNull();
        archivedBody!.Items.Should().ContainSingle(item =>
            item.LienId == archived.Id &&
            item.Status == SellingLienStatus.Archived);
    }

    [Fact]
    public async Task Dashboard_highest_bid_sort_uses_grouped_offer_values_before_paging()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var buyerOrgId = Guid.CreateVersion7();
        var lowerBidLien = CreateDashboardLien(
            fundingCompanyId,
            SellingLienStatus.Pending,
            2_000m,
            1_800m,
            0m);
        var higherBidLien = CreateDashboardLien(
            fundingCompanyId,
            SellingLienStatus.Pending,
            3_000m,
            2_700m,
            0m);

        await SeedDashboardLiensAsync(db =>
        {
            db.Liens.AddRange(lowerBidLien, higherBidLien);
            db.LienOffers.Add(LienOffer.Create(
                SeedHelper.TenantId,
                lowerBidLien.Id,
                buyerOrgId,
                SeedHelper.OrgId,
                1_200m,
                SeedHelper.UserId));
            db.LienOffers.Add(LienOffer.Create(
                SeedHelper.TenantId,
                higherBidLien.Id,
                buyerOrgId,
                SeedHelper.OrgId,
                1_900m,
                SeedHelper.UserId));
        });

        var response = await _client.GetAsync(
            $"/api/liens/selling/dashboard?tab=pending&fundingCompanyId={fundingCompanyId}&sortBy=highestBid&sortDirection=desc&page=1&pageSize=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<SellingDashboardResponse>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().Be(2);
        body.Items.Should().ContainSingle();
        body.Items[0].LienId.Should().Be(higherBidLien.Id);
        body.Items[0].HighestBidAmount.Should().Be(1_900m);
    }

    [Fact]
    public async Task Dashboard_requires_sale_read_permission()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateToken(
                SeedHelper.TenantId,
                SeedHelper.UserId,
                [LiensPermissions.LienSaleCreate]));

        var response = await _client.GetAsync("/api/liens/selling/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dashboard_prefers_canonical_selling_org_over_conflicting_legacy_org()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var included = CreateDashboardLien(
            fundingCompanyId,
            SellingLienStatus.Pending,
            1_000m,
            900m,
            0m);
        SetLienOwnership(included, Guid.CreateVersion7(), SeedHelper.OrgId);
        var excluded = CreateDashboardLien(
            fundingCompanyId,
            SellingLienStatus.Pending,
            8_000m,
            7_000m,
            0m);
        SetLienOwnership(excluded, SeedHelper.OrgId, Guid.CreateVersion7());
        await SeedDashboardLiensAsync(db => db.Liens.AddRange(included, excluded));

        var response = await _client.GetAsync(
            $"/api/liens/selling/dashboard?tab=pending&fundingCompanyId={fundingCompanyId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<SellingDashboardResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().ContainSingle(item => item.LienId == included.Id);
        body.Summary.TotalPending.Should().Be(1_000m);
    }

    [Fact]
    public async Task Dashboard_rejects_unknown_tabs()
    {
        var response = await _client.GetAsync("/api/liens/selling/dashboard?tab=ready-to-sell");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Dashboard_filters_and_labels_canonical_v2_selling_parties()
    {
        var fundingCompany = Company.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            CompanyDirectoryReferenceData.FundingCompanyId,
            "Canonical Funding LLC",
            SeedHelper.UserId);
        var lawFirm = Company.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            CompanyDirectoryReferenceData.LawFirmId,
            "Canonical Law LLP",
            SeedHelper.UserId);
        var caseManager = CompanyContactPerson.Create(
            SeedHelper.TenantId,
            lawFirm.Id,
            Guid.CreateVersion7(),
            "Cameron",
            "Manager",
            SeedHelper.UserId);
        var caseEntity = Case.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            $"CASE-CANONICAL-{Guid.NewGuid():N}"[..32],
            "Jamie",
            "Client",
            SeedHelper.UserId);
        caseEntity.LinkCanonicalCaseParties(lawFirm.Id, caseManager.Id);
        var lien = Lien.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            $"LIEN-CANONICAL-{Guid.NewGuid():N}"[..32],
            LienType.MedicalLien,
            1_500m,
            SeedHelper.UserId,
            caseId: caseEntity.Id,
            initialServiceDate: new DateOnly(2026, 1, 15));
        lien.LinkCanonicalSellingParties(fundingCompany.Id, null, null, null);

        await SeedDashboardLiensAsync(db => db.AddRange(
            fundingCompany,
            lawFirm,
            caseManager,
            caseEntity,
            lien));

        var response = await _client.GetAsync(
            $"/api/liens/selling/dashboard?tab=pending&fundingCompanyId={fundingCompany.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<SellingDashboardResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().ContainSingle();
        body.Items[0].FundingCompanyId.Should().Be(fundingCompany.Id);
        body.Items[0].FundingCompany.Should().Be("Canonical Funding LLC");
        body.Items[0].LawFirmId.Should().Be(lawFirm.Id);
        body.Items[0].LawFirm.Should().Be("Canonical Law LLP");
        body.Items[0].CaseManagerId.Should().Be(caseManager.Id);
        body.Items[0].CaseManager.Should().Be("Cameron Manager");
    }

    [Fact]
    public async Task Lien_list_returns_the_filtered_page_without_dashboard_summary()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var pending = CreateDashboardLien(fundingCompanyId, SellingLienStatus.Pending, 1_000m, 800m, 700m);
        await SeedDashboardLiensAsync(db =>
        {
            db.Liens.Add(pending);
            db.Liens.Add(CreateDashboardLien(fundingCompanyId, SellingLienStatus.Internal, 2_000m, 1_600m, 0m));
        });

        var response = await _client.GetAsync(
            $"/api/liens/selling/liens?tab=pending&fundingCompanyId={fundingCompanyId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement.TryGetProperty("summary", out _).Should().BeFalse();

        var body = JsonSerializer.Deserialize<SellingLienListResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        body.Should().NotBeNull();
        body!.TotalCount.Should().Be(1);
        body.Items.Should().ContainSingle();
        body.Items[0].LienId.Should().Be(pending.Id);
    }

    [Fact]
    public async Task Lien_list_defaults_to_create_date_sort()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var olderLien = CreateDashboardLien(fundingCompanyId, SellingLienStatus.Pending, 1_000m, 800m, 0m);
        var newerLien = CreateDashboardLien(fundingCompanyId, SellingLienStatus.Pending, 2_000m, 1_600m, 0m);
        SetCreatedAtUtc(olderLien, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SetCreatedAtUtc(newerLien, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        await SeedDashboardLiensAsync(db => db.Liens.AddRange(olderLien, newerLien));

        var response = await _client.GetAsync(
            $"/api/liens/selling/liens?tab=pending&fundingCompanyId={fundingCompanyId}&sortDirection=desc");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<SellingLienListResponse>();
        body.Should().NotBeNull();
        body!.Items.Select(item => item.LienId).Should().Equal(newerLien.Id, olderLien.Id);
    }

    [Theory]
    [InlineData("createDate")]
    [InlineData("createdAtUtc")]
    public async Task Lien_list_supports_creation_timestamp_sort_aliases(string sortBy)
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var olderLien = CreateDashboardLien(fundingCompanyId, SellingLienStatus.Pending, 1_000m, 800m, 0m);
        var newerLien = CreateDashboardLien(fundingCompanyId, SellingLienStatus.Pending, 2_000m, 1_600m, 0m);
        SetCreatedAtUtc(olderLien, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SetCreatedAtUtc(newerLien, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        await SeedDashboardLiensAsync(db => db.Liens.AddRange(olderLien, newerLien));

        var response = await _client.GetAsync(
            $"/api/liens/selling/liens?tab=pending&fundingCompanyId={fundingCompanyId}&sortBy={sortBy}&sortDirection=asc");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<SellingLienListResponse>();
        body.Should().NotBeNull();
        body!.Items.Select(item => item.LienId).Should().Equal(olderLien.Id, newerLien.Id);
    }

    [Fact]
    public async Task Lien_list_filters_rows_and_total_count_by_case_id()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var targetCase = Case.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            $"CASE-TARGET-{Guid.NewGuid():N}"[..32],
            "Target",
            "Plaintiff",
            SeedHelper.UserId);
        var otherCase = Case.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            $"CASE-OTHER-{Guid.NewGuid():N}"[..32],
            "Other",
            "Plaintiff",
            SeedHelper.UserId);
        var matchingLien = CreateDashboardLien(
            fundingCompanyId,
            SellingLienStatus.Pending,
            1_000m,
            800m,
            0m,
            caseId: targetCase.Id);
        var otherLien = CreateDashboardLien(
            fundingCompanyId,
            SellingLienStatus.Pending,
            2_000m,
            1_600m,
            0m,
            caseId: otherCase.Id);

        await SeedDashboardLiensAsync(db => db.AddRange(targetCase, otherCase, matchingLien, otherLien));

        var response = await _client.GetAsync(
            $"/api/liens/selling/liens?tab=all&caseId={targetCase.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<SellingLienListResponse>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().Be(1);
        body.Items.Should().ContainSingle(item =>
            item.LienId == matchingLien.Id && item.CaseId == targetCase.Id);
        body.Items.Should().NotContain(item => item.LienId == otherLien.Id);
    }

    private async Task SeedDashboardLiensAsync(Action<LiensDbContext> arrange)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        arrange(db);
        await db.SaveChangesAsync();
    }

    private static Lien CreateDashboardLien(
        Guid fundingCompanyId,
        string sellerStatus,
        decimal originalAmount,
        decimal askAmount,
        decimal highestBidAmount,
        Guid? sellerOrgId = null,
        decimal? purchasePrice = null,
        DateTime? soldAtUtc = null,
        Guid? caseId = null)
    {
        var orgId = sellerOrgId ?? SeedHelper.OrgId;
        var lien = Lien.Create(
            SeedHelper.TenantId,
            orgId,
            $"LIEN-DASHBOARD-{Guid.NewGuid():N}"[..32],
            LienType.MedicalLien,
            originalAmount,
            SeedHelper.UserId,
            caseId: caseId ?? SeedHelper.CaseId,
            facilityId: SeedHelper.FacilityId,
            initialServiceDate: new DateOnly(2026, 1, 15));

        if (purchasePrice.HasValue)
        {
            lien.SetFinancials(
                originalAmount,
                SeedHelper.UserId,
                currentBalance: originalAmount,
                purchasePrice: purchasePrice);
        }

        lien.UpdateSellingAnalyticsFields(
            SeedHelper.UserId,
            sellerStatus: sellerStatus,
            fundingCompanyId: fundingCompanyId,
            askAmount: askAmount,
            highestBidAmount: highestBidAmount,
            soldAtUtc: soldAtUtc);

        return lien;
    }

    private static Lien CreateAcceptedDashboardLien(Guid fundingCompanyId)
    {
        var lien = Lien.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            $"LIEN-ACCEPTED-{Guid.NewGuid():N}"[..32],
            LienType.MedicalLien,
            3_000m,
            SeedHelper.UserId,
            caseId: SeedHelper.CaseId,
            facilityId: SeedHelper.FacilityId,
            initialServiceDate: new DateOnly(2026, 1, 15));

        lien.ListForSale(2_500m, SeedHelper.UserId);
        lien.TransitionStatus(LienStatus.Accepted, SeedHelper.UserId);
        lien.UpdateSellingAnalyticsFields(
            SeedHelper.UserId,
            sellerStatus: SellingLienStatus.Accepted,
            fundingCompanyId: fundingCompanyId,
            askAmount: 2_500m,
            highestBidAmount: 2_500m);

        return lien;
    }

    private static void SetLienOwnership(Lien lien, Guid orgId, Guid? sellingOrgId)
    {
        typeof(Lien).GetProperty(nameof(Lien.OrgId))!.SetValue(lien, orgId);
        typeof(Lien).GetProperty(nameof(Lien.SellingOrgId))!.SetValue(lien, sellingOrgId);
    }

    private static void SetCreatedAtUtc(Lien lien, DateTime createdAtUtc) =>
        typeof(Lien).GetProperty(nameof(Lien.CreatedAtUtc))!.SetValue(lien, createdAtUtc);
}
