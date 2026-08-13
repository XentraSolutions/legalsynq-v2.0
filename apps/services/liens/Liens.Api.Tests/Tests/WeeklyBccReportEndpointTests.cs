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

public sealed class WeeklyBccReportEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public WeeklyBccReportEndpointTests(LiensApiFactory factory) => _factory = factory;

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
    public async Task GetWeeklyBcc_returns_requested_fields_and_applies_inclusive_as_of_date()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "CASE-BCC-001",
                "Ada",
                "Lovelace",
                SeedHelper.UserId,
                clientDob: new DateOnly(1985, 12, 10),
                clientPhone: "555-0100",
                clientAddress: "10 Main St, Austin, TX, 78701",
                dateOfIncident: new DateOnly(2026, 1, 15),
                notes: $"Weekly report note\n\n[legacy-meta]\nlawFirmId={SeedHelper.LawFirmId}; accidentType=Auto; accidentState=TX; currentMedicalStatus=Treating; trackingFollowUpDate=08/20/2026");
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LIEN-BCC-001",
                LienType.MedicalLien,
                1200m,
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                facilityId: SeedHelper.FacilityId,
                initialServiceDate: new DateOnly(2026, 2, 1),
                endServiceDate: new DateOnly(2026, 3, 1),
                purchaseDate: new DateOnly(2026, 8, 1));
            lien.SetFinancials(1200m, SeedHelper.UserId, purchasePrice: 700m);
            lien.SetLegacyMedicalStatus(LienStatus.Settled, SeedHelper.UserId);
            var futureLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LIEN-BCC-FUTURE",
                LienType.MedicalLien,
                500m,
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                purchaseDate: new DateOnly(2026, 8, 14));
            var medicalCode = ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "BCC-MED-001",
                "LegacyMedicalCode",
                "Medical code",
                "Legacy Import",
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                lienId: lien.Id,
                notes: "code=CPT-100; billingAmount=1200; purchaseAmount=700");
            var facilityInfo = ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "BCC-FAC-001",
                "LegacyMedicalFacilityInfo",
                "Facility information",
                "Legacy Import",
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                lienId: lien.Id,
                notes: $"facilityId={SeedHelper.FacilityId}; facilityName=Sunrise Clinic; medicalProviderId={SeedHelper.MedicalProviderId}; facilityContactPersonId={SeedHelper.FacilityContactId}");
            var activity = ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "BCC-ACT-001",
                "CaseTracking",
                "Called law firm",
                "Case Manager",
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                lienId: lien.Id,
                notes: "Awaiting response");
            var reduction = LienReduction.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                lien.Id,
                new DateOnly(2026, 8, 5),
                200m,
                SeedHelper.UserId);
            var futureReduction = LienReduction.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                lien.Id,
                new DateOnly(2026, 8, 14),
                500m,
                SeedHelper.UserId);
            var settlement = LienSettlement.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                lien.Id,
                1,
                1000m,
                SeedHelper.UserId,
                settlementDate: new DateOnly(2026, 8, 10));
            var payment = SettlementPaymentDetail.Create(
                SeedHelper.TenantId,
                caseEntity.Id,
                lien.Id,
                1,
                1000m,
                SeedHelper.UserId,
                paymentDate: new DateOnly(2026, 8, 11));
            var note = LienCaseNote.Create(
                caseEntity.Id,
                SeedHelper.TenantId,
                "Latest case note",
                CaseNoteCategory.General,
                SeedHelper.UserId,
                "Report User");
            var feedNote = LienCaseNote.Create(
                caseEntity.Id,
                SeedHelper.TenantId,
                "Latest feed note",
                CaseNoteCategory.Feed,
                SeedHelper.UserId,
                "Report User");
            var otherTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var otherTenantCase = Case.Create(
                otherTenantId,
                SeedHelper.OrgId,
                "CASE-BCC-OTHER-TENANT",
                "Hidden",
                "Plaintiff",
                SeedHelper.UserId);
            var otherTenantLien = Lien.Create(
                otherTenantId,
                SeedHelper.OrgId,
                "LIEN-BCC-OTHER-TENANT",
                LienType.MedicalLien,
                999m,
                SeedHelper.UserId,
                caseId: otherTenantCase.Id,
                purchaseDate: new DateOnly(2026, 8, 1));

            db.Cases.AddRange(caseEntity, otherTenantCase);
            db.Liens.AddRange(lien, futureLien, otherTenantLien);
            db.ServicingItems.AddRange(medicalCode, facilityInfo, activity);
            db.LienReductions.AddRange(reduction, futureReduction);
            db.LienSettlements.Add(settlement);
            db.SettlementPaymentDetails.Add(payment);
            db.LienCaseNotes.AddRange(note, feedNote);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/report/weekly-bcc", new
        {
            asOfDate = "2026-08-13",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var root = document!.RootElement;
        root.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        root.GetProperty("asOfDate").GetString().Should().Be("2026-08-13");
        root.GetProperty("totalCount").GetInt32().Should().Be(1);
        response.Headers.CacheControl?.NoStore.Should().BeTrue();

        var row = root.GetProperty("data")[0];
        row.GetProperty("plaintiffFirstName").GetString().Should().Be("Ada");
        row.GetProperty("plaintiffLastName").GetString().Should().Be("Lovelace");
        row.GetProperty("plaintiffCity").GetString().Should().Be("Austin");
        row.GetProperty("lienId").GetString().Should().Be("LIEN-BCC-001");
        row.GetProperty("caseId").GetString().Should().Be("CASE-BCC-001");
        row.GetProperty("daysSincePurchase").GetInt32().Should().Be(12);
        row.GetProperty("purchaseAmt").GetString().Should().Be("700.00");
        row.GetProperty("billingAmt").GetString().Should().Be("1,200.00");
        row.GetProperty("expectedSettlementAmt").GetString().Should().Be("500.00");
        row.GetProperty("returnedAmt").GetString().Should().Be("1,000.00");
        row.GetProperty("grossProfit").GetString().Should().Be("300.00");
        row.GetProperty("medicalCodeCount").GetInt32().Should().Be(1);
        row.GetProperty("medicalCodes").GetString().Should().Be("CPT-100");
        row.GetProperty("medicalFacility").GetString().Should().Be("Sunrise Clinic");
        row.GetProperty("noted").GetString().Should().Be("Latest feed note");
        row.GetProperty("lawfirm").GetString().Should().Be("Smith & Associates LLP");
        row.GetProperty("lastActivity").GetString().Should().Be("Awaiting response");
        row.GetProperty("lastCaseNote").GetString().Should().Be("Latest case note");
        row.GetProperty("reduction").GetString().Should().Be("700.00");

        var expectedFields = new[]
        {
            "plaintiffFirstName", "plaintiffLastName", "plaintiffDob", "plaintiffPhone",
            "plaintiffAddress", "plaintiffCity", "plaintiffState", "plaintiffZip", "lienId",
            "caseId", "purchaseDate", "daysSincePurchase", "purchaseAmt", "billingAmt",
            "expectedSettlementAmt", "reductionPercentage", "capitalProviders", "dateClosed",
            "returnedAmt", "grossProfit", "roi", "annualizedRoi", "medicalCodeCount",
            "medicalCodes", "initialServiceDate", "endServiceDate", "medicalProviders",
            "medicalFacilityContact", "medicalFacility", "medicalFacilityAddress",
            "medicalFacilityCity", "medicalFacilityState", "medicalFacilityZip", "noted",
            "lawfirm", "lawfirmAddress", "lawfirmCity", "lawfirmState", "lawfirmZip",
            "lawfirmPhone", "caseType", "stateOfIncident", "caseTrackingContact",
            "caseTrackingContactEmail", "caseManager", "amtToSettlement", "caseStatus",
            "medicalStatus", "caseTrackingFollowUpDate", "lastActivityDate", "lastActivity",
            "caseEnteredBy", "leadSource", "dateOfLoss", "lastCaseNote", "lastCaseNoteDate",
            "reduction",
        };
        row.EnumerateObject().Select(property => property.Name).Should().Equal(expectedFields);
    }

    [Fact]
    public async Task GetWeeklyBcc_requires_as_of_date()
    {
        var response = await _client.PostAsJsonAsync("/report/weekly-bcc", new { });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetWeeklyBcc_requires_request_body()
    {
        var response = await _client.SendAsync(new HttpRequestMessage(
            HttpMethod.Post,
            "/report/weekly-bcc"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("message").GetString().Should().Be("Request body is required.");
    }

    [Fact]
    public async Task GetWeeklyBcc_rejects_malformed_as_of_date()
    {
        var response = await _client.PostAsJsonAsync("/report/weekly-bcc", new
        {
            asOfDate = "2026/08/13",
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("message").GetString()
            .Should().Be("A valid asOfDate is required. Use MM/dd/yyyy or yyyy-MM-dd.");
    }

    [Fact]
    public async Task GetWeeklyBcc_requires_authentication()
    {
        var anonymousClient = _factory.CreateClient();
        var response = await anonymousClient.PostAsJsonAsync("/report/weekly-bcc", new
        {
            asOfDate = "2026-08-13",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
