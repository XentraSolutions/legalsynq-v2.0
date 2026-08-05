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

    [Fact]
    public async Task CreateReduction_accepts_legacy_bulk_payload()
    {
        var resp = await _client.PostAsJsonAsync("/service/liens/update/reduction", new
        {
            caseId = SeedHelper.CaseId,
            data = new[]
            {
                new
                {
                    liensId = SeedHelper.LienId,
                    reductionAmount = 111.1m,
                },
            },
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        var historyResp = await _client.GetAsync($"/service/settlement/history/{SeedHelper.CaseId}");
        historyResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await historyResp.Content.ReadAsStringAsync()}");

        var doc = await historyResp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.GetProperty("reductions")
            .EnumerateArray()
            .Should()
            .Contain(item =>
                item.GetProperty("lienId").GetGuid() == SeedHelper.LienId &&
                item.GetProperty("amount").GetDecimal() == 111.1m);
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

    [Theory]
    [InlineData("Open", LienStatus.Active)]
    [InlineData("Closed", LienStatus.Settled)]
    public async Task CreateSettlement_updates_lien_status_for_open_and_closed(
        string settlementStatus,
        string expectedLienStatus)
    {
        Lien lien;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"SETTLEMENT-STATUS-{Guid.CreateVersion7():N}",
                LienType.MedicalLien,
                1_000m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId);
            db.Liens.Add(lien);
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/liens/settlement/create", new
        {
            caseId = SeedHelper.CaseId,
            lienId = lien.Id,
            paymentNumber = 1,
            amount = 1_000m,
            status = settlementStatus,
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await resp.Content.ReadAsStringAsync()}");

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var persistedLien = await verificationDb.Liens.FindAsync(lien.Id);
        persistedLien!.Status.Should().Be(expectedLienStatus);
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

    [Fact]
    public async Task PaymentDetails_returns_fields_sent_by_the_current_payment_form()
    {
        Guid settlementTypeId;
        Guid settlementStatusId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            settlementTypeId = db.LookupValues.Single(x =>
                x.Category == LookupCategory.SettlementType && x.Code == "Full").Id;
            settlementStatusId = db.LookupValues.Single(x =>
                x.Category == LookupCategory.SettlementStatus && x.Code == "Pending").Id;
        }

        var createResponse = await _client.PostAsJsonAsync("/api/liens/settlement/payments", new
        {
            caseId = SeedHelper.CaseId,
            lienId = SeedHelper.LienId,
            amount = 100m,
            paymentDate = "2026-08-05",
            paymentMethod = "Check",
            referenceNumber = "CHK-CURRENT-UI",
            notes = "Payment received from counsel.",
            settlementType = settlementTypeId,
            settlementStatus = settlementStatusId,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await createResponse.Content.ReadAsStringAsync()}");

        var detailsResponse = await _client.GetAsync(
            $"/service/liens/settlement/payment-details/{SeedHelper.CaseId}");
        detailsResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await detailsResponse.Content.ReadAsStringAsync()}");

        var payload = await detailsResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var payment = payload!.RootElement.GetProperty("data").EnumerateArray().Single(item =>
            item.GetProperty("checkNumber").GetString() == "CHK-CURRENT-UI");

        payment.GetProperty("payor").GetString().Should().Be("Check");
        payment.GetProperty("note").GetString().Should().Be("Payment received from counsel.");
        payment.GetProperty("typeId").GetString().Should().Be(settlementTypeId.ToString());
        payment.GetProperty("type").GetString().Should().Be("Full Settlement");
        payment.GetProperty("statusId").GetString().Should().Be(settlementStatusId.ToString());
        payment.GetProperty("status").GetString().Should().Be("Pending");
        payment.GetProperty("netProfit").GetString().Should().Be("0.00");
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
