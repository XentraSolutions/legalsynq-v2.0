using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

public class ReferralThreadServiceTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid ReferringOrgId = Guid.CreateVersion7();
    private static readonly Guid ReceivingOrgId = Guid.CreateVersion7();

    [Fact]
    public async Task GetAuthenticatedCommentsAsync_ReturnsComments_ForReceivingParticipant()
    {
        var referral = BuildReferral();
        var repo = new Mock<IReferralRepository>();
        repo.Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var commentsRepo = new Mock<IReferralCommentRepository>();
        commentsRepo.Setup(r => r.GetByReferralAsync(referral.TenantId, referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ReferralComment
                {
                    TenantId = referral.TenantId,
                    ReferralId = referral.Id,
                    SenderType = "provider",
                    SenderName = "Dr. Gray",
                    Message = "We can take this case.",
                    CreatedAt = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
                },
            ]);

        var sut = BuildService(repo, commentsRepo);

        var result = await sut.GetAuthenticatedCommentsAsync(
            TenantId,
            referral.Id,
            ReceivingOrgId,
            callerEmail: null,
            useGlobalLookup: true,
            bypassParticipantCheck: false);

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal("provider", result[0].SenderType);
        Assert.Equal("Dr. Gray", result[0].SenderName);
    }

    [Fact]
    public async Task GetAuthenticatedCommentsAsync_ReturnsComments_ForPublicReferrerEmailMatch()
    {
        var referral = BuildReferral(referringOrganizationId: null);
        var repo = new Mock<IReferralRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var commentsRepo = new Mock<IReferralCommentRepository>();
        commentsRepo.Setup(r => r.GetByReferralAsync(referral.TenantId, referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = BuildService(repo, commentsRepo);

        var result = await sut.GetAuthenticatedCommentsAsync(
            TenantId,
            referral.Id,
            callerOrganizationId: null,
            callerEmail: "firm@example.com",
            useGlobalLookup: false,
            bypassParticipantCheck: false);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAuthenticatedCommentsAsync_ReturnsNull_ForWrongReceivingOrg()
    {
        var referral = BuildReferral();
        var repo = new Mock<IReferralRepository>();
        repo.Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var sut = BuildService(repo, new Mock<IReferralCommentRepository>());

        var result = await sut.GetAuthenticatedCommentsAsync(
            TenantId,
            referral.Id,
            Guid.CreateVersion7(),
            callerEmail: null,
            useGlobalLookup: true,
            bypassParticipantCheck: false);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAuthenticatedCommentsAsync_ReturnsComments_ForPlatformAdminBypass()
    {
        var referral = BuildReferral();
        var repo = new Mock<IReferralRepository>();
        repo.Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var commentsRepo = new Mock<IReferralCommentRepository>();
        commentsRepo.Setup(r => r.GetByReferralAsync(referral.TenantId, referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = BuildService(repo, commentsRepo);

        var result = await sut.GetAuthenticatedCommentsAsync(
            TenantId,
            referral.Id,
            callerOrganizationId: null,
            callerEmail: null,
            useGlobalLookup: true,
            bypassParticipantCheck: true);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task PostAuthenticatedCommentAsync_PersistsProviderComment_ForReceivingParticipant()
    {
        var referral = BuildReferral();
        var repo = new Mock<IReferralRepository>();
        repo.Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        ReferralComment? saved = null;
        var commentsRepo = new Mock<IReferralCommentRepository>();
        commentsRepo.Setup(r => r.AddAsync(It.IsAny<ReferralComment>(), It.IsAny<CancellationToken>()))
            .Callback<ReferralComment, CancellationToken>((comment, _) => saved = comment)
            .Returns(Task.CompletedTask);

        var sut = BuildService(repo, commentsRepo);

        var result = await sut.PostAuthenticatedCommentAsync(
            TenantId,
            referral.Id,
            ReceivingOrgId,
            callerEmail: null,
            "Dr. Gray",
            "We have availability next week.",
            useGlobalLookup: true);

        Assert.NotNull(result);
        Assert.NotNull(saved);
        Assert.Equal("provider", saved!.SenderType);
        Assert.Equal("Dr. Gray", saved.SenderName);
        Assert.Equal("We have availability next week.", saved.Message);
    }

    [Fact]
    public async Task PostAuthenticatedCommentAsync_PersistsReferrerComment_ForReferringParticipant()
    {
        var referral = BuildReferral();
        var repo = new Mock<IReferralRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        ReferralComment? saved = null;
        var commentsRepo = new Mock<IReferralCommentRepository>();
        commentsRepo.Setup(r => r.AddAsync(It.IsAny<ReferralComment>(), It.IsAny<CancellationToken>()))
            .Callback<ReferralComment, CancellationToken>((comment, _) => saved = comment)
            .Returns(Task.CompletedTask);

        var sut = BuildService(repo, commentsRepo);

        var result = await sut.PostAuthenticatedCommentAsync(
            TenantId,
            referral.Id,
            ReferringOrgId,
            callerEmail: null,
            "Sarah",
            "Following up on the referral.",
            useGlobalLookup: false);

        Assert.NotNull(result);
        Assert.NotNull(saved);
        Assert.Equal("referrer", saved!.SenderType);
        Assert.Equal("Sarah", saved.SenderName);
    }

    [Fact]
    public async Task PostAuthenticatedCommentAsync_ReturnsNull_ForMissingParticipant()
    {
        var referral = BuildReferral();
        var repo = new Mock<IReferralRepository>();
        repo.Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var sut = BuildService(repo, new Mock<IReferralCommentRepository>());

        var result = await sut.PostAuthenticatedCommentAsync(
            TenantId,
            referral.Id,
            callerOrganizationId: null,
            callerEmail: null,
            "Dr. Gray",
            "We have availability next week.",
            useGlobalLookup: true);

        Assert.Null(result);
    }

    [Fact]
    public async Task PostPublicCommentAsync_SendsNotification_ThroughSharedPath()
    {
        var referral = BuildReferral();
        SetProvider(referral, new ProviderStub
        {
            Name = "Dr. Gray",
            OrganizationName = "Gray Clinic",
            Email = "clinic@example.com",
            AccessStage = ProviderAccessStage.CommonPortal,
        }.ToDomain(referral.ReceivingOrganizationId));

        var emailService = new Mock<IReferralEmailService>();
        emailService.Setup(e => e.ValidateViewTokenDetailed("valid-token"))
            .Returns(CareConnect.Application.DTOs.ReferralTokenValidationOutcome.Success(referral.Id, referral.TokenVersion));
        emailService.Setup(e => e.SendCommentNotificationAsync(referral, It.IsAny<ReferralComment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var repo = new Mock<IReferralRepository>();
        repo.Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var commentsRepo = new Mock<IReferralCommentRepository>();
        commentsRepo.Setup(r => r.AddAsync(It.IsAny<ReferralComment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scopeFactory = BuildScopeFactory(emailService.Object);
        var sut = BuildService(repo, commentsRepo, emailService.Object, scopeFactory);

        var result = await sut.PostPublicCommentAsync("valid-token", "referrer", "Sarah", "Checking status.");
        await Task.Delay(50);

        Assert.NotNull(result);
        emailService.Verify(
            e => e.SendCommentNotificationAsync(referral, It.IsAny<ReferralComment>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Referral BuildReferral()
        => BuildReferral(ReferringOrgId);

    private static Referral BuildReferral(Guid? referringOrganizationId)
    {
        return Referral.Create(
            TenantId,
            referringOrganizationId: referringOrganizationId,
            receivingOrganizationId: ReceivingOrgId,
            providerId: Guid.CreateVersion7(),
            subjectPartyId: null,
            subjectNameSnapshot: null,
            subjectDobSnapshot: null,
            clientFirstName: "Jamie",
            clientLastName: "Stone",
            clientDob: null,
            clientPhone: "555-0100",
            clientEmail: "jamie@example.com",
            caseNumber: "CASE-1",
            requestedService: "MRI",
            urgency: Referral.ValidUrgencies.Normal,
            notes: "Needs imaging",
            createdByUserId: null,
            referrerEmail: "firm@example.com",
            referrerName: "Sarah");
    }

    private static ReferralThreadService BuildService(
        Mock<IReferralRepository> referrals,
        Mock<IReferralCommentRepository> comments,
        IReferralEmailService? emailService = null,
        IServiceScopeFactory? scopeFactory = null)
    {
        var attachments = new Mock<IReferralAttachmentRepository>();
        attachments.Setup(r => r.GetByReferralAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        emailService ??= Mock.Of<IReferralEmailService>(e =>
            e.ValidateViewTokenDetailed(It.IsAny<string>()) == CareConnect.Application.DTOs.ReferralTokenValidationOutcome.Failure(CareConnect.Application.DTOs.ReferralTokenFailureReasons.Malformed));
        scopeFactory ??= BuildScopeFactory(emailService);

        return new ReferralThreadService(
            referrals.Object,
            comments.Object,
            attachments.Object,
            emailService,
            scopeFactory,
            NullLogger<ReferralThreadService>.Instance);
    }

    private static IServiceScopeFactory BuildScopeFactory(IReferralEmailService emailService)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IReferralEmailService))).Returns(emailService);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);
        return scopeFactory.Object;
    }

    private static void SetProvider(Referral referral, Provider provider)
    {
        typeof(Referral).GetProperty(nameof(Referral.Provider))!.SetValue(referral, provider);
    }

    private sealed class ProviderStub
    {
        public string Name { get; init; } = string.Empty;
        public string OrganizationName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string AccessStage { get; init; } = ProviderAccessStage.CommonPortal;

        public Provider ToDomain(Guid? organizationId)
        {
            var provider = Provider.Create(
                TenantId,
                Name,
                OrganizationName,
                Email,
                "555-0101",
                "123 Main",
                "Las Vegas",
                "NV",
                "89101",
                isActive: true,
                acceptingReferrals: true,
                createdByUserId: null);

            if (organizationId.HasValue)
                provider.LinkOrganization(organizationId.Value);

            if (AccessStage == ProviderAccessStage.CommonPortal)
                provider.MarkCommonPortalActivated(null);
            else if (AccessStage == ProviderAccessStage.Tenant)
                provider.MarkTenantProvisioned(Guid.CreateVersion7());

            return provider;
        }
    }
}
