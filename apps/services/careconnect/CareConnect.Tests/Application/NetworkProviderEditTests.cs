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

public class NetworkProviderEditTests
{
    [Fact]
    public async Task UpdateProviderAsync_WhenProviderIsNotInNetwork_ThrowsNotFoundException()
    {
        var tenantId = Guid.CreateVersion7();
        var networkId = Guid.CreateVersion7();
        var providerId = Guid.CreateVersion7();
        var networks = new Mock<INetworkRepository>();
        networks.Setup(r => r.GetByIdAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "Network", string.Empty));
        networks.Setup(r => r.GetMembershipAsync(networkId, providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NetworkProvider?)null);

        var sut = BuildSut(networks.Object, Mock.Of<ISpecialtyRepository>());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateProviderAsync(tenantId, networkId, providerId, ValidUpdateRequest([Guid.CreateVersion7()]), null));

        networks.Verify(r => r.UpdateProviderInRegistryAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProviderAsync_WithActiveSpecialty_SyncsProviderSpecialties()
    {
        var tenantId = Guid.CreateVersion7();
        var networkId = Guid.CreateVersion7();
        var specialtyId = Guid.CreateVersion7();
        var specialty = Specialty.Create("Pain Doctors", "PAIN_DOCTORS", null);
        var provider = Provider.Create(
            tenantId,
            "Jane Provider",
            "Jane Practice",
            "jane@example.com",
            "555-0100",
            "123 Main St",
            "Austin",
            "TX",
            "78701",
            true,
            true,
            null);
        var providerId = provider.Id;

        var networks = new Mock<INetworkRepository>();
        networks.Setup(r => r.GetByIdAsync(tenantId, networkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderNetwork.Create(tenantId, "Network", string.Empty));
        networks.Setup(r => r.GetMembershipAsync(networkId, providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NetworkProvider.Create(tenantId, networkId, providerId));
        networks.SetupSequence(r => r.GetProviderByIdGlobalAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider)
            .ReturnsAsync(provider);
        networks.Setup(r => r.UpdateProviderInRegistryAsync(provider, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.SyncProviderSpecialtiesAsync(provider.Id, It.Is<List<Guid>>(ids => ids.SequenceEqual(new[] { specialtyId })), It.IsAny<CancellationToken>()))
            .Callback<Guid, List<Guid>, CancellationToken>((_, _, _) =>
            {
                provider.ProviderSpecialties.Clear();
                provider.ProviderSpecialties.Add(new ProviderSpecialty
                {
                    ProviderId = provider.Id,
                    SpecialtyId = specialty.Id,
                    Specialty = specialty,
                    IsPrimary = true
                });
            })
            .Returns(Task.CompletedTask);
        networks.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var specialties = new Mock<ISpecialtyRepository>();
        specialties.Setup(r => r.GetActiveByIdsAsync(It.Is<List<Guid>>(ids => ids.SequenceEqual(new[] { specialtyId })), It.IsAny<CancellationToken>()))
            .ReturnsAsync([specialty]);

        var sut = BuildSut(networks.Object, specialties.Object);

        var result = await sut.UpdateProviderAsync(tenantId, networkId, providerId, ValidUpdateRequest([specialtyId]), null);

        Assert.Equal("Pain Doctors", result.PrimarySpecialty);
        Assert.Equal("Dr.", result.Title);
        Assert.Equal("Dr. Jane Provider", result.Name);
        networks.Verify(r => r.SyncProviderSpecialtiesAsync(
            provider.Id,
            It.Is<List<Guid>>(ids => ids.SequenceEqual(new[] { specialtyId })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static NetworkService BuildSut(INetworkRepository networks, ISpecialtyRepository specialties) =>
        new(
            networks,
            Mock.Of<ICategoryRepository>(),
            specialties,
            Mock.Of<IProviderImportParser>(),
            NullLogger<NetworkService>.Instance);

    private static UpdateNetworkProviderRequest ValidUpdateRequest(List<Guid> specialtyIds) => new(
        FirstName: "Jane",
        LastName: "Provider",
        OrganizationName: "Jane Practice",
        Email: "jane@example.com",
        Phone: "555-0100",
        AddressLine1: "123 Main St",
        City: "Austin",
        State: "TX",
        PostalCode: "78701",
        IsActive: true,
        AcceptingReferrals: true,
        SpecialtyIds: specialtyIds,
        Title: "Dr.");
}
