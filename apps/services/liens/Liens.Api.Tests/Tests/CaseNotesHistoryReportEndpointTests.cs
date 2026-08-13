using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public sealed class CaseNotesHistoryReportEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public CaseNotesHistoryReportEndpointTests(LiensApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedHelper.SeedAsync(scope.ServiceProvider);
        _client = CreateAuthorizedClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task List_separates_tracking_and_feed_and_excludes_ineligible_rows()
    {
        await SeedReportNotesAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            (await db.LegacyIdCrosswalks.CountAsync()).Should().Be(0);
            (await scope.ServiceProvider.GetRequiredService<ICaseNotesHistoryReportService>()
                .IsLegacyHistoryReadyAsync(SeedHelper.TenantId)).Should().BeTrue();
        }

        var trackingResponse = await _client.PostAsJsonAsync(
            "/api/liens/reports/case-notes-history",
            Request("tracking", limit: 100));
        var feedResponse = await _client.PostAsJsonAsync(
            "/api/liens/reports/case-notes-history",
            Request("FEED", limit: 100));

        trackingResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await trackingResponse.Content.ReadAsStringAsync());
        feedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        trackingResponse.Headers.CacheControl?.NoStore.Should().BeTrue();

        using var tracking = JsonDocument.Parse(await trackingResponse.Content.ReadAsStringAsync());
        using var feed = JsonDocument.Parse(await feedResponse.Content.ReadAsStringAsync());

        tracking.RootElement.GetProperty("totalCount").GetInt32().Should().Be(2);
        tracking.RootElement.GetProperty("data").EnumerateArray()
            .Select(row => row.GetProperty("noteContent").GetString())
            .Should().BeEquivalentTo("General note", "Follow-up note");
        tracking.RootElement.GetProperty("data").EnumerateArray()
            .Should().OnlyContain(row => row.GetProperty("noteType").GetString() == "TRACKING");

        feed.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
        feed.RootElement.GetProperty("data")[0].GetProperty("noteContent").GetString().Should().Be("Feed note");
        feed.RootElement.GetProperty("data")[0].GetProperty("noteTypeLabel").GetString().Should().Be("Feed Note");
    }

    [Fact]
    public async Task List_is_tenant_safe_paged_stable_and_preserves_legacy_shape()
    {
        await SeedReportNotesAsync(includeTenantMismatch: true);

        var canonicalResponse = await _client.PostAsJsonAsync(
            "/api/liens/reports/case-notes-history",
            new
            {
                noteType = "TRACKING",
                page = 2,
                limit = 1,
                sortBy = "noteContent",
                sortDirection = "asc",
                tenantId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            });
        var legacyResponse = await _client.PostAsJsonAsync(
            "/report/case-notes-history",
            Request("TRACKING", page: 2, limit: 1, sortBy: "noteContent", sortDirection: "asc"));

        canonicalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        legacyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var canonical = JsonDocument.Parse(await canonicalResponse.Content.ReadAsStringAsync());
        using var legacy = JsonDocument.Parse(await legacyResponse.Content.ReadAsStringAsync());
        var canonicalRow = canonical.RootElement.GetProperty("data")[0];
        var legacyRow = legacy.RootElement.GetProperty("data")[0];

        canonical.RootElement.GetProperty("totalCount").GetInt32().Should().Be(2);
        canonicalRow.GetProperty("caseId").GetString().Should().Be("CASE-TEST-001");
        canonicalRow.GetProperty("caseName").GetString().Should().Be("John Plaintiff");
        canonicalRow.GetProperty("noteContent").GetString().Should().Be("General note");
        canonicalRow.TryGetProperty("createdAtUtc", out _).Should().BeTrue();
        legacyRow.TryGetProperty("createdAtUtc", out _).Should().BeFalse();
        legacyRow.GetProperty("noteId").GetString().Should().Be(canonicalRow.GetProperty("noteId").GetString());

        var emptyPage = await _client.PostAsJsonAsync(
            "/api/liens/reports/case-notes-history",
            Request("TRACKING", page: int.MaxValue, limit: 100));
        emptyPage.StatusCode.Should().Be(HttpStatusCode.OK);
        using var empty = JsonDocument.Parse(await emptyPage.Content.ReadAsStringAsync());
        empty.RootElement.GetProperty("data").GetArrayLength().Should().Be(0);
        empty.RootElement.GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Export_ignores_paging_and_neutralizes_formula_content()
    {
        await SeedReportNotesAsync(formulaContent: "  =HYPERLINK(\"https://invalid.example\"), \"José\"\nnext");

        var response = await _client.PostAsJsonAsync(
            "/report/case-notes-history/export",
            Request("TRACKING", page: 99, limit: 1, sortBy: "noteContent", sortDirection: "asc"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = body.RootElement.GetProperty("data")[0];
        item.GetProperty("export_format").GetString().Should().Be("csv");
        item.GetProperty("filename").GetString().Should().StartWith("case_notes_history_tracking_");

        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(item.GetProperty("base64").GetString()!));
        csv.Should().StartWith("Case ID,Case Name,Note Type,Note Date,Note Author,Note Content\r\n");
        csv.Should().Contain("Follow-up note");
        csv.Should().Contain("'  =HYPERLINK(\"\"https://invalid.example\"\")");
        csv.Should().Contain("José");
        csv.Should().Contain("\nnext");
    }

    [Fact]
    public async Task Export_has_no_row_cap_and_stops_incrementally_at_ten_mibibytes()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.LienCaseNotes.AddRange(Enumerable.Range(0, 10_001).Select(index =>
                Note($"Feed row {index:D5}", CaseNoteCategory.Feed, new DateTime(2026, 8, 13, 8, 0, 0, DateTimeKind.Utc))));
            await db.SaveChangesAsync();

            var export = await scope.ServiceProvider.GetRequiredService<ICaseNotesHistoryReportService>()
                .ExportCsvAsync(SeedHelper.TenantId, new()
                {
                    NoteType = "FEED",
                    Page = 99,
                    Limit = 1,
                    SortBy = "noteContent",
                    SortDirection = "asc",
                });

            export.SizeLimitExceeded.Should().BeFalse();
            Encoding.UTF8.GetString(export.Content).Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
                .Should().HaveCount(10_002);
        }

        using (var resetScope = _factory.Services.CreateScope())
            await SeedHelper.SeedAsync(resetScope.ServiceProvider);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var largeContent = new string('x', 5_000);
            db.LienCaseNotes.AddRange(Enumerable.Range(0, 2_100).Select(index =>
                Note($"{index:D4}{largeContent[4..]}", CaseNoteCategory.General, DateTime.UtcNow)));
            await db.SaveChangesAsync();

        }

        var oversizedResponse = await _client.PostAsJsonAsync(
            "/api/liens/reports/case-notes-history/export",
            Request("TRACKING"));
        oversizedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var oversizedBody = JsonDocument.Parse(await oversizedResponse.Content.ReadAsStringAsync());
        oversizedBody.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("validation_error");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"noteType\":\"FEEDING\"}")]
    [InlineData("{\"noteType\":\"TRACKING\",\"page\":0}")]
    [InlineData("{\"noteType\":\"TRACKING\",\"limit\":101}")]
    [InlineData("{\"noteType\":\"TRACKING\",\"sortBy\":\"sql\"}")]
    [InlineData("{\"noteType\":\"TRACKING\",\"sortDirection\":\"sideways\"}")]
    public async Task List_rejects_invalid_contract_values(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/liens/reports/case-notes-history", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("validation_error");
    }

    [Fact]
    public async Task Both_routes_require_auth_product_permission_and_reconciled_legacy_history()
    {
        var anonymous = _factory.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/liens/reports/case-notes-history", Request("TRACKING")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/report/case-notes-history", Request("TRACKING")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var withoutPermission = _factory.CreateClient();
        withoutPermission.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateToken(SeedHelper.TenantId, SeedHelper.UserId, []));
        (await withoutPermission.PostAsJsonAsync("/api/liens/reports/case-notes-history", Request("TRACKING")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AddLegacyCrosswalkAsync("unversioned-hash");
        var blocked = await _client.PostAsJsonAsync("/report/case-notes-history", Request("TRACKING"));
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var body = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("legacy_history_not_reconciled");
    }

    private HttpClient CreateAuthorizedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));
        return client;
    }

    private async Task SeedReportNotesAsync(bool includeTenantMismatch = false, string? formulaContent = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

        var general = Note(formulaContent ?? "General note", CaseNoteCategory.General, new DateTime(2026, 8, 13, 8, 0, 0, DateTimeKind.Utc));
        if (formulaContent is not null)
            Set(general, nameof(LienCaseNote.Content), formulaContent);
        var followUp = Note("Follow-up note", CaseNoteCategory.FollowUp, new DateTime(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc));
        var feed = Note("Feed note", CaseNoteCategory.Feed, new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc));
        var internalNote = Note("Internal note", CaseNoteCategory.Internal, new DateTime(2026, 8, 13, 11, 0, 0, DateTimeKind.Utc));
        var deleted = Note("Deleted note", CaseNoteCategory.General, new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc));
        deleted.SoftDelete();
        var blank = Note("placeholder", CaseNoteCategory.General, new DateTime(2026, 8, 13, 13, 0, 0, DateTimeKind.Utc));
        Set(blank, nameof(LienCaseNote.Content), "   ");
        db.LienCaseNotes.AddRange(general, followUp, feed, internalNote, deleted, blank);

        if (includeTenantMismatch)
        {
            var otherTenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var otherCase = Case.Create(otherTenant, SeedHelper.OrgId, "CASE-OTHER-TENANT", "Hidden", "Case", SeedHelper.UserId);
            db.Cases.Add(otherCase);
            db.LienCaseNotes.Add(LienCaseNote.Create(
                otherCase.Id,
                SeedHelper.TenantId,
                "Mismatched case tenant",
                CaseNoteCategory.General,
                SeedHelper.UserId,
                "Hidden User"));
            db.LienCaseNotes.Add(LienCaseNote.Create(
                SeedHelper.CaseId,
                otherTenant,
                "Mismatched note tenant",
                CaseNoteCategory.General,
                SeedHelper.UserId,
                "Hidden User"));
        }

        await db.SaveChangesAsync();
    }

    private static LienCaseNote Note(string content, string category, DateTime createdAtUtc)
    {
        var note = LienCaseNote.Create(
            SeedHelper.CaseId,
            SeedHelper.TenantId,
            content,
            category,
            SeedHelper.UserId,
            "Report Author");
        Set(note, nameof(LienCaseNote.CreatedAtUtc), createdAtUtc);
        return note;
    }

    private async Task AddLegacyCrosswalkAsync(string sourceHash)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var crosswalk = (LegacyIdCrosswalk)Activator.CreateInstance(typeof(LegacyIdCrosswalk), nonPublic: true)!;
        Set(crosswalk, nameof(LegacyIdCrosswalk.Id), Guid.CreateVersion7());
        Set(crosswalk, nameof(LegacyIdCrosswalk.TenantId), SeedHelper.TenantId);
        Set(crosswalk, nameof(LegacyIdCrosswalk.SourceSystem), "SL-CORE");
        Set(crosswalk, nameof(LegacyIdCrosswalk.SourceTable), "SL_CASE_NOTES");
        Set(crosswalk, nameof(LegacyIdCrosswalk.LegacyId), "1");
        Set(crosswalk, nameof(LegacyIdCrosswalk.TargetEntity), "CaseNote");
        Set(crosswalk, nameof(LegacyIdCrosswalk.TargetId), Guid.CreateVersion7());
        Set(crosswalk, nameof(LegacyIdCrosswalk.SourceHash), sourceHash);
        Set(crosswalk, nameof(LegacyIdCrosswalk.ImportRunId), Guid.CreateVersion7());
        Set(crosswalk, nameof(LegacyIdCrosswalk.CreatedAtUtc), DateTime.UtcNow);
        db.LegacyIdCrosswalks.Add(crosswalk);
        await db.SaveChangesAsync();
    }

    private static object Request(
        string noteType,
        int page = 1,
        int limit = 10,
        string sortBy = "noteDate",
        string sortDirection = "desc")
        => new { noteType, page, limit, sortBy, sortDirection };

    private static void Set<T>(T entity, string property, object value) where T : class
        => typeof(T).GetProperty(property)!.SetValue(entity, value);
}
