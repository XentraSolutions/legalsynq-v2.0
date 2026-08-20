using Contracts.DeepLinks;

namespace BuildingBlocks.Tests;

public sealed class DeepLinkRouteRegistryTests
{
    [Fact]
    public void Registry_LoadsTheFiveAuthoritativeRoutesOnce()
    {
        Assert.Equal(1, DeepLinkRouteRegistry.Version);
        Assert.Equal(5, DeepLinkRouteRegistry.All.Count);
        Assert.Equal(
            ["dashboard", "dealDetails", "contactDetails", "applicationDetails", "reportDetails"],
            DeepLinkRouteRegistry.All.Select(route => route.Key));
        Assert.Equal(5, DeepLinkRouteRegistry.All.Select(route => route.Key).Distinct().Count());
    }

    [Fact]
    public void Registry_ExposesTypedRouteMetadata()
    {
        var route = Assert.Single(
            DeepLinkRouteRegistry.All,
            candidate => candidate.Key == "dealDetails");

        Assert.Equal("/deals/:dealId", route.PathTemplate);
        Assert.Equal(["dealId"], route.RequiredPathParameters);
        Assert.Empty(route.OptionalQueryParameters);
        Assert.True(route.RequiresAuthentication);
        Assert.True(route.RequiresAuthorization);
        Assert.True(route.Enabled);
    }

    [Fact]
    public void Registry_LookupIsExplicitForUnknownRoutes()
    {
        Assert.True(DeepLinkRouteRegistry.TryGet("dashboard", out var route));
        Assert.Equal("/dashboard", route.PathTemplate);
        Assert.False(DeepLinkRouteRegistry.TryGet("unknown", out _));
        Assert.Null(DeepLinkRouteRegistry.Get("unknown"));
    }
}
