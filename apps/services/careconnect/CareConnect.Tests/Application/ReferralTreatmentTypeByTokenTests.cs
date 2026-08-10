// Tests for UpdateTreatmentTypeByTokenAsync — public token-gated treatment-type update.
// Covers: happy path set, happy path clear, revoked token, malformed token,
//         referralId mismatch, terminal status blocks, non-existent treatment type ID.
using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using LegalSynq.AuditClient;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

public class ReferralTreatmentTypeByTokenTests
{
    private const string TestSecret  = "TEST-TREATMENT-TYPE-BY-TOKEN-SECRET-2026";
    private const string TestBaseUrl = "http://localhost:3000";

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ReferralEmailService BuildEmailService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReferralToken:Secret"] = TestSecret,
                ["AppBaseUrl"]           = TestBaseUrl,
            })
            .Build();

        return new ReferralEmailService(
            new Mock<INotificationRepository>().Object,
            new Mock<INotificationsProducer>().Object,
            config,
            new Mock<ITenantServiceClient>().Object,
            new Mock<ITenantSubdomainCache>().Object,
            NullLogger<ReferralEmailService>.Instance);
    }

    private static Referral BuildReferral(string status = Referral.ValidStatuses.New)
    {
        var r = Referral.Create(
            tenantId:                  Guid.NewGuid(),
            referringOrganizationId:   null,
            receivingOrganizationId:   Guid.NewGuid(),
            providerId:                Guid.NewGuid(),
            subjectPartyId:            null,
            subjectNameSnapshot:       null,
            subjectDobSnapshot:        null,
            clientFirstName:           "Jane",
            clientLastName:            "Doe",
            clientDob:                 null,
            clientPhone:               "555-0100",
            clientEmail:               "jane@example.com",
            caseNumber:                null,
            requestedService:          "Therapy",
            urgency:                   Referral.ValidUrgencies.Normal,
            notes:                     null,
            createdByUserId:           null,
            organizationRelationshipId: null,
            referrerEmail:             null,
            referrerName:              null);

        if (status == Referral.ValidStatuses.NewOpened)  r.MarkAsOpened();
        if (status == Referral.ValidStatuses.Accepted)   r.MarkAsOpened(); // New → NewOpened first
        // For terminal statuses used in tests we just return New — terminal tests use Accept domain methods
        return r;
    }

    private static ReferralService BuildService(
        Referral            referral,
        IReferralEmailService? emailService      = null,
        string?             treatmentTypeName   = "Cognitive Behavioural Therapy",
        bool                treatmentTypeExists = true)
    {
        var referralRepo = new Mock<IReferralRepository>();
        var notifRepo    = new Mock<INotificationRepository>();
        var auditClient  = new Mock<IAuditEventClient>();
        var httpCtx      = new Mock<IHttpContextAccessor>();

        referralRepo.Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(referral);
        referralRepo.Setup(r => r.UpdateAsync(
                        It.IsAny<Referral>(),
                        It.IsAny<ReferralStatusHistory?>(),
                        It.IsAny<ReferralProviderReassignment?>(),
                        It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
        referralRepo.Setup(r => r.GetTreatmentTypeNameAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(treatmentTypeExists ? treatmentTypeName : null);

        notifRepo.Setup(r => r.GetLatestByReferralAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(),
                    It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((CareConnectNotification?)null);

        auditClient.Setup(a => a.IngestAsync(
                        It.IsAny<LegalSynq.AuditClient.DTOs.IngestAuditEventRequest>(),
                        It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new LegalSynq.AuditClient.DTOs.IngestResult(true, null, null, 200));

        var serviceProvider = new Mock<IServiceProvider>();
        var scope           = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        emailService ??= BuildEmailService();

        return new ReferralService(
            referralRepo.Object,
            new Mock<IProviderRepository>().Object,
            new Mock<ITenantServiceClient>().Object,
            new Mock<INotificationService>().Object,
            notifRepo.Object,
            emailService,
            new Mock<IIdentityOrganizationService>().Object,
            scopeFactory.Object,
            new Mock<IOrganizationRelationshipResolver>().Object,
            auditClient.Object,
            NullLogger<ReferralService>.Instance,
            httpCtx.Object,
            new Mock<IReferralAttachmentRepository>().Object,
            new Mock<IReferralAttributionRepository>().Object,
            activationRequests: null);
    }

    // ── Happy path: set a treatment type ─────────────────────────────────────

    [Fact]
    public async Task UpdateTreatmentTypeByToken_ValidToken_SetsTreatmentTypeId()
    {
        var emailSvc    = BuildEmailService();
        var referral    = BuildReferral();
        var token       = emailSvc.GenerateViewToken(referral.Id, referral.TokenVersion);
        var treatmentId = Guid.NewGuid();
        var svc         = BuildService(referral, emailSvc);

        await svc.UpdateTreatmentTypeByTokenAsync(referral.Id, token, treatmentId);

        Assert.Equal(treatmentId, referral.TreatmentTypeId);
    }

    [Fact]
    public async Task UpdateTreatmentTypeByToken_ValidToken_ReturnsResponse()
    {
        var emailSvc    = BuildEmailService();
        var referral    = BuildReferral();
        var token       = emailSvc.GenerateViewToken(referral.Id, referral.TokenVersion);
        var svc         = BuildService(referral, emailSvc);

        var result = await svc.UpdateTreatmentTypeByTokenAsync(referral.Id, token, Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal(referral.Id, result.Id);
    }

    // ── Happy path: clear a treatment type ───────────────────────────────────

    [Fact]
    public async Task UpdateTreatmentTypeByToken_NullTreatmentTypeId_ClearsTreatmentType()
    {
        var emailSvc    = BuildEmailService();
        var referral    = BuildReferral();
        // Pre-set a treatment type on the referral
        referral.Update(referral.RequestedService, referral.Urgency, referral.Status, referral.Notes,
                        updatedByUserId: null, treatmentTypeId: Guid.NewGuid());
        Assert.NotNull(referral.TreatmentTypeId);

        var token = emailSvc.GenerateViewToken(referral.Id, referral.TokenVersion);
        var svc   = BuildService(referral, emailSvc);

        await svc.UpdateTreatmentTypeByTokenAsync(referral.Id, token, treatmentTypeId: null);

        Assert.Null(referral.TreatmentTypeId);
    }

    // ── Token validation: malformed token ────────────────────────────────────

    [Fact]
    public async Task UpdateTreatmentTypeByToken_MalformedToken_ThrowsUnauthorized()
    {
        var referral = BuildReferral();
        var svc      = BuildService(referral);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.UpdateTreatmentTypeByTokenAsync(referral.Id, "not-a-valid-token", Guid.NewGuid()));
    }

    // ── Token validation: referralId mismatch ─────────────────────────────────

    [Fact]
    public async Task UpdateTreatmentTypeByToken_ReferralIdMismatch_ThrowsUnauthorized()
    {
        var emailSvc    = BuildEmailService();
        var referral    = BuildReferral();
        var differentId = Guid.NewGuid();
        // Token is valid but for a different referral ID
        var token = emailSvc.GenerateViewToken(differentId, tokenVersion: 1);
        var svc   = BuildService(referral, emailSvc);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.UpdateTreatmentTypeByTokenAsync(referral.Id, token, Guid.NewGuid()));
    }

    // ── Token validation: revoked token (version mismatch) ───────────────────

    [Fact]
    public async Task UpdateTreatmentTypeByToken_RevokedToken_ThrowsUnauthorized()
    {
        var emailSvc = BuildEmailService();
        var referral = BuildReferral();
        // Issue an old-version token before incrementing
        var staleToken = emailSvc.GenerateViewToken(referral.Id, tokenVersion: 1);
        referral.IncrementTokenVersion();   // referral now expects version 2
        var svc = BuildService(referral, emailSvc);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.UpdateTreatmentTypeByTokenAsync(referral.Id, staleToken, Guid.NewGuid()));
    }

    // ── Terminal status blocks update ─────────────────────────────────────────

    [Theory]
    [InlineData(Referral.ValidStatuses.Completed)]
    [InlineData(Referral.ValidStatuses.Cancelled)]
    [InlineData(Referral.ValidStatuses.Declined)]
    public async Task UpdateTreatmentTypeByToken_TerminalStatus_ThrowsInvalidOperation(string terminalStatus)
    {
        var emailSvc = BuildEmailService();
        var referral = BuildReferral();

        // Force the referral into the terminal status via the domain model
        if (terminalStatus == Referral.ValidStatuses.Declined)
            referral.Decline(updatedByUserId: null);
        else
            typeof(Referral).GetProperty(nameof(Referral.Status))!
                .SetValue(referral, terminalStatus, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, null, null);

        var token = emailSvc.GenerateViewToken(referral.Id, referral.TokenVersion);
        var svc   = BuildService(referral, emailSvc);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateTreatmentTypeByTokenAsync(referral.Id, token, Guid.NewGuid()));
    }

    // ── Non-existent treatment type ID ────────────────────────────────────────

    [Fact]
    public async Task UpdateTreatmentTypeByToken_UnknownTreatmentTypeId_ThrowsValidationException()
    {
        var emailSvc = BuildEmailService();
        var referral = BuildReferral();
        var token    = emailSvc.GenerateViewToken(referral.Id, referral.TokenVersion);
        var svc      = BuildService(referral, emailSvc, treatmentTypeExists: false);

        await Assert.ThrowsAsync<ValidationException>(() =>
            svc.UpdateTreatmentTypeByTokenAsync(referral.Id, token, Guid.NewGuid()));
    }

    // ── Status is preserved after treatment-type update ───────────────────────

    [Fact]
    public async Task UpdateTreatmentTypeByToken_NewReferral_StatusRemainsNew()
    {
        var emailSvc = BuildEmailService();
        var referral = BuildReferral(Referral.ValidStatuses.New);
        var token    = emailSvc.GenerateViewToken(referral.Id, referral.TokenVersion);
        var svc      = BuildService(referral, emailSvc);

        await svc.UpdateTreatmentTypeByTokenAsync(referral.Id, token, Guid.NewGuid());

        Assert.Equal(Referral.ValidStatuses.New, referral.Status);
    }
}
