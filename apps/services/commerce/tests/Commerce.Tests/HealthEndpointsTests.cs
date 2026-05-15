using System.Net;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests;

public class HealthEndpointsTests : IClassFixture<CommerceWebApplicationFactory>
{
    private readonly CommerceWebApplicationFactory _factory;

    public HealthEndpointsTests(CommerceWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_returns_503_when_db_not_configured()
    {
        // Test factory forces empty connection string -> readiness must report degraded.
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/ready");
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("not-configured");
    }
}
