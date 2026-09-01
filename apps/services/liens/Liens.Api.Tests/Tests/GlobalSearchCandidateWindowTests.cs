using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using BuildingBlocks.Domain;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public sealed class GlobalSearchCandidateWindowTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public GlobalSearchCandidateWindowTests(LiensApiFactory factory) => _factory = factory;

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
    public async Task Global_search_finds_exact_linked_case_name_older_than_fuzzy_candidate_window()
    {
        const string query = "Lazaro Arce Rodriguez";
        var targetCase = Case.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            "CASE-GLOBAL-WINDOW-TARGET",
            "Lazaro",
            "Arce Rodriguez",
            SeedHelper.UserId);
        var targetLien = Lien.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            "LIEN-GLOBAL-WINDOW-TARGET",
            LienType.MedicalLien,
            1_000m,
            SeedHelper.UserId,
            caseId: targetCase.Id);

        var oldCreatedAtUtc = DateTime.UtcNow.AddYears(-1);
        SetCreatedAtUtc(targetCase, oldCreatedAtUtc);
        SetCreatedAtUtc(targetLien, oldCreatedAtUtc);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Cases.Add(targetCase);
            db.Liens.Add(targetLien);

            for (var index = 0; index <= 5_000; index++)
            {
                var distractorCase = Case.Create(
                    SeedHelper.TenantId,
                    SeedHelper.OrgId,
                    $"CASE-GLOBAL-WINDOW-{index:D5}",
                    "Candidate",
                    $"Distractor{index:D5}",
                    SeedHelper.UserId);
                var distractorLien = Lien.Create(
                    SeedHelper.TenantId,
                    SeedHelper.OrgId,
                    $"LIEN-GLOBAL-WINDOW-{index:D5}",
                    LienType.MedicalLien,
                    100m,
                    SeedHelper.UserId,
                    caseId: distractorCase.Id);

                db.Cases.Add(distractorCase);
                db.Liens.Add(distractorLien);
            }

            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/liens/cases/global-search", new
        {
            query,
            page = 1,
            limit = 20,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        body["cases"]!["items"]!.AsArray().Should().ContainSingle(item =>
            item!["id"]!.GetValue<Guid>() == targetCase.Id);
        body["liens"]!["items"]!.AsArray().Should().ContainSingle(item =>
            item!["id"]!.GetValue<Guid>() == targetLien.Id);
        body["plaintiffs"]!.AsArray().Should().ContainSingle(item =>
            item!["caseId"]!.GetValue<string>() == targetCase.Id.ToString());
        body["servicing"]!.AsArray().Should().ContainSingle(item =>
            item!["caseId"]!.GetValue<string>() == targetCase.Id.ToString());
    }

    private static void SetCreatedAtUtc(AuditableEntity entity, DateTime value) =>
        typeof(AuditableEntity).GetProperty(nameof(AuditableEntity.CreatedAtUtc))!
            .SetValue(entity, value);
}
