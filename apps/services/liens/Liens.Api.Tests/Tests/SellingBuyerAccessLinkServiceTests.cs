using BuildingBlocks.Exceptions;
using Liens.Infrastructure.Persistence;
using Liens.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Liens.Api.Tests.Tests;

public sealed class SellingBuyerAccessLinkServiceTests
{
    [Theory]
    [InlineData("http://localhost:5009/api/liens/selling/public")]
    [InlineData("http://127.0.0.1:5009/api/liens/selling/public/{token}")]
    public async Task CreateOrGetForConfirmSaleAsync_rejects_loopback_buyer_portal_base_url(string buyerPortalBaseUrl)
    {
        await using var db = new LiensDbContext(new DbContextOptionsBuilder<LiensDbContext>()
            .UseInMemoryDatabase($"selling-buyer-access-link-{Guid.CreateVersion7()}")
            .Options);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Liens:Selling:BuyerPortalBaseUrl"] = buyerPortalBaseUrl,
            })
            .Build();
        var service = new SellingBuyerAccessLinkService(db, configuration);

        var act = () => service.CreateOrGetForConfirmSaleAsync(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "confirm-sale-loopback-url",
            TimeSpan.FromDays(30));

        var error = await act.Should().ThrowAsync<ValidationException>();
        error.Which.Message.Should().Contain("externally reachable");
    }

    [Fact]
    public async Task CreateOrGetForConfirmSaleAsync_allows_named_localhost_demo_alias()
    {
        await using var db = new LiensDbContext(new DbContextOptionsBuilder<LiensDbContext>()
            .UseInMemoryDatabase($"selling-buyer-access-link-{Guid.CreateVersion7()}")
            .Options);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Liens:Selling:BuyerPortalBaseUrl"] = "http://synqlien-demo.localhost:5000/selling/public",
            })
            .Build();
        var service = new SellingBuyerAccessLinkService(db, configuration);

        var result = await service.CreateOrGetForConfirmSaleAsync(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "confirm-sale-localhost-alias",
            TimeSpan.FromDays(30));

        result.BuyerPortalUrl.Should().StartWith("http://synqlien-demo.localhost:5000/selling/public/");
    }

    [Fact]
    public async Task CreateOrGetForConfirmSaleAsync_derives_localhost_demo_alias_from_synqlien_portal_hostname()
    {
        await using var db = new LiensDbContext(new DbContextOptionsBuilder<LiensDbContext>()
            .UseInMemoryDatabase($"selling-buyer-access-link-{Guid.CreateVersion7()}")
            .Options);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SYNQLIEN_COMMON_PORTAL_HOSTNAME"] = "synqlien-demo.localhost",
            })
            .Build();
        var service = new SellingBuyerAccessLinkService(db, configuration);

        var result = await service.CreateOrGetForConfirmSaleAsync(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "confirm-sale-derived-localhost-alias",
            TimeSpan.FromDays(30));

        result.BuyerPortalUrl.Should().StartWith("http://synqlien-demo.localhost:5000/selling/public/");
    }
}
