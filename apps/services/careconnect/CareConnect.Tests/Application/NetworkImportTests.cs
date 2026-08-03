using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

public class NetworkImportTests
{
    [Fact]
    public async Task ImportProvidersAsync_Execute_CreatesProviderAndMembership()
    {
        var tenantId = Guid.CreateVersion7();
        var networkId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var parser = new Mock<IProviderImportParser>();
        parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), "providers.csv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderImportParseResult(
                "providers.csv",
                1,
                [
                    new ProviderImportParsedRow(
                        2, "row-2", tenantId.ToString(), "Dr.", "Jane", "Smith",
                        "Smith Family Practice", "1234567890", "Jane@Example.com",
                        "555-0100", "123 Main St", "Chicago", "il", "60601",
                        "yes", "no",
                        SpecialtyCodesRaw: "Chiropractor",
                        LatitudeRaw: "41.881832",
                        LongitudeRaw: "-87.623177",
                        GeoPointSource: "nominatim")
                ]));

        Provider? createdProvider = null;
        NetworkProvider? createdMembership = null;
        var specialty = Specialty.Create("Chiropractor", "CHIROPRACTOR");
        List<Guid>? syncedSpecialtyIds = null;

        var networks = BuildRepositoryMock();
        networks.Setup(r => r.GetByIdGlobalAsync(networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "My Network", string.Empty));
        networks.Setup(r => r.GetProvidersByNpisAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetProvidersByTenantEmailsAsync(tenantId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetNetworkProviderIdsAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        networks.Setup(r => r.AddProviderToRegistryAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()))
            .Callback<Provider, CancellationToken>((provider, _) => createdProvider = provider)
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.AddProviderAsync(It.IsAny<NetworkProvider>(), It.IsAny<CancellationToken>()))
            .Callback<NetworkProvider, CancellationToken>((membership, _) => createdMembership = membership)
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.SyncProviderSpecialtiesAsync(It.IsAny<Guid>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, List<Guid>, CancellationToken>((_, ids, _) => syncedSpecialtyIds = ids)
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var specialties = new Mock<ISpecialtyRepository>();
        specialties.Setup(r => r.GetActiveByCodesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([specialty]);

        var sut = new NetworkService(networks.Object, Mock.Of<ICategoryRepository>(), specialties.Object, parser.Object, NullLogger<NetworkService>.Instance);

        await using var stream = new MemoryStream();
        var result = await sut.ImportProvidersAsync(networkId, stream, "providers.csv", dryRun: false, userId, CancellationToken.None);

        Assert.Equal(1, result.CreatedProviders);
        Assert.Equal(1, result.ProcessedRows);
        Assert.Equal(tenantId, result.TenantId);
        var row = Assert.Single(result.Rows);
        Assert.Equal("created", row.Status);
        Assert.NotNull(createdProvider);
        Assert.Equal("jane@example.com", createdProvider!.Email);
        Assert.Equal("Dr.", createdProvider.Title);
        Assert.Equal("Dr. Jane Smith", createdProvider.Name);
        Assert.Equal("IL", createdProvider.State);
        Assert.False(createdProvider.AcceptingReferrals);
        Assert.Equal(41.881832, createdProvider.Latitude);
        Assert.Equal(-87.623177, createdProvider.Longitude);
        Assert.Equal(GeoPointSource.Geocoded, createdProvider.GeoPointSource);
        Assert.NotNull(syncedSpecialtyIds);
        Assert.Equal(specialty.Id, Assert.Single(syncedSpecialtyIds!));
        Assert.NotNull(createdMembership);
        networks.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportProvidersAsync_DryRun_DoesNotWrite()
    {
        var tenantId = Guid.CreateVersion7();
        var networkId = Guid.CreateVersion7();
        var parser = new Mock<IProviderImportParser>();
        parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), "providers.csv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderImportParseResult(
                "providers.csv",
                1,
                [new ProviderImportParsedRow(
                    2, "row-2", tenantId.ToString(), null, "Jane", "Smith",
                    null, null, "jane@example.com", "555-0100", "123 Main St",
                    "Chicago", "IL", "60601", null, null,
                    SpecialtyCodesRaw: "Chiropractor")]));
        var specialty = Specialty.Create("Chiropractor", "CHIROPRACTOR");

        var networks = BuildRepositoryMock();
        networks.Setup(r => r.GetByIdGlobalAsync(networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "My Network", string.Empty));
        networks.Setup(r => r.GetProvidersByNpisAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetProvidersByTenantEmailsAsync(tenantId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetNetworkProviderIdsAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var specialties = new Mock<ISpecialtyRepository>();
        specialties.Setup(r => r.GetActiveByCodesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([specialty]);

        var sut = new NetworkService(networks.Object, Mock.Of<ICategoryRepository>(), specialties.Object, parser.Object, NullLogger<NetworkService>.Instance);

        await using var stream = new MemoryStream();
        var result = await sut.ImportProvidersAsync(networkId, stream, "providers.csv", dryRun: true, userId: null, CancellationToken.None);

        Assert.Equal(1, result.CreatedProviders);
        Assert.Equal("created", Assert.Single(result.Rows).Status);
        networks.Verify(r => r.AddProviderToRegistryAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()), Times.Never);
        networks.Verify(r => r.AddProviderAsync(It.IsAny<NetworkProvider>(), It.IsAny<CancellationToken>()), Times.Never);
        networks.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportProvidersAsync_NewProviderWithoutSpecialty_FailsRowWithoutWriting()
    {
        var tenantId = Guid.CreateVersion7();
        var networkId = Guid.CreateVersion7();
        var parser = new Mock<IProviderImportParser>();
        parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), "providers.csv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderImportParseResult(
                "providers.csv",
                1,
                [new ProviderImportParsedRow(
                    2, "row-2", tenantId.ToString(), null, "Jane", "Smith",
                    null, null, "jane@example.com", "555-0100", "123 Main St",
                    "Chicago", "IL", "60601", null, null)]));

        var networks = BuildRepositoryMock();
        networks.Setup(r => r.GetByIdGlobalAsync(networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "My Network", string.Empty));
        networks.Setup(r => r.GetProvidersByNpisAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetProvidersByTenantEmailsAsync(tenantId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetNetworkProviderIdsAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = new NetworkService(networks.Object, Mock.Of<ICategoryRepository>(), Mock.Of<ISpecialtyRepository>(), parser.Object, NullLogger<NetworkService>.Instance);

        await using var stream = new MemoryStream();
        var result = await sut.ImportProvidersAsync(networkId, stream, "providers.csv", dryRun: false, userId: null, CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal("failed", row.Status);
        Assert.Contains("Select at least one specialty.", row.Errors);
        networks.Verify(r => r.AddProviderToRegistryAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()), Times.Never);
        networks.Verify(r => r.AddProviderAsync(It.IsAny<NetworkProvider>(), It.IsAny<CancellationToken>()), Times.Never);
        networks.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportProvidersAsync_ReusesByNpiAndMarksAlreadyInNetwork()
    {
        var tenantId = Guid.CreateVersion7();
        var networkId = Guid.CreateVersion7();
        var existingProvider = Provider.Create(
            tenantId,
            "Jane Smith",
            "Smith Family Practice",
            "jane@example.com",
            "555-0100",
            "123 Main St",
            "Chicago",
            "IL",
            "60601",
            true,
            true,
            null,
            npi: "1234567890",
            firstName: "Jane",
            lastName: "Smith");

        var parser = new Mock<IProviderImportParser>();
        parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), "providers.csv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderImportParseResult(
                "providers.csv",
                1,
                [new ProviderImportParsedRow(
                    2, "row-2", tenantId.ToString(), null, "Jane", "Smith",
                    null, "1234567890", "jane@example.com", "555-0100",
                    "123 Main St", "Chicago", "IL", "60601", null, null)]));

        var networks = BuildRepositoryMock();
        networks.Setup(r => r.GetByIdGlobalAsync(networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "My Network", string.Empty));
        networks.Setup(r => r.GetProvidersByNpisAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal) { ["1234567890"] = existingProvider });
        networks.Setup(r => r.GetProvidersByTenantEmailsAsync(tenantId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal) { ["jane@example.com"] = existingProvider });
        networks.Setup(r => r.GetNetworkProviderIdsAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingProvider.Id]);

        var sut = new NetworkService(networks.Object, Mock.Of<ICategoryRepository>(), Mock.Of<ISpecialtyRepository>(), parser.Object, NullLogger<NetworkService>.Instance);

        await using var stream = new MemoryStream();
        var result = await sut.ImportProvidersAsync(networkId, stream, "providers.csv", dryRun: false, userId: null, CancellationToken.None);

        Assert.Equal(1, result.AlreadyInNetwork);
        Assert.Equal(0, result.ReusedByNpi);
        Assert.Equal("already_in_network", Assert.Single(result.Rows).Status);
        networks.Verify(r => r.AddProviderAsync(It.IsAny<NetworkProvider>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportProvidersAsync_InvalidBoolean_FailsRowWithoutWriting()
    {
        var tenantId = Guid.CreateVersion7();
        var networkId = Guid.CreateVersion7();
        var parser = new Mock<IProviderImportParser>();
        parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), "providers.csv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderImportParseResult(
                "providers.csv",
                1,
                [new ProviderImportParsedRow(
                    2, "row-2", tenantId.ToString(), null, "Jane", "Smith",
                    null, null, "jane@example.com", "555-0100", "123 Main St",
                    "Chicago", "IL", "60601", "maybe", null,
                    SpecialtyCodesRaw: "Chiropractor")]));

        var networks = BuildRepositoryMock();
        networks.Setup(r => r.GetByIdGlobalAsync(networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "My Network", string.Empty));
        networks.Setup(r => r.GetProvidersByNpisAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetProvidersByTenantEmailsAsync(tenantId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetNetworkProviderIdsAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = new NetworkService(networks.Object, Mock.Of<ICategoryRepository>(), Mock.Of<ISpecialtyRepository>(), parser.Object, NullLogger<NetworkService>.Instance);

        await using var stream = new MemoryStream();
        var result = await sut.ImportProvidersAsync(networkId, stream, "providers.csv", dryRun: false, userId: null, CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal("failed", row.Status);
        Assert.Single(row.Errors);
        networks.Verify(r => r.AddProviderToRegistryAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportProvidersAsync_NetworkNotFound_ThrowsNotFoundException()
    {
        var parser = new Mock<IProviderImportParser>();
        var networks = BuildRepositoryMock();
        networks.Setup(r => r.GetByIdGlobalAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProviderNetwork?)null);

        var sut = new NetworkService(networks.Object, Mock.Of<ICategoryRepository>(), Mock.Of<ISpecialtyRepository>(), parser.Object, NullLogger<NetworkService>.Instance);

        await using var stream = new MemoryStream();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.ImportProvidersAsync(Guid.CreateVersion7(), stream, "providers.csv", false, null, CancellationToken.None));
    }

    private static Mock<INetworkRepository> BuildRepositoryMock()
    {
        var mock = new Mock<INetworkRepository>(MockBehavior.Strict);
        mock.Setup(r => r.ClearTracking());
        return mock;
    }
}
