using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
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
}
