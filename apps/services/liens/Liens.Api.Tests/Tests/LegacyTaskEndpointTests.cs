using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
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

    [Theory]
    [InlineData("High", "High", "HIGH")]
    [InlineData("MEDIUM", "Medium", "MEDIUM")]
    [InlineData("low", "Low", "LOW")]
    public async Task CreateTaskLegacy_accepts_supported_priorities_case_insensitively(
        string requestedPriority,
        string expectedPriority,
        string expectedPriorityId)
    {
        var title = $"Priority {requestedPriority} {Guid.CreateVersion7():N}";
        var response = await _client.PostAsJsonAsync("/api/liens/cases/tasks/create", new
        {
            caseId = SeedHelper.CaseId,
            title,
            description = "Priority compatibility task",
            assignedTo = "qa.user@example.com",
            priority = requestedPriority,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var listResponse = await _client.GetAsync(
            $"/api/liens/tasks/legacy/get-task/{SeedHelper.CaseId}");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await listResponse.Content.ReadAsStringAsync()}");

        using var document = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var task = document.RootElement.GetProperty("data").EnumerateArray().Single(item =>
            item.GetProperty("title").GetString() == title);
        task.GetProperty("priority").GetString().Should().Be(expectedPriority);
        task.GetProperty("priorityId").GetString().Should().Be(expectedPriorityId);
    }

    [Fact]
    public async Task UpdateTaskLegacy_accepts_post_on_case_task_update_route()
    {
        var originalTitle = $"Task before update {Guid.CreateVersion7():N}";
        var createResponse = await _client.PostAsJsonAsync("/api/liens/cases/tasks/create", new
        {
            caseId = SeedHelper.CaseId,
            title = originalTitle,
            description = "Before update",
            assignedTo = "qa.user@example.com",
            priority = "Low",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await createResponse.Content.ReadAsStringAsync()}");

        var initialListResponse = await _client.GetAsync(
            $"/api/liens/tasks/legacy/get-task/{SeedHelper.CaseId}");
        using var initialList = JsonDocument.Parse(await initialListResponse.Content.ReadAsStringAsync());
        var taskId = initialList.RootElement.GetProperty("data").EnumerateArray().Single(item =>
            item.GetProperty("title").GetString() == originalTitle).GetProperty("taskId").GetString();

        var updatedTitle = $"Task after update {Guid.CreateVersion7():N}";
        var updateResponse = await _client.PostAsJsonAsync("/api/liens/cases/task/update", new
        {
            taskId,
            title = updatedTitle,
            description = "After update",
            assignedTo = "qa.user@example.com",
            priority = "Medium",
            status = "UPCOMING",
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await updateResponse.Content.ReadAsStringAsync()}");

        var updatedListResponse = await _client.GetAsync(
            $"/api/liens/tasks/legacy/get-task/{SeedHelper.CaseId}");
        using var updatedList = JsonDocument.Parse(await updatedListResponse.Content.ReadAsStringAsync());
        var updatedTask = updatedList.RootElement.GetProperty("data").EnumerateArray().Single(item =>
            item.GetProperty("taskId").GetString() == taskId);
        updatedTask.GetProperty("title").GetString().Should().Be(updatedTitle);
        updatedTask.GetProperty("description").GetString().Should().Be("After update");
        updatedTask.GetProperty("priority").GetString().Should().Be("Medium");
        updatedTask.GetProperty("priorityId").GetString().Should().Be("MEDIUM");
        updatedTask.GetProperty("status").GetString().Should().Be("UPCOMING");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var servicingItem = await db.ServicingItems.FindAsync(Guid.Parse(taskId!));
        servicingItem.Should().NotBeNull();
        servicingItem!.Status.Should().Be(ServicingStatus.Pending);
    }
}
