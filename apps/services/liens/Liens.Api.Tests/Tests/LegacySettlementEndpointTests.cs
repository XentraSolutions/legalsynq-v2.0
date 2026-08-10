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
    public async Task CreatePayment_with_closed_lien_status_preserves_settlement_fields_and_moves_lien_to_closed_list()
    {
        Lien lien;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"PAYMENT-CLOSED-{Guid.CreateVersion7():N}",
                LienType.MedicalLien,
                1_000m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId);
            lien.SetLegacyMedicalStatus("Open", SeedHelper.UserId);
            db.Liens.Add(lien);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/liens/settlement/payments", new
        {
            amount = 0,
            caseId = SeedHelper.CaseId,
            lienId = lien.Id,
            notes = "",
            paymentDate = "2026-08-05",
            paymentMethod = "Check",
            referenceNumber = "123123123",
            lienStatus = "Closed",
            settlementType = "by_attorney",
            settlementStatus = "full_payment",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var createdPayment = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var createdPaymentId = createdPayment!.RootElement.GetProperty("id").GetGuid();
        var createdPaymentNumber = createdPayment.RootElement.GetProperty("paymentNumber").GetInt32();
        createdPaymentNumber.Should().BePositive();

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var persistedLien = await verificationDb.Liens.FindAsync(lien.Id);
        persistedLien!.Status.Should().Be(LienStatus.Settled);
        persistedLien.ClosedAtUtc.Should().NotBeNull();

        var closedListResponse = await _client.GetAsync(
            $"/api/liens/liens/?search={Uri.EscapeDataString(lien.LienNumber)}&status=Closed&page=1&pageSize=20");
        closedListResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await closedListResponse.Content.ReadAsStringAsync()}");

        var closedList = await closedListResponse.Content.ReadFromJsonAsync<JsonDocument>();
        closedList!.RootElement.GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetGuid() == lien.Id);

        var paymentDetailsResponse = await _client.GetAsync(
            $"/service/liens/settlement/payment-details/{SeedHelper.CaseId}");
        paymentDetailsResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await paymentDetailsResponse.Content.ReadAsStringAsync()}");

        var paymentDetails = await paymentDetailsResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var recordedPayment = paymentDetails!.RootElement.GetProperty("data").EnumerateArray()
            .Single(item => item.GetProperty("checkNumber").GetString() == "123123123");
        recordedPayment.GetProperty("id").GetGuid().Should().Be(createdPaymentId);
        recordedPayment.GetProperty("paymentNumber").GetString().Should().Be(createdPaymentNumber.ToString());
        recordedPayment.GetProperty("amount").GetString().Should().Be("0.00");
        recordedPayment.GetProperty("amountToSettle").GetString().Should().Be("1000.00");
        recordedPayment.GetProperty("checkAmount").GetString().Should().Be("1000.00");
        recordedPayment.GetProperty("lienStatus").GetString().Should().Be("Closed");
        recordedPayment.GetProperty("lienStatusId").GetString().Should().Be(LienStatus.Settled);
        recordedPayment.GetProperty("typeId").GetString().Should().Be("by_attorney");
        recordedPayment.GetProperty("type").GetString().Should().Be("By Attorney");
        recordedPayment.GetProperty("statusId").GetString().Should().Be("full_payment");
        recordedPayment.GetProperty("status").GetString().Should().Be("Full Payment");
    }

    [Fact]
    public async Task CreatePayment_current_frontend_payload_keeps_full_payment_separate_from_closed_lien_status()
    {
        Lien lien;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"PAYMENT-CURRENT-PAYLOAD-{Guid.CreateVersion7():N}",
                LienType.MedicalLien,
                3_590m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId);
            lien.SetLegacyMedicalStatus("Open", SeedHelper.UserId);
            db.Liens.Add(lien);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/liens/settlement/payments", new
        {
            caseId = SeedHelper.CaseId,
            lienId = lien.Id,
            amount = 3_590m,
            paymentDate = "2026-08-06",
            paymentMethod = "Check",
            referenceNumber = "453346",
            notes = "",
            settlementType = "by_attorney",
            settlementStatus = "full_payment",
            lienStatus = "Closed",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var created = await response.Content.ReadFromJsonAsync<JsonDocument>();
        created!.RootElement.GetProperty("settlementTypeId").GetString().Should().Be("by_attorney");
        created.RootElement.GetProperty("settlementStatusId").GetString().Should().Be("full_payment");
        var paymentId = created.RootElement.GetProperty("id").GetGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var persistedPayment = await db.SettlementPaymentDetails.FindAsync(paymentId);
            persistedPayment!.Note.Should().Contain("type=by_attorney");
            persistedPayment.Note.Should().Contain("status=full_payment");
            persistedPayment.Note.Should().NotContain("status=Closed");
        }

        var detailsResponse = await _client.GetAsync(
            $"/service/liens/settlement/payment-details/{SeedHelper.CaseId}");
        detailsResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await detailsResponse.Content.ReadAsStringAsync()}");
        var details = await detailsResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var payment = details!.RootElement.GetProperty("data").EnumerateArray().Single(item =>
            item.GetProperty("checkNumber").GetString() == "453346");

        payment.GetProperty("typeId").GetString().Should().Be("by_attorney");
        payment.GetProperty("type").GetString().Should().Be("By Attorney");
        payment.GetProperty("statusId").GetString().Should().Be("full_payment");
        payment.GetProperty("status").GetString().Should().Be("Full Payment");
        payment.GetProperty("lienStatus").GetString().Should().Be("Closed");
    }

    [Fact]
    public async Task PaymentDetails_uses_recorded_amount_when_closed_lien_balance_is_zero()
    {
        Lien lien;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            lien = Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                $"PAYMENT-ZERO-BALANCE-{Guid.CreateVersion7():N}",
                LienType.MedicalLien,
                1_200m,
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId);
            lien.SetLegacyMedicalStatus("Open", SeedHelper.UserId);
            lien.Settle(1_200m, SeedHelper.UserId);
            db.Liens.Add(lien);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/liens/settlement/payments", new
        {
            amount = 1_200m,
            caseId = SeedHelper.CaseId,
            lienId = lien.Id,
            paymentDate = "2026-08-06",
            paymentMethod = "Check",
            referenceNumber = "CHK-ZERO-BALANCE",
            settlementType = "by_attorney",
            settlementStatus = "full_payment",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var detailsResponse = await _client.GetAsync(
            $"/service/liens/settlement/payment-details/{SeedHelper.CaseId}");
        var details = await detailsResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var payment = details!.RootElement.GetProperty("data").EnumerateArray().Single(item =>
            item.GetProperty("checkNumber").GetString() == "CHK-ZERO-BALANCE");

        payment.GetProperty("paymentNumber").GetString().Should().NotBe("0");
        payment.GetProperty("amountToSettle").GetString().Should().Be("1200.00");
        payment.GetProperty("checkAmount").GetString().Should().Be("1200.00");
    }

    [Fact]
    public async Task PaymentDetails_assigns_distinct_display_numbers_to_historical_zero_number_rows()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.SettlementPaymentDetails.AddRange(
                SettlementPaymentDetail.Create(
                    SeedHelper.TenantId,
                    SeedHelper.CaseId,
                    SeedHelper.LienId,
                    0,
                    250m,
                    SeedHelper.UserId,
                    new DateOnly(2026, 8, 6),
                    checkNumber: "CHK-HISTORICAL-ZERO-1"),
                SettlementPaymentDetail.Create(
                    SeedHelper.TenantId,
                    SeedHelper.CaseId,
                    SeedHelper.LienId,
                    0,
                    300m,
                    SeedHelper.UserId,
                    new DateOnly(2026, 8, 6),
                    checkNumber: "CHK-HISTORICAL-ZERO-2"));
            await db.SaveChangesAsync();
        }

        var detailsResponse = await _client.GetAsync(
            $"/service/liens/settlement/payment-details/{SeedHelper.CaseId}");
        var details = await detailsResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var payments = details!.RootElement.GetProperty("data").EnumerateArray()
            .Where(item => item.GetProperty("checkNumber").GetString() is
                "CHK-HISTORICAL-ZERO-1" or "CHK-HISTORICAL-ZERO-2")
            .ToList();

        payments.Should().HaveCount(2);
        payments.Select(item => item.GetProperty("paymentNumber").GetString())
            .Should().OnlyHaveUniqueItems().And.NotContain("0");
    }

    [Fact]
    public async Task CreatePayment_legacy_closed_settlement_status_still_moves_lien_to_closed_list()
    {
        var response = await _client.PostAsJsonAsync("/api/liens/settlement/payments", new
        {
            amount = 100m,
            caseId = SeedHelper.CaseId,
            lienId = SeedHelper.LienId,
            paymentDate = "2026-08-05",
            paymentMethod = "Check",
            referenceNumber = "CHK-LEGACY-CLOSED",
            settlementStatus = "Closed",
            settlementType = "full_payment",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var persistedLien = await db.Liens.FindAsync(SeedHelper.LienId);
        persistedLien!.Status.Should().Be(LienStatus.Settled);

        var detailsResponse = await _client.GetAsync(
            $"/service/liens/settlement/payment-details/{SeedHelper.CaseId}");
        var details = await detailsResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var payment = details!.RootElement.GetProperty("data").EnumerateArray().Single(item =>
            item.GetProperty("checkNumber").GetString() == "CHK-LEGACY-CLOSED");
        payment.GetProperty("typeId").GetString().Should().Be("other");
        payment.GetProperty("type").GetString().Should().Be("Other");
        payment.GetProperty("statusId").GetString().Should().Be("full_payment");
        payment.GetProperty("status").GetString().Should().Be("Full Payment");
    }

    [Theory]
    [InlineData("by_attorney", "By Attorney")]
    [InlineData("by_medical_provider", "By Medical Provider")]
    [InlineData("by_funding_company", "By Funding Company")]
    [InlineData("other", "Other")]
    public async Task PaymentDetails_returns_each_supported_settlement_type(
        string settlementType,
        string expectedDisplayName)
    {
        var checkNumber = $"CHK-TYPE-{settlementType}";
        var createResponse = await _client.PostAsJsonAsync("/api/liens/settlement/payments", new
        {
            caseId = SeedHelper.CaseId,
            lienId = SeedHelper.LienId,
            amount = 100m,
            paymentDate = "2026-08-06",
            paymentMethod = "Check",
            referenceNumber = checkNumber,
            settlementType,
            settlementStatus = "full_payment",
            lienStatus = "Active",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await createResponse.Content.ReadAsStringAsync()}");
        var createdPayment = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        createdPayment!.RootElement.GetProperty("settlementTypeId").GetString()
            .Should().Be(settlementType);

        var detailsResponse = await _client.GetAsync(
            $"/service/liens/settlement/payment-details/{SeedHelper.CaseId}");
        var details = await detailsResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var payment = details!.RootElement.GetProperty("data").EnumerateArray().Single(item =>
            item.GetProperty("checkNumber").GetString() == checkNumber);

        payment.GetProperty("typeId").GetString().Should().Be(settlementType);
        payment.GetProperty("type").GetString().Should().Be(expectedDisplayName);
    }

    [Fact]
    public async Task CreatePayment_accepts_legacy_type_and_status_aliases()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/liens/settlement/payments", new
        {
            caseId = SeedHelper.CaseId,
            lienId = SeedHelper.LienId,
            amount = 100m,
            paymentDate = "2026-08-06",
            paymentMethod = "Check",
            referenceNumber = "CHK-LEGACY-ALIASES",
            type = "by_medical_provider",
            status = "full_payment",
            lienStatus = "Active",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await createResponse.Content.ReadAsStringAsync()}");

        var detailsResponse = await _client.GetAsync(
            $"/service/liens/settlement/payment-details/{SeedHelper.CaseId}");
        var details = await detailsResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var payment = details!.RootElement.GetProperty("data").EnumerateArray().Single(item =>
            item.GetProperty("checkNumber").GetString() == "CHK-LEGACY-ALIASES");

        payment.GetProperty("typeId").GetString().Should().Be("by_medical_provider");
        payment.GetProperty("type").GetString().Should().Be("By Medical Provider");
        payment.GetProperty("statusId").GetString().Should().Be("full_payment");
        payment.GetProperty("status").GetString().Should().Be("Full Payment");
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
                x.Category == LookupCategory.SettlementStatus && x.Code == "Pending").Id;
            settlementStatusId = db.LookupValues.Single(x =>
                x.Category == LookupCategory.SettlementType && x.Code == "Full").Id;
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
        payment.GetProperty("type").GetString().Should().Be("Pending");
        payment.GetProperty("statusId").GetString().Should().Be(settlementStatusId.ToString());
        payment.GetProperty("status").GetString().Should().Be("Full Settlement");
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
