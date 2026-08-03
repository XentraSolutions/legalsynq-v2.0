using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

public class ProviderSpecialtyValidationTests
{
    [Fact]
    public async Task CreateAsync_WithoutSpecialties_ThrowsValidationException()
    {
        var sut = BuildSut(out var providers, out _);
        var request = ValidCreateRequest();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            sut.CreateAsync(Guid.CreateVersion7(), null, request));

        Assert.Contains("Select at least one specialty.", ex.Errors["specialtyIds"]);
        providers.Verify(r => r.AddAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithActiveSpecialties_SyncsSelectedSpecialties()
    {
        var tenantId = Guid.CreateVersion7();
        var specialtyId = Guid.CreateVersion7();
        var request = ValidCreateRequest();
        request.SpecialtyIds.Add(specialtyId);
        var specialty = Specialty.Create("Physical Therapy", "PHYSICAL_THERAPY", null);

        var sut = BuildSut(out var providers, out var specialties);
        Provider? capturedProvider = null;

        specialties.Setup(r => r.GetActiveByIdsAsync(
                It.Is<List<Guid>>(ids => ids.SequenceEqual(new[] { specialtyId })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([specialty]);

        providers.Setup(r => r.AddAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()))
            .Callback<Provider, CancellationToken>((provider, _) => capturedProvider = provider)
            .Returns(Task.CompletedTask);

        providers.Setup(r => r.SyncSpecialtiesAsync(
                It.IsAny<Guid>(),
                It.Is<List<Guid>>(ids => ids.SequenceEqual(new[] { specialtyId })),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, List<Guid>, CancellationToken>((providerId, _, _) =>
            {
                capturedProvider!.ProviderSpecialties.Add(new ProviderSpecialty
                {
                    ProviderId = providerId,
                    SpecialtyId = specialty.Id,
                    Specialty = specialty,
                    IsPrimary = true
                });
            })
            .Returns(Task.CompletedTask);

        providers.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => capturedProvider);

        var result = await sut.CreateAsync(tenantId, null, request);

        Assert.NotNull(capturedProvider);
        Assert.Equal("Dr.", result.Title);
        Assert.Equal("Physical Therapy", result.PrimarySpecialty);
        providers.Verify(r => r.SyncSpecialtiesAsync(
            capturedProvider!.Id,
            It.Is<List<Guid>>(ids => ids.SequenceEqual(new[] { specialtyId })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ProviderService BuildSut(
        out Mock<IProviderRepository> providers,
        out Mock<ISpecialtyRepository> specialties)
    {
        providers = new Mock<IProviderRepository>();
        specialties = new Mock<ISpecialtyRepository>();
        return new ProviderService(
            providers.Object,
            Mock.Of<IReferralRepository>(),
            Mock.Of<IAppointmentSlotRepository>(),
            specialties.Object,
            NullLogger<ProviderService>.Instance);
    }

    private static CreateProviderRequest ValidCreateRequest() => new()
    {
        Name = "Jane Provider",
        Email = "jane@example.com",
        Phone = "555-0100",
        AddressLine1 = "123 Main St",
        City = "Austin",
        State = "TX",
        PostalCode = "78701",
        IsActive = true,
        AcceptingReferrals = true,
        Title = "Dr.",
    };
}
