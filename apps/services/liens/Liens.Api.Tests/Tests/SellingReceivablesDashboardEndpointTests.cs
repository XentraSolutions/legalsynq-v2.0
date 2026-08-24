using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Liens.Api.Tests.Helpers;
using Liens.Application.DTOs;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Liens.Api.Tests.Tests;

public sealed class SellingReceivablesDashboardEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public SellingReceivablesDashboardEndpointTests(LiensApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedHelper.SeedAsync(scope.ServiceProvider);
        await ResetDashboardDataAsync(scope.ServiceProvider.GetRequiredService<LiensDbContext>());
        _client = CreateAuthorizedClient(_factory);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Dashboard_aggregates_today_and_resolves_canonical_and_legacy_org_buyers()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var canonicalCompany = Company.Create(
            SeedHelper.TenantId, SeedHelper.OrgId, CompanyDirectoryReferenceData.FundingCompanyId,
            "Apex Mutual", SeedHelper.UserId);
        var legacyBuyerOrgId = Guid.CreateVersion7();
        var legacyBuyer = Contact.Create(
            SeedHelper.TenantId, legacyBuyerOrgId, ContactType.LienHolder,
            "Nova", "Care", SeedHelper.UserId, organization: "Nova Care");

        var reductionLien = CreateLien("AR-REDUCTION", 1_000m, 900m, today.AddDays(-17));
        reductionLien.LinkCanonicalSellingParties(canonicalCompany.Id, null, null, null);
        var settledLien = CreateLien("AR-SETTLED", 2_000m, 700m, today.AddDays(-87));
        settledLien.UpdateSellingAnalyticsFields(SeedHelper.UserId, fundingCompanyId: legacyBuyerOrgId);
        var activeLegacyLien = CreateLien("AR-LEGACY-ID", 100m, 100m, today.AddDays(-5));
        activeLegacyLien.UpdateSellingAnalyticsFields(SeedHelper.UserId, fundingCompanyId: legacyBuyer.Id);
        var paidLien = CreateLien("AR-PAID", 500m, 0m, today.AddDays(-179));
        paidLien.LinkCanonicalSellingParties(canonicalCompany.Id, null, null, null);
        var closedLien = CreateLien("AR-CLOSED", 50m, 50m, today.AddDays(3), LienStatus.Cancelled);
        var unagedLien = CreateLien("AR-UNAGED", 100m, 100m, null);
        var otherTenantId = Guid.CreateVersion7();

        await ArrangeAsync(db =>
        {
            db.Companies.Add(canonicalCompany);
            db.Contacts.Add(legacyBuyer);
            db.Liens.AddRange(reductionLien, settledLien, activeLegacyLien, paidLien, closedLien, unagedLien);
            db.LienReductions.AddRange(
                LienReduction.Create(SeedHelper.TenantId, SeedHelper.CaseId, reductionLien.Id, monthStart, 100m, SeedHelper.UserId),
                LienReduction.Create(SeedHelper.TenantId, SeedHelper.CaseId, settledLien.Id, monthStart, 100m, SeedHelper.UserId),
                LienReduction.Create(SeedHelper.TenantId, SeedHelper.CaseId, paidLien.Id, monthStart, 100m, SeedHelper.UserId),
                LienReduction.Create(otherTenantId, SeedHelper.CaseId, activeLegacyLien.Id, monthStart, 999m, SeedHelper.UserId));
            db.LienSettlements.AddRange(
                LienSettlement.Create(SeedHelper.TenantId, SeedHelper.CaseId, settledLien.Id, 1, 600m, SeedHelper.UserId, settlementDate: monthStart),
                LienSettlement.Create(SeedHelper.TenantId, SeedHelper.CaseId, paidLien.Id, 1, 400m, SeedHelper.UserId, settlementDate: monthStart),
                LienSettlement.Create(otherTenantId, SeedHelper.CaseId, activeLegacyLien.Id, 1, 1m, SeedHelper.UserId, settlementDate: monthStart));
            db.SettlementPaymentDetails.AddRange(
                SettlementPaymentDetail.Create(SeedHelper.TenantId, SeedHelper.CaseId, settledLien.Id, 1, 300m, SeedHelper.UserId, monthStart),
                SettlementPaymentDetail.Create(SeedHelper.TenantId, SeedHelper.CaseId, paidLien.Id, 1, 400m, SeedHelper.UserId, monthStart),
                SettlementPaymentDetail.Create(SeedHelper.TenantId, SeedHelper.CaseId, unagedLien.Id, 1, 25m, SeedHelper.UserId),
                SettlementPaymentDetail.Create(SeedHelper.TenantId, SeedHelper.CaseId, activeLegacyLien.Id, 1, 999m, SeedHelper.UserId, today.AddDays(1)),
                SettlementPaymentDetail.Create(otherTenantId, SeedHelper.CaseId, activeLegacyLien.Id, 2, 1m, SeedHelper.UserId, monthStart));

            var deletedPayment = SettlementPaymentDetail.Create(
                SeedHelper.TenantId, SeedHelper.CaseId, settledLien.Id, 2, 600m, SeedHelper.UserId, monthStart);
            deletedPayment.SoftDelete(SeedHelper.UserId);
            db.SettlementPaymentDetails.Add(deletedPayment);
        });

        var response = await _client.GetAsync(
            "/api/liens/selling/analytics/receivables-dashboard?months=2&topBuyerLimit=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
        var body = await response.Content.ReadFromJsonAsync<SellingReceivablesDashboardResponse>();
        body.Should().NotBeNull();
        body!.AsOfDate.Should().Be(today);
        body.Summary.TotalReceivables.Amount.Should().Be(3_750m);
        body.Summary.TotalOutstanding.Amount.Should().Be(1_850m);
        body.Summary.PastDueBalance.Amount.Should().Be(1_700m);
        body.Summary.PaymentsReceived.Amount.Should().Be(700m);
        body.Summary.TotalOutstanding.TrendAvailable.Should().BeFalse();
        body.Summary.TotalOutstanding.TrendPercent.Should().BeNull();

        body.AgingSummary.Buckets.Single(bucket => bucket.Key == "0_30")
            .Should().BeEquivalentTo(new { LienCount = 3, Amount = 1_050m, Percent = 56.76m });
        body.AgingSummary.Buckets.Single(bucket => bucket.Key == "61_90")
            .Should().BeEquivalentTo(new { LienCount = 1, Amount = 700m, Percent = 37.84m });
        body.AgingSummary.UnagedBalance.Should().Be(100m);

        body.StatusBreakdown.TotalLiens.Should().Be(6);
        body.StatusBreakdown.Items.Single(item => item.Key == "active").Count.Should().Be(2);
        body.StatusBreakdown.Items.Where(item => item.Key != "active").Should().OnlyContain(item => item.Count == 1);

        body.TopBuyers.Should().HaveCount(2);
        body.TopBuyers[0].BuyerName.Should().Be("Apex Mutual");
        body.TopBuyers[0].ActiveLienCount.Should().Be(0);
        body.TopBuyers[1].BuyerName.Should().Be("Nova Care");
        body.TopBuyers[1].OutstandingBalance.Should().Be(800m);
        body.TopBuyers[1].ActiveLienCount.Should().Be(1);
        body.BuyerAging.Single(item => item.BuyerId == legacyBuyer.Id).OutstandingBalance.Should().Be(800m);

        body.LiensOverTime.Should().HaveCount(2);
        body.LiensOverTime[0].DataAvailable.Should().BeFalse();
        body.LiensOverTime[1].DataAvailable.Should().BeTrue();
        body.DataQuality.MissingDueDateCount.Should().Be(1);
        body.DataQuality.UnassignedBuyerCount.Should().Be(2);
        body.DataQuality.UndatedPaymentCount.Should().Be(1);
    }

    [Fact]
    public async Task Dashboard_uses_exact_aging_boundaries()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dueOffsets = new[] { 0, 30, 31, 60, 61, 90, 91, 120, 121, -1 };
        await ArrangeAsync(db =>
        {
            foreach (var offset in dueOffsets)
                db.Liens.Add(CreateLien($"AR-BOUNDARY-{offset}", 10m, 10m, today.AddDays(-offset)));
        });

        var body = await _client.GetFromJsonAsync<SellingReceivablesDashboardResponse>(
            "/api/liens/selling/analytics/receivables-dashboard");

        body!.AgingSummary.Buckets.Single(bucket => bucket.Key == "0_30").LienCount.Should().Be(3);
        body.AgingSummary.Buckets.Single(bucket => bucket.Key == "31_60").LienCount.Should().Be(2);
        body.AgingSummary.Buckets.Single(bucket => bucket.Key == "61_90").LienCount.Should().Be(2);
        body.AgingSummary.Buckets.Single(bucket => bucket.Key == "91_120").LienCount.Should().Be(2);
        body.AgingSummary.Buckets.Single(bucket => bucket.Key == "120_plus").LienCount.Should().Be(1);
    }

    [Fact]
    public async Task Dashboard_rejects_noncurrent_as_of_dates_and_invalid_limits()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        (await _client.GetAsync($"/api/liens/selling/analytics/receivables-dashboard?asOfDate={today.AddDays(-1):yyyy-MM-dd}"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.GetAsync($"/api/liens/selling/analytics/receivables-dashboard?asOfDate={today.AddDays(1):yyyy-MM-dd}"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.GetAsync("/api/liens/selling/analytics/receivables-dashboard?months=13&topBuyerLimit=0"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Dashboard_uses_injected_utc_date_for_default_and_validation()
    {
        var fixedDate = new DateOnly(2031, 4, 12);
        await using var factory = new FixedTimeLiensApiFactory(fixedDate);
        using (var scope = factory.Services.CreateScope())
        {
            await SeedHelper.SeedAsync(scope.ServiceProvider);
            await ResetDashboardDataAsync(scope.ServiceProvider.GetRequiredService<LiensDbContext>());
        }
        using var client = CreateAuthorizedClient(factory);

        var body = await client.GetFromJsonAsync<SellingReceivablesDashboardResponse>(
            "/api/liens/selling/analytics/receivables-dashboard");
        body!.AsOfDate.Should().Be(fixedDate);
        (await client.GetAsync("/api/liens/selling/analytics/receivables-dashboard?asOfDate=2031-04-11"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Dashboard_preserves_legacy_org_scope_fallback_and_excludes_other_scopes()
    {
        var fallback = CreateLien("AR-ORG-FALLBACK", 100m, 80m, null);
        SetPrivateProperty(fallback, nameof(Lien.SellingOrgId), Guid.CreateVersion7());
        await ArrangeAsync(db =>
        {
            db.Liens.Add(fallback);
            db.Liens.Add(CreateLien("AR-OTHER-ORG", 900m, 800m, null, sellerOrgId: Guid.CreateVersion7()));
            db.Liens.Add(CreateLien("AR-OTHER-TENANT", 700m, 600m, null, tenantId: Guid.CreateVersion7()));
        });

        var body = await _client.GetFromJsonAsync<SellingReceivablesDashboardResponse>(
            "/api/liens/selling/analytics/receivables-dashboard");

        body!.Summary.TotalReceivables.Amount.Should().Be(100m);
        body.Summary.TotalOutstanding.Amount.Should().Be(80m);
        body.StatusBreakdown.TotalLiens.Should().Be(1);
    }

    [Fact]
    public async Task Dashboard_treats_inactive_or_wrong_type_buyers_as_unassigned()
    {
        var inactiveCompany = Company.Create(
            SeedHelper.TenantId, SeedHelper.OrgId, CompanyDirectoryReferenceData.FundingCompanyId,
            "Inactive Buyer", SeedHelper.UserId);
        inactiveCompany.Deactivate(SeedHelper.UserId);
        var wrongTypeContact = Contact.Create(
            SeedHelper.TenantId, Guid.CreateVersion7(), ContactType.Provider,
            "Wrong", "Type", SeedHelper.UserId);
        var canonicalLien = CreateLien("AR-INACTIVE-BUYER", 100m, 100m, null);
        canonicalLien.LinkCanonicalSellingParties(inactiveCompany.Id, null, null, null);
        var legacyLien = CreateLien("AR-WRONG-TYPE", 100m, 100m, null);
        legacyLien.UpdateSellingAnalyticsFields(SeedHelper.UserId, fundingCompanyId: wrongTypeContact.OrgId);
        await ArrangeAsync(db =>
        {
            db.Companies.Add(inactiveCompany);
            db.Contacts.Add(wrongTypeContact);
            db.Liens.AddRange(canonicalLien, legacyLien);
        });

        var body = await _client.GetFromJsonAsync<SellingReceivablesDashboardResponse>(
            "/api/liens/selling/analytics/receivables-dashboard");

        body!.TopBuyers.Should().BeEmpty();
        body.DataQuality.UnassignedBuyerCount.Should().Be(2);
    }

    [Fact]
    public async Task Dashboard_requires_view_analytics_permission()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateToken(SeedHelper.TenantId, SeedHelper.UserId, [LiensPermissions.LienSaleRead]));

        (await _client.GetAsync("/api/liens/selling/analytics/receivables-dashboard"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dashboard_returns_complete_empty_shape()
    {
        var response = await _client.GetAsync("/api/liens/selling/analytics/receivables-dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SellingReceivablesDashboardResponse>();
        body!.Summary.TotalOutstanding.Amount.Should().Be(0m);
        body.AgingSummary.Buckets.Should().HaveCount(5);
        body.StatusBreakdown.Items.Should().HaveCount(5);
        body.TopBuyers.Should().BeEmpty();
        body.BuyerAging.Should().BeEmpty();
    }

    private static HttpClient CreateAuthorizedClient(LiensApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));
        return client;
    }

    private async Task ArrangeAsync(Action<LiensDbContext> arrange)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        arrange(db);
        await db.SaveChangesAsync();
    }

    private static async Task ResetDashboardDataAsync(LiensDbContext db)
    {
        db.SettlementPaymentDetails.RemoveRange(db.SettlementPaymentDetails);
        db.LienSettlements.RemoveRange(db.LienSettlements);
        db.LienReductions.RemoveRange(db.LienReductions);
        db.Liens.RemoveRange(db.Liens);
        await db.SaveChangesAsync();
    }

    private static Lien CreateLien(
        string lienNumber,
        decimal originalAmount,
        decimal currentBalance,
        DateOnly? receivableDueDate,
        string status = LienStatus.Active,
        Guid? sellerOrgId = null,
        Guid? tenantId = null)
    {
        var lien = Lien.Create(
            tenantId ?? SeedHelper.TenantId, sellerOrgId ?? SeedHelper.OrgId, lienNumber,
            LienType.MedicalLien, originalAmount, SeedHelper.UserId, caseId: SeedHelper.CaseId);
        lien.SetFinancials(originalAmount, SeedHelper.UserId, currentBalance: currentBalance);
        lien.SetReceivableDueDate(receivableDueDate, SeedHelper.UserId);
        lien.SetLegacyMedicalStatus(status, SeedHelper.UserId);
        return lien;
    }

    private static void SetPrivateProperty<T>(T entity, string propertyName, object? value) where T : class
    {
        typeof(T).GetProperty(propertyName)!.SetValue(entity, value);
    }

    private sealed class FixedTimeLiensApiFactory : LiensApiFactory
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeLiensApiFactory(DateOnly date) =>
            _utcNow = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(_utcNow));
            });
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
