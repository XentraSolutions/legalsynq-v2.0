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

public class CaseEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public CaseEndpointTests(LiensApiFactory factory) => _factory = factory;

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
    public async Task CreateCase_defaults_case_number_from_current_year_and_next_sequence()
    {
        var yearPrefix = DateTime.UtcNow.ToString("yy");

        var first = await _client.PostAsJsonAsync("/api/liens/cases", new
        {
            caseNumber = "",
            clientFirstName = "Case",
            clientLastName = "One",
        });

        first.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await first.Content.ReadAsStringAsync()}");
        var firstBody = await first.Content.ReadFromJsonAsync<CaseResponseBody>();
        firstBody!.CaseNumber.Should().Be($"{yearPrefix}-000001");

        var second = await _client.PostAsJsonAsync("/api/liens/cases", new
        {
            caseNumber = "",
            clientFirstName = "Case",
            clientLastName = "Two",
        });

        second.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await second.Content.ReadAsStringAsync()}");
        var secondBody = await second.Content.ReadFromJsonAsync<CaseResponseBody>();
        secondBody!.CaseNumber.Should().Be($"{yearPrefix}-000002");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyCreateCase_persists_client_contact_fields(bool useClientPrefixedNames)
    {
        var payload = new Dictionary<string, object?>
        {
            ["code"] = ($"LEGACY-{Guid.NewGuid():N}")[..20],
            ["firstname"] = "Legacy",
            ["lastname"] = "Contact",
        };

        if (useClientPrefixedNames)
        {
            payload["clientEmail"] = "legacy.client@example.com";
            payload["clientPhone"] = "555-0110";
        }
        else
        {
            payload["email"] = "legacy.client@example.com";
            payload["phone"] = "555-0110";
        }

        var create = await _client.PostAsJsonAsync("/api/liens/cases/create", payload);
        create.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await create.Content.ReadAsStringAsync()}");

        var createBody = await create.Content.ReadFromJsonAsync<LegacyCreateCaseResponseBody>();
        createBody!.Data.Should().ContainKey("id");

        var caseId = createBody.Data["id"];
        var detail = await _client.GetAsync($"/api/liens/cases/{caseId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await detail.Content.ReadAsStringAsync()}");

        var detailBody = await detail.Content.ReadFromJsonAsync<CaseDetailResponseBody>();
        detailBody!.ClientEmail.Should().Be("legacy.client@example.com");
        detailBody.ClientPhone.Should().Be("555-0110");
    }

    [Fact]
    public async Task LegacyCreateCase_persists_case_manager_and_accident_type_metadata()
    {
        var caseManagerId = Guid.CreateVersion7();
        var caseNumber = $"LEGACY-{Guid.CreateVersion7():N}"[..20];
        Guid accidentTypeLookupId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            accidentTypeLookupId = db.LookupValues
                .Where(x => x.TenantId == SeedHelper.TenantId
                    && x.Category == LookupCategory.AccidentType
                    && x.Code == "MVA")
                .Select(x => x.Id)
                .Single();

            var caseManager = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                ContactType.CaseManager,
                "John",
                "Doe",
                SeedHelper.UserId);
            typeof(Contact).GetProperty(nameof(Contact.Id))!.SetValue(caseManager, caseManagerId);

            db.Contacts.Add(caseManager);
            await db.SaveChangesAsync();
        }

        var create = await _client.PostAsJsonAsync("/api/liens/cases/create", new
        {
            code = caseNumber,
            firstname = "Legacy",
            lastname = "Metadata",
            externalReference = "EXT-LEGACY-META",
            policyNumber = "POL-123",
            claimNumber = "CLM-456",
            notes = "legacy metadata note",
            caseStatusId = "DemandSent",
            lawFirmId = SeedHelper.LawFirmId.ToString(),
            accidentTypeId = accidentTypeLookupId.ToString(),
            accidentStateId = "AL",
            caseManagerId = caseManagerId.ToString(),
            caseType = accidentTypeLookupId.ToString(),
            stateOfIncident = "AL",
        });

        create.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await create.Content.ReadAsStringAsync()}");

        var createBody = await create.Content.ReadFromJsonAsync<LegacyCreateCaseResponseBody>();
        createBody!.Data.Should().ContainKey("id");

        var caseId = createBody.Data["id"];
        var detail = await _client.GetAsync($"/api/liens/cases/{caseId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await detail.Content.ReadAsStringAsync()}");

        var detailBody = await detail.Content.ReadFromJsonAsync<JsonDocument>();
        var detailRoot = detailBody!.RootElement;
        detailRoot.GetProperty("externalReference").GetString().Should().Be("EXT-LEGACY-META");
        detailRoot.GetProperty("policyNumber").GetString().Should().Be("POL-123");
        detailRoot.GetProperty("claimNumber").GetString().Should().Be("CLM-456");
        detailRoot.GetProperty("notes").GetString().Should().Be("legacy metadata note");
        detailRoot.GetProperty("status").GetString().Should().Be("DemandSent");
        detailRoot.GetProperty("lawFirmId").GetString().Should().Be(SeedHelper.LawFirmId.ToString());
        detailRoot.GetProperty("lawFirm").GetString().Should().Be("Smith & Associates LLP");
        detailRoot.GetProperty("caseManagerId").GetString().Should().Be(caseManagerId.ToString());
        detailRoot.GetProperty("caseManager").GetString().Should().Be("John Doe");
        detailRoot.GetProperty("accidentTypeId").GetString().Should().Be(accidentTypeLookupId.ToString());
        detailRoot.GetProperty("accidentType").GetString().Should().Be("Motor Vehicle Accident");
        detailRoot.GetProperty("stateOfIncident").GetString().Should().Be("AL");

        var search = await _client.PostAsJsonAsync("/api/liens/cases/v3", new
        {
            keyword = caseNumber,
            page = 1,
            limit = 20,
            sortBy = "",
            sortDirection = "",
            statusId = "DemandSent",
        });

        search.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await search.Content.ReadAsStringAsync()}");

        var searchBody = await search.Content.ReadFromJsonAsync<JsonDocument>();
        searchBody!.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);

        var item = searchBody.RootElement.GetProperty("data").EnumerateArray().Single();
        item.GetProperty("lawFirmId").GetString().Should().Be(SeedHelper.LawFirmId.ToString());
        item.GetProperty("lawFirm").GetString().Should().Be("Smith & Associates LLP");
        item.GetProperty("caseManagerId").GetString().Should().Be(caseManagerId.ToString());
        item.GetProperty("caseManager").GetString().Should().Be("John Doe");
        item.GetProperty("accidentTypeId").GetString().Should().Be(accidentTypeLookupId.ToString());
        item.GetProperty("accidentType").GetString().Should().Be("Motor Vehicle Accident");
    }

    [Theory]
    [InlineData("New", CaseStatus.PreDemand)]
    [InlineData("Processing", CaseStatus.PreDemand)]
    [InlineData("Pre-demand", CaseStatus.PreDemand)]
    [InlineData("Demand Sent", CaseStatus.DemandSent)]
    [InlineData("Negotiations", CaseStatus.InNegotiation)]
    [InlineData("Litigation", CaseStatus.InNegotiation)]
    [InlineData("Case Settled", CaseStatus.CaseSettled)]
    [InlineData("Closed", CaseStatus.Closed)]
    public async Task LegacyCreateCase_accepts_legacy_status_labels(
        string legacyStatus,
        string expectedStatus)
    {
        var create = await _client.PostAsJsonAsync("/api/liens/cases/create", new
        {
            code = $"LEGACY-{Guid.CreateVersion7():N}"[..20],
            firstname = "Legacy",
            lastname = "Status",
            caseStatusId = legacyStatus,
        });

        create.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await create.Content.ReadAsStringAsync()}");

        var createBody = await create.Content.ReadFromJsonAsync<LegacyCreateCaseResponseBody>();
        createBody!.Data.Should().ContainKey("id");

        var caseId = createBody.Data["id"];
        var detail = await _client.GetAsync($"/api/liens/cases/{caseId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await detail.Content.ReadAsStringAsync()}");

        var detailBody = await detail.Content.ReadFromJsonAsync<JsonDocument>();
        detailBody!.RootElement.GetProperty("status").GetString().Should().Be(expectedStatus);
    }

    [Fact]
    public async Task LegacyCreateCase_accepts_accident_type_label_without_lookup_id()
    {
        var create = await _client.PostAsJsonAsync("/api/liens/cases/create", new
        {
            code = $"LEGACY-{Guid.CreateVersion7():N}"[..20],
            firstname = "Legacy",
            lastname = "AccidentType",
            accidentTypeId = "MedicalMalpractice",
            caseType = "Medical Malpractice",
        });

        create.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await create.Content.ReadAsStringAsync()}");

        var createBody = await create.Content.ReadFromJsonAsync<LegacyCreateCaseResponseBody>();
        createBody!.Data.Should().ContainKey("id");

        var caseId = createBody.Data["id"];
        var detail = await _client.GetAsync($"/api/liens/cases/{caseId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await detail.Content.ReadAsStringAsync()}");

        var detailBody = await detail.Content.ReadFromJsonAsync<JsonDocument>();
        var root = detailBody!.RootElement;
        root.GetProperty("accidentTypeId").GetString().Should().Be("MedicalMalpractice");
        root.GetProperty("accidentType").GetString().Should().Be("Medical Malpractice");
    }

    [Fact]
    public async Task LegacyBatchReassign_accepts_named_lawfirm_contact_type()
    {
        var oldLawFirmOrgId = Guid.Parse("30000000-0000-0000-0000-000000000101");
        var newLawFirmOrgId = Guid.Parse("30000000-0000-0000-0000-000000000102");

        Guid caseId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = Case.Create(
                SeedHelper.TenantId,
                oldLawFirmOrgId,
                $"CASE-{Guid.NewGuid():N}"[..20],
                "Batch",
                "Reassign",
                SeedHelper.UserId);

            caseId = caseEntity.Id;
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/liens/cases/batch-reassign", new
        {
            contactType = "LawFirm",
            oldId = oldLawFirmOrgId.ToString(),
            newId = newLawFirmOrgId.ToString(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var updatedCase = await verifyDb.Cases.FindAsync(caseId);

        updatedCase.Should().NotBeNull();
        updatedCase!.OrgId.Should().Be(newLawFirmOrgId);
    }

    private sealed class CaseResponseBody
    {
        public string CaseNumber { get; init; } = string.Empty;
    }

    private sealed class LegacyCreateCaseResponseBody
    {
        public Dictionary<string, string> Data { get; init; } = [];
    }

    private sealed class CaseDetailResponseBody
    {
        public string? ClientEmail { get; init; }
        public string? ClientPhone { get; init; }
    }
}
