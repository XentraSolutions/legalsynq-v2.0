using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class LegacyTaskEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public LegacyTaskEndpointTests(LiensApiFactory factory) => _factory = factory;

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
    public async Task GetTasksLegacy_formats_createdAt_in_pacific_time()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var task = ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "CT-TIME-001",
                "LegacyCaseTask",
                "Timestamp regression task",
                "qa.user@example.com",
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                notes: "title=Timestamp Regression; status=Open");

            typeof(ServicingItem).GetProperty(nameof(ServicingItem.Id))!
                .SetValue(task, Guid.CreateVersion7());
            typeof(BuildingBlocks.Domain.AuditableEntity).GetProperty(nameof(BuildingBlocks.Domain.AuditableEntity.CreatedAtUtc))!
                .SetValue(task, new DateTime(2026, 7, 21, 14, 35, 0, DateTimeKind.Utc));
            typeof(BuildingBlocks.Domain.AuditableEntity).GetProperty(nameof(BuildingBlocks.Domain.AuditableEntity.UpdatedAtUtc))!
                .SetValue(task, new DateTime(2026, 7, 21, 14, 35, 0, DateTimeKind.Utc));

            db.ServicingItems.Add(task);
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/liens/tasks/legacy/get-task/{SeedHelper.CaseId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = doc.RootElement.GetProperty("data").EnumerateArray()
            .Single(entry => entry.GetProperty("title").GetString() == "Timestamp Regression");

        item.GetProperty("createdAt").GetString().Should().Be("07/21/2026 07:35 AM");
    }
}
