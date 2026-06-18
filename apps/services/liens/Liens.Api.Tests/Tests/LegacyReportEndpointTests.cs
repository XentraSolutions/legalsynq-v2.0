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
        doc!.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("message").GetString().Should().Be("Liens report generated.");
        doc.RootElement.TryGetProperty("summaryTotals", out var summary).Should().BeTrue();
        summary.TryGetProperty("totalLiens", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("data", out var data).Should().BeTrue();
        data.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetProperty("page").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("limit").GetInt32().Should().Be(10);
        doc.RootElement.TryGetProperty("totalCount", out _).Should().BeTrue();

        if (data.GetArrayLength() > 0)
        {
            var row = data[0];
            row.TryGetProperty("plaintiff_first_name", out _).Should().BeTrue();
            row.TryGetProperty("plaintiff_last_name", out _).Should().BeTrue();
            row.TryGetProperty("case_id", out _).Should().BeTrue();
            row.TryGetProperty("lien_id", out _).Should().BeTrue();
            row.TryGetProperty("purchase_amt", out _).Should().BeTrue();
            row.TryGetProperty("billing_amt", out _).Should().BeTrue();
            row.TryGetProperty("case_status", out _).Should().BeTrue();
            row.TryGetProperty("date_of_loss", out _).Should().BeTrue();
        }
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
            config = new
            {
                reportType = "LIENS",
                statusView = "ALL",
                columns = new[]
                {
                    new { key = "billing_amt", label = "Billing Amt" },
                    new { key = "case_id", label = "Case Id" },
                },
                page = 1,
                limit = 50,
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.TryGetProperty("id", out _).Should().BeTrue();
        doc.RootElement.GetProperty("reportId").GetString().Should().NotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("reportName").GetString().Should().Be("My New Report");
        doc.RootElement.GetProperty("reportType").GetString().Should().Be("LIENS");
        doc.RootElement.GetProperty("createdAt").GetString().Should().MatchRegex(@"^\d{2}/\d{2}/\d{4}$");
        doc.RootElement.GetProperty("updatedAt").GetString().Should().MatchRegex(@"^\d{2}/\d{2}/\d{4}$");
        doc.RootElement.GetProperty("columnCount").GetInt32().Should().Be(2);

        var configColumns = doc.RootElement
            .GetProperty("config")
            .GetProperty("columns")
            .EnumerateArray()
            .Select(c => c.GetProperty("key").GetString())
            .ToList();
        configColumns.Should().Equal("billing_amt", "case_id");

        var reportConfigColumns = doc.RootElement
            .GetProperty("reportConfig")
            .GetProperty("columns")
            .EnumerateArray()
            .Select(c => c.GetString())
            .ToList();
        reportConfigColumns.Should().Equal("billing_amt", "case_id");
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
