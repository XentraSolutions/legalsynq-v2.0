using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Liens.Api;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Api.Tests.Helpers;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class LegacyCaseEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public LegacyCaseEndpointTests(LiensApiFactory factory) => _factory = factory;

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
    public async Task GetCasesV3_accepts_comma_separated_status_codes()
    {
        var resp = await _client.PostAsJsonAsync("/api/liens/cases/v3", new
        {
            keyword = "",
            page = 1,
            limit = 20,
            sortBy = "",
            sortDirection = "",
            statusId = "PreDemand,DemandSent",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task GetCasesV3_filters_by_law_firm_accident_type_and_case_manager()
    {
        var accidentTypeId = $"ACC-{Guid.CreateVersion7():N}";
        var caseManagerId = Guid.CreateVersion7();
        var otherOrgId = Guid.CreateVersion7();
        var otherManagerId = Guid.CreateVersion7();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-FILTER-MATCH-{Guid.CreateVersion7():N}",
                "Filter",
                "Match",
                SeedHelper.UserId,
                notes: $"accidentTypeId={accidentTypeId}; caseManagerId={caseManagerId}"));

            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                otherOrgId,
                $"CASE-FILTER-OTHER-ORG-{Guid.CreateVersion7():N}",
                "Filter",
                "OtherOrg",
                SeedHelper.UserId,
                notes: $"accidentTypeId={accidentTypeId}; caseManagerId={caseManagerId}"));

            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-FILTER-OTHER-META-{Guid.CreateVersion7():N}",
                "Filter",
                "OtherMeta",
                SeedHelper.UserId,
                notes: $"accidentTypeId={accidentTypeId}-OTHER; caseManagerId={otherManagerId}"));

            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/v3", new
        {
            keyword = "",
            page = 1,
            limit = 20,
            sortBy = "",
            sortDirection = "",
            statusId = "PreDemand,DemandSent",
            lawFirmId = SeedHelper.OrgId.ToString(),
            accidentTypeId,
            caseManagerId = caseManagerId.ToString(),
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetCasesV3_filters_contact_ids_and_legacy_status_aliases_with_multi_select_values()
    {
        var matchCaseNumber = $"CASE-V3-CONTACT-{Guid.CreateVersion7():N}";
        var otherCaseNumber = $"CASE-V3-CONTACT-OTHER-{Guid.CreateVersion7():N}";
        var falsePositiveCaseNumber = $"CASE-V3-CONTACT-FALSE-POSITIVE-{Guid.CreateVersion7():N}";
        var crossTenantCaseNumber = $"CASE-V3-CONTACT-OTHER-TENANT-{Guid.CreateVersion7():N}";
        var lawFirmId = SeedHelper.LawFirmId;
        var otherLawFirmId = Guid.CreateVersion7();
        var caseManagerId = Guid.CreateVersion7();
        var accidentTypeId = Guid.CreateVersion7().ToString();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                Guid.CreateVersion7(),
                matchCaseNumber,
                "Contact",
                "Match",
                SeedHelper.UserId,
                notes: $"lawFirmId={lawFirmId}; accidentTypeId={accidentTypeId}; caseManagerId={caseManagerId}"));
            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                Guid.CreateVersion7(),
                otherCaseNumber,
                "Contact",
                "Other",
                SeedHelper.UserId,
                notes: $"lawFirmId={otherLawFirmId}; accidentTypeId=other; caseManagerId={Guid.CreateVersion7()}"));
            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                Guid.CreateVersion7(),
                falsePositiveCaseNumber,
                "Contact",
                "FalsePositive",
                SeedHelper.UserId,
                notes: $"notlawFirmId={lawFirmId}; accidentTypeId={accidentTypeId}; caseManagerId={caseManagerId}"));
            db.Cases.Add(Case.Create(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                crossTenantCaseNumber,
                "Contact",
                "Tenant",
                SeedHelper.UserId,
                notes: $"lawFirmId={lawFirmId}; accidentTypeId={accidentTypeId}; caseManagerId={caseManagerId}"));
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/liens/cases/v3", new
        {
            keyword = "Contact",
            page = 1,
            limit = 50,
            statusId = "Pre-Demand",
            lawFirmId = $"{lawFirmId},{Guid.CreateVersion7()}",
            accidentTypeId = $"{accidentTypeId},{Guid.CreateVersion7()}",
            caseManagerId = $"{caseManagerId},{Guid.CreateVersion7()}",
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var items = payload!.RootElement.GetProperty("data").EnumerateArray().ToList();
        items.Should().ContainSingle(item => item.GetProperty("caseNumber").GetString() == matchCaseNumber);
        items.Should().NotContain(item => item.GetProperty("caseNumber").GetString() == otherCaseNumber);
        items.Should().NotContain(item => item.GetProperty("caseNumber").GetString() == falsePositiveCaseNumber);
        items.Should().NotContain(item => item.GetProperty("caseNumber").GetString() == crossTenantCaseNumber);
    }

    [Fact]
    public async Task GetCasesV3_maps_negotiations_to_in_negotiation()
    {
        var caseNumber = $"CASE-V3-NEGOTIATIONS-{Guid.CreateVersion7():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId, SeedHelper.OrgId, caseNumber, "Negotiation", "Match", SeedHelper.UserId);
            caseEntity.TransitionStatus(CaseStatus.DemandSent, SeedHelper.UserId);
            caseEntity.TransitionStatus(CaseStatus.InNegotiation, SeedHelper.UserId);
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/liens/cases/v3", new
        {
            keyword = caseNumber,
            page = 1,
            limit = 20,
            statusId = "Negotiations",
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        payload!.RootElement.GetProperty("data").EnumerateArray()
            .Should().Contain(item => item.GetProperty("caseNumber").GetString() == caseNumber);
    }

    [Fact]
    public async Task GetCasesV3_returns_only_cases_matching_each_selected_legacy_status()
    {
        var prefix = $"CASE-STATUS-{Guid.CreateVersion7():N}"[..24];
        var expectedCaseNumbers = new Dictionary<string, string>
        {
            ["New"] = $"{prefix}-NEW",
            ["Processing"] = $"{prefix}-PROCESSING",
            ["Closed"] = $"{prefix}-CLOSED",
            ["Pre-Demand"] = $"{prefix}-PRE-DEMAND",
            ["Demand Sent"] = $"{prefix}-DEMAND-SENT",
            ["Negotiations"] = $"{prefix}-NEGOTIATIONS",
            ["Litigation"] = $"{prefix}-LITIGATION",
            ["Case Settled"] = $"{prefix}-CASE-SETTLED",
        };

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            AddCase("New", CaseStatus.PreDemand, "New");
            AddCase("Processing", CaseStatus.PreDemand, "Processing");
            AddCase("Closed", CaseStatus.Closed);
            AddCase("Pre-Demand", CaseStatus.PreDemand);
            AddCase("Demand Sent", CaseStatus.DemandSent);
            AddCase("Negotiations", CaseStatus.InNegotiation);
            AddCase("Litigation", CaseStatus.InNegotiation, "Litigation");
            AddCase("Case Settled", CaseStatus.CaseSettled);

            await db.SaveChangesAsync();

            void AddCase(string displayStatus, string canonicalStatus, string? statusLabel = null)
            {
                var caseEntity = Case.Create(
                    SeedHelper.TenantId,
                    SeedHelper.OrgId,
                    expectedCaseNumbers[displayStatus],
                    "StatusMatrix",
                    displayStatus.Replace(" ", string.Empty, StringComparison.Ordinal),
                    SeedHelper.UserId,
                    notes: statusLabel is null
                        ? null
                        : $"[legacy-meta]{Environment.NewLine}statusLabel={statusLabel}");

                if (canonicalStatus != CaseStatus.PreDemand)
                    caseEntity.TransitionStatus(canonicalStatus, SeedHelper.UserId);

                db.Cases.Add(caseEntity);
            }
        }

        foreach (var (selectedStatus, expectedCaseNumber) in expectedCaseNumbers)
        {
            var response = await _client.PostAsJsonAsync("/api/liens/cases/v3", new
            {
                keyword = prefix,
                page = 1,
                limit = 20,
                statusId = selectedStatus,
            });
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"Status: {selectedStatus}; Body: {await response.Content.ReadAsStringAsync()}");

            var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
            var items = payload!.RootElement.GetProperty("data").EnumerateArray().ToList();
            items.Should().ContainSingle($"only the {selectedStatus} case should match");
            items.Single().GetProperty("caseNumber").GetString().Should().Be(expectedCaseNumber);
        }
    }

    [Fact]
    public async Task GetCasesV3_returns_law_firm_case_manager_and_accident_type_display_values()
    {
        var caseManagerId = Guid.CreateVersion7();
        var caseNumber = $"CASE-V3-DISPLAY-{Guid.CreateVersion7():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var caseManager = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                ContactType.CaseManager,
                "John",
                "Doe",
                SeedHelper.UserId);
            typeof(Contact).GetProperty(nameof(Contact.Id))!.SetValue(caseManager, caseManagerId);

            db.Contacts.Add(caseManager);
            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                caseNumber,
                "Display",
                "Match",
                SeedHelper.UserId,
                notes: $"lawFirmId={SeedHelper.LawFirmId}; accidentTypeId=MVA; accidentType=Motor Vehicle Accident; caseManagerId={caseManagerId}"));

            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/v3", new
        {
            keyword = caseNumber,
            page = 1,
            limit = 20,
            sortBy = "",
            sortDirection = "",
            statusId = "PreDemand,DemandSent",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        var item = doc!.RootElement.GetProperty("data").EnumerateArray().Single();

        item.GetProperty("lawFirmId").GetString().Should().Be(SeedHelper.LawFirmId.ToString());
        item.GetProperty("lawFirm").GetString().Should().Be("Smith & Associates LLP");
        item.GetProperty("caseManagerId").GetString().Should().Be(caseManagerId.ToString());
        item.GetProperty("caseManager").GetString().Should().Be("John Doe");
        item.GetProperty("accidentTypeId").GetString().Should().Be("MVA");
        item.GetProperty("accidentType").GetString().Should().Be("Motor Vehicle Accident");
    }

    [Fact]
    public async Task GenerateCaseCsv_exports_migrated_accident_type_and_filters_by_its_canonical_id()
    {
        var accidentTypeId = $"ACC-{Guid.CreateVersion7():N}";
        var caseManagerId = Guid.CreateVersion7();
        var caseNumber = $"CASE-CSV-{Guid.CreateVersion7():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseManager = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                ContactType.CaseManager,
                "Casey",
                "Manager",
                SeedHelper.UserId);
            typeof(Contact).GetProperty(nameof(Contact.Id))!.SetValue(caseManager, caseManagerId);
            db.Contacts.Add(caseManager);

            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                caseNumber,
                "CSV",
                "Export",
                SeedHelper.UserId,
                clientDob: new DateOnly(1990, 1, 2),
                clientPhone: "702-555-0100",
                clientEmail: "csv.export@example.com",
                clientAddress: "123 Main St, Las Vegas, NV, 89101",
                dateOfIncident: new DateOnly(2026, 7, 31),
                description: "Export summary",
                notes: $"""
                    Export note

                    [legacy-meta]
                    accidentTypeId={accidentTypeId}; accidentType=Motor Vehicle Accident; isServicing=Yes; isUccFiled=Yes; isBulk=No; accidentState=NV; lawFirmId={SeedHelper.LawFirmId}; caseManagerId={caseManagerId}; currentMedicalStatus=Treating; currentAttributes=Active; gender=Female; ssn=***-**-6789; toGeneratePdf=Yes; switchedDate=08/01/2026
                    """));
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/liens/cases/generate-csv", new
        {
            caseId = caseNumber,
            accidentTypeId,
            legacyFormat = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var encodedCsv = payload!.RootElement.GetProperty("data")[0].GetProperty("base64").GetString();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCsv!));
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var headers = ParseCsvLine(lines[0]);
        var values = ParseCsvLine(lines[1]);
        var row = headers.Zip(values).ToDictionary(pair => pair.First, pair => pair.Second);

        row["Address"].Should().Be("123 Main St");
        row["City"].Should().Be("Las Vegas");
        row["State"].Should().Be("NV");
        row["ZipCode"].Should().Be("89101");
        row["IsServicing"].Should().Be("Yes");
        row["IsUccFiled"].Should().Be("Yes");
        row["IsBulk"].Should().Be("No");
        row["AccidentType"].Should().Be("Motor Vehicle Accident");
        row["AccidentState"].Should().Be("NV");
        row["LawFirm"].Should().Be("Smith & Associates LLP");
        row["CaseManager"].Should().Be("Casey Manager");
        row["Note"].Should().Be("Export note");
        row["CreateBy"].Should().Be(SeedHelper.UserId.ToString());
        row["UpdateBy"].Should().Be(SeedHelper.UserId.ToString());
        row["CurrentMedicalStatus"].Should().Be("Treating");
        row["CurrentAttributes"].Should().Be("Active");
        row["Email"].Should().Be("csv.export@example.com");
        row["Phone"].Should().Be("702-555-0100");
        row["Gender"].Should().Be("Female");
        row["SSN"].Should().Be("***-**-6789");
        row["Summary"].Should().Be("Export summary");
        row["ToGeneratePdf"].Should().Be("Yes");
        row["SwitchedDate"].Should().Be("08/01/2026");
    }

    [Fact]
    public async Task GenerateCaseCsv_applies_the_same_multi_select_filters_as_case_search()
    {
        var selectedLawFirmId = Guid.CreateVersion7();
        var selectedCaseManagerId = Guid.CreateVersion7();
        var selectedAccidentTypeId = $"ACC-{Guid.CreateVersion7():N}";
        var matchingCaseNumber = $"CASE-CSV-MULTI-{Guid.CreateVersion7():N}";
        var excludedCaseNumber = $"CASE-CSV-EXCLUDED-{Guid.CreateVersion7():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                selectedLawFirmId,
                matchingCaseNumber,
                "CSV",
                "Match",
                SeedHelper.UserId,
                notes: $"accidentTypeId={selectedAccidentTypeId}; caseManagerId={selectedCaseManagerId}"));
            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                Guid.CreateVersion7(),
                excludedCaseNumber,
                "CSV",
                "Excluded",
                SeedHelper.UserId,
                notes: $"accidentTypeId={selectedAccidentTypeId}; caseManagerId={selectedCaseManagerId}"));
            await db.SaveChangesAsync();
        }

        var filters = new
        {
            keyword = "",
            caseId = (string?)null,
            lawFirmId = $"{Guid.CreateVersion7()},{selectedLawFirmId}",
            accidentTypeId = $"{Guid.CreateVersion7()},{selectedAccidentTypeId}",
            statusId = CaseStatus.PreDemand,
            caseManagerId = $"{Guid.CreateVersion7()},{selectedCaseManagerId}",
        };

        var searchResponse = await _client.PostAsJsonAsync("/api/liens/cases/v3", filters);
        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await searchResponse.Content.ReadAsStringAsync()}");
        var searchBody = await searchResponse.Content.ReadFromJsonAsync<JsonDocument>();
        searchBody!.RootElement.GetProperty("data").EnumerateArray()
            .Select(item => item.GetProperty("caseNumber").GetString())
            .Should().Equal(matchingCaseNumber);

        var exportResponse = await _client.PostAsJsonAsync("/api/liens/cases/generate-csv", filters);
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await exportResponse.Content.ReadAsStringAsync()}");

        var exportBody = await exportResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var encodedCsv = exportBody!.RootElement.GetProperty("data")[0].GetProperty("base64").GetString();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCsv!));
        csv.Should().Contain(matchingCaseNumber);
        csv.Should().NotContain(excludedCaseNumber);
    }

    [Fact]
    public async Task GenerateCaseCsv_defaults_to_screen_columns_filters_and_sort()
    {
        var accidentTypeId = $"ACC-LIST-{Guid.CreateVersion7():N}";
        var caseNumberPrefix = $"CASE-LIST-CSV-{Guid.CreateVersion7():N}";
        var firstCaseNumber = $"{caseNumberPrefix}-A";
        var secondCaseNumber = $"{caseNumberPrefix}-B";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                secondCaseNumber,
                "List Export",
                "Beta",
                SeedHelper.UserId,
                clientDob: new DateOnly(1991, 4, 2),
                dateOfIncident: new DateOnly(2026, 8, 2),
                notes: $"accidentTypeId={accidentTypeId}; accidentType=Dog Bite"));
            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                firstCaseNumber,
                "List Export",
                "Alpha",
                SeedHelper.UserId,
                clientDob: new DateOnly(1990, 3, 1),
                dateOfIncident: new DateOnly(2026, 8, 1),
                notes: $"accidentTypeId={accidentTypeId}; accidentType=Dog Bite"));
            await db.SaveChangesAsync();
        }

        var request = new
        {
            keyword = "",
            accidentTypeId,
            page = 1,
            limit = 100,
            sortBy = "caseCode",
            sortDirection = "asc",
        };

        var searchResponse = await _client.PostAsJsonAsync("/api/liens/cases/v3", request);
        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await searchResponse.Content.ReadAsStringAsync()}");
        var searchBody = await searchResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var screenCaseNumbers = searchBody!.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(item => item.GetProperty("caseNumber").GetString())
            .ToArray();
        screenCaseNumbers.Should().Equal(firstCaseNumber, secondCaseNumber);

        var exportResponse = await _client.PostAsJsonAsync("/api/liens/cases/generate-csv", request);
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await exportResponse.Content.ReadAsStringAsync()}");

        var exportBody = await exportResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var encodedCsv = exportBody!.RootElement.GetProperty("data")[0].GetProperty("base64").GetString();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCsv!));
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        ParseCsvLine(lines[0]).Should().Equal(
            "Case ID",
            "Plaintiff Name",
            "Law Firm",
            "Case Manager",
            "Accident Type",
            "Date of Loss",
            "DOB",
            "Status");

        var rows = lines.Skip(1).Select(ParseCsvLine).ToArray();
        rows.Select(row => row[0]).Should().Equal(screenCaseNumbers);
        rows[0].Should().Equal(
            firstCaseNumber,
            "List Export Alpha",
            "Smith & Associates LLP",
            "",
            "Dog Bite",
            "08/01/2026",
            "03/01/1990",
            "Pre-Demand");
    }

    [Fact]
    public async Task GenerateLiensCsv_applies_keyword_multi_select_status_and_date_filters()
    {
        var selectedLawFirmId = Guid.CreateVersion7();
        var selectedCaseManagerId = Guid.CreateVersion7();
        var matchingCase = Case.Create(
            SeedHelper.TenantId,
            selectedLawFirmId,
            $"CASE-LIEN-CSV-{Guid.CreateVersion7():N}",
            "Lien",
            "Match",
            SeedHelper.UserId,
            notes: $"caseManagerId={selectedCaseManagerId}");
        var excludedCase = Case.Create(
            SeedHelper.TenantId,
            Guid.CreateVersion7(),
            $"CASE-LIEN-CSV-EXCLUDED-{Guid.CreateVersion7():N}",
            "Lien",
            "Excluded",
            SeedHelper.UserId,
            notes: $"caseManagerId={selectedCaseManagerId}");
        var matchingLienNumber = $"CSV-LIEN-FILTER-{Guid.CreateVersion7():N}";
        var wrongLawFirmLienNumber = $"CSV-LIEN-FILTER-LAW-{Guid.CreateVersion7():N}";
        var wrongFacilityLienNumber = $"CSV-LIEN-FILTER-FAC-{Guid.CreateVersion7():N}";
        var wrongPurchaseDateLienNumber = $"CSV-LIEN-FILTER-PURCHASE-{Guid.CreateVersion7():N}";
        var wrongClosedDateLienNumber = $"CSV-LIEN-FILTER-CLOSED-{Guid.CreateVersion7():N}";
        var wrongFacilityId = Guid.CreateVersion7();
        var purchaseDate = new DateOnly(2026, 8, 20);

        var matchingLien = Lien.Create(
            SeedHelper.TenantId,
            selectedLawFirmId,
            matchingLienNumber,
            LienType.MedicalLien,
            100m,
            SeedHelper.UserId,
            caseId: matchingCase.Id,
            facilityId: SeedHelper.FacilityId,
            purchaseDate: purchaseDate);
        matchingLien.SetLegacyMedicalStatus("Closed", SeedHelper.UserId);
        var wrongLawFirmLien = Lien.Create(
            SeedHelper.TenantId,
            excludedCase.OrgId,
            wrongLawFirmLienNumber,
            LienType.MedicalLien,
            100m,
            SeedHelper.UserId,
            caseId: excludedCase.Id,
            facilityId: SeedHelper.FacilityId,
            purchaseDate: purchaseDate);
        wrongLawFirmLien.SetLegacyMedicalStatus("Closed", SeedHelper.UserId);
        var wrongFacilityLien = Lien.Create(
            SeedHelper.TenantId,
            selectedLawFirmId,
            wrongFacilityLienNumber,
            LienType.MedicalLien,
            100m,
            SeedHelper.UserId,
            caseId: matchingCase.Id,
            facilityId: wrongFacilityId,
            purchaseDate: purchaseDate);
        wrongFacilityLien.SetLegacyMedicalStatus("Closed", SeedHelper.UserId);
        var wrongPurchaseDateLien = Lien.Create(
            SeedHelper.TenantId,
            selectedLawFirmId,
            wrongPurchaseDateLienNumber,
            LienType.MedicalLien,
            100m,
            SeedHelper.UserId,
            caseId: matchingCase.Id,
            facilityId: SeedHelper.FacilityId,
            purchaseDate: purchaseDate.AddDays(-1));
        wrongPurchaseDateLien.SetLegacyMedicalStatus("Closed", SeedHelper.UserId);
        var wrongClosedDateLien = Lien.Create(
            SeedHelper.TenantId,
            selectedLawFirmId,
            wrongClosedDateLienNumber,
            LienType.MedicalLien,
            100m,
            SeedHelper.UserId,
            caseId: matchingCase.Id,
            facilityId: SeedHelper.FacilityId,
            purchaseDate: purchaseDate);
        wrongClosedDateLien.SetLegacyMedicalStatus("Closed", SeedHelper.UserId);
        typeof(Lien).GetProperty(nameof(Lien.ClosedAtUtc))!.SetValue(
            wrongClosedDateLien,
            matchingLien.ClosedAtUtc!.Value.AddDays(-1));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var wrongFacility = Facility.Create(
                SeedHelper.TenantId,
                selectedLawFirmId,
                "Wrong CSV Facility",
                SeedHelper.UserId);
            typeof(Facility).GetProperty(nameof(Facility.Id))!.SetValue(wrongFacility, wrongFacilityId);
            db.Facilities.Add(wrongFacility);
            db.Cases.AddRange(matchingCase, excludedCase);
            db.Liens.AddRange(
                matchingLien,
                wrongLawFirmLien,
                wrongFacilityLien,
                wrongPurchaseDateLien,
                wrongClosedDateLien);
            await db.SaveChangesAsync();
        }

        var closedDate = DateOnly.FromDateTime(matchingLien.ClosedAtUtc!.Value);
        var response = await _client.PostAsJsonAsync("/api/liens/cases/liens/generate-csv", new
        {
            keyword = "CSV-LIEN-FILTER",
            caseId = (string?)null,
            lawFirmId = $"{Guid.CreateVersion7()},{selectedLawFirmId}",
            medicalFacilityId = $"{Guid.CreateVersion7()},{SeedHelper.FacilityId}",
            caseManagerId = $"{Guid.CreateVersion7()},{selectedCaseManagerId}",
            lienStatusId = "Closed",
            purchaseDate = purchaseDate.ToString("yyyy-MM-dd"),
            closedDate = closedDate.ToString("yyyy-MM-dd"),
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var encodedCsv = payload!.RootElement.GetProperty("data")[0].GetProperty("base64").GetString();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCsv!));
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        ParseCsvLine(lines[0]).Should().Equal(
            "Lien ID",
            "Plaintiff Name",
            "Law Firm",
            "Medical Facility",
            "Purchase Date",
            "Purchase Amount",
            "Billing Amount",
            "Lien Status",
            "Initial Service Date",
            "Case Manager");
        csv.Should().Contain(matchingLienNumber);
        csv.Should().NotContain(wrongLawFirmLienNumber);
        csv.Should().NotContain(wrongFacilityLienNumber);
        csv.Should().NotContain(wrongPurchaseDateLienNumber);
        csv.Should().NotContain(wrongClosedDateLienNumber);
    }

    [Fact]
    public async Task GenerateLiensCsv_accepts_relationship_filter_ids_from_liens_screen()
    {
        var lawFirmOrgId = Guid.CreateVersion7();
        var lawFirmContact = Contact.Create(
            SeedHelper.TenantId,
            lawFirmOrgId,
            ContactType.LawFirm,
            "Screen",
            "Law Firm",
            SeedHelper.UserId);
        var caseManager = Contact.Create(
            SeedHelper.TenantId,
            lawFirmOrgId,
            ContactType.CaseManager,
            "Screen",
            "Manager",
            SeedHelper.UserId);
        var facility = Facility.Create(
            SeedHelper.TenantId,
            lawFirmOrgId,
            "Screen Facility",
            SeedHelper.UserId);
        var facilityContact = Contact.Create(
            SeedHelper.TenantId,
            lawFirmOrgId,
            ContactType.MedicalFacility,
            "Screen",
            "Facility",
            SeedHelper.UserId,
            facilityId: facility.Id);
        var caseEntity = Case.Create(
            SeedHelper.TenantId,
            lawFirmOrgId,
            $"CASE-SCREEN-FILTER-{Guid.CreateVersion7():N}",
            "Screen",
            "Plaintiff",
            SeedHelper.UserId,
            notes: $"caseManagerId={caseManager.Id}");
        var lienNumber = $"LIEN-SCREEN-FILTER-{Guid.CreateVersion7():N}";
        var purchaseDate = new DateOnly(2026, 8, 20);
        var lien = Lien.Create(
            SeedHelper.TenantId,
            lawFirmOrgId,
            lienNumber,
            LienType.MedicalLien,
            100m,
            SeedHelper.UserId,
            caseId: caseEntity.Id,
            facilityId: facility.Id,
            purchaseDate: purchaseDate);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Contacts.AddRange(lawFirmContact, caseManager, facilityContact);
            db.Facilities.Add(facility);
            db.Cases.Add(caseEntity);
            db.Liens.Add(lien);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/liens/cases/liens/generate-csv", new
        {
            keyword = lienNumber,
            lawFirmId = lawFirmContact.Id,
            medicalFacilityId = facilityContact.Id,
            caseManagerId = caseManager.Id,
            lienStatusId = "Open",
            purchaseDate = purchaseDate.ToString("yyyy-MM-dd"),
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var encodedCsv = payload!.RootElement.GetProperty("data")[0].GetProperty("base64").GetString();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCsv!));
        csv.Should().Contain(lienNumber);
    }

    [Fact]
    public async Task GenerateLiensCsv_defaults_to_screen_columns_and_supports_legacy_format()
    {
        var response = await _client.PostAsJsonAsync("/api/liens/cases/liens/generate-csv", new
        {
            liensId = SeedHelper.LienId,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var encodedCsv = payload!.RootElement.GetProperty("data")[0].GetProperty("base64").GetString();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCsv!));
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        ParseCsvLine(lines[0]).Should().Equal(
            "Lien ID",
            "Plaintiff Name",
            "Law Firm",
            "Medical Facility",
            "Purchase Date",
            "Purchase Amount",
            "Billing Amount",
            "Lien Status",
            "Initial Service Date",
            "Case Manager");
        ParseCsvLine(lines[1]).Should().Equal(
            "LIEN-TEST-001",
            "John Plaintiff",
            "Smith & Associates LLP",
            "",
            "",
            "0.00",
            "0.00",
            "Open",
            "",
            "");

        var legacyResponse = await _client.PostAsJsonAsync("/api/liens/cases/liens/generate-csv", new
        {
            liensId = SeedHelper.LienId,
            legacyFormat = true,
        });
        legacyResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await legacyResponse.Content.ReadAsStringAsync()}");

        var legacyPayload = await legacyResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var legacyEncodedCsv = legacyPayload!.RootElement.GetProperty("data")[0].GetProperty("base64").GetString();
        var legacyCsv = Encoding.UTF8.GetString(Convert.FromBase64String(legacyEncodedCsv!));
        var legacyHeader = legacyCsv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)[0];
        ParseCsvLine(legacyHeader).Should().Equal(
            "CaseCode",
            "LiensCode",
            "Status",
            "PurchaseDate",
            "InitialServiceDate",
            "EndServiceDate",
            "Note",
            "FacilityEmail",
            "FacilityPhone",
            "TotalPurchase",
            "TotalBilling",
            "LawFirm",
            "CaseManager",
            "FacilityName",
            "FacilityContactName",
            "MedicalProvider",
            "PlainTiffName",
            "ClosedDate");
    }

    [Fact]
    public async Task GenerateLiensCsv_includes_all_batched_medical_codes_beyond_search_page_limit()
    {
        const int medicalCodeCount = 125;
        var orgId = Guid.CreateVersion7();
        var canonicalLawFirm = Company.Create(
            SeedHelper.TenantId,
            orgId,
            CompanyDirectoryReferenceData.LawFirmId,
            "Batched Export Law Firm",
            SeedHelper.UserId);
        var caseEntity = Case.Create(
            SeedHelper.TenantId,
            orgId,
            $"CASE-LIEN-CSV-BATCH-{Guid.CreateVersion7():N}",
            "Batch",
            "Export",
            SeedHelper.UserId);
        caseEntity.LinkCanonicalCaseParties(canonicalLawFirm.Id, null);
        var lien = Lien.Create(
            SeedHelper.TenantId,
            orgId,
            $"LIEN-CSV-BATCH-{Guid.CreateVersion7():N}"[..36],
            LienType.MedicalLien,
            100m,
            SeedHelper.UserId,
            caseId: caseEntity.Id);
        var medicalCodes = Enumerable.Range(1, medicalCodeCount)
            .Select(index => ServicingItem.Create(
                SeedHelper.TenantId,
                orgId,
                $"CSV-BATCH-CODE-{index:D3}-{Guid.CreateVersion7():N}",
                "LegacyMedicalCode",
                "Batched CSV medical code",
                "system",
                SeedHelper.UserId,
                caseId: caseEntity.Id,
                lienId: lien.Id,
                notes: "purchaseAmount=1.00; billingAmount=2.00"))
            .ToList();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Companies.Add(canonicalLawFirm);
            db.Cases.Add(caseEntity);
            db.Liens.Add(lien);
            db.ServicingItems.AddRange(medicalCodes);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/liens/cases/liens/generate-csv", new
        {
            liensId = lien.Id,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var encodedCsv = payload!.RootElement.GetProperty("data")[0].GetProperty("base64").GetString();
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCsv!));
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var row = ParseCsvLine(lines[1]);

        row[0].Should().Be(lien.LienNumber);
        row[2].Should().Be(canonicalLawFirm.Name);
        row[5].Should().Be("125.00");
        row[6].Should().Be("250.00");
    }

    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString());
        return values.ToArray();
    }

    [Fact]
    public async Task GetCasesV3_returns_law_firm_subcontact_case_manager_display_value()
    {
        var caseManagerId = Guid.CreateVersion7();
        var caseNumber = $"CASE-V3-LF-CM-{Guid.CreateVersion7():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var caseManager = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                ContactType.LawFirm,
                "Jamie",
                "Manager",
                SeedHelper.UserId,
                lawFirmId: SeedHelper.LawFirmId,
                contactSubtype: ContactSubtype.LawFirmCaseManager,
                organization: "Smith & Associates LLP");
            typeof(Contact).GetProperty(nameof(Contact.Id))!.SetValue(caseManager, caseManagerId);

            db.Contacts.Add(caseManager);
            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                caseNumber,
                "Display",
                "Subcontact",
                SeedHelper.UserId,
                notes: $"lawFirmId={SeedHelper.LawFirmId}; accidentTypeId=MVA; accidentType=Motor Vehicle Accident; caseManagerId={caseManagerId}"));

            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/v3", new
        {
            keyword = caseNumber,
            page = 1,
            limit = 20,
            sortBy = "",
            sortDirection = "",
            statusId = "PreDemand,DemandSent",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        var item = doc!.RootElement.GetProperty("data").EnumerateArray().Single();

        item.GetProperty("caseManagerId").GetString().Should().Be(caseManagerId.ToString());
        item.GetProperty("caseManager").GetString().Should().Be("Jamie Manager");
    }

    [Fact]
    public async Task GetLawFirmV3_returns_cases_when_filtered_by_law_firm_contact_id()
    {
        var resp = await _client.PostAsJsonAsync("/api/liens/cases/law/v3", new
        {
            lawFirmId = SeedHelper.LawFirmId,
            page = 1,
            limit = 10,
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        body.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Should()
            .Contain(SeedHelper.CaseId);
    }

    [Fact]
    public async Task GetMedicalV3_returns_cases_when_linked_by_medical_provider_contact_id()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LMFI-MED-{Guid.CreateVersion7():N}",
                "LegacyMedicalFacilityInfo",
                "Legacy medical facility information",
                "system",
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                lienId: SeedHelper.LienId,
                notes: $"medicalProviderId={SeedHelper.MedicalProviderId}"));
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/medical/v3", new
        {
            medicalId = SeedHelper.MedicalProviderId,
            page = 1,
            limit = 10,
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        body.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Should()
            .Contain(SeedHelper.CaseId);
    }

    [Fact]
    public async Task GetMedicalFacilityV3_returns_cases_when_filtered_by_medical_facility_contact_id()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LMFI-FAC-{Guid.CreateVersion7():N}",
                "LegacyMedicalFacilityInfo",
                "Legacy medical facility information",
                "system",
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                lienId: SeedHelper.LienId,
                notes: $"facilityId={SeedHelper.MedicalFacilityContactId}"));
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/medical/facility/v3", new
        {
            facilityId = SeedHelper.MedicalFacilityContactId,
            page = 1,
            limit = 10,
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        body.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Should()
            .Contain(SeedHelper.CaseId);
    }

    [Fact]
    public async Task GetFundingV3_returns_cases_when_linked_by_funding_company_contact_id()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Liens.Add(Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-FUND-{Guid.CreateVersion7():N}",
                "MedicalLien",
                1250m,
                SeedHelper.UserId,
                externalReference: SeedHelper.FundingCompanyId.ToString(),
                caseId: SeedHelper.CaseId));
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/funding/v3", new
        {
            fundingCompanyId = SeedHelper.FundingCompanyId,
            page = 1,
            limit = 10,
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        body.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Should()
            .Contain(SeedHelper.CaseId);
    }

    [Fact]
    public async Task GetLeadsV3_returns_cases_when_linked_by_lead_contact_id()
    {
        Guid leadCaseId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-LEAD-{Guid.CreateVersion7():N}",
                "Lead",
                "Matched",
                SeedHelper.UserId,
                notes: $"leadId={SeedHelper.LeadContactId}");
            leadCaseId = caseEntity.Id;
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/leads/v3", new
        {
            leadId = SeedHelper.LeadContactId,
            page = 1,
            limit = 10,
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        body.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Should()
            .Contain(leadCaseId);
    }

    [Fact]
    public async Task DetailsUpdate_persists_extended_tracking_flags_and_get_case_by_id_returns_them()
    {
        var patchResp = await _client.PatchAsJsonAsync("/api/liens/cases/details-update", new
        {
            caseId = SeedHelper.CaseId,
            currentStatus = "PreDemand",
            currentMedicalStatus = "Treating",
            caseType = "Motor Vehicle Accident",
            stateOfIncident = "CA",
            trackingFollowUp = "07/16/2026",
            dateOfLoss = "06/15/2024",
            leadId = SeedHelper.LeadContactId.ToString(),
            shareCase = "true",
            minorComp = "false",
            caseDropped = "false",
            childSupportLiens = "true",
            isUccFiled = "false",
        });

        patchResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await patchResp.Content.ReadAsStringAsync()}");

        var getResp = await _client.GetAsync($"/api/liens/cases/{SeedHelper.CaseId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await getResp.Content.ReadAsStringAsync()}");

        var body = await getResp.Content.ReadFromJsonAsync<JsonDocument>();
        body.Should().NotBeNull();

        var root = body!.RootElement;
        root.GetProperty("shareCase").GetString().Should().Be("Yes");
        root.GetProperty("minorComp").GetString().Should().Be("No");
        root.GetProperty("caseDropped").GetString().Should().Be("No");
        root.GetProperty("childSupportLiens").GetString().Should().Be("Yes");
        root.GetProperty("isUccFiled").GetString().Should().Be("No");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var caseEntity = await db.Cases.FindAsync(SeedHelper.CaseId);
        caseEntity.Should().NotBeNull();
        caseEntity!.Notes.Should().Contain("shareCase=Yes");
        caseEntity.Notes.Should().Contain("minorComp=No");
        caseEntity.Notes.Should().Contain("caseDropped=No");
        caseEntity.Notes.Should().Contain("childSupportLiens=Yes");
        caseEntity.Notes.Should().Contain("isUccFiled=No");
    }

    [Theory]
    [InlineData("New")]
    [InlineData("Processing")]
    public async Task DetailsUpdate_preserves_legacy_pre_demand_status_variants(string currentStatus)
    {
        Guid caseId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-PRE-{Guid.CreateVersion7():N}",
                "Legacy",
                "Status",
                SeedHelper.UserId);
            caseId = caseEntity.Id;
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
        }

        var patchResp = await _client.PatchAsJsonAsync("/api/liens/cases/details-update", new
        {
            caseId,
            currentStatus,
            currentMedicalStatus = "Treating",
            caseType = "Motor Vehicle Accident",
            stateOfIncident = "CA",
        });

        patchResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await patchResp.Content.ReadAsStringAsync()}");

        var getResp = await _client.GetAsync($"/api/liens/cases/{caseId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await getResp.Content.ReadAsStringAsync()}");

        var body = await getResp.Content.ReadFromJsonAsync<JsonDocument>();
        body.Should().NotBeNull();
        body!.RootElement.GetProperty("status").GetString()
            .Should().Be(currentStatus);
        body.RootElement.GetProperty("statusLabel").GetString()
            .Should().Be(currentStatus);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var updatedCase = await verifyDb.Cases.FindAsync(caseId);
        updatedCase.Should().NotBeNull();
        updatedCase!.Status.Should().Be(CaseStatus.PreDemand);
    }

    [Theory]
    [InlineData("Litigation(Pending)")]
    [InlineData("Litigation(Open)")]
    [InlineData("Litigation(Closed)")]
    public async Task DetailsUpdate_preserves_legacy_litigation_status_variants(string currentStatus)
    {
        Guid caseId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-LIT-{Guid.CreateVersion7():N}",
                "Legacy",
                "Litigation",
                SeedHelper.UserId);
            caseId = caseEntity.Id;
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
        }

        var patchResp = await _client.PatchAsJsonAsync("/api/liens/cases/details-update", new
        {
            caseId,
            currentStatus,
            currentMedicalStatus = "Treating",
            caseType = "Motor Vehicle Accident",
            stateOfIncident = "CA",
        });

        patchResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await patchResp.Content.ReadAsStringAsync()}");

        var getResp = await _client.GetAsync($"/api/liens/cases/{caseId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await getResp.Content.ReadAsStringAsync()}");

        var body = await getResp.Content.ReadFromJsonAsync<JsonDocument>();
        body.Should().NotBeNull();
        var expectedLegacyStatus = currentStatus.Replace("(", " (", StringComparison.Ordinal);
        body!.RootElement.GetProperty("status").GetString()
            .Should().Be(expectedLegacyStatus);
        body.RootElement.GetProperty("statusLabel").GetString()
            .Should().Be(expectedLegacyStatus);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var updatedCase = await verifyDb.Cases.FindAsync(caseId);
        updatedCase.Should().NotBeNull();
        updatedCase!.Status.Should().Be(currentStatus switch
        {
            "Litigation(Pending)" => CaseStatus.LitigationPending,
            "Litigation(Open)" => CaseStatus.LitigationOpen,
            _ => CaseStatus.InNegotiation,
        });
    }

    [Fact]
    public async Task DetailsUpdate_replaces_litigation_label_when_status_changes_to_negotiations()
    {
        Guid caseId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-NEG-{Guid.CreateVersion7():N}",
                "Legacy",
                "Negotiations",
                SeedHelper.UserId,
                notes: "[legacy-meta]\nstatusLabel=Litigation (Open)");
            caseEntity.TransitionStatus(CaseStatus.LitigationOpen, SeedHelper.UserId);
            caseId = caseEntity.Id;
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
        }

        var patchResp = await _client.PatchAsJsonAsync("/api/liens/cases/details-update", new
        {
            caseId,
            currentStatus = "Negotiations",
        });

        patchResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await patchResp.Content.ReadAsStringAsync()}");

        var getResp = await _client.GetAsync($"/api/liens/cases/{caseId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await getResp.Content.ReadAsStringAsync()}");

        var body = await getResp.Content.ReadFromJsonAsync<JsonDocument>();
        body.Should().NotBeNull();
        body!.RootElement.GetProperty("status").GetString().Should().Be("Negotiations");
        body.RootElement.GetProperty("statusLabel").GetString().Should().Be("Negotiations");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var updatedCase = await verifyDb.Cases.FindAsync(caseId);
        updatedCase.Should().NotBeNull();
        updatedCase!.Status.Should().Be(CaseStatus.InNegotiation);
        updatedCase.Notes.Should().Contain("statusLabel=Negotiations");
        updatedCase.Notes.Should().NotContain("statusLabel=Litigation (Open)");
    }

    [Fact]
    public async Task GetCaseById_returns_default_false_flags_when_metadata_is_missing()
    {
        Guid caseId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-FLAGS-{Guid.CreateVersion7():N}",
                "Default",
                "Flags",
                SeedHelper.UserId);
            caseId = caseEntity.Id;
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
        }

        var resp = await _client.GetAsync($"/api/liens/cases/{caseId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body.Should().NotBeNull();

        var root = body!.RootElement;
        root.GetProperty("shareCase").GetString().Should().Be("false");
        root.GetProperty("minorComp").GetString().Should().Be("false");
        root.GetProperty("caseDropped").GetString().Should().Be("false");
        root.GetProperty("childSupportLiens").GetString().Should().Be("false");
        root.GetProperty("isUccFiled").GetString().Should().Be("false");
        root.GetProperty("lienStatus").GetString().Should().BeEmpty();
        root.GetProperty("lienStatusId").GetString().Should().BeEmpty();
        root.GetProperty("settlementStatus").GetString().Should().BeEmpty();
        root.GetProperty("settlementStatusId").GetString().Should().BeEmpty();
    }

    [Fact]
    public async Task UploadDocument_rejects_disallowed_file_extension()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedHelper.CaseId.ToString()), "caseId");
        form.Add(new ByteArrayContent("hello"u8.ToArray()), "file", "payload.exe");

        var resp = await _client.PostAsync("/api/liens/cases/upload/document", form);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"Body: {await resp.Content.ReadAsStringAsync()}");
        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("message").GetString()
            .Should().Contain("File type not allowed");
    }

    [Fact]
    public async Task UploadLienDocument_rejects_non_form_payload_with_actionable_message()
    {
        var resp = await _client.PostAsJsonAsync("/api/liens/cases/liens/upload/document", new
        {
            liensId = SeedHelper.LienId,
            DocFileTypeId = "14",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"Body: {await resp.Content.ReadAsStringAsync()}");
        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("message").GetString()
            .Should().Be("Content-Type must be multipart/form-data.");
    }

    [Fact]
    public async Task UploadDocument_uploads_case_document_and_records_legacy_metadata()
    {
        using var scope = _factory.Services.CreateScope();
        var uploadClient = scope.ServiceProvider.GetRequiredService<CapturingLegacyDocumentUploadClient>();
        uploadClient.Clear();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedHelper.CaseId.ToString()), "caseId");
        form.Add(new StringContent("14"), "DocFileTypeId");
        form.Add(new StringContent("payoff-quote"), "DocName");
        form.Add(new StringContent("Payoff Statement"), "DocDescription");
        var file = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "payoff-quote.pdf");

        var resp = await _client.PostAsync("/api/liens/cases/upload/document", form);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        uploadClient.Uploads.Should().ContainSingle();
        var upload = uploadClient.Uploads.Single();
        upload.ReferenceId.Should().Be(SeedHelper.CaseId);
        upload.ReferenceType.Should().Be("Case");
        upload.Title.Should().Be("payoff-quote");

        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var item = db.ServicingItems.Single(i =>
            i.TaskType == "LegacyCaseDocument" &&
            i.Notes != null &&
            i.Notes.Contains(upload.DocumentId.ToString()));
        item.CaseId.Should().Be(SeedHelper.CaseId);
        item.Notes.Should().Contain("typeId=14");
        item.Notes.Should().Contain(upload.DocumentId.ToString());

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("data").GetProperty("url").GetString()
            .Should().Be($"/documents/{upload.DocumentId}");
    }

    [Fact]
    public async Task UploadDocument_accepts_document_up_to_50mb()
    {
        using var scope = _factory.Services.CreateScope();
        var uploadClient = scope.ServiceProvider.GetRequiredService<CapturingLegacyDocumentUploadClient>();
        uploadClient.Clear();

        var content = new byte[(int)LiensUploadLimits.MaxBytes];
        "%PDF-1.4"u8.CopyTo(content);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedHelper.CaseId.ToString()), "caseId");
        form.Add(new StringContent("large-document"), "DocName");
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "large-document.pdf");

        var resp = await _client.PostAsync("/api/liens/cases/upload/document", form);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");
        uploadClient.Uploads.Should().ContainSingle();
        uploadClient.Uploads.Single().Length.Should().Be(content.LongLength);
    }

    [Fact]
    public async Task UploadDocument_returns_size_error_when_document_exceeds_50mb()
    {
        var content = new byte[(int)LiensUploadLimits.MaxBytes + (2 * 1024 * 1024)];
        "%PDF-1.4"u8.CopyTo(content);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedHelper.CaseId.ToString()), "caseId");
        form.Add(new StringContent("too-large-document"), "DocName");
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "too-large-document.pdf");

        var resp = await _client.PostAsync("/api/liens/cases/upload/document", form);
        var bodyText = await resp.Content.ReadAsStringAsync();

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"Body: {bodyText}");
        using var body = JsonDocument.Parse(bodyText);
        body.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("message").GetString()
            .Should().Be("File size exceeds the allowed limit (50 MB)");
    }

    [Fact]
    public async Task PayoffQuote_legacy_misspelled_route_ignores_existing_document_and_generates_new_quote()
    {
        using var scope = _factory.Services.CreateScope();
        var uploadClient = scope.ServiceProvider.GetRequiredService<CapturingLegacyDocumentUploadClient>();
        uploadClient.Clear();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedHelper.CaseId.ToString()), "caseId");
        form.Add(new StringContent("14"), "DocFileTypeId");
        form.Add(new StringContent("payoff-quote"), "DocName");
        var file = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "payoff-quote.pdf");

        var uploadResp = await _client.PostAsync("/api/liens/cases/upload/document", form);
        uploadResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await uploadResp.Content.ReadAsStringAsync()}");
        var existingUpload = uploadClient.Uploads.Single();

        var resp = await _client.GetAsync($"/api/liens/cases/payoff-qoute/{SeedHelper.CaseId}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");
        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        uploadClient.Uploads.Should().HaveCount(2);
        var generatedUpload = uploadClient.Uploads[1];
        generatedUpload.DocumentId.Should().NotBe(existingUpload.DocumentId);
        body.RootElement.GetProperty("url").GetString()
            .Should().Be($"/documents/{generatedUpload.DocumentId}");
        body.RootElement.GetProperty("base64").GetString()
            .Should().Be(Convert.ToBase64String(generatedUpload.Content));
    }

    [Fact]
    public async Task PayoffQuote_generates_new_document_on_every_call()
    {
        using var scope = _factory.Services.CreateScope();
        var uploadClient = scope.ServiceProvider.GetRequiredService<CapturingLegacyDocumentUploadClient>();
        uploadClient.Clear();

        var firstResp = await _client.GetAsync($"/api/liens/cases/payoff-qoute/{SeedHelper.CaseId}");
        var secondResp = await _client.GetAsync($"/api/liens/cases/payoff-qoute/{SeedHelper.CaseId}");

        firstResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await firstResp.Content.ReadAsStringAsync()}");
        secondResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await secondResp.Content.ReadAsStringAsync()}");

        uploadClient.Uploads.Should().HaveCount(2);
        uploadClient.Uploads[0].DocumentId.Should().NotBe(uploadClient.Uploads[1].DocumentId);

        var firstBody = await firstResp.Content.ReadFromJsonAsync<JsonDocument>();
        firstBody!.RootElement.GetProperty("url").GetString()
            .Should().Be($"/documents/{uploadClient.Uploads[0].DocumentId}");
        firstBody.RootElement.GetProperty("base64").GetString()
            .Should().Be(Convert.ToBase64String(uploadClient.Uploads[0].Content));

        var secondBody = await secondResp.Content.ReadFromJsonAsync<JsonDocument>();
        secondBody!.RootElement.GetProperty("url").GetString()
            .Should().Be($"/documents/{uploadClient.Uploads[1].DocumentId}");
        secondBody.RootElement.GetProperty("base64").GetString()
            .Should().Be(Convert.ToBase64String(uploadClient.Uploads[1].Content));
    }

    [Fact]
    public async Task PayoffQuote_generates_document_when_missing()
    {
        Guid caseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-NODOC-{Guid.NewGuid():N}"[..20],
                "No",
                "Document",
                SeedHelper.UserId);
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
            caseId = caseEntity.Id;
        }

        using var uploadScope = _factory.Services.CreateScope();
        var uploadClient = uploadScope.ServiceProvider.GetRequiredService<CapturingLegacyDocumentUploadClient>();
        uploadClient.Clear();

        var resp = await _client.GetAsync($"/api/liens/cases/payoff-qoute/{caseId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("url").GetString()
            .Should().Be($"/documents/{uploadClient.Uploads.Single().DocumentId}");
        body.RootElement.GetProperty("base64").GetString().Should().NotBeNullOrWhiteSpace();

        var upload = uploadClient.Uploads.Single();
        upload.ReferenceId.Should().Be(caseId);
        upload.ReferenceType.Should().Be("Case");
        upload.Title.Should().Be($"PayoffQuote_{caseId}");
        upload.FileName.Should().Be($"PayoffQuote_{caseId}.pdf");
        upload.ContentType.Should().Be("application/pdf");
        upload.Length.Should().BeGreaterThan(0);
        body.RootElement.GetProperty("base64").GetString()
            .Should().Be(Convert.ToBase64String(upload.Content));
        StubDocumentsServiceHandler.GetMetadataRequestCount(upload.DocumentId).Should().Be(2);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var item = verifyDb.ServicingItems.Single(i =>
            i.CaseId == caseId &&
            i.TaskType == "LegacyCaseDocument");
        item.Notes.Should().Contain("typeId=14");
        item.Notes.Should().Contain(upload.DocumentId.ToString());
    }

    [Fact]
    public async Task PayoffQuote_uses_legacy_medical_metadata_for_generated_rows()
    {
        var caseId = Guid.CreateVersion7();
        var lienId = Guid.CreateVersion7();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-PAYOFF-DATA-{Guid.NewGuid():N}"[..20],
                "Patria",
                "Test",
                SeedHelper.UserId,
                dateOfIncident: new DateOnly(2026, 7, 30));
            SetProperty(caseEntity, nameof(Case.Id), caseId);
            db.Cases.Add(caseEntity);

            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LIEN-PAYOFF-{Guid.NewGuid():N}"[..20],
                LienType.MedicalLien,
                999m,
                SeedHelper.UserId,
                caseId: caseId,
                initialServiceDate: new DateOnly(2026, 7, 27),
                isServicing: "1");
            SetProperty(lien, nameof(Lien.Id), lienId);
            db.Liens.Add(lien);

            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LMFI-{Guid.CreateVersion7():N}"[..20],
                "LegacyMedicalFacilityInfo",
                "Legacy medical facility information",
                "system",
                SeedHelper.UserId,
                caseId: caseId,
                lienId: lienId,
                notes: "facilityName=Universal Spine & Joint Specialists"));
            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"LMC-{Guid.CreateVersion7():N}"[..20],
                "LegacyMedicalCode",
                "Legacy medical code",
                "system",
                SeedHelper.UserId,
                caseId: caseId,
                lienId: lienId,
                notes: "billingAmount=234.00; purchaseAmount=120.00"));

            await db.SaveChangesAsync();
        }

        using var serviceScope = _factory.Services.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<IPayoffQuoteService>();
        var method = service.GetType().GetMethod(
            "BuildPayoffLinesAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var task = (Task)method!.Invoke(
            service,
            [SeedHelper.TenantId, new List<LienResponse>
            {
                new()
                {
                    Id = lienId,
                    CaseId = caseId,
                    Status = LienStatus.Draft,
                    OriginalAmount = 999m,
                    InitialServiceDate = new DateOnly(2026, 7, 27),
                    IsServicing = "1",
                },
            }, CancellationToken.None])!;

        await task;
        var result = (System.Collections.IEnumerable)task.GetType().GetProperty("Result")!.GetValue(task)!;
        var line = result.Cast<object>().Single();

        line.GetType().GetProperty("MedicalFacility")!.GetValue(line)
            .Should().Be("Universal Spine & Joint Specialists");
        line.GetType().GetProperty("DateOfService")!.GetValue(line)
            .Should().Be("07/27/2026");
        line.GetType().GetProperty("Amount")!.GetValue(line)
            .Should().Be(234m);
    }

    [Fact]
    public async Task CaseUpdatesV3_returns_ok_with_empty_data_when_case_has_no_notes()
    {
        Guid caseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-NONOTES-{Guid.NewGuid():N}"[..20],
                "No",
                "Notes",
                SeedHelper.UserId);
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
            caseId = caseEntity.Id;
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/case-updates/v3", new
        {
            CaseId = caseId,
            page = 1,
            limit = 10,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("data").EnumerateArray().Should().BeEmpty();
        body.RootElement.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task DuplicateCheck_returns_match_for_similar_name_with_exact_dob_and_dol()
    {
        Guid existingCaseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var existingCase = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-DUP-{Guid.NewGuid():N}"[..20],
                "Jonathan",
                "Smith",
                SeedHelper.UserId,
                clientDob: new DateOnly(1985, 4, 10),
                dateOfIncident: new DateOnly(2026, 1, 15));
            db.Cases.Add(existingCase);
            await db.SaveChangesAsync();
            existingCaseId = existingCase.Id;
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/duplicate-check", new
        {
            firstname = "Jon",
            lastname = "Smit",
            dob = "04/10/1985",
            dateOfLoss = "01/15/2026",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");
        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("isDuplicate").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("message").GetString()
            .Should().Be("A case with similar information already exists. Would you like to view the existing case?");
        var match = body.RootElement.GetProperty("matches").EnumerateArray().Single();
        match.GetProperty("id").GetGuid().Should().Be(existingCaseId);
        match.GetProperty("clientDisplayName").GetString().Should().Be("Jonathan Smith");
    }

    [Fact]
    public async Task CreateCase_rejects_potential_duplicate_before_saving()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-BLOCK-{Guid.NewGuid():N}"[..20],
                "Maria",
                "Garcia",
                SeedHelper.UserId,
                clientDob: new DateOnly(1990, 6, 2),
                dateOfIncident: new DateOnly(2025, 12, 9)));
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/create", new
        {
            firstname = "Maria",
            lastname = "Garcia",
            dob = "06/02/1990",
            dateOfLoss = "12/09/2025",
            caseStatusId = "New",
            accidentTypeId = "MVA",
            accidentStateId = "NV",
            lawFirmId = SeedHelper.LawFirmId.ToString(),
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict,
            $"Body: {await resp.Content.ReadAsStringAsync()}");
        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("error").GetProperty("message").GetString()
            .Should().Be("A case with similar information already exists. Would you like to view the existing case?");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        verifyDb.Cases.Count(c =>
                c.TenantId == SeedHelper.TenantId &&
                c.ClientDob == new DateOnly(1990, 6, 2) &&
                c.DateOfIncident == new DateOnly(2025, 12, 9) &&
                c.ClientFirstName == "Maria" &&
                c.ClientLastName == "Garcia")
            .Should().Be(1);
    }

    [Fact]
    public async Task CaseUpdatesV3_normalizes_legacy_note_updated_description()
    {
        Guid caseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-OLD-NOTE-{Guid.CreateVersion7():N}"[..24],
                "Old",
                "Note",
                SeedHelper.UserId);
            db.Cases.Add(caseEntity);
            db.LienCaseNotes.Add(LienCaseNote.Create(
                caseEntity.Id,
                SeedHelper.TenantId,
                "Note updated",
                CaseNoteCategory.Internal,
                SeedHelper.UserId,
                "Legacy User"));
            db.LienCaseNotes.Add(LienCaseNote.Create(
                caseEntity.Id,
                SeedHelper.TenantId,
                "Case updated: status changed to Closed; Note updated.",
                CaseNoteCategory.Internal,
                SeedHelper.UserId,
                "Legacy User"));
            await db.SaveChangesAsync();
            caseId = caseEntity.Id;
        }

        var response = await _client.PostAsJsonAsync("/api/liens/cases/case-updates/v3", new
        {
            CaseId = caseId,
            page = 1,
            limit = 10,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var updates = payload!.RootElement.GetProperty("data").EnumerateArray().ToList();
        updates.Select(update => update.GetProperty("description").GetString()).Should().BeEquivalentTo(
            "Case Tracking Note Update",
            "Case updated: status changed to Closed; Case Tracking Note Update.");
        updates.Select(update => update.GetProperty("note").GetString()).Should().BeEquivalentTo(
            "Case Tracking Note Update",
            "Case updated: status changed to Closed; Case Tracking Note Update.");
    }

    [Fact]
    public async Task DetailsUpdate_creates_case_update_entry_visible_in_case_updates_v3()
    {
        Guid caseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-UPD-{Guid.NewGuid():N}"[..20],
                "Update",
                "Case",
                SeedHelper.UserId);
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
            caseId = caseEntity.Id;
        }

        var patchResp = await _client.PatchAsJsonAsync("/api/liens/cases/details-update", new
        {
            caseId,
            currentStatus = "New",
            currentMedicalStatus = "Treating",
            caseType = "Motor Vehicle Accident",
            stateOfIncident = "CA",
            notes = "status change from details update",
        });
        patchResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await patchResp.Content.ReadAsStringAsync()}");

        var resp = await _client.PostAsJsonAsync("/api/liens/cases/case-updates/v3", new
        {
            CaseId = caseId,
            page = 1,
            limit = 10,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        body.Should().NotBeNull();
        body!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        body.RootElement.GetProperty("data").EnumerateArray().Should().Contain(item =>
            item.GetProperty("description").GetString()!.Contains("Case updated:", StringComparison.Ordinal) &&
            item.GetProperty("description").GetString()!.Contains("medical status changed to Treating", StringComparison.Ordinal) &&
            item.GetProperty("description").GetString()!.Contains(
                "Case Tracking Note Update: status change from details update",
                StringComparison.Ordinal) &&
            item.GetProperty("action").GetString() == "Case Details Update");
    }

    [Fact]
    public async Task DetailsUpdate_when_notes_change_records_case_update_without_duplicates()
    {
        Guid caseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-NOTE-UPD-{Guid.NewGuid():N}"[..24],
                "Note",
                "Update",
                SeedHelper.UserId,
                notes: "Original note");
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
            caseId = caseEntity.Id;
        }

        async Task<HttpResponseMessage> UpdateNotesAsync(string notes) =>
            await _client.PatchAsJsonAsync("/api/liens/cases/details-update", new
            {
                caseId,
                notes,
            });

        var changedResponse = await UpdateNotesAsync("Updated note");
        changedResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await changedResponse.Content.ReadAsStringAsync()}");

        var unchangedResponse = await UpdateNotesAsync("Updated note");
        unchangedResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await unchangedResponse.Content.ReadAsStringAsync()}");

        var clearedResponse = await UpdateNotesAsync("   ");
        clearedResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await clearedResponse.Content.ReadAsStringAsync()}");

        var updatesResponse = await _client.PostAsJsonAsync("/api/liens/cases/case-updates/v3", new
        {
            CaseId = caseId,
            page = 1,
            limit = 10,
        });
        updatesResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await updatesResponse.Content.ReadAsStringAsync()}");

        var payload = await updatesResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var updates = payload!.RootElement.GetProperty("data").EnumerateArray().ToList();
        updates.Should().HaveCount(2);
        updates.Should().OnlyContain(item =>
            item.GetProperty("action").GetString() == "Case Details Update");
        updates.Select(item => item.GetProperty("description").GetString()).Should().BeEquivalentTo(
            "Case Tracking Note Update: Updated note",
            "Case Tracking Note Update");

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var storedNotes = await verificationDb.LienCaseNotes
            .Where(note => note.TenantId == SeedHelper.TenantId && note.CaseId == caseId)
            .ToListAsync();
        storedNotes.Count(note => note.Category == CaseNoteCategory.General).Should().Be(1);
        storedNotes.Count(note => note.Category == CaseNoteCategory.Internal).Should().Be(2);
    }

    [Fact]
    public async Task DetailsUpdate_with_equivalent_legacy_status_records_only_the_submitted_tracking_note()
    {
        Guid caseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-LEGACY-STATUS-{Guid.NewGuid():N}"[..28],
                "Legacy",
                "Status",
                SeedHelper.UserId,
                notes: "Original note");
            SetProperty(caseEntity, nameof(Case.Status), "Negotiations");
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
            caseId = caseEntity.Id;
        }

        const string submittedNote = "Client called with a treatment update";
        var patchResponse = await _client.PatchAsJsonAsync("/api/liens/cases/details-update", new
        {
            caseId,
            currentStatus = "In Negotiation",
            notes = submittedNote,
        });
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await patchResponse.Content.ReadAsStringAsync()}");

        var updatesResponse = await _client.PostAsJsonAsync("/api/liens/cases/case-updates/v3", new
        {
            CaseId = caseId,
            page = 1,
            limit = 10,
        });
        updatesResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await updatesResponse.Content.ReadAsStringAsync()}");

        using var payload = JsonDocument.Parse(await updatesResponse.Content.ReadAsStringAsync());
        var updates = payload.RootElement.GetProperty("data").EnumerateArray().ToList();
        updates.Should().ContainSingle();
        updates[0].GetProperty("action").GetString().Should().Be("Case Details Update");
        updates[0].GetProperty("description").GetString().Should().Be(
            $"Case Tracking Note Update: {submittedNote}");
        updates[0].GetProperty("description").GetString().Should().NotContain("status changed");
    }

    [Fact]
    public async Task DetailsUpdate_when_history_write_fails_rolls_back_case_and_tracking_note()
    {
        using var factory = new TransactionalLiensApiFactory();
        Guid caseId;

        using (var setupScope = factory.Services.CreateScope())
        {
            await SeedHelper.SeedAsync(setupScope.ServiceProvider);
            var db = setupScope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-NOTE-ROLLBACK-{Guid.NewGuid():N}"[..28],
                "Rollback",
                "Case",
                SeedHelper.UserId,
                notes: "Original note");
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
            caseId = caseEntity.Id;
        }

        factory.Services.GetRequiredService<CapturingAuditPublisher>().Clear();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));

        var response = await client.PatchAsJsonAsync("/api/liens/cases/details-update", new
        {
            caseId,
            notes = "Updated note",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var storedCase = await verificationDb.Cases
            .AsNoTracking()
            .SingleAsync(caseEntity => caseEntity.Id == caseId);
        storedCase.Notes.Should().Be("Original note");
        (await verificationDb.LienCaseNotes
                .AsNoTracking()
                .Where(note => note.CaseId == caseId)
                .ToListAsync())
            .Should().BeEmpty();
        verificationScope.ServiceProvider
            .GetRequiredService<CapturingAuditPublisher>()
            .Events.Should().BeEmpty();
    }

    [Fact]
    public async Task DetailsUpdate_with_default_false_flags_records_only_the_status_change()
    {
        Guid caseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"CASE-STATUS-{Guid.NewGuid():N}"[..20],
                "Status",
                "Only",
                SeedHelper.UserId);
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
            caseId = caseEntity.Id;
        }

        var patchResp = await _client.PatchAsJsonAsync("/api/liens/cases/details-update", new
        {
            caseId,
            currentStatus = "Closed",
            shareCase = "false",
            minorComp = "false",
            caseDropped = "false",
            childSupportLiens = "false",
            isUccFiled = "false",
        });
        patchResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await patchResp.Content.ReadAsStringAsync()}");

        var response = await _client.PostAsJsonAsync("/api/liens/cases/case-updates/v3", new
        {
            CaseId = caseId,
            page = 1,
            limit = 10,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("data").EnumerateArray()
            .Select(item => item.GetProperty("description").GetString())
            .Should().Contain("Case updated: status changed to Closed.");
    }

    [Fact]
    public async Task UploadLienDocument_uploads_lien_document_and_records_legacy_metadata()
    {
        using var scope = _factory.Services.CreateScope();
        var uploadClient = scope.ServiceProvider.GetRequiredService<CapturingLegacyDocumentUploadClient>();
        uploadClient.Clear();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedHelper.LienId.ToString()), "liensId");
        form.Add(new StringContent(Guid.Parse("00000000-0000-0000-0000-0000000000A1").ToString()), "DocFileTypeId");
        var file = new ByteArrayContent("name,amount\none,1"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "lien-upload.csv");

        var resp = await _client.PostAsync("/api/liens/cases/liens/upload/document", form);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        uploadClient.Uploads.Should().ContainSingle();
        var upload = uploadClient.Uploads.Single();
        upload.ReferenceId.Should().Be(SeedHelper.LienId);
        upload.ReferenceType.Should().Be("Lien");
        upload.Title.Should().Be("lien-upload");

        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var item = db.ServicingItems.Single(i =>
            i.TaskType == "LegacyLienDocument" &&
            i.Notes != null &&
            i.Notes.Contains(upload.DocumentId.ToString()));
        item.LienId.Should().Be(SeedHelper.LienId);
        item.Notes.Should().Contain(upload.DocumentId.ToString());
    }

    private static void SetProperty<T>(T entity, string propertyName, object? value) where T : class
    {
        var property = typeof(T).GetProperty(
            propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        property.Should().NotBeNull();
        property!.SetValue(entity, value);
    }
}
