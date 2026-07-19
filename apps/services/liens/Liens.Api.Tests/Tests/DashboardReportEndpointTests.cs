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

public class DashboardReportEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private static readonly Guid CaseManagerContactId = new("40000000-0000-0000-0000-000000000099");

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

        var item = payload.RootElement.GetProperty("items")[0];
        item.GetProperty("caseNumber").GetString().Should().Be("CASE-TEST-001");
        item.GetProperty("lawFirm").GetString().Should().Be("Smith & Associates LLP");
        item.GetProperty("totalLienAmount").GetDecimal().Should().Be(5000m);
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

        var item = payload.RootElement.GetProperty("items")[0];
        item.GetProperty("lienNumber").GetString().Should().Be("LIEN-TEST-001");
        item.GetProperty("caseId").GetString().Should().Be("CASE-TEST-001");
        item.GetProperty("caseRecordId").GetString().Should().Be(SeedHelper.CaseId.ToString());
        item.GetProperty("caseNumber").GetString().Should().Be("CASE-TEST-001");
        item.GetProperty("medicalProvider").GetString().Should().Be("City Medical Center");
        item.GetProperty("totalPurchaseAmount").GetDecimal().Should().Be(100m);
        item.GetProperty("totalBillingAmount").GetDecimal().Should().Be(150m);
    }

    [Fact]
    public async Task TotalLienReportV3_resolves_facility_id_from_contact_facility_name()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/liens/cases/dashboard/total-lien-report-export/v3",
            new { page = 1, limit = 10 });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = payload.RootElement.GetProperty("items")[0];
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
        payload.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
        payload.RootElement.GetProperty("items")[0].GetProperty("lawFirmId").GetString()
            .Should().Be(SeedHelper.LawFirmId.ToString());
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
        payload.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
        payload.RootElement.GetProperty("items")[0].GetProperty("medicalProviderId").GetString()
            .Should().Be(SeedHelper.MedicalProviderId.ToString());
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

        await db.SaveChangesAsync();
    }

    private static void SetId<T>(T entity, Guid id) where T : class
    {
        var prop = typeof(T).GetProperty("Id",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(entity, id);
    }
}
