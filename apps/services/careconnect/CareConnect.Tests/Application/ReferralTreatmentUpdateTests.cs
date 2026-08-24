// Treatment-type update service-layer tests.
// Covers the partial-update contract introduced alongside ReferralTreatmentField:
//   - Updating treatment type on a NewOpened referral with Status=null does not throw
//     and preserves the existing status.
//   - Updating treatment type preserves RequestedService and Notes when not supplied.
//   - ClearNotes=true explicitly clears notes regardless of Notes field value.
//   - Supplying a Status alongside TreatmentTypeId still performs the transition.
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using LegalSynq.AuditClient;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

public class ReferralTreatmentUpdateTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Referral BuildNewOpenedReferral(Guid tenantId, string notes = "Original notes", string requestedService = "Therapy")
    {
        var r = Referral.Create(
            tenantId:                  tenantId,
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
            requestedService:          requestedService,
            urgency:                   Referral.ValidUrgencies.Normal,
            notes:                     notes,
            createdByUserId:           null,
            organizationRelationshipId: null,
            referrerEmail:             null,
            referrerName:              null);
        r.MarkAsOpened();   // New → NewOpened
        return r;
    }

    private static ReferralService BuildService(
        Referral referral,
        string?  treatmentTypeName = "Cognitive Behavioural Therapy")
    {
        var referralRepo = new Mock<IReferralRepository>();
        var notifRepo    = new Mock<INotificationRepository>();
        var auditClient  = new Mock<IAuditEventClient>();
        var httpCtx      = new Mock<IHttpContextAccessor>();

        referralRepo
            .Setup(r => r.GetByIdAsync(referral.TenantId, referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);
        referralRepo
            .Setup(r => r.GetByIdGlobalAsync(referral.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);
        referralRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Referral>(), It.IsAny<ReferralStatusHistory?>(),
                It.IsAny<ReferralProviderReassignment?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        referralRepo
            .Setup(r => r.GetTreatmentTypeNameAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(treatmentTypeName);

        notifRepo
            .Setup(r => r.GetLatestByReferralAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CareConnectNotification?)null);

        auditClient
            .Setup(a => a.IngestAsync(It.IsAny<LegalSynq.AuditClient.DTOs.IngestAuditEventRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegalSynq.AuditClient.DTOs.IngestResult(true, null, null, 200));

        var serviceProvider = new Mock<IServiceProvider>();
        var scope           = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return new ReferralService(
            referralRepo.Object,
            new Mock<IProviderRepository>().Object,
            new Mock<ITenantServiceClient>().Object,
            new Mock<INotificationService>().Object,
            notifRepo.Object,
            new Mock<IReferralEmailService>().Object,
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

    // ── Core bug regression: NewOpened + null Status ──────────────────────────

    [Fact]
    public async Task UpdateAsync_NullStatus_OnNewOpenedReferral_DoesNotThrow()
    {
        var tenantId = Guid.NewGuid();
        var referral = BuildNewOpenedReferral(tenantId);
        var svc      = BuildService(referral);

        var request = new UpdateReferralRequest
        {
            Urgency         = Referral.ValidUrgencies.Normal,
            Status          = null,   // treatment-type-only — no status change
            TreatmentTypeId = Guid.NewGuid(),
        };

        var ex = await Record.ExceptionAsync(() =>
            svc.UpdateAsync(tenantId, referral.Id, userId: null, request));

        Assert.Null(ex);
    }

    [Fact]
    public async Task UpdateAsync_NullStatus_OnNewOpenedReferral_StatusRemainsNewOpened()
    {
        var tenantId = Guid.NewGuid();
        var referral = BuildNewOpenedReferral(tenantId);
        var svc      = BuildService(referral);

        var request = new UpdateReferralRequest
        {
            Urgency         = Referral.ValidUrgencies.Normal,
            Status          = null,
            TreatmentTypeId = Guid.NewGuid(),
        };

        await svc.UpdateAsync(tenantId, referral.Id, userId: null, request);

        Assert.Equal(Referral.ValidStatuses.NewOpened, referral.Status);
    }

    [Fact]
    public async Task UpdateAsync_NullStatus_TreatmentTypeIdIsApplied()
    {
        var tenantId     = Guid.NewGuid();
        var referral     = BuildNewOpenedReferral(tenantId);
        var treatmentId  = Guid.NewGuid();
        var svc          = BuildService(referral);

        var request = new UpdateReferralRequest
        {
            Urgency         = Referral.ValidUrgencies.Normal,
            Status          = null,
            TreatmentTypeId = treatmentId,
        };

        await svc.UpdateAsync(tenantId, referral.Id, userId: null, request);

        Assert.Equal(treatmentId, referral.TreatmentTypeId);
    }

    // ── Null-field preservation ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_NullRequestedService_PreservesExistingValue()
    {
        var tenantId = Guid.NewGuid();
        var referral = BuildNewOpenedReferral(tenantId, requestedService: "Original Service");
        var svc      = BuildService(referral);

        var request = new UpdateReferralRequest
        {
            Urgency          = Referral.ValidUrgencies.Normal,
            RequestedService = null,   // omitted — should not clear
        };

        await svc.UpdateAsync(tenantId, referral.Id, userId: null, request);

        Assert.Equal("Original Service", referral.RequestedService);
    }

    [Fact]
    public async Task UpdateAsync_NullNotes_PreservesExistingNotes()
    {
        var tenantId = Guid.NewGuid();
        var referral = BuildNewOpenedReferral(tenantId, notes: "Important notes");
        var svc      = BuildService(referral);

        var request = new UpdateReferralRequest
        {
            Urgency    = Referral.ValidUrgencies.Normal,
            Notes      = null,    // omitted — should not clear
            ClearNotes = false,
        };

        await svc.UpdateAsync(tenantId, referral.Id, userId: null, request);

        Assert.Equal("Important notes", referral.Notes);
    }

    [Fact]
    public async Task UpdateAsync_ClearNotesTrue_ClearsExistingNotes()
    {
        var tenantId = Guid.NewGuid();
        var referral = BuildNewOpenedReferral(tenantId, notes: "Notes to clear");
        var svc      = BuildService(referral);

        var request = new UpdateReferralRequest
        {
            Urgency    = Referral.ValidUrgencies.Normal,
            ClearNotes = true,
        };

        await svc.UpdateAsync(tenantId, referral.Id, userId: null, request);

        Assert.Null(referral.Notes);
    }

    // ── Status + TreatmentTypeId together still works ─────────────────────────

    [Fact]
    public async Task UpdateAsync_WithStatusAndTreatmentTypeId_TransitionsAndSetsType()
    {
        var tenantId    = Guid.NewGuid();
        var referral    = BuildNewOpenedReferral(tenantId);
        var treatmentId = Guid.NewGuid();
        var svc         = BuildService(referral);

        var request = new UpdateReferralRequest
        {
            Urgency         = Referral.ValidUrgencies.Normal,
            Status          = Referral.ValidStatuses.Accepted,
            TreatmentTypeId = treatmentId,
        };

        await svc.UpdateAsync(tenantId, referral.Id, userId: null, request);

        Assert.Equal(Referral.ValidStatuses.Accepted, referral.Status);
        Assert.Equal(treatmentId, referral.TreatmentTypeId);
    }
}
