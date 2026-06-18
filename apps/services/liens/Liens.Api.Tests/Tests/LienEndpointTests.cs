using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class LienEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public LienEndpointTests(LiensApiFactory factory) => _factory = factory;

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
    public async Task CreateLien_defaults_lien_number_from_case_number_and_next_sequence()
    {
        var caseId = Guid.CreateVersion7();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Cases.Add(Case.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "26-000001",
                "Sequence",
                "Patient",
                SeedHelper.UserId));

            var caseEntity = db.Cases.Local.Single(c => c.CaseNumber == "26-000001");
            typeof(Case).GetProperty(nameof(Case.Id))!.SetValue(caseEntity, caseId);
            await db.SaveChangesAsync();
        }

        var first = await _client.PostAsJsonAsync("/api/liens/liens", new
        {
            lienNumber = "",
            lienType = LienType.MedicalLien,
            caseId,
            originalAmount = 100m,
        });

        first.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await first.Content.ReadAsStringAsync()}");
        var firstBody = await first.Content.ReadFromJsonAsync<LienResponseBody>();
        firstBody!.LienNumber.Should().Be("26-000001-01");

        var second = await _client.PostAsJsonAsync("/api/liens/liens", new
        {
            lienNumber = "",
            lienType = LienType.MedicalLien,
            caseId,
            originalAmount = 200m,
        });

        second.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await second.Content.ReadAsStringAsync()}");
        var secondBody = await second.Content.ReadFromJsonAsync<LienResponseBody>();
        secondBody!.LienNumber.Should().Be("26-000001-02");
    }

    private sealed class LienResponseBody
    {
        public string LienNumber { get; init; } = string.Empty;
    }
}
