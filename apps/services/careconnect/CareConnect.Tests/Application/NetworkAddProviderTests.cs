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

public class NetworkAddProviderTests
{
    [Fact]
    public async Task AddProviderAsync_NewProviderWithDuplicateNpi_RejectsInsteadOfAddingLocation()
    {
        var tenantId = Guid.CreateVersion7();
        var networkId = Guid.CreateVersion7();
        var existing = Provider.Create(
            tenantId,
            "Dr. Jane Smith",
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
            lastName: "Smith",
            title: "Dr.");

        var networks = new Mock<INetworkRepository>();
        networks.Setup(r => r.GetByIdAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "Network", string.Empty));
        networks.Setup(r => r.GetProviderByNpiAsync("1234567890", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var sut = BuildSut(networks.Object);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.AddProviderAsync(tenantId, networkId, NewProviderRequest(npi: "1234567890"), null));

        Assert.Contains("use Add new location", ex.Message);
        Assert.Equal("DUPLICATE_PROVIDER", ex.ErrorCode);
        networks.Verify(r => r.AddProviderToRegistryAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()), Times.Never);
        networks.Verify(r => r.AddFacilityAsync(It.IsAny<Facility>(), It.IsAny<CancellationToken>()), Times.Never);
        networks.Verify(r => r.AddProviderAsync(It.IsAny<NetworkProvider>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddProviderAsync_ExistingProviderWithNewProviderData_AddsNewLocationOnly()
    {
        var tenantId = Guid.CreateVersion7();
        var networkId = Guid.CreateVersion7();
        var provider = Provider.Create(
            tenantId,
            "Dr. Jane Smith",
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
            lastName: "Smith",
            title: "Dr.");

        Facility? createdFacility = null;
        NetworkProvider? createdMembership = null;

        var networks = new Mock<INetworkRepository>();
        networks.Setup(r => r.GetByIdAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "Network", string.Empty));
        networks.Setup(r => r.GetProviderByIdGlobalAsync(provider.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider);
        networks.Setup(r => r.FindFacilityAsync(
                tenantId,
                "Smith Family Practice - North",
                "456 Oak Ave",
                "Naperville",
                "IL",
                "60540",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Facility?)null);
        networks.Setup(r => r.AddFacilityAsync(It.IsAny<Facility>(), It.IsAny<CancellationToken>()))
            .Callback<Facility, CancellationToken>((facility, _) => createdFacility = facility)
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.GetProviderFacilityAsync(provider.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProviderFacility?)null);
        networks.Setup(r => r.AddProviderFacilityAsync(It.IsAny<ProviderFacility>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.GetMembershipAsync(networkId, provider.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NetworkProvider?)null);
        networks.Setup(r => r.AddProviderAsync(It.IsAny<NetworkProvider>(), It.IsAny<CancellationToken>()))
            .Callback<NetworkProvider, CancellationToken>((membership, _) => createdMembership = membership)
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = BuildSut(networks.Object);

        var result = await sut.AddProviderAsync(
            tenantId,
            networkId,
            NewLocationRequest(provider.Id),
            null);

        Assert.NotNull(createdFacility);
        Assert.Equal("Smith Family Practice - North", createdFacility!.Name);
        Assert.Equal("456 Oak Ave", createdFacility.AddressLine1);
        Assert.NotNull(createdMembership);
        Assert.Equal(provider.Id, createdMembership!.ProviderId);
        Assert.Equal(createdFacility.Id, createdMembership.FacilityId);
        Assert.Equal(createdFacility.Id, result.FacilityId);
        Assert.Equal(provider.Id, result.ProviderId);
        networks.Verify(r => r.GetProviderByNpiAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        networks.Verify(r => r.AddProviderToRegistryAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()), Times.Never);
        networks.Verify(r => r.SyncProviderSpecialtiesAsync(It.IsAny<Guid>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddProviderAsync_NewProvider_ReturnsSpecialtiesImmediately()
    {
        // Regression: SyncProviderSpecialtiesAsync only sets the ProviderSpecialty.SpecialtyId
        // FK — it never wires up the .Specialty navigation on the in-memory `provider` object
        // built earlier in AddProviderAsync. MapSpecialties silently drops entries with a null
        // .Specialty, so the response must come from a freshly reloaded, fully-included
        // membership (GetMembershipAsync's Include chain), not that stale local `provider`.
        var tenantId = Guid.CreateVersion7();
        var networkId = Guid.CreateVersion7();
        var specialty = Specialty.Create("Pain Doctors", "PAIN", null);

        var networks = new Mock<INetworkRepository>();
        networks.Setup(r => r.GetByIdAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "Network", string.Empty));
        networks.Setup(r => r.GetProviderByTenantEmailAsync(tenantId, "jane@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Provider?)null);
        networks.Setup(r => r.AddProviderToRegistryAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.SyncProviderSpecialtiesAsync(It.IsAny<Guid>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.FindFacilityAsync(tenantId, It.IsAny<string>(), "123 Main St", "Chicago", "IL", "60601", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Facility?)null);
        networks.Setup(r => r.AddFacilityAsync(It.IsAny<Facility>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.GetProviderFacilityAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProviderFacility?)null);
        networks.Setup(r => r.AddProviderFacilityAsync(It.IsAny<ProviderFacility>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.AddProviderAsync(It.IsAny<NetworkProvider>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // First call: "does this membership already exist" check, before creation — none yet.
        // Second call: the post-save reload used to build the response — simulates the fixed
        // repository method returning a fully-included Provider/Facility.
        networks.SetupSequence(r => r.GetMembershipAsync(networkId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NetworkProvider?)null)
            .ReturnsAsync(() =>
            {
                var reloadedProvider = Provider.Create(
                    tenantId, "Dr. Jane Smith", "Smith Family Practice", "jane@example.com", "555-0100",
                    "123 Main St", "Chicago", "IL", "60601", true, true, null,
                    firstName: "Jane", lastName: "Smith", title: "Dr.");
                reloadedProvider.ProviderSpecialties.Add(new ProviderSpecialty
                {
                    ProviderId = reloadedProvider.Id,
                    SpecialtyId = specialty.Id,
                    Specialty = specialty,
                    IsPrimary = true,
                });
                var reloadedFacility = Facility.Create(
                    tenantId, "Smith Family Practice", "123 Main St", "Chicago", "IL", "60601", "555-0100",
                    true, null, "jane@example.com");
                var reloadedMembership = NetworkProvider.Create(
                    tenantId, networkId, reloadedProvider.Id, reloadedFacility.Id, true, true);
                SetNavigation(reloadedMembership, nameof(NetworkProvider.Provider), reloadedProvider);
                SetNavigation(reloadedMembership, nameof(NetworkProvider.Facility), reloadedFacility);
                return reloadedMembership;
            });

        var specialties = new Mock<ISpecialtyRepository>();
        specialties.Setup(r => r.GetActiveByCodesAsync(It.Is<IEnumerable<string>>(codes => codes.Contains("PAIN")), It.IsAny<CancellationToken>()))
            .ReturnsAsync([specialty]);

        var sut = new NetworkService(
            networks.Object,
            Mock.Of<ICategoryRepository>(),
            specialties.Object,
            Mock.Of<IProviderImportParser>(),
            NullLogger<NetworkService>.Instance);

        var result = await sut.AddProviderAsync(tenantId, networkId, NewProviderRequest(), null);

        Assert.Single(result.Specialties);
        Assert.Equal("Pain Doctors", result.PrimarySpecialty);
    }

    [Fact]
    public async Task SearchProvidersAsync_ReturnsOneResultPerFacility()
    {
        var tenantId = Guid.CreateVersion7();
        var provider = Provider.Create(
            tenantId,
            "Dr. John Doe4",
            "JD Clinic4",
            "john@example.com",
            "3136136161",
            "120 Green Street",
            "Greenland",
            "AR",
            "72701",
            true,
            true,
            null,
            npi: "5245147573",
            firstName: "John",
            lastName: "Doe4",
            title: "Dr.");
        var greenland = Facility.Create(
            tenantId,
            "JD Clinic4",
            "120 Green Street",
            "Greenland",
            "AR",
            "72701",
            "3136136161",
            true,
            null,
            "john@example.com");
        var bay = Facility.Create(
            tenantId,
            "JD Clinic4 - Bay",
            "120 Market Street",
            "San Francisco",
            "CA",
            "94111",
            "3136136161",
            true,
            null,
            "bay@example.com");
        var greenlandLink = ProviderFacility.Create(provider.Id, greenland.Id, isPrimary: true);
        var bayLink = ProviderFacility.Create(provider.Id, bay.Id, isPrimary: false);
        SetNavigation(greenlandLink, nameof(ProviderFacility.Facility), greenland);
        SetNavigation(bayLink, nameof(ProviderFacility.Facility), bay);
        provider.ProviderFacilities.Add(greenlandLink);
        provider.ProviderFacilities.Add(bayLink);

        var networks = new Mock<INetworkRepository>();
        networks.Setup(r => r.SearchProvidersGlobalAsync(null, null, "5245147573", null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync([provider]);

        var sut = BuildSut(networks.Object);

        var results = await sut.SearchProvidersAsync(null, null, "5245147573", null);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.FacilityId == greenland.Id && r.FacilityName == "JD Clinic4");
        Assert.Contains(results, r => r.FacilityId == bay.Id && r.FacilityName == "JD Clinic4 - Bay");
    }

    private static NetworkService BuildSut(INetworkRepository networks) =>
        new(
            networks,
            Mock.Of<ICategoryRepository>(),
            Mock.Of<ISpecialtyRepository>(),
            Mock.Of<IProviderImportParser>(),
            NullLogger<NetworkService>.Instance);

    private static AddProviderToNetworkRequest NewProviderRequest(string? npi = null) => new(
        ExistingProviderId: null,
        ExistingFacilityId: null,
        NewProvider: new NewProviderData(
            FirstName: "Jane",
            LastName: "Smith",
            OrganizationName: "Smith Family Practice",
            Email: "jane@example.com",
            Phone: "555-0100",
            AddressLine1: "123 Main St",
            City: "Chicago",
            State: "IL",
            PostalCode: "60601",
            IsActive: true,
            AcceptingReferrals: true,
            Npi: npi,
            SpecialtyCodes: ["PAIN"],
            PrimarySpecialtyCode: "PAIN",
            Title: "Dr."));

    private static AddProviderToNetworkRequest NewLocationRequest(Guid providerId) => new(
        ExistingProviderId: providerId,
        ExistingFacilityId: null,
        NewProvider: new NewProviderData(
            FirstName: string.Empty,
            LastName: string.Empty,
            OrganizationName: "Smith Family Practice - North",
            Email: "north@example.com",
            Phone: "555-0200",
            AddressLine1: "456 Oak Ave",
            City: "Naperville",
            State: "IL",
            PostalCode: "60540",
            IsActive: true,
            AcceptingReferrals: true,
            Npi: null));

    private static void SetNavigation<T>(object target, string propertyName, T value)
    {
        var property = target.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"Property {propertyName} was not found.");
        property.SetValue(target, value);
    }
}
