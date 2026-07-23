using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class LegacyBatchUploadEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public LegacyBatchUploadEndpointTests(LiensApiFactory factory) => _factory = factory;

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
    public async Task Create_list_process_and_delete_batch_upload_work()
    {
        const string csv = "Case Code*,Case Status*\nCASE-TEST-001,Open\n";

        var createResponse = await _client.PostAsJsonAsync("/Batch/create", new
        {
            label = "Case tracking import",
            template = "UPDATE_CASE_TRACKING_STATUS",
            caseId = SeedHelper.CaseId,
            file = "tracking.csv",
            date = "06/29/2026",
            rows = 1,
            dataContext = csv,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await createResponse.Content.ReadAsStringAsync()}");

        var createBody = JsonNode.Parse(await createResponse.Content.ReadAsStringAsync())!;
        var batchId = Guid.Parse(createBody["id"]!.GetValue<string>());
        createBody["data"]!.AsArray().Count.Should().Be(1);

        var getResponse = await _client.GetAsync($"/Batch/list/{batchId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await getResponse.Content.ReadAsStringAsync()}");

        var listResponse = await _client.PostAsJsonAsync("/Batch/list", new
        {
            page = 1,
            limit = 10,
            keyword = "tracking",
        });
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await listResponse.Content.ReadAsStringAsync()}");

        var dataContextResponse = await _client.PostAsJsonAsync("/Batch/data-context", new
        {
            id = batchId,
            page = 1,
            limit = 10,
        });
        dataContextResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await dataContextResponse.Content.ReadAsStringAsync()}");

        var processResponse = await _client.PostAsJsonAsync("/Batch/process", new
        {
            batchUploadId = batchId,
            templateId = "UPDATE_CASE_TRACKING_STATUS",
            caseId = SeedHelper.CaseId,
        });
        processResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await processResponse.Content.ReadAsStringAsync()}");

        var detailsResponse = await _client.GetAsync($"/Batch/details/{batchId}");
        detailsResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await detailsResponse.Content.ReadAsStringAsync()}");

        var detailsBody = JsonNode.Parse(await detailsResponse.Content.ReadAsStringAsync())!;
        detailsBody["successCount"]!.GetValue<int>().Should().Be(1);

        var deleteResponse = await _client.DeleteAsync($"/Batch/delete/{batchId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await deleteResponse.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task Upload_and_download_template_work()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Lien import"), "label");
        form.Add(new StringContent("ADD_LIENS_EXISTING_CASE"), "template");
        form.Add(new StringContent(SeedHelper.CaseId.ToString()), "caseId");
        form.Add(new StringContent("06/29/2026"), "date");
        var file = new ByteArrayContent("Case Code*,Lien Status*\nCASE-TEST-001,Active\n"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "liens.csv");

        var uploadResponse = await _client.PostAsync("/Batch/Upload", form);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await uploadResponse.Content.ReadAsStringAsync()}");

        var downloadResponse = await _client.GetAsync("/Batch/download-template/ADD_LIENS_EXISTING_CASE");
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await downloadResponse.Content.ReadAsStringAsync()}");

        var body = JsonNode.Parse(await downloadResponse.Content.ReadAsStringAsync())!;
        var downloadItem = body["data"]!.AsArray()[0]!;
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(downloadItem["base64"]!.GetValue<string>()));
        csv.Should().Be("Case Code*,Lien Status*,Purchase Date*,Initial Service Date*,End Service Date,Notes,Is Bulk,Funding Company,Facility Name*,Contact Person,Facility Email Address,Medical Provider Name,Medical Code & Description*,Medicare Cost,Billing Amount*,Purchase Amount*,Payee,Outbound Check Number,Document Type*,Attachment");
    }

    [Fact]
    public async Task Download_template_reconciles_an_existing_lien_template_header()
    {
        var seededResponse = await _client.GetAsync("/Batch/download-template/ADD_LIENS_EXISTING_CASE");
        seededResponse.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var template = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
                db.BatchTemplates.Where(item =>
                    item.Code == "ADD_LIENS_EXISTING_CASE" && item.IsSystem && item.TenantId == null));
            template.UpdateSystemDefinition("Add Liens To Existing Case", "Case Code*|Lien Status*", SeedHelper.UserId);
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/Batch/download-template/ADD_LIENS_EXISTING_CASE");
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var downloadItem = body["data"]!.AsArray()[0]!;
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(downloadItem["base64"]!.GetValue<string>()));
        csv.Should().Be("Case Code*,Lien Status*,Purchase Date*,Initial Service Date*,End Service Date,Notes,Is Bulk,Funding Company,Facility Name*,Contact Person,Facility Email Address,Medical Provider Name,Medical Code & Description*,Medicare Cost,Billing Amount*,Purchase Amount*,Payee,Outbound Check Number,Document Type*,Attachment");
    }

    [Fact]
    public async Task Delete_detail_marks_detail_deleted()
    {
        const string csv = "Case Code*,Case Status*\nCASE-TEST-001,Open\n";
        var createResponse = await _client.PostAsJsonAsync("/Batch/create", new
        {
            label = "Delete detail",
            template = "UPDATE_CASE_TRACKING_STATUS",
            caseId = SeedHelper.CaseId,
            file = "detail.csv",
            date = "06/29/2026",
            rows = 1,
            dataContext = csv,
        });
        createResponse.EnsureSuccessStatusCode();
        var createBody = JsonNode.Parse(await createResponse.Content.ReadAsStringAsync())!;
        var batchId = Guid.Parse(createBody["id"]!.GetValue<string>());

        var detailsResponse = await _client.GetAsync($"/Batch/details/{batchId}");
        detailsResponse.EnsureSuccessStatusCode();
        var detailsBody = JsonNode.Parse(await detailsResponse.Content.ReadAsStringAsync())!;
        var detailId = Guid.Parse(detailsBody["data"]![0]!["id"]!.GetValue<string>());

        var deleteResponse = await _client.DeleteAsync($"/Batch/details/delete/{detailId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await deleteResponse.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task Update_batch_upload_updates_label_and_template()
    {
        const string csv = "Case Code*,Case Status*\nCASE-TEST-001,Open\n";
        var createResponse = await _client.PostAsJsonAsync("/Batch/create", new
        {
            label = "Original label",
            template = "UPDATE_CASE_TRACKING_STATUS",
            caseId = SeedHelper.CaseId,
            file = "original.csv",
            date = "06/29/2026",
            rows = 1,
            dataContext = csv,
        });
        createResponse.EnsureSuccessStatusCode();
        var createBody = JsonNode.Parse(await createResponse.Content.ReadAsStringAsync())!;
        var batchId = Guid.Parse(createBody["id"]!.GetValue<string>());

        var updateResponse = await _client.PostAsJsonAsync("/Batch/update", new
        {
            id = batchId,
            label = "Updated label",
            template = "ADD_LIENS_EXISTING_CASE",
            caseId = SeedHelper.CaseId,
            file = "updated.csv",
            date = "06/30/2026",
            rows = 1,
            dataContext = "Case Code*,Lien Status*\nCASE-TEST-001,Active\n",
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await updateResponse.Content.ReadAsStringAsync()}");

        var getResponse = await _client.GetAsync($"/Batch/list/{batchId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await getResponse.Content.ReadAsStringAsync()}");

        var getBody = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync())!;
        var item = getBody["data"]![0]!;
        item["label"]!.GetValue<string>().Should().Be("Updated label");
        item["template"]!.GetValue<string>().Should().Be("ADD_LIENS_EXISTING_CASE");
        item["file"]!.GetValue<string>().Should().Be("updated.csv");
    }

    [Fact]
    public async Task CreateBatchUpload_initial_case_import_creates_case_and_returns_import_counts()
    {
        var externalRef = $"BATCH-{Guid.NewGuid():N}"[..20];
        var csv = $"First Name*,Last Name*,Date of Birth*,Address,City,State,Zip Code,Is Servicing*,Case Status*,Accident Type*,Accident State*,Date of Loss,Law Firm*,Case Manager,Notes\nMaria,Lopez,01/02/1990,123 Main St,Austin,TX,78701,Yes,Open,Motor Vehicle,TX,06/01/2026,Firm A,Manager A,{externalRef}\n";

        var createResponse = await _client.PostAsJsonAsync("/Batch/create", new
        {
            label = "Initial case import",
            template = "INITIAL_CASE_IMPORT",
            file = "initial-case-import.csv",
            date = "06/29/2026",
            rows = 1,
            dataContext = csv,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await createResponse.Content.ReadAsStringAsync()}");

        var createBody = JsonNode.Parse(await createResponse.Content.ReadAsStringAsync())!;
        createBody["importedCount"]!.GetValue<int>().Should().Be(1);
        createBody["createdCount"]!.GetValue<int>().Should().Be(1);
        createBody["updatedCount"]!.GetValue<int>().Should().Be(0);
        createBody["failedCount"]!.GetValue<int>().Should().Be(0);

        var listResponse = await _client.PostAsJsonAsync("/api/liens/cases/v3", new
        {
            page = 1,
            limit = 50,
        });
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await listResponse.Content.ReadAsStringAsync()}");

        var listBody = JsonNode.Parse(await listResponse.Content.ReadAsStringAsync())!;
        listBody["data"]!.AsArray().Should().Contain(item =>
            item!["notes"]!.GetValue<string>() == externalRef);
    }

    [Fact]
    public async Task Process_initial_case_import_allows_empty_case_id()
    {
        var externalCaseId = $"EXT-PROCESS-{Guid.NewGuid():N}"[..20];
        var csv = $"Case Code*,First Name*,Last Name*,Date Of Loss,Date Of Birth,Address,City,State,Zipcode,Note,External Case Id\n,Maria,Lopez,06/01/2026,01/02/1990,123 Main St,Austin,TX,78701,Imported from batch,{externalCaseId}\n";

        var createResponse = await _client.PostAsJsonAsync("/Batch/create", new
        {
            label = "Initial case import process check",
            template = "INITIAL_CASE_IMPORT",
            caseId = "",
            file = "initial-case-import.csv",
            date = "07/11/2026",
            rows = 1,
            dataContext = csv,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await createResponse.Content.ReadAsStringAsync()}");

        var createBody = JsonNode.Parse(await createResponse.Content.ReadAsStringAsync())!;
        createBody["importedCount"]!.GetValue<int>().Should().Be(1);
        createBody["failedCount"]!.GetValue<int>().Should().Be(0);
        var batchId = createBody["id"]!.GetValue<string>();

        var processResponse = await _client.PostAsJsonAsync("/Batch/process", new
        {
            batchUploadId = batchId,
            templateId = "INITIAL_CASE_IMPORT",
            caseId = "",
        });
        processResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await processResponse.Content.ReadAsStringAsync()}");

        var processBody = JsonNode.Parse(await processResponse.Content.ReadAsStringAsync())!;
        processBody["successCount"]!.GetValue<int>().Should().Be(1);
        processBody["failedCount"]!.GetValue<int>().Should().Be(0);

        var listResponse = await _client.PostAsJsonAsync("/api/liens/cases/v3", new
        {
            page = 1,
            limit = 10,
            keyword = externalCaseId,
        });
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await listResponse.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task Download_template_returns_updated_payment_header()
    {
        var response = await _client.GetAsync("/Batch/download-template/ADD_PAYMENTS_EXISTING_LIENS");
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var downloadItem = body["data"]!.AsArray()[0]!;
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(downloadItem["base64"]!.GetValue<string>()));
        csv.Should().Be("Lien Code*,Lien Status*,Amount to Settle,Check Amount*,Check Received*,Check Number*,Settlement Type*,Settlement Status,Notes");
    }

    [Fact]
    public async Task Download_template_reconciles_an_existing_payment_template_header()
    {
        var seededResponse = await _client.GetAsync("/Batch/download-template/ADD_PAYMENTS_EXISTING_LIENS");
        seededResponse.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var template = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
                db.BatchTemplates.Where(item =>
                    item.Code == "ADD_PAYMENTS_EXISTING_LIENS" && item.IsSystem && item.TenantId == null));
            template.UpdateSystemDefinition("Add Payments To Existing Liens", "Lien Code*|Amount*", SeedHelper.UserId);
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/Batch/download-template/ADD_PAYMENTS_EXISTING_LIENS");
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var downloadItem = body["data"]!.AsArray()[0]!;
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(downloadItem["base64"]!.GetValue<string>()));
        csv.Should().Be("Lien Code*,Lien Status*,Amount to Settle,Check Amount*,Check Received*,Check Number*,Settlement Type*,Settlement Status,Notes");
    }

    [Fact]
    public async Task Download_template_reconciles_an_existing_case_tracking_template_header()
    {
        var seededResponse = await _client.GetAsync("/Batch/download-template/UPDATE_CASE_TRACKING_STATUS");
        seededResponse.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var template = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
                db.BatchTemplates.Where(item =>
                    item.Code == "UPDATE_CASE_TRACKING_STATUS" && item.IsSystem && item.TenantId == null));
            template.UpdateSystemDefinition("Update Case Tracking Status", "Case Code*|Current Status*", SeedHelper.UserId);
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/Batch/download-template/UPDATE_CASE_TRACKING_STATUS");
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var downloadItem = body["data"]!.AsArray()[0]!;
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(downloadItem["base64"]!.GetValue<string>()));
        csv.Should().Be("Case Code*,Current Status*,Current Medical Status,Case Type*,State of Incident*,Lead,Date of Loss,Notes");
    }

    [Fact]
    public async Task Process_update_case_tracking_status_supports_new_template_headers()
    {
        var createResponse = await _client.PostAsJsonAsync("/Batch/create", new
        {
            label = "Case tracking import",
            template = "UPDATE_CASE_TRACKING_STATUS",
            caseId = SeedHelper.CaseId,
            file = "tracking.csv",
            date = "07/20/2026",
            rows = 1,
            dataContext = "Case Code*,Current Status*,Current Medical Status,Case Type*,State of Incident*,Lead,Date of Loss,Notes\nCASE-TEST-001,Open,Recovering,PI,TX,,06/01/2026,Updated from new template\n",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await createResponse.Content.ReadAsStringAsync()}");

        var createBody = JsonNode.Parse(await createResponse.Content.ReadAsStringAsync())!;
        createBody["failedCount"]!.GetValue<int>().Should().Be(0);
        var batchId = createBody["id"]!.GetValue<string>();

        var processResponse = await _client.PostAsJsonAsync("/Batch/process", new
        {
            batchUploadId = batchId,
            templateId = "UPDATE_CASE_TRACKING_STATUS",
            caseId = SeedHelper.CaseId,
        });
        processResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await processResponse.Content.ReadAsStringAsync()}");

        var processBody = JsonNode.Parse(await processResponse.Content.ReadAsStringAsync())!;
        processBody["successCount"]!.GetValue<int>().Should().Be(1);
        processBody["failedCount"]!.GetValue<int>().Should().Be(0);
    }
}
