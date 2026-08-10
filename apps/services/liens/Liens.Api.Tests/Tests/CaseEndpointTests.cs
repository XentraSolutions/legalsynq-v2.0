using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

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
    public async Task DashboardTaskSummary_returns_the_legacy_assignee_scoped_envelope()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var prefix = $"DASH-{Guid.NewGuid():N}"[..20];

            db.ServicingItems.AddRange(
                CreateLegacyDashboardTask($"{prefix}-UP", "1", "Upcoming task"),
                CreateLegacyDashboardTask($"{prefix}-IP", "2", "In progress task"),
                CreateLegacyDashboardTask($"{prefix}-IR", "3", "In review task"),
                CreateLegacyDashboardTask($"{prefix}-CO", "4", "Completed task"),
                CreateLegacyDashboardTask($"{prefix}-CA", "CANCELLED", "Cancelled task"),
                ServicingItem.Create(
                    SeedHelper.TenantId,
                    SeedHelper.OrgId,
                    $"{prefix}-OTHER",
                    "LegacyCaseTask",
                    "Another user's task",
                    "another-user",
                    SeedHelper.UserId,
                    caseId: SeedHelper.CaseId,
                    notes: "title=Excluded task; status=1"));
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/liens/cases/dashboard/task-summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        root.GetProperty("message").GetString().Should().Be("Successfully retrieved all tasks.");

        var data = root.GetProperty("data");
        data.GetProperty("totalTasks").GetInt32().Should().Be(5);
        data.GetProperty("upcomingTasks").GetInt32().Should().Be(1);
        data.GetProperty("inProgressTasks").GetInt32().Should().Be(1);
        data.GetProperty("inReviewTasks").GetInt32().Should().Be(1);
        data.GetProperty("completedTasks").GetInt32().Should().Be(1);

        var completed = data.GetProperty("tasks").EnumerateArray()
            .Single(task => task.GetProperty("title").GetString() == "Completed task");
        completed.GetProperty("caseId").GetString().Should().Be(SeedHelper.CaseId.ToString());
        completed.GetProperty("caseCode").GetString().Should().NotBeNullOrEmpty();
        completed.GetProperty("caseName").GetString().Should().NotBeNullOrEmpty();
        completed.GetProperty("status").GetString().Should().Be("COMPLETED");
        completed.GetProperty("statusId").GetString().Should().Be("4");
        completed.GetProperty("priority").GetString().Should().Be("Normal");
        completed.GetProperty("priorityId").GetString().Should().Be("Normal");

        var cancelled = data.GetProperty("tasks").EnumerateArray()
            .Single(task => task.GetProperty("title").GetString() == "Cancelled task");
        cancelled.GetProperty("status").GetString().Should().Be("CANCELLED");
        cancelled.GetProperty("statusId").GetString().Should().Be("CANCELLED");
    }

    [Fact]
    public async Task DashboardTaskSummary_counts_mixed_legacy_and_ui_status_values()
    {
        var dashboardUserId = Guid.CreateVersion7();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var prefix = $"MIXED-{Guid.CreateVersion7():N}"[..20];

            db.ServicingItems.AddRange(
                CreateLegacyDashboardTask($"{prefix}-UP1", "Upcoming", "Upcoming display task", dashboardUserId),
                CreateLegacyDashboardTask($"{prefix}-IP1", "In Progress", "In progress display task", dashboardUserId),
                CreateLegacyDashboardTask($"{prefix}-UP2", "UPCOMING", "Upcoming UI-code task", dashboardUserId),
                CreateLegacyDashboardTask($"{prefix}-IP2", "INPROGRESS", "In progress UI-code task", dashboardUserId),
                CreateLegacyDashboardTask($"{prefix}-IR1", "In Review", "In review display task", dashboardUserId));
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",
                JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, dashboardUserId));

        var response = await client.GetAsync("/api/liens/cases/dashboard/task-summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");
        data.GetProperty("totalTasks").GetInt32().Should().Be(5);
        data.GetProperty("upcomingTasks").GetInt32().Should().Be(2);
        data.GetProperty("inProgressTasks").GetInt32().Should().Be(2);
        data.GetProperty("inReviewTasks").GetInt32().Should().Be(1);
        data.GetProperty("completedTasks").GetInt32().Should().Be(0);

        var tasks = data.GetProperty("tasks").EnumerateArray().ToList();
        tasks.Single(task => task.GetProperty("title").GetString() == "Upcoming display task")
            .GetProperty("status").GetString().Should().Be("UPCOMING");
        tasks.Single(task => task.GetProperty("title").GetString() == "In progress display task")
            .GetProperty("status").GetString().Should().Be("INPROGRESS");
        var uiCodeTask = tasks.Single(task =>
            task.GetProperty("title").GetString() == "Upcoming UI-code task");
        uiCodeTask.GetProperty("status").GetString().Should().Be("UPCOMING");
        uiCodeTask.GetProperty("statusId").GetString().Should().Be("UPCOMING");
        tasks.Single(task => task.GetProperty("title").GetString() == "In review display task")
            .GetProperty("status").GetString().Should().Be("INREVIEW");
    }

    private static ServicingItem CreateLegacyDashboardTask(
        string taskNumber,
        string status,
        string title,
        Guid? assignedToUserId = null) =>
        ServicingItem.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            taskNumber,
            "LegacyCaseTask",
            $"{title} description",
            (assignedToUserId ?? SeedHelper.UserId).ToString(),
            SeedHelper.UserId,
            caseId: SeedHelper.CaseId,
            assignedToUserId: assignedToUserId ?? SeedHelper.UserId,
            notes: $"title={title}; status={status}");

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

        var updates = await _client.PostAsJsonAsync("/api/liens/cases/case-updates/v3", new
        {
            caseId = firstBody.Id,
            page = 1,
            limit = 10,
        });
        updates.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await updates.Content.ReadAsStringAsync()}");

        var updatesBody = await updates.Content.ReadFromJsonAsync<JsonDocument>();
        var creationEntry = updatesBody!.RootElement.GetProperty("data")
            .EnumerateArray()
            .Single(item => item.GetProperty("action").GetString() == "Case Created");
        creationEntry.GetProperty("description").GetString()
            .Should().Contain($"Code: {firstBody.CaseNumber}; Client: Case One;");
        creationEntry.GetProperty("createdBy").GetString().Should().Be(SeedHelper.UserId.ToString());
        creationEntry.GetProperty("updatedBy").GetString().Should().Be(SeedHelper.UserId.ToString());

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

        var updates = await _client.PostAsJsonAsync("/api/liens/cases/case-updates/v3", new
        {
            caseId,
            page = 1,
            limit = 10,
        });
        updates.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await updates.Content.ReadAsStringAsync()}");

        var updatesBody = await updates.Content.ReadFromJsonAsync<JsonDocument>();
        updatesBody!.RootElement.GetProperty("data").EnumerateArray().Should().Contain(item =>
            item.GetProperty("action").GetString() == "Case Created" &&
            item.GetProperty("description").GetString()!.Contains("Client: Legacy Contact;", StringComparison.Ordinal));
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
    [InlineData("New", "New")]
    [InlineData("Processing", "Processing")]
    [InlineData("Pre-demand", CaseStatus.PreDemand)]
    [InlineData("Demand Sent", CaseStatus.DemandSent)]
    [InlineData("Negotiations", CaseStatus.InNegotiation)]
    [InlineData("Litigation", "Litigation")]
    [InlineData("Litigation(Pending)", "Litigation (Pending)")]
    [InlineData("Litigation (Pending)", "Litigation (Pending)")]
    [InlineData("Litigation(Open)", "Litigation (Open)")]
    [InlineData("Litigation (Open)", "Litigation (Open)")]
    [InlineData("Litigation(Close)", "Litigation (Closed)")]
    [InlineData("Litigation (Close)", "Litigation (Closed)")]
    [InlineData("Litigation(Closed)", "Litigation (Closed)")]
    [InlineData("Litigation (Closed)", "Litigation (Closed)")]
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

    [Theory]
    [InlineData("Litigation(Pending)", "Litigation (Pending)")]
    [InlineData("Litigation(Open)", "Litigation (Open)")]
    [InlineData("Litigation(Closed)", "Litigation (Closed)")]
    public async Task LegacyCreateCase_normalizes_litigation_variant_status_label(
        string legacyStatus,
        string expectedStatus)
    {
        var create = await _client.PostAsJsonAsync("/api/liens/cases/create", new
        {
            code = $"LEGACY-{Guid.CreateVersion7():N}"[..20],
            firstname = "Legacy",
            lastname = "Litigation",
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
        detailBody.RootElement.GetProperty("statusLabel").GetString().Should().Be(expectedStatus);
    }

    [Fact]
    public async Task LegacyCreateCase_persists_minor_comp_flag_and_returns_it_from_case_by_id()
    {
        var create = await _client.PostAsJsonAsync("/api/liens/cases/create", new
        {
            code = $"LEGACY-{Guid.CreateVersion7():N}"[..20],
            firstname = "Legacy",
            lastname = "MinorComp",
            minorComp = "true",
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
        detailBody!.RootElement.GetProperty("minorComp").GetString().Should().Be("Yes");
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

    [Fact]
    public async Task DeleteLien_keeps_rejected_lien_out_of_case_liens_response()
    {
        var lienId = Guid.Parse("70000000-0000-0000-0000-000000000777");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LIEN-DELETE-777",
                LienType.MedicalLien,
                2000m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                facilityId: SeedHelper.FacilityId);
            SetId(lien, lienId);

            db.Liens.Add(lien);
            await db.SaveChangesAsync();
        }

        var deleteResponse = await _client.DeleteAsync($"/api/liens/cases/liens/delete/{lienId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await deleteResponse.Content.ReadAsStringAsync()}");

        var listResponse = await _client.PostAsJsonAsync("/api/liens/cases/liens/v3", new
        {
            caseId = SeedHelper.CaseId,
            page = 1,
            limit = 50,
        });
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await listResponse.Content.ReadAsStringAsync()}");

        var listBody = await listResponse.Content.ReadFromJsonAsync<PaginatedLienResponseBody>();
        listBody.Should().NotBeNull();
        listBody!.Items.Should().NotContain(item => item.Id == lienId);
        listBody.TotalCount.Should().Be(listBody.Items.Count);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var storedLien = await verifyDb.Liens.SingleAsync(item => item.Id == lienId);
        storedLien.Status.Should().Be(LienStatus.Cancelled);
    }

    [Fact]
    public async Task DeleteLien_keeps_closed_lien_out_of_case_liens_response()
    {
        var lienId = Guid.Parse("70000000-0000-0000-0000-000000000778");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LIEN-DELETE-CLOSED-778",
                LienType.MedicalLien,
                2000m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                facilityId: SeedHelper.FacilityId);
            SetId(lien, lienId);
            lien.SetLegacyMedicalStatus("Closed", SeedHelper.UserId);

            db.Liens.Add(lien);
            await db.SaveChangesAsync();
        }

        var deleteResponse = await _client.DeleteAsync($"/api/liens/cases/liens/delete/{lienId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await deleteResponse.Content.ReadAsStringAsync()}");

        var listResponse = await _client.PostAsJsonAsync("/api/liens/cases/liens/v3", new
        {
            caseId = SeedHelper.CaseId,
            page = 1,
            limit = 50,
        });
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await listResponse.Content.ReadAsStringAsync()}");

        var listBody = await listResponse.Content.ReadFromJsonAsync<PaginatedLienResponseBody>();
        listBody.Should().NotBeNull();
        listBody!.Items.Should().NotContain(item => item.Id == lienId);
        listBody.TotalCount.Should().Be(listBody.Items.Count);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var storedLien = await verifyDb.Liens.SingleAsync(item => item.Id == lienId);
        storedLien.Status.Should().Be(LienStatus.Cancelled);
    }

    [Fact]
    public async Task Lien_updates_keep_closed_and_delete_as_separate_history_entries()
    {
        Guid lienId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LIEN-HISTORY-STATUS-001",
                LienType.MedicalLien,
                1000m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                facilityId: SeedHelper.FacilityId);
            db.Liens.Add(lien);
            await db.SaveChangesAsync();
            lienId = lien.Id;
        }

        var closeResponse = await _client.PatchAsJsonAsync("/service/liens/update/status", new
        {
            liensId = lienId,
            statusId = "Closed",
        });
        closeResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await closeResponse.Content.ReadAsStringAsync()}");

        var deleteResponse = await _client.DeleteAsync($"/api/liens/cases/liens/delete/{lienId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await deleteResponse.Content.ReadAsStringAsync()}");

        var updatesResponse = await _client.PostAsJsonAsync("/api/liens/cases/liens-updates/v3", new
        {
            caseId = SeedHelper.CaseId,
            page = 1,
            limit = 10,
        });
        updatesResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await updatesResponse.Content.ReadAsStringAsync()}");

        using var updates = JsonDocument.Parse(await updatesResponse.Content.ReadAsStringAsync());
        var descriptions = updates.RootElement.GetProperty("data")
            .EnumerateArray()
            .Where(item => item.GetProperty("lienId").GetString() == lienId.ToString())
            .Select(item => item.GetProperty("description").GetString())
            .ToList();

        descriptions.Should().Contain("Lien status updated to Closed.");
        descriptions.Should().Contain("Lien status updated to Delete.");
        updates.RootElement.GetProperty("data").EnumerateArray()
            .Where(item => item.GetProperty("lienId").GetString() == lienId.ToString())
            .Select(item => item.GetProperty("updatedBy").GetString())
            .Should().OnlyContain(name => name == "Demo User");
    }

    [Fact]
    public async Task SearchLiensV3_includes_status_field_in_json_response()
    {
        var response = await _client.PostAsJsonAsync("/api/liens/cases/liens/v3", new
        {
            caseId = SeedHelper.CaseId,
            page = 1,
            limit = 50,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var firstItem = doc.RootElement.GetProperty("items").EnumerateArray().First();

        firstItem.TryGetProperty("status", out var statusProperty).Should().BeTrue();
        statusProperty.GetString().Should().NotBeNullOrWhiteSpace();
    }

    private sealed class CaseResponseBody
    {
        public Guid Id { get; init; }
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

    private sealed class PaginatedLienResponseBody
    {
        public List<LienListItemBody> Items { get; init; } = [];
        public int TotalCount { get; init; }
    }

    private sealed class LienListItemBody
    {
        public Guid Id { get; init; }
    }

    private static void SetId<T>(T entity, Guid id) where T : class
    {
        var prop = typeof(T).GetProperty("Id",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(entity, id);
    }
}
