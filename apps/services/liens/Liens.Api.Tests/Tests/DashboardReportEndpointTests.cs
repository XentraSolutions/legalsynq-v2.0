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

public class DashboardReportEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private static readonly Guid CaseManagerContactId = new("40000000-0000-0000-0000-000000000099");
    private static readonly Guid AliasedLawFirmCaseId = new("60000000-0000-0000-0000-000000000099");
    private static readonly Guid AliasedProviderLienId = new("70000000-0000-0000-0000-000000000099");
    private static readonly Guid ActiveDashboardLienId = new("70000000-0000-0000-0000-000000000100");
    private static readonly Guid CancelledDashboardLienId = new("70000000-0000-0000-0000-000000000101");
    private static readonly Guid SettledDashboardLienId = new("70000000-0000-0000-0000-000000000102");

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
    }

    [Fact]
    public async Task TotalLienReportV3_excludes_rejected_and_normalizes_remaining_business_statuses()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/total-lien-report-export/v3",
            new { page = 1, limit = 50 });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = payload.RootElement.GetProperty("items").EnumerateArray().ToList();

        items.Should().Contain(i =>
            i.GetProperty("id").GetString() == SeedHelper.LienId.ToString() &&
            i.GetProperty("status").GetString() == "Open");
        items.Should().Contain(i =>
            i.GetProperty("id").GetString() == ActiveDashboardLienId.ToString() &&
            i.GetProperty("status").GetString() == "Open");
        items.Should().NotContain(i =>
            i.GetProperty("id").GetString() == CancelledDashboardLienId.ToString());
        items.Should().NotContain(i =>
            i.GetProperty("status").GetString() == "Rejected");
        items.Should().Contain(i =>
            i.GetProperty("id").GetString() == SettledDashboardLienId.ToString() &&
            i.GetProperty("status").GetString() == "Closed");

        var counts = items
            .GroupBy(i => i.GetProperty("status").GetString())
            .ToDictionary(group => group.Key!, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        counts["Open"].Should().Be(3);
        counts["Closed"].Should().Be(1);
        payload.RootElement.GetProperty("totalCount").GetInt32().Should().Be(4);
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
            notes: lienEntity.Notes);

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
                facilityId: SeedHelper.FacilityId);
            SetId(aliasedLien, AliasedProviderLienId);
            db.Liens.Add(aliasedLien);
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
                facilityId: SeedHelper.FacilityId);
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
                facilityId: SeedHelper.FacilityId);
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
                facilityId: SeedHelper.FacilityId);
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

        await db.SaveChangesAsync();
    }

    private static void SetId<T>(T entity, Guid id) where T : class
    {
        var prop = typeof(T).GetProperty("Id",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(entity, id);
    }
}
