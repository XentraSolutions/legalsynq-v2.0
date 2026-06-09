using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class LegacyReportEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public LegacyReportEndpointTests(LiensApiFactory factory) => _factory = factory;

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

    // ── GET /report/diy ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetSavedReports_returns200_with_seeded_report()
    {
        var resp = await _client.GetAsync("/report/diy");
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var list = await resp.Content.ReadFromJsonAsync<List<JsonElement>>();
        list.Should().NotBeNull();
        list!.Should().ContainSingle(r => r.GetProperty("id").GetGuid() == SeedHelper.ReportConfigId);
    }

    // ── POST /report/diy (run) ────────────────────────────────────────────────

    [Fact]
    public async Task RunReport_returns200_with_paginated_result()
    {
        var resp = await _client.PostAsJsonAsync("/report/diy", new
        {
            config = new { status = "Open" },
            page   = 1,
            limit  = 10,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.TryGetProperty("items", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("totalCount", out _).Should().BeTrue();
    }

    // ── POST /report/diy/export ───────────────────────────────────────────────

    [Fact]
    public async Task ExportReport_returns200_with_base64_data()
    {
        var resp = await _client.PostAsJsonAsync("/report/diy/export", new
        {
            config = new { status = "Open" },
            page   = 1,
            limit  = 10,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.TryGetProperty("data", out var dataProp).Should().BeTrue();
        dataProp.GetString().Should().NotBeNull();
    }

    // ── POST /report/diy/save ─────────────────────────────────────────────────

    [Fact]
    public async Task SaveReport_returns201()
    {
        var resp = await _client.PostAsJsonAsync("/report/diy/save", new
        {
            name   = "My New Report",
            config = new { status = "Closed" },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.TryGetProperty("id", out _).Should().BeTrue();
    }

    // ── DELETE /report/diy/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task DeleteReport_returns200()
    {
        // Save a fresh report to delete.
        var saveResp = await _client.PostAsJsonAsync("/report/diy/save", new
        {
            name   = "Report To Delete",
            config = new { },
        });
        saveResp.EnsureSuccessStatusCode();
        var doc  = await saveResp.Content.ReadFromJsonAsync<JsonDocument>();
        var id   = doc!.RootElement.GetProperty("id").GetGuid();

        var deleteResp = await _client.DeleteAsync($"/report/diy/{id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await deleteResp.Content.ReadAsStringAsync()}");
    }

    // ── Auth enforcement ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetSavedReports_without_auth_returns_401()
    {
        var anonClient = _factory.CreateClient();
        var resp = await anonClient.GetAsync("/report/diy");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RunReport_without_auth_returns_401()
    {
        var anonClient = _factory.CreateClient();
        var resp = await anonClient.PostAsJsonAsync("/report/diy",
            new { config = new { } });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
