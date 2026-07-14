using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Liens.Api.Tests.Helpers;
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
        body["data"]!.AsArray().Count.Should().Be(1);
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
}
