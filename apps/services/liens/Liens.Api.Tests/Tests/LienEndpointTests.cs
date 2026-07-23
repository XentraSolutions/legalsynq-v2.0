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
        var caseId = Guid.CreateVersion7();
        var lienId = Guid.CreateVersion7();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-410001",
                "Billing",
                "Case",
                SeedHelper.UserId);
            typeof(Case).GetProperty(nameof(Case.Id))!.SetValue(caseEntity, caseId);
            db.Cases.Add(caseEntity);

            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-410001-01",
                LienType.MedicalLien,
                150m,
                SeedHelper.UserId,
                caseId: caseId,
                facilityId: SeedHelper.FacilityId,
                subjectFirstName: "Billing",
                subjectLastName: "Case",
                incidentDate: new DateOnly(2024, 6, 15));
            typeof(Lien).GetProperty(nameof(Lien.Id))!.SetValue(lien, lienId);
            db.Liens.Add(lien);

            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "SVC-LIEN-001",
                "LegacyMedicalCode",
                "Medical code for lien list",
                "system",
                SeedHelper.UserId,
                caseId: caseId,
                lienId: lienId,
                notes: "code=12345; medicareCost=75.00; billingAmount=150.00; purchaseAmount=100.00; payee=Health System; outboundCheckNumber=CHK-100"));
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/liens/liens?caseId={caseId}&page=1&pageSize=20");

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
    public async Task ListLiens_serializes_datetime_fields_in_pacific_time()
    {
        var response = await _client.GetAsync("/api/liens/liens?page=1&pageSize=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var createdAtUtc = doc.RootElement
            .GetProperty("items")[0]
            .GetProperty("createdAtUtc")
            .GetString();

        createdAtUtc.Should().NotBeNullOrWhiteSpace();
        (createdAtUtc!.EndsWith("-07:00", StringComparison.Ordinal) ||
         createdAtUtc.EndsWith("-08:00", StringComparison.Ordinal))
            .Should().BeTrue($"expected Pacific offset in serialized timestamp but got '{createdAtUtc}'");
    }

    [Fact]
    public async Task ListLiens_includes_plaintiff_law_firm_medical_facility_and_case_manager()
    {
        var caseManagerId = Guid.CreateVersion7();
        var lienNumber = "LIEN-LIST-CONTEXT-001";
        var lienId = Guid.CreateVersion7();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var caseManager = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                ContactType.CaseManager,
                "Jamie",
                "Manager",
                SeedHelper.UserId,
                lawFirmId: SeedHelper.LawFirmId,
                contactSubtype: ContactSubtype.LawFirmCaseManager);
            typeof(Contact).GetProperty(nameof(Contact.Id))!.SetValue(caseManager, caseManagerId);
            db.Contacts.Add(caseManager);

            var caseEntity = db.Cases.Single(c => c.Id == SeedHelper.CaseId);
            caseEntity.Update(
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
                notes: "[legacy-meta]\nlawFirmId=40000000-0000-0000-0000-000000000010;caseManagerId=" + caseManagerId);

            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                lienNumber,
                LienType.MedicalLien,
                150m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                facilityId: SeedHelper.FacilityId,
                subjectFirstName: "Context",
                subjectLastName: "Lien");
            typeof(Lien).GetProperty(nameof(Lien.Id))!.SetValue(lien, lienId);
            db.Liens.Add(lien);

            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LMFI-LIST-001",
                "LegacyMedicalFacilityInfo",
                "Legacy medical facility information",
                "system",
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                lienId: lienId,
                notes: $"facilityId={SeedHelper.FacilityId};facilityName=Sunrise Clinic"));

            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/liens/liens?search={lienNumber}&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<PaginatedLiensResponseBody>();
        body.Should().NotBeNull();
        var match = body!.Items.Single(item => item.LienNumber == lienNumber);
        match.Plaintiff.Should().Be("John Plaintiff");
        match.LawFirm.Should().Be("Smith & Associates LLP");
        match.MedicalFacility.Should().Be("Sunrise Clinic");
        match.CaseManager.Should().Be("Jamie Manager");
    }

    [Theory]
    [InlineData("Open")]
    [InlineData("Closed")]
    public async Task ListLiens_returns_business_status_label(string requestedStatusLabel)
    {
        var lienNumber = $"LIEN-STATUS-{requestedStatusLabel}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                lienNumber,
                LienType.MedicalLien,
                125m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId);

            lien.SetLegacyMedicalStatus(requestedStatusLabel, SeedHelper.UserId);

            db.Liens.Add(lien);
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/liens/liens?search={lienNumber}&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<PaginatedLiensResponseBody>();
        body.Should().NotBeNull();
        var match = body!.Items.Single(item => item.LienNumber == lienNumber);
        match.Status.Should().Be(requestedStatusLabel);
        match.StatusLabel.Should().Be(requestedStatusLabel);
    }

    [Fact]
    public async Task ListLiens_excludes_rejected_and_cancelled_before_pagination()
    {
        var prefix = $"LIEN-LIST-HIDE-{Guid.NewGuid():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var openLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"{prefix}-OPEN",
                LienType.MedicalLien,
                125m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId);

            var rejectedLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"{prefix}-REJECTED",
                LienType.MedicalLien,
                125m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId);
            rejectedLien.SetLegacyMedicalStatus("Rejected", SeedHelper.UserId);

            var cancelledLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"{prefix}-CANCELLED",
                LienType.MedicalLien,
                125m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId);
            cancelledLien.SetLegacyMedicalStatus("Cancelled", SeedHelper.UserId);

            db.Liens.AddRange(openLien, rejectedLien, cancelledLien);
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync(
            $"/api/liens/liens?search={prefix}&page=1&pageSize=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<PaginatedLiensResponseBody>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().Be(1);
        body.Items.Should().ContainSingle();
        body.Items[0].LienNumber.Should().Be($"{prefix}-OPEN");
        body.Items[0].Status.Should().NotBe("Rejected").And.NotBe("Cancelled");
    }

    [Theory]
    [InlineData("Open")]
    [InlineData("Closed")]
    public async Task SearchLiensV3_returns_business_status_label(string requestedStatusLabel)
    {
        var lienNumber = $"CASE-LIEN-STATUS-{requestedStatusLabel}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                lienNumber,
                LienType.MedicalLien,
                125m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId);

            lien.SetLegacyMedicalStatus(requestedStatusLabel, SeedHelper.UserId);

            db.Liens.Add(lien);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/liens/cases/liens/v3", new
        {
            caseId = SeedHelper.CaseId,
            page = 1,
            limit = 50,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<PaginatedLiensResponseBody>();
        body.Should().NotBeNull();
        var match = body!.Items.Single(item => item.LienNumber == lienNumber);
        match.Status.Should().Be(requestedStatusLabel);
        match.StatusLabel.Should().Be(requestedStatusLabel);
    }

    [Fact]
    public async Task ListLiens_by_caseId_excludes_rejected_liens_from_response()
    {
        var lienNumber = "CASE-LIEN-HIDE-REJECTED";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                lienNumber,
                LienType.MedicalLien,
                125m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId);

            lien.SetLegacyMedicalStatus("Rejected", SeedHelper.UserId);

            db.Liens.Add(lien);
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/liens/liens?caseId={SeedHelper.CaseId}&page=1&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<PaginatedLiensResponseBody>();
        body.Should().NotBeNull();
        body!.Items.Should().NotContain(item => item.LienNumber == lienNumber);
        body.TotalCount.Should().Be(body.Items.Count);
    }

    [Fact]
    public async Task ListLiens_supports_advanced_get_filters()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var caseEntity = db.Cases.Single(c => c.Id == SeedHelper.CaseId);
            caseEntity.Update(
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
                notes: $"[legacy-meta]{Environment.NewLine}lawFirmId={SeedHelper.LawFirmId}");

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
                incidentDate: new DateOnly(2026, 7, 16),
                initialServiceDate: lien.InitialServiceDate,
                endServiceDate: lien.EndServiceDate,
                isBulk: lien.IsBulk,
                isServicing: lien.IsServicing,
                description: lien.Description,
                notes: lien.Notes);

            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync(
            $"/api/liens/liens?page=1&pageSize=10" +
            $"&lawFirmIds={SeedHelper.LawFirmId}" +
            $"&purchaseDateFrom=2026-07-16" +
            $"&purchaseDateTo=2026-07-16");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<PaginatedLiensResponseBody>();
        body.Should().NotBeNull();
        body!.Items.Should().ContainSingle();
        body.TotalCount.Should().Be(1);
        body.Items.Single().LienNumber.Should().Be("LIEN-TEST-001");
    }

    [Fact]
    public async Task SearchLiens_post_supports_advanced_filters()
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
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/liens/liens/search", new
        {
            page = 1,
            pageSize = 10,
            lienStatusIds = Array.Empty<string>(),
            lawFirmIds = Array.Empty<string>(),
            medicalFacilityIds = Array.Empty<string>(),
            caseManagerIds = Array.Empty<string>(),
            purchaseDateFrom = "2024-06-15",
            purchaseDateTo = "2024-06-15",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<PaginatedLiensResponseBody>();
        body.Should().NotBeNull();
        body!.Items.Should().NotBeEmpty();
        body.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ListLiens_supports_sorting_for_enriched_and_amount_fields()
    {
        var secondCaseId = Guid.CreateVersion7();
        var secondLienId = Guid.CreateVersion7();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var secondCase = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-499999",
                "Aaron",
                "Alpha",
                SeedHelper.UserId);
            typeof(Case).GetProperty(nameof(Case.Id))!.SetValue(secondCase, secondCaseId);
            db.Cases.Add(secondCase);

            var secondLien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-499999-01",
                LienType.MedicalLien,
                90m,
                SeedHelper.UserId,
                caseId: secondCaseId,
                facilityId: SeedHelper.FacilityId,
                subjectFirstName: "Aaron",
                subjectLastName: "Alpha",
                incidentDate: new DateOnly(2024, 6, 10),
                initialServiceDate: new DateOnly(2024, 4, 1));
            typeof(Lien).GetProperty(nameof(Lien.Id))!.SetValue(secondLien, secondLienId);
            db.Liens.Add(secondLien);

            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "SVC-LIEN-SORT-001",
                "LegacyMedicalCode",
                "Medical code for sorting",
                "system",
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                lienId: SeedHelper.LienId,
                notes: "code=12345; medicareCost=75.00; billingAmount=150.00; purchaseAmount=100.00; payee=Health System; outboundCheckNumber=CHK-100"));

            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "SVC-LIEN-SORT-002",
                "LegacyMedicalCode",
                "Medical code for sorting",
                "system",
                SeedHelper.UserId,
                caseId: secondCaseId,
                lienId: secondLienId,
                notes: "code=98765; medicareCost=45.00; billingAmount=90.00; purchaseAmount=60.00; payee=Alpha Health; outboundCheckNumber=CHK-200"));

            await db.SaveChangesAsync();
        }

        var plaintiffAscResponse = await _client.GetAsync(
            "/api/liens/liens?page=1&pageSize=10&sortBy=plaintiffName&sortDirection=asc");

        plaintiffAscResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await plaintiffAscResponse.Content.ReadAsStringAsync()}");

        var plaintiffAscBody = await plaintiffAscResponse.Content.ReadFromJsonAsync<PaginatedLiensResponseBody>();
        plaintiffAscBody.Should().NotBeNull();
        plaintiffAscBody!.Items.Should().HaveCountGreaterOrEqualTo(2);
        plaintiffAscBody.Items.Take(2).Select(item => item.Plaintiff)
            .Should().Equal("Aaron Alpha", "John Plaintiff");

        var billingDescResponse = await _client.GetAsync(
            "/api/liens/liens?page=1&pageSize=10&sortBy=billingAmount&sortDirection=desc");

        billingDescResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await billingDescResponse.Content.ReadAsStringAsync()}");

        var billingDescBody = await billingDescResponse.Content.ReadFromJsonAsync<PaginatedLiensResponseBody>();
        billingDescBody.Should().NotBeNull();
        billingDescBody!.Items.Should().HaveCountGreaterOrEqualTo(2);
        billingDescBody.Items.Take(2).Select(item => item.TotalBilling)
            .Should().Equal(150m, 90m);
    }

    [Fact]
    public async Task ListLiens_purchase_date_range_is_inclusive_for_from_and_to()
    {
        var july17CaseId = Guid.CreateVersion7();
        var july18CaseId = Guid.CreateVersion7();
        var july23CaseId = Guid.CreateVersion7();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var july17Case = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-700017",
                "Range",
                "Seventeen",
                SeedHelper.UserId);
            typeof(Case).GetProperty(nameof(Case.Id))!.SetValue(july17Case, july17CaseId);
            db.Cases.Add(july17Case);

            var july18Case = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-700018",
                "Range",
                "Eighteen",
                SeedHelper.UserId);
            typeof(Case).GetProperty(nameof(Case.Id))!.SetValue(july18Case, july18CaseId);
            db.Cases.Add(july18Case);

            var july23Case = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-700023",
                "Range",
                "TwentyThree",
                SeedHelper.UserId);
            typeof(Case).GetProperty(nameof(Case.Id))!.SetValue(july23Case, july23CaseId);
            db.Cases.Add(july23Case);

            db.Liens.Add(Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-700017-01",
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                caseId: july17CaseId,
                incidentDate: new DateOnly(2026, 7, 17)));

            db.Liens.Add(Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-700018-01",
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                caseId: july18CaseId,
                incidentDate: new DateOnly(2026, 7, 18)));

            db.Liens.Add(Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-700023-01",
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                caseId: july23CaseId,
                incidentDate: new DateOnly(2026, 7, 23)));

            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync(
            "/api/liens/liens?page=1&pageSize=20&purchaseDateFrom=2026-07-17&purchaseDateTo=2026-07-18&sortBy=lienNumber&sortDirection=asc");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<PaginatedLiensResponseBody>();
        body.Should().NotBeNull();
        body!.Items.Select(item => item.LienNumber)
            .Should().Contain(["26-700017-01", "26-700018-01"]);
        body.Items.Select(item => item.LienNumber)
            .Should().NotContain("26-700023-01");
    }

    [Fact]
    public async Task ListLiens_purchase_date_filters_support_from_only_and_to_only()
    {
        var july17CaseId = Guid.CreateVersion7();
        var july18CaseId = Guid.CreateVersion7();
        var july23CaseId = Guid.CreateVersion7();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var july17Case = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-710017",
                "Only",
                "Seventeen",
                SeedHelper.UserId);
            typeof(Case).GetProperty(nameof(Case.Id))!.SetValue(july17Case, july17CaseId);
            db.Cases.Add(july17Case);

            var july18Case = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-710018",
                "Only",
                "Eighteen",
                SeedHelper.UserId);
            typeof(Case).GetProperty(nameof(Case.Id))!.SetValue(july18Case, july18CaseId);
            db.Cases.Add(july18Case);

            var july23Case = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-710023",
                "Only",
                "TwentyThree",
                SeedHelper.UserId);
            typeof(Case).GetProperty(nameof(Case.Id))!.SetValue(july23Case, july23CaseId);
            db.Cases.Add(july23Case);

            db.Liens.Add(Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-710017-01",
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                caseId: july17CaseId,
                incidentDate: new DateOnly(2026, 7, 17)));

            db.Liens.Add(Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-710018-01",
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                caseId: july18CaseId,
                incidentDate: new DateOnly(2026, 7, 18)));

            db.Liens.Add(Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-710023-01",
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                caseId: july23CaseId,
                incidentDate: new DateOnly(2026, 7, 23)));

            await db.SaveChangesAsync();
        }

        var fromOnlyResponse = await _client.GetAsync(
            "/api/liens/liens?page=1&pageSize=20&purchaseDateFrom=2026-07-18&sortBy=lienNumber&sortDirection=asc");

        fromOnlyResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await fromOnlyResponse.Content.ReadAsStringAsync()}");

        var fromOnlyBody = await fromOnlyResponse.Content.ReadFromJsonAsync<PaginatedLiensResponseBody>();
        fromOnlyBody.Should().NotBeNull();
        fromOnlyBody!.Items.Select(item => item.LienNumber)
            .Should().Contain(["26-710018-01", "26-710023-01"]);
        fromOnlyBody.Items.Select(item => item.LienNumber)
            .Should().NotContain("26-710017-01");

        var toOnlyResponse = await _client.GetAsync(
            "/api/liens/liens?page=1&pageSize=20&purchaseDateTo=2026-07-17&sortBy=lienNumber&sortDirection=asc");

        toOnlyResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await toOnlyResponse.Content.ReadAsStringAsync()}");

        var toOnlyBody = await toOnlyResponse.Content.ReadFromJsonAsync<PaginatedLiensResponseBody>();
        toOnlyBody.Should().NotBeNull();
        toOnlyBody!.Items.Select(item => item.LienNumber)
            .Should().Contain("26-710017-01");
        toOnlyBody.Items.Select(item => item.LienNumber)
            .Should().NotContain(["26-710018-01", "26-710023-01"]);
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
        public int TotalCount { get; init; }
    }

    private sealed class LienListItemResponseBody
    {
        public string LienNumber { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string StatusLabel { get; init; } = string.Empty;
        public string PurchaseDate { get; init; } = string.Empty;
        public decimal? TotalPurchase { get; init; }
        public decimal? TotalBilling { get; init; }
        public string? Plaintiff { get; init; }
        public string? LawFirm { get; init; }
        public string? MedicalFacility { get; init; }
        public string? CaseManager { get; init; }
    }
}
