using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using LegalSynq.AuditClient;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

public class ReferralAttributionAccessCodeServiceTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid UserId = Guid.CreateVersion7();

    private static ReferralAttributionAccessCodeService BuildService(
        Mock<IReferralAttributionAccessCodeRepository> accessCodes,
        Mock<IReferralAttributionRepository> attributions,
        Mock<IAuditEventClient>? auditClient = null)
    {
        var configuration = new ConfigurationBuilder().Build();
        return new ReferralAttributionAccessCodeService(
            accessCodes.Object,
            attributions.Object,
            configuration,
            (auditClient ?? new Mock<IAuditEventClient>()).Object,
            new Mock<IHttpContextAccessor>().Object);
    }

    private static Mock<IReferralAttributionRepository> AttributionRepoReturning(ReferralAttribution attribution)
    {
        var repo = new Mock<IReferralAttributionRepository>();
        repo.Setup(r => r.GetByIdAsync(attribution.TenantId, attribution.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attribution);
        return repo;
    }

    [Fact]
    public async Task GenerateAsync_ValidAttribution_CreatesCodeAndReturnsPlaintextOnce()
    {
        var attribution = ReferralAttribution.Create(TenantId, "Cam", "Perry", "CAM_PERRY", null, true, 1, null);
        var attributions = AttributionRepoReturning(attribution);
        var accessCodes = new Mock<IReferralAttributionAccessCodeRepository>();
        accessCodes.Setup(a => a.GetByHashAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReferralAttributionAccessCode?)null);
        ReferralAttributionAccessCode? added = null;
        accessCodes.Setup(a => a.AddAsync(It.IsAny<ReferralAttributionAccessCode>(), It.IsAny<CancellationToken>()))
            .Callback<ReferralAttributionAccessCode, CancellationToken>((c, _) => added = c)
            .Returns(Task.CompletedTask);

        var service = BuildService(accessCodes, attributions);
        var result = await service.GenerateAsync(TenantId, Guid.CreateVersion7(), "Admin", new CreateReferralAttributionAccessCodeRequest
        {
            ReferralAttributionId = attribution.Id,
        });

        Assert.NotEmpty(result.Code);
        Assert.Matches(@"^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{2}$", result.Code);
        Assert.Equal(attribution.Id, added!.ReferralAttributionId);
        Assert.True(added.IsActive);
        // The stored record never carries the plaintext code.
        Assert.DoesNotContain(result.Code.Replace("-", ""), added.CodeHash);
    }

    [Fact]
    public async Task GenerateAsync_ExistingActiveCode_ThrowsConflict()
    {
        var attribution = ReferralAttribution.Create(TenantId, "Cam", "Perry", "CAM_PERRY", null, true, 1, null);
        var attributions = AttributionRepoReturning(attribution);
        var accessCodes = new Mock<IReferralAttributionAccessCodeRepository>();
        accessCodes.Setup(a => a.CountActiveAsync(TenantId, attribution.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = BuildService(accessCodes, attributions);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.GenerateAsync(TenantId, null, null, new CreateReferralAttributionAccessCodeRequest
            {
                ReferralAttributionId = attribution.Id,
            }));
        Assert.Equal("ACTIVE_CODE_EXISTS", ex.ErrorCode);
        accessCodes.Verify(a => a.AddAsync(It.IsAny<ReferralAttributionAccessCode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_UnknownAttributionForTenant_ThrowsValidation()
    {
        var attributions = new Mock<IReferralAttributionRepository>();
        attributions.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReferralAttribution?)null);
        var accessCodes = new Mock<IReferralAttributionAccessCodeRepository>();

        var service = BuildService(accessCodes, attributions);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.GenerateAsync(TenantId, null, null, new CreateReferralAttributionAccessCodeRequest
            {
                ReferralAttributionId = Guid.CreateVersion7(),
            }));
    }

    [Fact]
    public async Task VerifyAsync_ValidCode_ReturnsOkWithAttribution()
    {
        var attribution = ReferralAttribution.Create(TenantId, "Cam", "Perry", "CAM_PERRY", null, true, 1, null);
        var attributions = AttributionRepoReturning(attribution);
        var accessCodes = new Mock<IReferralAttributionAccessCodeRepository>();

        // Generate first to capture the real hash the service computes internally.
        accessCodes.Setup(a => a.GetByHashAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReferralAttributionAccessCode?)null);
        ReferralAttributionAccessCode? generated = null;
        accessCodes.Setup(a => a.AddAsync(It.IsAny<ReferralAttributionAccessCode>(), It.IsAny<CancellationToken>()))
            .Callback<ReferralAttributionAccessCode, CancellationToken>((c, _) => generated = c)
            .Returns(Task.CompletedTask);

        var service = BuildService(accessCodes, attributions);
        var generateResult = await service.GenerateAsync(TenantId, null, null, new CreateReferralAttributionAccessCodeRequest
        {
            ReferralAttributionId = attribution.Id,
        });

        // Now verify it — repository lookup by hash returns the generated record.
        accessCodes.Setup(a => a.GetByHashAsync(TenantId, generated!.CodeHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(generated);

        var verifyResult = await service.VerifyAsync(TenantId, generateResult.Code, ct: default);

        Assert.True(verifyResult.Ok);
        Assert.Equal(attribution.Id, verifyResult.ReferralAttributionId);
        Assert.Equal(attribution.FullName, verifyResult.ReferralAttributionFullName);
    }

    [Fact]
    public async Task VerifyAsync_UnknownCode_ReturnsNotOk()
    {
        var accessCodes = new Mock<IReferralAttributionAccessCodeRepository>();
        accessCodes.Setup(a => a.GetByHashAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReferralAttributionAccessCode?)null);
        var attributions = new Mock<IReferralAttributionRepository>();

        var service = BuildService(accessCodes, attributions);
        var result = await service.VerifyAsync(TenantId, "BOGUS-CODE-XX", ct: default);

        Assert.False(result.Ok);
        Assert.Null(result.ReferralAttributionId);
    }

    [Fact]
    public async Task VerifyAsync_InactiveCode_ReturnsNotOk()
    {
        var attribution = ReferralAttribution.Create(TenantId, "Cam", "Perry", "CAM_PERRY", null, true, 1, null);
        var accessCode = ReferralAttributionAccessCode.Create(TenantId, attribution.Id, "hash", null, null, null);
        accessCode.SetActive(false, null);

        var accessCodes = new Mock<IReferralAttributionAccessCodeRepository>();
        accessCodes.Setup(a => a.GetByHashAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessCode);
        var attributions = AttributionRepoReturning(attribution);

        var service = BuildService(accessCodes, attributions);
        var result = await service.VerifyAsync(TenantId, "SOME-CODE-XX", ct: default);

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task VerifyAsync_DeactivatedAttribution_ReturnsNotOk()
    {
        var attribution = ReferralAttribution.Create(TenantId, "Cam", "Perry", "CAM_PERRY", null, true, 1, null);
        attribution.SetActive(false, null);
        var accessCode = ReferralAttributionAccessCode.Create(TenantId, attribution.Id, "hash", null, null, null);

        var accessCodes = new Mock<IReferralAttributionAccessCodeRepository>();
        accessCodes.Setup(a => a.GetByHashAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessCode);
        var attributions = AttributionRepoReturning(attribution);

        var service = BuildService(accessCodes, attributions);
        var result = await service.VerifyAsync(TenantId, "SOME-CODE-XX", ct: default);

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task SetActiveAsync_Deactivate_PersistsAndReturnsUpdated()
    {
        var accessCode = ReferralAttributionAccessCode.Create(TenantId, Guid.CreateVersion7(), "hash", null, null, null);
        var accessCodes = new Mock<IReferralAttributionAccessCodeRepository>();
        accessCodes.Setup(a => a.GetByIdAsync(TenantId, accessCode.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessCode);
        accessCodes.Setup(a => a.UpdateAsync(accessCode, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var attributions = new Mock<IReferralAttributionRepository>();

        var service = BuildService(accessCodes, attributions);
        var result = await service.SetActiveAsync(TenantId, accessCode.Id, Guid.CreateVersion7(), "Admin", isActive: false);

        Assert.False(result.IsActive);
        accessCodes.Verify(a => a.UpdateAsync(accessCode, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetActiveAsync_UnknownId_ThrowsNotFound()
    {
        var accessCodes = new Mock<IReferralAttributionAccessCodeRepository>();
        accessCodes.Setup(a => a.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReferralAttributionAccessCode?)null);
        var attributions = new Mock<IReferralAttributionRepository>();

        var service = BuildService(accessCodes, attributions);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.SetActiveAsync(TenantId, Guid.CreateVersion7(), null, null, isActive: false));
    }

    [Fact]
    public async Task GetActiveByAttributionAsync_NoActiveCode_ReturnsNull()
    {
        var accessCodes = new Mock<IReferralAttributionAccessCodeRepository>();
        var attributionId = Guid.CreateVersion7();
        accessCodes.Setup(a => a.GetActiveByAttributionAsync(TenantId, attributionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReferralAttributionAccessCode?)null);
        var attributions = new Mock<IReferralAttributionRepository>();

        var service = BuildService(accessCodes, attributions);
        var result = await service.GetActiveByAttributionAsync(TenantId, attributionId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveByAttributionAsync_ActiveCodeExists_ReturnsIt()
    {
        var attributionId = Guid.CreateVersion7();
        var accessCode = ReferralAttributionAccessCode.Create(TenantId, attributionId, "hash", null, null, null);
        var accessCodes = new Mock<IReferralAttributionAccessCodeRepository>();
        accessCodes.Setup(a => a.GetActiveByAttributionAsync(TenantId, attributionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessCode);
        var attributions = new Mock<IReferralAttributionRepository>();

        var service = BuildService(accessCodes, attributions);
        var result = await service.GetActiveByAttributionAsync(TenantId, attributionId);

        Assert.NotNull(result);
        Assert.Equal(accessCode.Id, result!.Id);
    }
}
