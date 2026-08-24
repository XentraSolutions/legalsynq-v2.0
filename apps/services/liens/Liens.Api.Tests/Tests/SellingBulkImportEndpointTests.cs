using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
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
        content.Split('\n')[0].TrimEnd('\r').Should().Be(
            "Case Code*,Lien Status,Listing Visibility,Purchase Date,Initial Service Date*,End Service Date,Lien Notes,Funding Company,Facility Name*,Medical Provider,Medical Code & Description*,Medicare Cost,Billing Amount*,Target Ask Amount");
        content.Should().NotContain("Document Type*");
        content.Should().NotContain("Attachment");
        content.Should().Contain("CASE-10001");
    }

    [Fact]
    public async Task Create_bulk_import_stages_csv_rows_with_the_requested_defaults()
    {
        const string csv = "Case Code*,Lien Status*,Facility Name*,Billing Amount*\r\nCASE-10001,Internal,Example Medical Center,250.00\r\n";
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
        detail.DataJson.Should().Contain("\"Lien Status*\":\"Internal\"");
        detail.DataJson.Should().NotContain("\"Lien Status\":\"Pending\"");
    }

    [Fact]
    public async Task Confirm_bulk_import_maps_the_canonical_template_to_lien_details()
    {
        const string csv = "Case Code*,Lien Status,Listing Visibility,Purchase Date,Initial Service Date*,End Service Date,Lien Notes,Funding Company,Facility Name*,Medical Provider,Medical Code & Description*,Medicare Cost,Billing Amount*,Target Ask Amount\r\nCASE-MAPPED-001,Internal,Public,2026-07-15,2026-07-19,2026-07-22,Imported detail notes,Capital Fund LLC,Sunrise Clinic,City Medical Center,45385 - Colonoscopy,879.00,250.00,175.00\r\n";
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "selling-lien-import.csv");
        form.Add(new StringContent("SellingLienImport"), "templateType");

        var upload = await _client.PostAsync("/api/liens/selling/bulk-imports", form);
        upload.EnsureSuccessStatusCode();
        using var uploadJson = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var importId = uploadJson.RootElement.GetProperty("importId").GetGuid();

        (await _client.PostAsync($"/api/liens/selling/bulk-imports/{importId}/validate", null)).EnsureSuccessStatusCode();
        using var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/bulk-imports/{importId}/confirm");
        confirm.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        (await _client.SendAsync(confirm)).EnsureSuccessStatusCode();

        Guid lienId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseId = db.Cases.Single(item => item.CaseNumber == "CASE-MAPPED-001").Id;
            var lien = db.Liens.Single(item => item.CaseId == caseId);
            lienId = lien.Id;
            lien.SellerStatus.Should().Be("Internal");
            lien.ListingVisibility.Should().Be("Public");
            lien.PurchaseDate.Should().Be(new DateOnly(2026, 7, 15));
            lien.InitialServiceDate.Should().Be(new DateOnly(2026, 7, 19));
            lien.EndServiceDate.Should().Be(new DateOnly(2026, 7, 22));
            lien.Notes.Should().Be("Imported detail notes");
            lien.AskAmount.Should().Be(175m);
        }

        var detailResponse = await _client.GetAsync($"/api/liens/selling/liens/{lienId}");
        detailResponse.EnsureSuccessStatusCode();
        using var detail = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        detail.RootElement.GetProperty("lienInformation").GetProperty("purchaseDate").GetString().Should().Be("2026-07-15");
        detail.RootElement.GetProperty("caseInformation").GetProperty("caseNumber").GetString().Should().Be("CASE-MAPPED-001");
        detail.RootElement.GetProperty("facility").GetProperty("name").GetString().Should().Be("Sunrise Clinic");
        detail.RootElement.GetProperty("medicalProvider").GetProperty("name").GetString().Should().Be("City Medical Center");
        detail.RootElement.GetProperty("medicalPricing").GetProperty("askAmount").GetDecimal().Should().Be(175m);
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

    [Fact]
    public async Task Confirm_bulk_import_creates_valid_rows()
    {
        const string csv = "Case Code*,Initial Service Date*,Funding Company,Facility Name*,Medical Provider Name,Medical Code & Description*,Medicare Cost,Billing Amount*,Purchase Amount*\r\nCASE-TEST-001,2026-07-19,Capital Fund LLC,Sunrise Clinic,City Medical Center,45385 - Colonoscopy,879.00,250.00,175.00\r\n";
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "selling-lien-import.csv");
        form.Add(new StringContent("SellingLienImport"), "templateType");

        var upload = await _client.PostAsync("/api/liens/selling/bulk-imports", form);
        upload.EnsureSuccessStatusCode();
        using var uploadJson = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var importId = uploadJson.RootElement.GetProperty("importId").GetGuid();

        (await _client.PostAsync($"/api/liens/selling/bulk-imports/{importId}/validate", null)).EnsureSuccessStatusCode();
        using var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/bulk-imports/{importId}/confirm");
        confirm.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        var confirmed = await _client.SendAsync(confirm);
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());

        using var json = JsonDocument.Parse(await confirmed.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("status").GetString().Should().Be("CONFIRMED");
        json.RootElement.GetProperty("createdCount").GetInt32().Should().Be(1);
        json.RootElement.GetProperty("failedCount").GetInt32().Should().Be(0);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        db.BatchUploadDetails.Single(row => row.BatchUploadId == importId).Status.Should().Be("CREATED");
        var lien = db.Liens.Single(item => item.FundingCompanyId == SeedHelper.FundingCompanyId);
        lien.FacilityId.Should().Be(SeedHelper.FacilityId);
        db.ServicingItems.Should().Contain(item => item.LienId == lien.Id && item.TaskType == "SellingMedicalPricing" && item.Description == "45385");
        db.ServicingItems.Should().Contain(item => item.LienId == lien.Id && item.TaskType == "LegacyMedicalCode" && item.Notes!.Contains("code=45385"));
        db.ServicingItems.Should().Contain(item => item.LienId == lien.Id && item.TaskType == "LegacyMedicalFacilityInfo" && item.Notes!.Contains($"medicalProviderId={SeedHelper.MedicalProviderId}"));

        var revalidate = await _client.PostAsync($"/api/liens/selling/bulk-imports/{importId}/validate", null);
        revalidate.StatusCode.Should().Be(HttpStatusCode.Conflict, await revalidate.Content.ReadAsStringAsync());
        (await revalidate.Content.ReadAsStringAsync()).Should().Contain("import_already_confirmed");
    }

    [Fact]
    public async Task Confirm_bulk_import_marks_a_failed_row_without_duplicating_created_rows_on_retry()
    {
        var importId = Guid.CreateVersion7();
        var validValues = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Case Code*"] = "CASE-10001", ["Initial Service Date*"] = "2026-07-19", ["Facility Name*"] = "Sunrise Clinic", ["Medical Code & Description*"] = "99213 - Office visit", ["Billing Amount*"] = "250.00",
        });
        var invalidValues = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Case Code*"] = "CASE-10002", ["Initial Service Date*"] = "2026-07-19", ["Billing Amount*"] = "-1.00",
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var liensBefore = db.Liens.Count();
            var batch = BatchUpload.Create(SeedHelper.TenantId, SeedHelper.UserId, "retry test", "SellingLienImport", "retry.csv", 2, "{}");
            SetId(batch, importId);
            var validRow = BatchUploadDetail.Create(SeedHelper.TenantId, importId, 2, validValues, SeedHelper.UserId);
            validRow.SetResult("VALID", null, SeedHelper.UserId);
            var invalidRow = BatchUploadDetail.Create(SeedHelper.TenantId, importId, 3, invalidValues, SeedHelper.UserId);
            invalidRow.SetResult("VALID", null, SeedHelper.UserId);
            db.BatchUploads.Add(batch);
            db.BatchUploadDetails.AddRange(validRow, invalidRow);
            await db.SaveChangesAsync();

            using var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/bulk-imports/{importId}/confirm");
            confirm.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
            var confirmed = await _client.SendAsync(confirm);
            confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());
            using var json = JsonDocument.Parse(await confirmed.Content.ReadAsStringAsync());
            json.RootElement.GetProperty("status").GetString().Should().Be("PARTIAL");
            json.RootElement.GetProperty("createdCount").GetInt32().Should().Be(1);
            json.RootElement.GetProperty("failedCount").GetInt32().Should().Be(1);

            db.ChangeTracker.Clear();
            db.BatchUploadDetails.Where(row => row.BatchUploadId == importId && row.Status == "CREATED").Should().ContainSingle();
            db.BatchUploadDetails.Where(row => row.BatchUploadId == importId && row.Status == "FAILED").Should().ContainSingle();
            db.Liens.Count().Should().Be(liensBefore + 1);
        }

        using var retry = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/bulk-imports/{importId}/confirm");
        retry.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        (await _client.SendAsync(retry)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Confirm_bulk_import_rejects_rows_marked_invalid_by_validation()
    {
        const string csv = "Case Code*,Initial Service Date*,Facility Name*,Medical Code & Description*,Billing Amount*\r\nCASE-10001,2026-07-19,,45385 - Colonoscopy,250.00\r\n";
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "selling-lien-import.csv");
        form.Add(new StringContent("SellingLienImport"), "templateType");
        var upload = await _client.PostAsync("/api/liens/selling/bulk-imports", form);
        upload.EnsureSuccessStatusCode();
        using var uploadJson = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var importId = uploadJson.RootElement.GetProperty("importId").GetGuid();

        (await _client.PostAsync($"/api/liens/selling/bulk-imports/{importId}/validate", null)).EnsureSuccessStatusCode();
        using var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/bulk-imports/{importId}/confirm");
        confirm.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        var response = await _client.SendAsync(confirm);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).Should().Contain("Correct invalid rows before confirming");
    }

    [Fact]
    public async Task Confirm_bulk_import_preserves_unmatched_lookup_names()
    {
        const string csv = "Case Code*,Initial Service Date*,Funding Company,Facility Name*,Medical Provider Name,Medical Code & Description*,Billing Amount*\r\nCASE-10001,2026-07-31,Demo Funding Company,Demo Medical Facility,Demo Medical Provider,45385 - Colonoscopy,879.00\r\n";
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "selling-lien-import.csv");
        form.Add(new StringContent("SellingLienImport"), "templateType");
        var upload = await _client.PostAsync("/api/liens/selling/bulk-imports", form);
        upload.EnsureSuccessStatusCode();
        using var uploadJson = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var importId = uploadJson.RootElement.GetProperty("importId").GetGuid();

        (await _client.PostAsync($"/api/liens/selling/bulk-imports/{importId}/validate", null)).EnsureSuccessStatusCode();
        using var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/bulk-imports/{importId}/confirm");
        confirm.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        var confirmed = await _client.SendAsync(confirm);
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var lien = db.Liens.Single(item => item.ExternalReference == "Demo Funding Company");
        lien.FundingCompanyId.Should().BeNull();
        lien.FacilityId.Should().BeNull();
        db.ServicingItems.Should().Contain(item => item.LienId == lien.Id && item.TaskType == "SellingMedicalPricing" && item.Description == "45385");
        db.ServicingItems.Should().Contain(item => item.LienId == lien.Id && item.TaskType == "LegacyMedicalFacilityInfo" &&
            item.Notes!.Contains("facilityName=Demo Medical Facility") && item.Notes.Contains("medicalProvider=Demo Medical Provider"));
    }

    [Fact]
    public async Task Confirm_bulk_import_creates_every_valid_csv_row()
    {
        const string csv = "Case Code*,Initial Service Date*,Funding Company,Facility Name*,Medical Provider Name,Medical Code & Description*,Billing Amount*\r\nCASE-10002,1/6/26,Example Funding Co.,Example Medical Center,Example Medical Center,99213 - Office visit,305\r\nCASE-10002,1/7/26,Nae Funding,Example Medical Center,Example Medical Center,99213 - Office visit,300\r\nCASE-10002,1/6/26,Example Funding Co.,Example Medical Center,Example Medical Center,99213 - Office visit,250\r\n";
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "selling-lien-import.csv");
        form.Add(new StringContent("SellingLienImport"), "templateType");
        var upload = await _client.PostAsync("/api/liens/selling/bulk-imports", form);
        upload.EnsureSuccessStatusCode();
        using var uploadJson = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var importId = uploadJson.RootElement.GetProperty("importId").GetGuid();

        (await _client.PostAsync($"/api/liens/selling/bulk-imports/{importId}/validate", null)).EnsureSuccessStatusCode();
        using var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/bulk-imports/{importId}/confirm");
        confirm.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        var confirmed = await _client.SendAsync(confirm);
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await confirmed.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("status").GetString().Should().Be("CONFIRMED");
        json.RootElement.GetProperty("createdCount").GetInt32().Should().Be(3);
        json.RootElement.GetProperty("failedCount").GetInt32().Should().Be(0);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        db.BatchUploadDetails.Count(row => row.BatchUploadId == importId && row.Status == "CREATED").Should().Be(3);
        var importedCase = db.Cases.Single(caseEntity => caseEntity.CaseNumber == "CASE-10002");
        var liens = db.Liens.Where(lien => lien.CaseId == importedCase.Id).ToList();
        liens.Should().HaveCount(3);
        liens.Select(lien => lien.CaseId).Distinct().Should().ContainSingle();
        liens.Select(lien => lien.LienNumber).Should().OnlyHaveUniqueItems();
        liens.Should().OnlyContain(lien => lien.LienNumber.StartsWith("SL-") && lien.LienNumber.Length == 35);

        var importedLienIds = liens.Select(lien => lien.Id).ToHashSet();
        var servicingItems = db.ServicingItems.Where(item => item.LienId.HasValue && importedLienIds.Contains(item.LienId.Value)).ToList();
        AssertUniqueImportTaskNumbers(servicingItems, "SellingMedicalPricing", 36);
        AssertUniqueImportTaskNumbers(servicingItems, "LegacyMedicalCode", 36);
        AssertUniqueImportTaskNumbers(servicingItems, "LegacyMedicalFacilityInfo", 37);
    }

    [Fact]
    public async Task Confirm_bulk_import_rejects_a_second_confirmation_while_the_batch_transition_is_in_progress()
    {
        const string csv = "Case Code*,Initial Service Date*,Facility Name*,Medical Code & Description*,Billing Amount*\r\nCASE-10001,2026-07-19,Sunrise Clinic,45385 - Colonoscopy,250.00\r\n";
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "selling-lien-import.csv");
        form.Add(new StringContent("SellingLienImport"), "templateType");
        var upload = await _client.PostAsync("/api/liens/selling/bulk-imports", form);
        upload.EnsureSuccessStatusCode();
        using var uploadJson = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var importId = uploadJson.RootElement.GetProperty("importId").GetGuid();
        (await _client.PostAsync($"/api/liens/selling/bulk-imports/{importId}/validate", null)).EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.SellingIdempotencyRecords.Add(SellingIdempotencyRecord.Create(
                SeedHelper.TenantId, "BulkImportTransition", importId,
                "/api/liens/selling/bulk-imports/{importId}/confirm-transition", "BulkImport", importId.ToString(),
                "bulk-import-confirm-transition-v1", new string('a', 64)));
            await db.SaveChangesAsync();
        }

        using var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/bulk-imports/{importId}/confirm");
        confirm.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        var response = await _client.SendAsync(confirm);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).Should().Contain("import_confirmation_in_progress");
    }

    private static void SetId<T>(T entity, Guid id) where T : class
        => typeof(T).GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!.SetValue(entity, id);

    private static void AssertUniqueImportTaskNumbers(
        IReadOnlyCollection<ServicingItem> items,
        string taskType,
        int expectedLength)
    {
        var matching = items.Where(item => item.TaskType == taskType).ToList();
        matching.Should().HaveCount(3);
        matching.Select(item => item.TaskNumber).Should().OnlyHaveUniqueItems();
        matching.Should().OnlyContain(item => item.TaskNumber.Length == expectedLength);
    }
}
