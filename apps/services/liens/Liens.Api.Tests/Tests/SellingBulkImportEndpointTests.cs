using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class SellingBulkImportEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public SellingBulkImportEndpointTests(LiensApiFactory factory) => _factory = factory;

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
    public async Task Download_template_returns_csv_with_required_headers_and_example_row()
    {
        var response = await _client.GetAsync("/api/liens/selling/bulk-import-template");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("selling-lien-import-template.csv");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Case Code*");
        content.Should().Contain("Billing Amount*");
        content.Should().Contain("CASE-10001");
    }

    [Fact]
    public async Task Create_bulk_import_stages_csv_rows_with_the_requested_defaults()
    {
        const string csv = "Case Code*,Facility Name*,Billing Amount*\r\nCASE-10001,Example Medical Center,250.00\r\n";
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "selling-lien-import.csv");
        form.Add(new StringContent("SellingLienImport"), "templateType");
        form.Add(new StringContent("Private"), "defaultListingVisibility");
        form.Add(new StringContent("Pending"), "defaultSellerStatus");

        var response = await _client.PostAsync("/api/liens/selling/bulk-imports", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var importId = document.RootElement.GetProperty("importId").GetGuid();
        document.RootElement.GetProperty("status").GetString().Should().Be("Uploaded");
        document.RootElement.GetProperty("totalRows").GetInt32().Should().Be(1);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var batch = await db.BatchUploads.FindAsync(importId);
        batch.Should().NotBeNull();
        batch!.TenantId.Should().Be(SeedHelper.TenantId);
        batch.Template.Should().Be("SellingLienImport");
        batch.ProcessStatus.Should().Be("UPLOADED");
        batch.Rows.Should().Be(1);

        var detail = db.BatchUploadDetails.Single(item => item.BatchUploadId == importId);
        detail.DataJson.Should().Contain("\"Listing Visibility\":\"Private\"");
        detail.DataJson.Should().Contain("\"Seller Status\":\"Pending\"");
    }

    [Fact]
    public async Task Create_bulk_import_rejects_an_unknown_template_type()
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes("Case Code*\r\nCASE-10001\r\n"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "selling-lien-import.csv");
        form.Add(new StringContent("UnknownTemplate"), "templateType");

        var response = await _client.PostAsync("/api/liens/selling/bulk-imports", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("unsupported_template_type");
    }
}
