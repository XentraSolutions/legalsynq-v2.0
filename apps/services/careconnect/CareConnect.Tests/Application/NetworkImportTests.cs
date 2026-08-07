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
                    ImportRow(
                        tenantId,
                        title: "Dr.",
                        firstName: "Jane",
                        lastName: "Smith",
                        organizationName: "Smith Family Practice",
                        facilityName: "Smith Family Practice",
                        npi: "1234567890",
                        email: "Jane@Example.com",
                        isActive: "yes",
                        acceptingReferrals: "no",
                        SpecialtyCodesRaw: "Chiropractor",
                        LatitudeRaw: "41.881832",
                        LongitudeRaw: "-87.623177",
                        GeoPointSource: "nominatim")
                ]));

        Provider? createdProvider = null;
        Facility? createdFacility = null;
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
        networks.Setup(r => r.GetNetworkProviderLocationKeysAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        networks.Setup(r => r.AddFacilityAsync(It.IsAny<Facility>(), It.IsAny<CancellationToken>()))
            .Callback<Facility, CancellationToken>((facility, _) => createdFacility = facility)
            .Returns(Task.CompletedTask);
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
        Assert.Equal("3125550100", createdProvider.Phone);
        Assert.Equal("Dr.", createdProvider.Title);
        Assert.Equal("Dr. Jane Smith", createdProvider.Name);
        Assert.Equal("IL", createdProvider.State);
        Assert.False(createdProvider.AcceptingReferrals);
        Assert.Equal(41.881832, createdProvider.Latitude);
        Assert.Equal(-87.623177, createdProvider.Longitude);
        Assert.Equal(GeoPointSource.Geocoded, createdProvider.GeoPointSource);
        Assert.NotNull(createdFacility);
        Assert.Equal("Smith Family Practice", createdFacility!.Name);
        Assert.Equal("Chicago", createdFacility.City);
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
                [ImportRow(tenantId, SpecialtyCodesRaw: "Chiropractor")]));
        var specialty = Specialty.Create("Chiropractor", "CHIROPRACTOR");

        var networks = BuildRepositoryMock();
        networks.Setup(r => r.GetByIdGlobalAsync(networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "My Network", string.Empty));
        networks.Setup(r => r.GetProvidersByNpisAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetProvidersByTenantEmailsAsync(tenantId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetNetworkProviderLocationKeysAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
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
    public async Task ImportProvidersAsync_RowWithoutTenantId_UsesTargetNetworkTenant()
    {
        var tenantId = Guid.CreateVersion7();
        var networkId = Guid.CreateVersion7();
        var parser = new Mock<IProviderImportParser>();
        parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), "providers.xlsx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderImportParseResult(
                "providers.xlsx",
                1,
                [
                    ImportRow(
                        null,
                        providerName: "Dr. Stuart Baird",
                        firstName: null,
                        lastName: null,
                        organizationName: null,
                        facilityName: "Precision Pain Center",
                        npi: "1336383504",
                        email: "info@precisionpaincenter.com",
                        phone: "702-781-1700",
                        addressLine1: "7380 W. Sahara Ave Ste. 160",
                        city: "Las Vegas",
                        state: "NV",
                        postalCode: "89117",
                        SpecialtyCodesRaw: "Pain")
                ]));
        var pain = Specialty.Create("Pain", "PAIN");

        var networks = BuildRepositoryMock();
        networks.Setup(r => r.GetByIdGlobalAsync(networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "My Network", string.Empty));
        networks.Setup(r => r.GetProvidersByNpisAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetProvidersByTenantEmailsAsync(tenantId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetNetworkProviderLocationKeysAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var specialties = new Mock<ISpecialtyRepository>();
        specialties.Setup(r => r.GetActiveByCodesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pain]);

        var sut = new NetworkService(networks.Object, Mock.Of<ICategoryRepository>(), specialties.Object, parser.Object, NullLogger<NetworkService>.Instance);

        await using var stream = new MemoryStream();
        var result = await sut.ImportProvidersAsync(networkId, stream, "providers.xlsx", dryRun: true, userId: null, CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal("created", row.Status);
        Assert.NotNull(row.NormalizedProvider);
        Assert.Equal(tenantId, row.NormalizedProvider!.TenantId);
        Assert.Equal("Dr.", row.NormalizedProvider.Title);
        Assert.Equal("Stuart", row.NormalizedProvider.FirstName);
        Assert.Equal("Baird", row.NormalizedProvider.LastName);
        Assert.Equal("Precision Pain Center", row.NormalizedProvider.FacilityName);
        networks.Verify(r => r.AddProviderToRegistryAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()), Times.Never);
        networks.Verify(r => r.AddProviderAsync(It.IsAny<NetworkProvider>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportProvidersAsync_RowWithNoProviderName_LeavesFirstAndLastNameEmpty()
    {
        var tenantId = Guid.CreateVersion7();
        var networkId = Guid.CreateVersion7();
        var parser = new Mock<IProviderImportParser>();
        parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), "providers.csv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderImportParseResult(
                "providers.csv",
                1,
                [
                    ImportRow(
                        tenantId,
                        title: null,
                        providerName: null,
                        firstName: null,
                        lastName: null,
                        organizationName: null,
                        facilityName: "Precision Pain Center",
                        npi: "1336383504",
                        email: "info@precisionpaincenter.com",
                        SpecialtyCodesRaw: "Pain")
                ]));
        var pain = Specialty.Create("Pain", "PAIN");

        var networks = BuildRepositoryMock();
        networks.Setup(r => r.GetByIdGlobalAsync(networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "My Network", string.Empty));
        networks.Setup(r => r.GetProvidersByNpisAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetProvidersByTenantEmailsAsync(tenantId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetNetworkProviderLocationKeysAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var specialties = new Mock<ISpecialtyRepository>();
        specialties.Setup(r => r.GetActiveByCodesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pain]);

        var sut = new NetworkService(networks.Object, Mock.Of<ICategoryRepository>(), specialties.Object, parser.Object, NullLogger<NetworkService>.Instance);

        await using var stream = new MemoryStream();
        var result = await sut.ImportProvidersAsync(networkId, stream, "providers.csv", dryRun: true, userId: null, CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal("created", row.Status);
        Assert.NotNull(row.NormalizedProvider);
        Assert.Equal(string.Empty, row.NormalizedProvider!.FirstName);
        Assert.Equal(string.Empty, row.NormalizedProvider.LastName);
        Assert.Equal("Precision Pain Center", row.NormalizedProvider.FacilityName);
        Assert.Equal("Precision Pain Center", row.NormalizedProvider.OrganizationName);
    }

    [Fact]
    public async Task ImportProvidersAsync_SameNpiDifferentLocations_CreatesOneProviderWithMultipleLocationMemberships()
    {
        var tenantId = Guid.CreateVersion7();
        var networkId = Guid.CreateVersion7();
        var parser = new Mock<IProviderImportParser>();
        parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), "providers.csv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderImportParseResult(
                "providers.csv",
                2,
                [
                    ImportRow(
                        tenantId,
                        npi: "1234567890",
                        SpecialtyCodesRaw: "Pain",
                        PrimarySpecialtyCode: "Pain"),
                    ImportRow(
                        tenantId,
                        facilityName: "Smith Family Practice - Naperville",
                        organizationName: "Smith Family Practice",
                        npi: "1234567890",
                        addressLine1: "456 Oak St",
                        city: "Naperville",
                        postalCode: "60540",
                        SpecialtyCodesRaw: "Chiropractor",
                        PrimarySpecialtyCode: "Chiropractor")
                ]));

        var pain = Specialty.Create("Pain", "PAIN");
        var chiropractor = Specialty.Create("Chiropractor", "CHIROPRACTOR");
        var allSpecialties = new[] { pain, chiropractor };
        var providersByNpi = new Dictionary<string, Provider>(StringComparer.Ordinal);
        var providersByEmail = new Dictionary<string, Provider>(StringComparer.Ordinal);
        var locationKeys = new HashSet<string>(StringComparer.Ordinal);
        var createdProviders = new List<Provider>();
        var createdFacilities = new List<Facility>();
        var createdMemberships = new List<NetworkProvider>();
        var syncedSpecialtyIds = new List<List<Guid>>();

        var networks = BuildRepositoryMock();
        networks.Setup(r => r.GetByIdGlobalAsync(networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "My Network", string.Empty));
        networks.Setup(r => r.GetProvidersByNpisAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(providersByNpi);
        networks.Setup(r => r.GetProvidersByTenantEmailsAsync(tenantId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(providersByEmail);
        networks.Setup(r => r.GetNetworkProviderLocationKeysAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(locationKeys);
        networks.Setup(r => r.AddProviderToRegistryAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()))
            .Callback<Provider, CancellationToken>((provider, _) => createdProviders.Add(provider))
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.AddFacilityAsync(It.IsAny<Facility>(), It.IsAny<CancellationToken>()))
            .Callback<Facility, CancellationToken>((facility, _) => createdFacilities.Add(facility))
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.AddProviderAsync(It.IsAny<NetworkProvider>(), It.IsAny<CancellationToken>()))
            .Callback<NetworkProvider, CancellationToken>((membership, _) => createdMemberships.Add(membership))
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.SyncProviderSpecialtiesAsync(It.IsAny<Guid>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, List<Guid>, CancellationToken>((_, ids, _) => syncedSpecialtyIds.Add([.. ids]))
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var specialties = new Mock<ISpecialtyRepository>();
        specialties.Setup(r => r.GetActiveByCodesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> codes, CancellationToken _) =>
            {
                var requested = codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
                return allSpecialties.Where(s => requested.Contains(s.Code)).ToList();
            });

        var sut = new NetworkService(networks.Object, Mock.Of<ICategoryRepository>(), specialties.Object, parser.Object, NullLogger<NetworkService>.Instance);

        await using var stream = new MemoryStream();
        var result = await sut.ImportProvidersAsync(networkId, stream, "providers.csv", dryRun: false, userId: null, CancellationToken.None);

        Assert.Equal(1, result.CreatedProviders);
        Assert.Equal(2, result.CreatedFacilities);
        Assert.Equal(2, result.LinkedLocations);
        Assert.Equal(1, result.ReusedByNpi);
        Assert.Single(createdProviders);
        Assert.Equal(2, createdFacilities.Count);
        Assert.Equal(2, createdMemberships.Count);
        Assert.All(createdMemberships, membership => Assert.Equal(createdProviders[0].Id, membership.ProviderId));
        Assert.Equal(new[] { pain.Id }, syncedSpecialtyIds[0]);
        Assert.Equal(new[] { pain.Id, chiropractor.Id }, syncedSpecialtyIds[1]);
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
                [ImportRow(tenantId)]));

        var networks = BuildRepositoryMock();
        networks.Setup(r => r.GetByIdGlobalAsync(networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "My Network", string.Empty));
        networks.Setup(r => r.GetProvidersByNpisAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetProvidersByTenantEmailsAsync(tenantId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetNetworkProviderLocationKeysAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
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
    public async Task ImportProvidersAsync_RowWithInvalidPhone_FailsRowWithoutWriting()
    {
        var tenantId = Guid.CreateVersion7();
        var networkId = Guid.CreateVersion7();
        var parser = new Mock<IProviderImportParser>();
        parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), "providers.csv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderImportParseResult(
                "providers.csv",
                1,
                [ImportRow(tenantId, phone: "555-01", SpecialtyCodesRaw: "Chiropractor")]));
        var chiropractor = Specialty.Create("Chiropractor", "CHIROPRACTOR");

        var networks = BuildRepositoryMock();
        networks.Setup(r => r.GetByIdGlobalAsync(networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "My Network", string.Empty));
        networks.Setup(r => r.GetProvidersByNpisAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetProvidersByTenantEmailsAsync(tenantId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetNetworkProviderLocationKeysAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var specialties = new Mock<ISpecialtyRepository>();
        specialties.Setup(r => r.GetActiveByCodesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([chiropractor]);

        var sut = new NetworkService(networks.Object, Mock.Of<ICategoryRepository>(), specialties.Object, parser.Object, NullLogger<NetworkService>.Instance);

        await using var stream = new MemoryStream();
        var result = await sut.ImportProvidersAsync(networkId, stream, "providers.csv", dryRun: false, userId: null, CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal("failed", row.Status);
        Assert.Contains("Provider phone must be a valid 10-digit US phone number.", row.Errors);
        networks.Verify(r => r.AddProviderToRegistryAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()), Times.Never);
        networks.Verify(r => r.AddProviderAsync(It.IsAny<NetworkProvider>(), It.IsAny<CancellationToken>()), Times.Never);
        networks.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportProvidersAsync_ReusesProviderByNpi_AlwaysCreatesNewFacility()
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

        var existingFacility = Facility.Create(
            tenantId,
            "Smith Family Practice",
            "123 Main St",
            "Chicago",
            "IL",
            "60601",
            "555-0100",
            true,
            null,
            "jane@example.com");

        var parser = new Mock<IProviderImportParser>();
        parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), "providers.csv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderImportParseResult(
                "providers.csv",
                1,
                [ImportRow(
                    tenantId,
                    organizationName: "Smith Family Practice",
                    facilityName: "Smith Family Practice",
                    npi: "1234567890",
                    SpecialtyCodesRaw: "Chiropractor")]));

        Facility? createdFacility = null;
        var networks = BuildRepositoryMock();
        networks.Setup(r => r.GetByIdGlobalAsync(networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "My Network", string.Empty));
        networks.Setup(r => r.GetProvidersByNpisAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal) { ["1234567890"] = existingProvider });
        networks.Setup(r => r.GetProvidersByTenantEmailsAsync(tenantId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal) { ["jane@example.com"] = existingProvider });
        networks.Setup(r => r.GetNetworkProviderLocationKeysAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingProvider.Id.ToString() + "|" + existingFacility.Id.ToString()]);
        networks.Setup(r => r.AddFacilityAsync(It.IsAny<Facility>(), It.IsAny<CancellationToken>()))
            .Callback<Facility, CancellationToken>((facility, _) => createdFacility = facility)
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.AddProviderAsync(It.IsAny<NetworkProvider>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.SyncProviderSpecialtiesAsync(It.IsAny<Guid>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var specialty = Specialty.Create("Chiropractor", "CHIROPRACTOR");
        var specialties = new Mock<ISpecialtyRepository>();
        specialties.Setup(r => r.GetActiveByCodesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([specialty]);

        var sut = new NetworkService(networks.Object, Mock.Of<ICategoryRepository>(), specialties.Object, parser.Object, NullLogger<NetworkService>.Instance);

        await using var stream = new MemoryStream();
        var result = await sut.ImportProvidersAsync(networkId, stream, "providers.csv", dryRun: false, userId: null, CancellationToken.None);

        // Even though this provider+address already has a membership, facility resolution never
        // reuses an existing Facility during import (see ResolveImportFacilityAsync), so this
        // is treated as reused-by-NPI plus a brand-new facility/membership rather than a no-op.
        Assert.Equal(0, result.AlreadyInNetwork);
        Assert.Equal(1, result.ReusedByNpi);
        Assert.Equal(1, result.CreatedFacilities);
        Assert.Equal("reused_npi", Assert.Single(result.Rows).Status);
        Assert.NotNull(createdFacility);
        Assert.NotEqual(existingFacility.Id, createdFacility!.Id);
        networks.Verify(r => r.AddProviderAsync(It.IsAny<NetworkProvider>(), It.IsAny<CancellationToken>()), Times.Once);
        networks.Verify(r => r.FindFacilityAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
                [ImportRow(tenantId, isActive: "maybe", SpecialtyCodesRaw: "Chiropractor")]));

        var networks = BuildRepositoryMock();
        networks.Setup(r => r.GetByIdGlobalAsync(networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "My Network", string.Empty));
        networks.Setup(r => r.GetProvidersByNpisAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetProvidersByTenantEmailsAsync(tenantId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Provider>(StringComparer.Ordinal));
        networks.Setup(r => r.GetNetworkProviderLocationKeysAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
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
        mock.Setup(r => r.FindFacilityAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Facility?)null);
        mock.Setup(r => r.GetProviderFacilityAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProviderFacility?)null);
        mock.Setup(r => r.GetPrimaryProviderFacilityAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProviderFacility?)null);
        mock.Setup(r => r.AddProviderFacilityAsync(It.IsAny<ProviderFacility>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(r => r.AddFacilityAsync(It.IsAny<Facility>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(r => r.UpdateFacilityAsync(It.IsAny<Facility>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static ProviderImportParsedRow ImportRow(
        Guid? tenantId,
        string? title = null,
        string? providerName = null,
        string? facilityName = "Smith Family Practice",
        string? firstName = "Jane",
        string? lastName = "Smith",
        string? organizationName = "Smith Family Practice",
        string? npi = null,
        string? email = "jane@example.com",
        string? phone = "312-555-0100",
        string? addressLine1 = "123 Main St",
        string? city = "Chicago",
        string? state = "IL",
        string? postalCode = "60601",
        string? isActive = null,
        string? acceptingReferrals = null,
        string? CategoryCodesRaw = null,
        string? PrimaryCategoryCode = null,
        string? SpecialtyCodesRaw = null,
        string? PrimarySpecialtyCode = null,
        string? LatitudeRaw = null,
        string? LongitudeRaw = null,
        string? GeoPointSource = null)
        => new(
            RowNumber: 2,
            SourceKey: "row-2",
            TenantId: tenantId?.ToString(),
            Title: title,
            ProviderName: providerName,
            FacilityName: facilityName,
            FirstName: firstName,
            LastName: lastName,
            OrganizationName: organizationName,
            Npi: npi,
            Email: email,
            Phone: phone,
            AddressLine1: addressLine1,
            City: city,
            State: state,
            PostalCode: postalCode,
            IsActiveRaw: isActive,
            AcceptingReferralsRaw: acceptingReferrals,
            CategoryCodesRaw: CategoryCodesRaw,
            PrimaryCategoryCode: PrimaryCategoryCode,
            SpecialtyCodesRaw: SpecialtyCodesRaw,
            PrimarySpecialtyCode: PrimarySpecialtyCode,
            LatitudeRaw: LatitudeRaw,
            LongitudeRaw: LongitudeRaw,
            GeoPointSource: GeoPointSource);
}
