using System.Net;
using System.Net.Http.Json;
using Commerce.Contracts.System;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests;

public class SystemEndpointsTests : IClassFixture<CommerceWebApplicationFactory>
{
    private readonly CommerceWebApplicationFactory _factory;

    public SystemEndpointsTests(CommerceWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SystemInfo_returns_commerce_service_metadata()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/commerce/system/info");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SystemInfoResponse>();
        body.Should().NotBeNull();
        body!.ServiceName.Should().Be("Commerce");
        body.Version.Should().NotBeNullOrWhiteSpace();
        body.Environment.Should().NotBeNullOrWhiteSpace();
        body.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }
}
