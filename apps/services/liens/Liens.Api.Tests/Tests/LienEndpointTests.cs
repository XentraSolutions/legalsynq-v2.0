using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class LienEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public LienEndpointTests(LiensApiFactory factory) => _factory = factory;

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

    [Fact]
    public async Task CreateLien_defaults_lien_number_from_case_number_and_next_sequence()
    {
        var caseId = Guid.CreateVersion7();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-000001",
                "Sequence",
                "Patient",
                SeedHelper.UserId));

            var caseEntity = db.Cases.Local.Single(c => c.CaseNumber == "26-000001");
            typeof(Case).GetProperty(nameof(Case.Id))!.SetValue(caseEntity, caseId);
            await db.SaveChangesAsync();
        }

        var first = await _client.PostAsJsonAsync("/api/liens/liens", new
        {
            lienNumber = "",
            lienType = LienType.MedicalLien,
            caseId,
            originalAmount = 100m,
        });

        first.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await first.Content.ReadAsStringAsync()}");
        var firstBody = await first.Content.ReadFromJsonAsync<LienResponseBody>();
        firstBody!.LienNumber.Should().Be("26-000001-01");

        var second = await _client.PostAsJsonAsync("/api/liens/liens", new
        {
            lienNumber = "",
            lienType = LienType.MedicalLien,
            caseId,
            originalAmount = 200m,
        });

        second.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await second.Content.ReadAsStringAsync()}");
        var secondBody = await second.Content.ReadFromJsonAsync<LienResponseBody>();
        secondBody!.LienNumber.Should().Be("26-000001-02");
    }

    [Fact]
    public async Task ListLiens_by_caseId_includes_purchaseDate_totalPurchase_and_totalBilling()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var lien = db.Liens.Single(l => l.Id == SeedHelper.LienId);
            lien.Update(
                lien.LienType,
                lien.OriginalAmount,
                SeedHelper.UserId,
                externalReference: lien.ExternalReference,
                subjectFirstName: lien.SubjectFirstName,
                subjectLastName: lien.SubjectLastName,
                isConfidential: lien.IsConfidential,
                jurisdiction: lien.Jurisdiction,
                incidentDate: new DateOnly(2024, 6, 15),
                initialServiceDate: lien.InitialServiceDate,
                endServiceDate: lien.EndServiceDate,
                isBulk: lien.IsBulk,
                isServicing: lien.IsServicing,
                description: lien.Description,
                notes: lien.Notes);
            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "SVC-LIEN-001",
                "LegacyMedicalCode",
                "Medical code for lien list",
                "system",
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                lienId: SeedHelper.LienId,
                notes: "code=12345; medicareCost=75.00; billingAmount=150.00; purchaseAmount=100.00; payee=Health System; outboundCheckNumber=CHK-100"));
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/liens/liens?caseId={SeedHelper.CaseId}&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<PaginatedLiensResponseBody>();
        body.Should().NotBeNull();
        body!.Items.Should().ContainSingle();
        body.Items[0].PurchaseDate.Should().Be("06/15/2024");
        body.Items[0].TotalPurchase.Should().Be(100m);
        body.Items[0].TotalBilling.Should().Be(150m);
    }

    [Fact]
    public async Task CreateLien_with_standalone_facility_contact_id_links_backing_facility()
    {
        var response = await _client.PostAsJsonAsync("/api/liens/liens", new
        {
            lienNumber = "LIEN-TEST-FACILITY-CONTACT",
            lienType = LienType.MedicalLien,
            caseId = SeedHelper.CaseId,
            facilityId = SeedHelper.MedicalFacilityContactId,
            originalAmount = 250m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

        var createdLien = db.Liens.Single(l => l.LienNumber == "LIEN-TEST-FACILITY-CONTACT");
        createdLien.FacilityId.Should().NotBeNull();
        createdLien.FacilityId.Should().NotBe(SeedHelper.MedicalFacilityContactId);

        var facilityContact = db.Contacts.Single(c => c.Id == SeedHelper.MedicalFacilityContactId);
        facilityContact.FacilityId.Should().Be(createdLien.FacilityId);

        db.Facilities.Single(f => f.Id == createdLien.FacilityId!.Value).Name.Should().Be("Sunrise Clinic");
    }

    [Fact]
    public async Task ReassignFacility_updates_legacy_facility_name_metadata()
    {
        Guid newFacilityContactId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var newFacilityContact = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                ContactType.MedicalFacility,
                "Valley",
                "Clinic",
                SeedHelper.UserId,
                organization: "Valley Clinic");
            newFacilityContactId = newFacilityContact.Id;
            db.Contacts.Add(newFacilityContact);

            var lien = db.Liens.Single(l => l.Id == SeedHelper.LienId);
            lien.AttachFacility(SeedHelper.FacilityId, SeedHelper.UserId);

            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LMFI-TEST-001",
                "LegacyMedicalFacilityInfo",
                "Legacy medical facility information",
                "system",
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                lienId: SeedHelper.LienId,
                notes: $"facilityId={SeedHelper.FacilityId}; facilityName=Sunrise Clinic"));

            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/liens/liens/reassign/facility", new
        {
            facility = newFacilityContactId,
            liensId = SeedHelper.LienId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>();

        var updatedLien = verifyDb.Liens.Single(l => l.Id == SeedHelper.LienId);
        var updatedFacilityContact = verifyDb.Contacts.Single(c => c.Id == newFacilityContactId);
        var facilityInfo = verifyDb.ServicingItems.Single(i =>
            i.LienId == SeedHelper.LienId &&
            i.TaskType == "LegacyMedicalFacilityInfo");

        updatedLien.FacilityId.Should().Be(updatedFacilityContact.FacilityId);
        updatedFacilityContact.FacilityId.Should().NotBeNull();
        facilityInfo.Notes.Should().Contain($"facilityId={newFacilityContactId}");
        facilityInfo.Notes.Should().Contain("facilityName=Valley Clinic");
    }

    private sealed class LienResponseBody
    {
        public string LienNumber { get; init; } = string.Empty;
    }

    private sealed class PaginatedLiensResponseBody
    {
        public List<LienListItemResponseBody> Items { get; init; } = [];
    }

    private sealed class LienListItemResponseBody
    {
        public string PurchaseDate { get; init; } = string.Empty;
        public decimal TotalPurchase { get; init; }
        public decimal TotalBilling { get; init; }
    }
}
