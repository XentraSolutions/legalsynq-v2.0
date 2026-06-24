using CareConnect.Application.Interfaces;
using CareConnect.Application.Services;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

public class ReferralTenantNameResolverTests
{
    [Fact]
    public async Task ResolveAsync_DeduplicatesTenantIds_AndAppliesFallback()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();

        var tenantClient = new Mock<ITenantServiceClient>();
        tenantClient
            .Setup(c => c.GetDisplayNameAsync(tenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Tenant A");
        tenantClient
            .Setup(c => c.GetDisplayNameAsync(tenantB, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await ReferralTenantNameResolver.ResolveAsync(
            [tenantA, tenantA, tenantB],
            tenantClient.Object);

        Assert.Equal("Tenant A", result[tenantA]);
        Assert.Equal(ReferralTenantNameResolver.Fallback, result[tenantB]);
        tenantClient.Verify(
            c => c.GetDisplayNameAsync(tenantA, It.IsAny<CancellationToken>()),
            Times.Once);
        tenantClient.Verify(
            c => c.GetDisplayNameAsync(tenantB, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
