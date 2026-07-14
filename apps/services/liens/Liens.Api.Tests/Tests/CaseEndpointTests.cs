using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Liens.Api.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class CaseEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public CaseEndpointTests(LiensApiFactory factory) => _factory = factory;

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
    public async Task CreateCase_defaults_case_number_from_current_year_and_next_sequence()
    {
        var yearPrefix = DateTime.UtcNow.ToString("yy");

        var first = await _client.PostAsJsonAsync("/api/liens/cases", new
        {
            caseNumber = "",
            clientFirstName = "Case",
            clientLastName = "One",
        });

        first.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await first.Content.ReadAsStringAsync()}");
        var firstBody = await first.Content.ReadFromJsonAsync<CaseResponseBody>();
        firstBody!.CaseNumber.Should().Be($"{yearPrefix}-000001");

        var second = await _client.PostAsJsonAsync("/api/liens/cases", new
        {
            caseNumber = "",
            clientFirstName = "Case",
            clientLastName = "Two",
        });

        second.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Body: {await second.Content.ReadAsStringAsync()}");
        var secondBody = await second.Content.ReadFromJsonAsync<CaseResponseBody>();
        secondBody!.CaseNumber.Should().Be($"{yearPrefix}-000002");
    }

    private sealed class CaseResponseBody
    {
        public string CaseNumber { get; init; } = string.Empty;
    }
}
