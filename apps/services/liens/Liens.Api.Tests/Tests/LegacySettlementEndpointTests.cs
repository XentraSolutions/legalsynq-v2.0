using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class LegacySettlementEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public LegacySettlementEndpointTests(LiensApiFactory factory) => _factory = factory;

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

    // ── POST /service/liens/update/reduction ──────────────────────────────────

    [Fact]
    public async Task CreateReduction_returns201()
    {
        var resp = await _client.PostAsJsonAsync("/service/liens/update/reduction", new
        {
            caseId        = SeedHelper.CaseId,
            lienId        = SeedHelper.LienId,
            reductionDate = "2025-03-01",
            amount        = 250.00m,
            note          = "Test reduction",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await resp.Content.ReadAsStringAsync()}");
    }

    // ── POST /service/liens/update/settlement ─────────────────────────────────

    [Fact]
    public async Task CreateSettlement_returns201()
    {
        var resp = await _client.PostAsJsonAsync("/service/liens/update/settlement", new
        {
            caseId        = SeedHelper.CaseId,
            lienId        = SeedHelper.LienId,
            paymentNumber = 2,
            amount        = 2000m,
            status        = "Pending",
            note          = "Second payment",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await resp.Content.ReadAsStringAsync()}");
    }

    // ── POST /service/liens/settlement/payment ────────────────────────────────

    [Fact]
    public async Task CreatePayment_returns201()
    {
        var resp = await _client.PostAsJsonAsync("/service/liens/settlement/payment", new
        {
            caseId        = SeedHelper.CaseId,
            lienId        = SeedHelper.LienId,
            paymentNumber = 1,
            amount        = 1000m,
            paymentDate   = "2025-04-15",
            payee         = "Smith Law",
            checkNumber   = "CHK-9001",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await resp.Content.ReadAsStringAsync()}");
    }

    // ── DELETE /service/delete-payment/{id} ───────────────────────────────────

    [Fact]
    public async Task DeletePayment_returns200()
    {
        // First create a payment to delete.
        var createResp = await _client.PostAsJsonAsync("/service/liens/settlement/payment", new
        {
            caseId        = SeedHelper.CaseId,
            lienId        = SeedHelper.LienId,
            paymentNumber = 99,
            amount        = 99m,
        });
        createResp.EnsureSuccessStatusCode();
        var doc  = await createResp.Content.ReadFromJsonAsync<JsonDocument>();
        var id   = doc!.RootElement.GetProperty("id").GetGuid();

        var deleteResp = await _client.DeleteAsync($"/service/delete-payment/{id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await deleteResp.Content.ReadAsStringAsync()}");
    }

    // ── GET /service/settlement/history/{caseId} ──────────────────────────────

    [Fact]
    public async Task GetSettlementHistory_returns200_with_expected_keys()
    {
        var resp = await _client.GetAsync($"/service/settlement/history/{SeedHelper.CaseId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.TryGetProperty("settlements", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("reductions",   out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("payments",     out _).Should().BeTrue();
    }

    // ── Auth enforcement ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetSettlementHistory_without_auth_returns_401()
    {
        var anonClient = _factory.CreateClient();
        var resp = await anonClient.GetAsync(
            $"/service/settlement/history/{SeedHelper.CaseId}");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
