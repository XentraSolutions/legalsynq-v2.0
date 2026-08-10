using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public sealed class AssistantToolEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public AssistantToolEndpointTests(LiensApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedHelper.SeedAsync(scope.ServiceProvider);

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetLien_routes_return_latest_recorded_reduction_amount()
    {
        var routes = new[]
        {
            $"/api/assistant-tools/liens/{SeedHelper.LienId}",
            "/api/assistant-tools/liens/by-number/LIEN-TEST-001",
        };

        foreach (var route in routes)
        {
            var response = await _client.GetAsync(route);

            response.StatusCode.Should().Be(
                HttpStatusCode.OK,
                $"Body: {await response.Content.ReadAsStringAsync()}");

            var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
            document!.RootElement
                .GetProperty("lien")
                .GetProperty("reductionAmount")
                .GetDecimal()
                .Should()
                .Be(500m);
        }
    }
}
