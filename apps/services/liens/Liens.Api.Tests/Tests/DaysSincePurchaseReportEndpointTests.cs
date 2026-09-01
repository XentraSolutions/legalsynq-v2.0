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

public class DaysSincePurchaseReportEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public DaysSincePurchaseReportEndpointTests(LiensApiFactory factory) => _factory = factory;

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
    public async Task RunReport_returns_negative_days_since_purchase_for_future_purchase_date()
    {
        var purchaseDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);
        var lienNumber = $"LIEN-DIY-FUTURE-{Guid.CreateVersion7():N}"[..30];

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Liens.Add(Lien.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                lienNumber,
                LienType.MedicalLien,
                100m,
                SeedHelper.UserId,
                isBulk: "N",
                purchaseDate: purchaseDate));
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/report/diy", new
        {
            reportType = "LIENS",
            search = lienNumber,
            isBulk = "N",
            columns = new[] { "lien_id", "purchase_date", "days_since_purchase" },
            page = 1,
            limit = 10,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = payload.RootElement.GetProperty("data").EnumerateArray().Single();
        row.GetProperty("lien_id").GetString().Should().Be(lienNumber);
        int.Parse(row.GetProperty("days_since_purchase").GetString()!).Should().BeNegative();
    }
}
