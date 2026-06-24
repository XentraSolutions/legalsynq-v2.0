using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

public class SlotGenerationServiceTests
{
    private readonly Guid _tenantId = Guid.CreateVersion7();
    private readonly Guid _providerId = Guid.CreateVersion7();
    private readonly Guid _facilityId = Guid.CreateVersion7();

    [Fact]
    public async Task GenerateSlotsAsync_UsesTenantTimezoneForDstAwareUtcConversion()
    {
        var providerRepo = new Mock<IProviderRepository>(MockBehavior.Strict);
        var templateRepo = new Mock<IAvailabilityTemplateRepository>(MockBehavior.Strict);
        var slotRepo = new Mock<IAppointmentSlotRepository>(MockBehavior.Strict);
        var exceptionRepo = new Mock<IAvailabilityExceptionRepository>(MockBehavior.Strict);
        var tenantClient = new Mock<ITenantServiceClient>(MockBehavior.Strict);

        var provider = Provider.Create(
            _tenantId, "Dr. Test", null, "test@example.com",
            "555-0000", "123 Main St", "London", "LN", "N1",
            true, true, null);

        var template = ProviderAvailabilityTemplate.Create(
            _tenantId,
            _providerId,
            _facilityId,
            null,
            dayOfWeek: 1,
            startTimeLocal: TimeSpan.FromHours(9),
            endTimeLocal: TimeSpan.FromHours(10),
            slotDurationMinutes: 60,
            capacity: 1,
            effectiveFrom: null,
            effectiveTo: null,
            isActive: true,
            createdByUserId: null);

        List<AppointmentSlot>? savedSlots = null;
        DateTime capturedRangeStart = default;
        DateTime capturedRangeEnd = default;

        providerRepo
            .Setup(r => r.GetByIdAsync(_tenantId, _providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider);

        templateRepo
            .Setup(r => r.GetActiveByProviderAsync(_tenantId, _providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([template]);

        tenantClient
            .Setup(c => c.GetTimezoneAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Europe/London");

        slotRepo
            .Setup(r => r.GetExistingStartTimesAsync(
                _tenantId,
                _providerId,
                template.Id,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, Guid?, DateTime, DateTime, CancellationToken>((_, _, _, from, to, _) =>
            {
                capturedRangeStart = from;
                capturedRangeEnd = to;
            })
            .ReturnsAsync(new HashSet<DateTime>());

        exceptionRepo
            .Setup(r => r.GetActiveInRangeAsync(
                _tenantId,
                _providerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        slotRepo
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<AppointmentSlot>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<AppointmentSlot>, CancellationToken>((slots, _) => savedSlots = slots.ToList())
            .Returns(Task.CompletedTask);

        var sut = new SlotGenerationService(
            providerRepo.Object,
            templateRepo.Object,
            slotRepo.Object,
            exceptionRepo.Object,
            tenantClient.Object);

        var request = new GenerateSlotsRequest
        {
            FromDateUtc = new DateTime(2026, 6, 15),
            ToDateUtc = new DateTime(2026, 6, 15),
        };

        var result = await sut.GenerateSlotsAsync(_tenantId, _providerId, null, request);

        Assert.Equal(1, result.SlotsCreated);
        Assert.NotNull(savedSlots);
        Assert.Single(savedSlots);
        Assert.Equal(new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc), savedSlots[0].StartAtUtc);
        Assert.Equal(new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc), savedSlots[0].EndAtUtc);
        Assert.Equal(new DateTime(2026, 6, 14, 23, 0, 0, DateTimeKind.Utc), capturedRangeStart);
        Assert.Equal(new DateTime(2026, 6, 15, 23, 0, 0, DateTimeKind.Utc), capturedRangeEnd);
    }

    [Fact]
    public async Task GenerateSlotsAsync_FallsBackToUtcWhenTenantTimezoneUnavailable()
    {
        var providerRepo = new Mock<IProviderRepository>(MockBehavior.Strict);
        var templateRepo = new Mock<IAvailabilityTemplateRepository>(MockBehavior.Strict);
        var slotRepo = new Mock<IAppointmentSlotRepository>(MockBehavior.Strict);
        var exceptionRepo = new Mock<IAvailabilityExceptionRepository>(MockBehavior.Strict);
        var tenantClient = new Mock<ITenantServiceClient>(MockBehavior.Strict);

        var provider = Provider.Create(
            _tenantId, "Dr. Test", null, "test@example.com",
            "555-0000", "123 Main St", "Las Vegas", "NV", "89103",
            true, true, null);

        var template = ProviderAvailabilityTemplate.Create(
            _tenantId,
            _providerId,
            _facilityId,
            null,
            dayOfWeek: 1,
            startTimeLocal: TimeSpan.FromHours(9),
            endTimeLocal: TimeSpan.FromHours(10),
            slotDurationMinutes: 60,
            capacity: 1,
            effectiveFrom: null,
            effectiveTo: null,
            isActive: true,
            createdByUserId: null);

        List<AppointmentSlot>? savedSlots = null;

        providerRepo
            .Setup(r => r.GetByIdAsync(_tenantId, _providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider);

        templateRepo
            .Setup(r => r.GetActiveByProviderAsync(_tenantId, _providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([template]);

        tenantClient
            .Setup(c => c.GetTimezoneAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        slotRepo
            .Setup(r => r.GetExistingStartTimesAsync(
                _tenantId,
                _providerId,
                template.Id,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<DateTime>());

        exceptionRepo
            .Setup(r => r.GetActiveInRangeAsync(
                _tenantId,
                _providerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        slotRepo
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<AppointmentSlot>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<AppointmentSlot>, CancellationToken>((slots, _) => savedSlots = slots.ToList())
            .Returns(Task.CompletedTask);

        var sut = new SlotGenerationService(
            providerRepo.Object,
            templateRepo.Object,
            slotRepo.Object,
            exceptionRepo.Object,
            tenantClient.Object);

        var request = new GenerateSlotsRequest
        {
            FromDateUtc = new DateTime(2026, 6, 15),
            ToDateUtc = new DateTime(2026, 6, 15),
        };

        await sut.GenerateSlotsAsync(_tenantId, _providerId, null, request);

        Assert.NotNull(savedSlots);
        Assert.Single(savedSlots);
        Assert.Equal(new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc), savedSlots[0].StartAtUtc);
        Assert.Equal(new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc), savedSlots[0].EndAtUtc);
    }
}
