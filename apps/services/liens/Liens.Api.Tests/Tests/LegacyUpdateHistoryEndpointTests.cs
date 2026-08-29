using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public sealed class LegacyUpdateHistoryEndpointTests
{
    [Fact]
    public async Task Disabled_history_preserves_existing_empty_response_behavior()
    {
        await using var factory = new LiensApiFactory();
        var client = await CreateAuthenticatedClientAsync(factory);
        var (caseId, lienId) = await AddEmptyCaseAndLienAsync(factory);

        await AddUpdateEventsAsync(factory,
            CreateEvent(caseId, null, LegacyUpdateEvent.CaseScope, 10),
            CreateEvent(caseId, lienId, LegacyUpdateEvent.LienScope, 20));

        var caseResponse = await client.PostAsJsonAsync("/api/liens/cases/case-updates/v3", new
        {
            CaseId = caseId,
            page = 1,
            limit = 10,
        });
        caseResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await caseResponse.Content.ReadAsStringAsync()}");
        using var caseBody = JsonDocument.Parse(await caseResponse.Content.ReadAsStringAsync());
        caseBody.RootElement.GetProperty("data").GetArrayLength().Should().Be(0);
        caseBody.RootElement.GetProperty("totalCount").GetInt32().Should().Be(0);

        var lienResponse = await client.PostAsJsonAsync("/api/liens/cases/liens-updates/v3", new
        {
            CaseId = caseId,
            page = 1,
            limit = 10,
        });
        lienResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Enabled_case_history_merges_sources_projects_compatibility_fields_and_pages_deterministically()
    {
        await using var factory = new EnabledLegacyUpdateHistoryFactory();
        var client = await CreateAuthenticatedClientAsync(factory);
        var occurredAtUtc = Utc(2026, 8, 20, 17, 22, 8);

        var native = LienCaseNote.Create(
            SeedHelper.CaseId,
            SeedHelper.TenantId,
            "Native history",
            CaseNoteCategory.Internal,
            SeedHelper.UserId,
            "Native User");
        SetProperty(native, nameof(LienCaseNote.CreatedAtUtc), occurredAtUtc);

        var importedHigh = CreateEvent(
            SeedHelper.CaseId,
            null,
            LegacyUpdateEvent.CaseScope,
            102,
            occurredAtUtc,
            description: "Attorney ÔåÆ Funding ?",
            actor: null);
        var importedLow = CreateEvent(
            SeedHelper.CaseId,
            null,
            LegacyUpdateEvent.CaseScope,
            101,
            occurredAtUtc,
            description: null,
            actor: "Legacy User");
        var crossTenant = LegacyUpdateEvent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SeedHelper.CaseId,
            null,
            LegacyUpdateEvent.CaseScope,
            "Case Details Update",
            "must not leak",
            "Other Tenant",
            occurredAtUtc.AddHours(1),
            Utc(2026, 8, 29, 1, 0, 0),
            Guid.NewGuid(),
            "SL-CORE",
            "SL_CASE_UPDATE_LOG",
            "999",
            999);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.LienCaseNotes.Add(native);
            db.LegacyUpdateEvents.AddRange(importedLow, importedHigh, crossTenant);
            await db.SaveChangesAsync();
        }

        var firstResponse = await client.PostAsJsonAsync("/api/liens/cases/case-updates/v3", new
        {
            CaseId = SeedHelper.CaseId,
            page = 1,
            limit = 2,
        });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await firstResponse.Content.ReadAsStringAsync()}");
        using var firstBody = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        firstBody.RootElement.GetProperty("totalCount").GetInt32().Should().Be(3);
        var firstPage = firstBody.RootElement.GetProperty("data").EnumerateArray().ToList();
        firstPage.Select(item => item.GetProperty("id").GetString())
            .Should().Equal(native.Id.ToString(), importedHigh.Id.ToString());

        var imported = firstPage[1];
        imported.GetProperty("caseId").GetString().Should().Be(SeedHelper.CaseId.ToString());
        imported.GetProperty("action").GetString().Should().Be("Case Details Update");
        imported.GetProperty("description").GetString().Should().Be("Attorney → Funding ?");
        imported.GetProperty("note").GetString().Should().Be("Attorney → Funding ?");
        imported.GetProperty("category").GetString().Should().Be("legacy");
        imported.GetProperty("isPinned").GetBoolean().Should().BeFalse();
        imported.GetProperty("isEdited").GetBoolean().Should().BeFalse();
        imported.GetProperty("createdBy").GetString().Should().BeEmpty();
        imported.GetProperty("updatedBy").GetString().Should().BeEmpty();
        imported.GetProperty("updated").GetString().Should().BeEmpty();

        var secondResponse = await client.PostAsJsonAsync("/api/liens/cases/case-updates/v3", new
        {
            CaseId = SeedHelper.CaseId,
            page = 2,
            limit = 2,
        });
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var secondBody = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        var secondPage = secondBody.RootElement.GetProperty("data").EnumerateArray().ToList();
        secondPage.Should().ContainSingle();
        secondPage[0].GetProperty("id").GetString().Should().Be(importedLow.Id.ToString());
        secondPage[0].GetProperty("description").GetString().Should().BeEmpty();
        secondPage[0].GetProperty("updatedBy").GetString().Should().Be("Legacy User");
    }

    [Fact]
    public async Task Enabled_lien_history_returns_imported_rows_and_excludes_other_tenants()
    {
        await using var factory = new EnabledLegacyUpdateHistoryFactory();
        var client = await CreateAuthenticatedClientAsync(factory);
        var imported = CreateEvent(
            SeedHelper.CaseId,
            SeedHelper.LienId,
            LegacyUpdateEvent.LienScope,
            4890,
            Utc(2026, 8, 21, 8, 0, 0),
            "Payee ÔåÆ Created ?",
            "Legacy Actor");
        var crossTenant = LegacyUpdateEvent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SeedHelper.CaseId,
            SeedHelper.LienId,
            LegacyUpdateEvent.LienScope,
            "Lien Update",
            "must not leak",
            "Other Tenant",
            Utc(2026, 8, 22, 8, 0, 0),
            Utc(2026, 8, 29, 1, 0, 0),
            Guid.NewGuid(),
            "SL-CORE",
            "SL_LIENS_UPDATE_LOG",
            "999",
            999);
        await AddUpdateEventsAsync(factory, imported, crossTenant);

        var response = await client.PostAsJsonAsync("/api/liens/cases/liens-updates/v3", new
        {
            CaseId = SeedHelper.CaseId,
            page = 1,
            limit = 50,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = body.RootElement.GetProperty("data").EnumerateArray()
            .Where(row => row.GetProperty("id").GetString() == imported.Id.ToString())
            .ToList();
        rows.Should().ContainSingle();
        rows[0].GetProperty("caseId").GetString().Should().Be(SeedHelper.CaseId.ToString());
        rows[0].GetProperty("lienId").GetString().Should().Be(SeedHelper.LienId.ToString());
        rows[0].GetProperty("action").GetString().Should().Be("Lien Update");
        rows[0].GetProperty("description").GetString().Should().Be("Payee → Created ?");
        rows[0].GetProperty("updatedBy").GetString().Should().Be("Legacy Actor");
        body.RootElement.GetProperty("data").EnumerateArray()
            .Should().NotContain(row => row.GetProperty("description").GetString() == "must not leak");
    }

    [Fact]
    public async Task Enabled_case_history_pages_a_25000_event_timeline()
    {
        await using var factory = new EnabledLegacyUpdateHistoryFactory();
        var client = await CreateAuthenticatedClientAsync(factory);
        const int eventCount = 25_000;
        var firstAtUtc = Utc(2025, 1, 1, 0, 0, 0);
        var events = Enumerable.Range(1, eventCount)
            .Select(sequence => CreateEvent(
                SeedHelper.CaseId,
                null,
                LegacyUpdateEvent.CaseScope,
                sequence,
                firstAtUtc.AddSeconds(sequence)))
            .ToArray();
        await AddUpdateEventsAsync(factory, events);

        var response = await client.PostAsJsonAsync("/api/liens/cases/case-updates/v3", new
        {
            CaseId = SeedHelper.CaseId,
            page = 1,
            limit = 25,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("totalCount").GetInt32().Should().Be(eventCount);
        var page = body.RootElement.GetProperty("data").EnumerateArray().ToList();
        page.Should().HaveCount(25);
        page[0].GetProperty("id").GetString().Should().Be(events[^1].Id.ToString());
        page[^1].GetProperty("id").GetString().Should().Be(events[^25].Id.ToString());
    }

    [Theory]
    [InlineData("/api/liens/cases/case-updates/v3", 1, 201)]
    [InlineData("/api/liens/cases/case-updates/v3", 126, 200)]
    [InlineData("/api/liens/cases/liens-updates/v3", 1, 201)]
    [InlineData("/api/liens/cases/liens-updates/v3", 126, 200)]
    public async Task History_endpoints_reject_unbounded_pagination_windows(string path, int page, int limit)
    {
        await using var factory = new EnabledLegacyUpdateHistoryFactory();
        var client = await CreateAuthenticatedClientAsync(factory);

        var response = await client.PostAsJsonAsync(path, new
        {
            CaseId = SeedHelper.CaseId,
            page,
            limit,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("message").GetString().Should().Contain("Pagination is limited");
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(LiensApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        await SeedHelper.SeedAsync(scope.ServiceProvider);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));
        return client;
    }

    private static async Task AddUpdateEventsAsync(
        LiensApiFactory factory,
        params LegacyUpdateEvent[] events)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        db.LegacyUpdateEvents.AddRange(events);
        await db.SaveChangesAsync();
    }

    private static async Task<(Guid CaseId, Guid LienId)> AddEmptyCaseAndLienAsync(LiensApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var caseEntity = Case.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            $"UPDATE-HISTORY-{Guid.NewGuid():N}"[..28],
            "Legacy",
            "History",
            SeedHelper.UserId);
        var lien = Lien.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            $"UPDATE-LIEN-{Guid.NewGuid():N}"[..28],
            LienType.MedicalLien,
            100m,
            SeedHelper.UserId,
            caseId: caseEntity.Id);
        db.Cases.Add(caseEntity);
        db.Liens.Add(lien);
        await db.SaveChangesAsync();
        return (caseEntity.Id, lien.Id);
    }

    private static LegacyUpdateEvent CreateEvent(
        Guid caseId,
        Guid? lienId,
        string scope,
        long sequence,
        DateTime? occurredAtUtc = null,
        string? description = "legacy description",
        string? actor = "Legacy Actor") =>
        LegacyUpdateEvent.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            caseId,
            lienId,
            scope,
            scope == LegacyUpdateEvent.CaseScope ? "Case Details Update" : "Lien Update",
            description,
            actor,
            occurredAtUtc ?? Utc(2026, 8, 20, 17, 22, 8),
            Utc(2026, 8, 29, 1, 0, 0),
            Guid.NewGuid(),
            "SL-CORE",
            scope == LegacyUpdateEvent.CaseScope ? "SL_CASE_UPDATE_LOG" : "SL_LIENS_UPDATE_LOG",
            sequence.ToString(),
            sequence);

    private static void SetProperty<T>(T entity, string propertyName, object value) where T : class =>
        typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(entity, value);

    private static DateTime Utc(int year, int month, int day, int hour, int minute, int second) =>
        new(year, month, day, hour, minute, second, DateTimeKind.Utc);

    private sealed class EnabledLegacyUpdateHistoryFactory : LiensApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LegacyUpdateHistory:Enabled"] = "true",
                }));
        }
    }
}
