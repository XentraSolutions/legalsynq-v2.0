using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using LegalSynq.AuditClient;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

public class ReferralAttributionServiceTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid OtherTenantId = Guid.CreateVersion7();

    private static ReferralAttributionService BuildService(
        Mock<IReferralAttributionRepository> repo,
        Mock<IAuditEventClient>? auditClient = null,
        Mock<IReferralAttributionAccessCodeRepository>? accessCodes = null)
    {
        var codes = accessCodes ?? new Mock<IReferralAttributionAccessCodeRepository>();
        codes.Setup(c => c.CountActiveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        return new ReferralAttributionService(
            repo.Object,
            codes.Object,
            (auditClient ?? new Mock<IAuditEventClient>()).Object,
            new Mock<IHttpContextAccessor>().Object);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesAndNormalizesCode()
    {
        var repo = new Mock<IReferralAttributionRepository>();
        repo.Setup(r => r.GetByCodeAsync(TenantId, "CAM_PERRY", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReferralAttribution?)null);
        ReferralAttribution? added = null;
        repo.Setup(r => r.AddAsync(It.IsAny<ReferralAttribution>(), It.IsAny<CancellationToken>()))
            .Callback<ReferralAttribution, CancellationToken>((a, _) => added = a)
            .Returns(Task.CompletedTask);

        var service = BuildService(repo);
        var result = await service.CreateAsync(TenantId, Guid.CreateVersion7(), "Admin", new CreateReferralAttributionRequest
        {
            FirstName = "Cam", LastName = "Perry",
            Code = "cam_perry",
            IsActive = true,
            DisplayOrder = 1,
        });

        Assert.Equal("CAM_PERRY", result.Code);
        Assert.Equal(TenantId, added!.TenantId);
        Assert.Equal("CAM_PERRY", added.Code);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCodeInSameTenant_Throws()
    {
        var existing = ReferralAttribution.Create(TenantId, "Cam", "Perry", "CAM_PERRY", null, true, 1, null);
        var repo = new Mock<IReferralAttributionRepository>();
        repo.Setup(r => r.GetByCodeAsync(TenantId, "CAM_PERRY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var service = BuildService(repo);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(TenantId, null, null, new CreateReferralAttributionRequest
            {
                FirstName = "Cam", LastName = "Perry Duplicate",
                Code = "CAM_PERRY",
            }));
    }

    [Fact]
    public async Task CreateAsync_SameCodeDifferentTenant_Allowed()
    {
        // Tenant-scoped uniqueness: GetByCodeAsync is scoped to OtherTenantId and returns null,
        // even though TenantId has an attribution with the same code.
        var repo = new Mock<IReferralAttributionRepository>();
        repo.Setup(r => r.GetByCodeAsync(OtherTenantId, "CAM_PERRY", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReferralAttribution?)null);
        repo.Setup(r => r.AddAsync(It.IsAny<ReferralAttribution>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = BuildService(repo);
        var result = await service.CreateAsync(OtherTenantId, null, null, new CreateReferralAttributionRequest
        {
            FirstName = "Cam", LastName = "Perry",
            Code = "CAM_PERRY",
        });

        Assert.Equal(OtherTenantId, result.TenantId);
    }

    [Fact]
    public async Task SetActiveAsync_Deactivate_PersistsAndReturnsUpdated()
    {
        var attribution = ReferralAttribution.Create(TenantId, "Cam", "Perry", "CAM_PERRY", null, true, null, null);
        var repo = new Mock<IReferralAttributionRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, attribution.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attribution);
        repo.Setup(r => r.UpdateAsync(attribution, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = BuildService(repo);
        var result = await service.SetActiveAsync(TenantId, attribution.Id, Guid.CreateVersion7(), "Admin", isActive: false);

        Assert.False(result.IsActive);
        repo.Verify(r => r.UpdateAsync(attribution, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetActiveAsync_UnknownId_ThrowsNotFound()
    {
        var repo = new Mock<IReferralAttributionRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReferralAttribution?)null);

        var service = BuildService(repo);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.SetActiveAsync(TenantId, Guid.CreateVersion7(), null, null, isActive: false));
    }

    [Fact]
    public async Task GetByIdAsync_CrossTenantId_ThrowsNotFound()
    {
        // GetByIdAsync is tenant-scoped in the repository contract — a different tenant's ID
        // returns null, which the service must surface as NotFound (not the record).
        var repo = new Mock<IReferralAttributionRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReferralAttribution?)null);

        var service = BuildService(repo);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(TenantId, Guid.CreateVersion7()));
    }

    [Fact]
    public async Task SeedAsync_FirstCall_CreatesAttribution()
    {
        var repo = new Mock<IReferralAttributionRepository>();
        repo.Setup(r => r.GetByCodeAsync(TenantId, "CAM_PERRY", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReferralAttribution?)null);
        var addCount = 0;
        repo.Setup(r => r.AddAsync(It.IsAny<ReferralAttribution>(), It.IsAny<CancellationToken>()))
            .Callback(() => addCount++)
            .Returns(Task.CompletedTask);

        var service = BuildService(repo);
        await service.SeedAsync(TenantId, new CreateReferralAttributionRequest
        {
            FirstName = "Cam", LastName = "Perry", Code = "CAM_PERRY", IsActive = true, DisplayOrder = 1,
        });

        Assert.Equal(1, addCount);
    }

    [Fact]
    public async Task SeedAsync_SecondCall_IsIdempotent_NoOp()
    {
        var existing = ReferralAttribution.Create(TenantId, "Cam", "Perry", "CAM_PERRY", null, true, 1, null);
        var repo = new Mock<IReferralAttributionRepository>();
        repo.Setup(r => r.GetByCodeAsync(TenantId, "CAM_PERRY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var service = BuildService(repo);
        await service.SeedAsync(TenantId, new CreateReferralAttributionRequest
        {
            FirstName = "Cam", LastName = "Perry", Code = "CAM_PERRY", IsActive = true, DisplayOrder = 1,
        });

        repo.Verify(r => r.AddAsync(It.IsAny<ReferralAttribution>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
