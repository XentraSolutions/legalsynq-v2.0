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

public class ReferralSearchDisplayNameTests
{
    [Fact]
    public async Task SearchAsync_PopulatesNetworkNameFromReferralTenantDisplayName()
    {
        var referralTenantId = Guid.CreateVersion7();
        var providerTenantId = Guid.CreateVersion7();
        var provider = BuildProvider(providerTenantId, "Provider One");
        var referral = BuildReferral(referralTenantId, provider);

        var referrals = new Mock<IReferralRepository>();
        referrals
            .Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<GetReferralsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Referral> { referral }, 1));

        var tenantClient = new Mock<ITenantServiceClient>();
        tenantClient
            .Setup(c => c.GetDisplayNameAsync(referralTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Referrer Tenant");

        var service = BuildService(referrals.Object, tenantClient.Object);

        var result = await service.SearchAsync(Guid.CreateVersion7(), new GetReferralsQuery());

        var item = Assert.Single(result.Items);
        Assert.Equal("Referrer Tenant", item.NetworkName);
        tenantClient.Verify(
            c => c.GetDisplayNameAsync(referralTenantId, It.IsAny<CancellationToken>()),
            Times.Once);
        tenantClient.Verify(
            c => c.GetDisplayNameAsync(providerTenantId, It.IsAny<CancellationToken>()),
            Times.Never);
        referrals.Verify(
            r => r.GetProviderNetworkNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchAsync_UsesDashWhenTenantLookupFails()
    {
        var referralTenantId = Guid.CreateVersion7();
        var providerTenantId = Guid.CreateVersion7();
        var provider = BuildProvider(providerTenantId, "Provider Two");
        var referral = BuildReferral(referralTenantId, provider);

        var referrals = new Mock<IReferralRepository>();
        referrals
            .Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<GetReferralsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Referral> { referral }, 1));

        var tenantClient = new Mock<ITenantServiceClient>();
        tenantClient
            .Setup(c => c.GetDisplayNameAsync(referralTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var service = BuildService(referrals.Object, tenantClient.Object);

        var result = await service.SearchAsync(Guid.CreateVersion7(), new GetReferralsQuery());

        Assert.Equal("-", Assert.Single(result.Items).NetworkName);
    }

    [Fact]
    public async Task SearchAsync_BatchesDistinctReferralTenantLookups()
    {
        var sharedReferralTenantId = Guid.CreateVersion7();
        var firstProvider = BuildProvider(Guid.CreateVersion7(), "Provider One");
        var secondProvider = BuildProvider(Guid.CreateVersion7(), "Provider Two");

        var referrals = new Mock<IReferralRepository>();
        referrals
            .Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<GetReferralsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new List<Referral>
                {
                    BuildReferral(sharedReferralTenantId, firstProvider),
                    BuildReferral(sharedReferralTenantId, secondProvider),
                },
                2));

        var tenantClient = new Mock<ITenantServiceClient>();
        tenantClient
            .Setup(c => c.GetDisplayNameAsync(sharedReferralTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Shared Tenant");

        var service = BuildService(referrals.Object, tenantClient.Object);

        var result = await service.SearchAsync(Guid.CreateVersion7(), new GetReferralsQuery());

        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal("Shared Tenant", item.NetworkName));
        tenantClient.Verify(
            c => c.GetDisplayNameAsync(sharedReferralTenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ReferralService BuildService(
        IReferralRepository referrals,
        ITenantServiceClient tenantClient)
    {
        return new ReferralService(
            referrals,
            new Mock<IProviderRepository>().Object,
            tenantClient,
            new Mock<INotificationService>().Object,
            new Mock<INotificationRepository>().Object,
            new Mock<IReferralEmailService>().Object,
            new Mock<IServiceScopeFactory>().Object,
            new Mock<IOrganizationRelationshipResolver>().Object,
            new Mock<IAuditEventClient>().Object,
            NullLogger<ReferralService>.Instance,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IReferralAttachmentRepository>().Object,
            activationRequests: null);
    }

    private static Provider BuildProvider(Guid tenantId, string name)
    {
        return Provider.Create(
            tenantId: tenantId,
            name: name,
            organizationName: "Org",
            email: "provider@example.com",
            phone: "555-0100",
            addressLine1: "1 Main St",
            city: "Chicago",
            state: "IL",
            postalCode: "60601",
            isActive: true,
            acceptingReferrals: true,
            createdByUserId: null);
    }

    private static Referral BuildReferral(Guid tenantId, Provider provider)
    {
        var referral = Referral.Create(
            tenantId: tenantId,
            referringOrganizationId: null,
            receivingOrganizationId: null,
            providerId: provider.Id,
            subjectPartyId: null,
            subjectNameSnapshot: null,
            subjectDobSnapshot: null,
            clientFirstName: "Jane",
            clientLastName: "Doe",
            clientDob: null,
            clientPhone: "555-0200",
            clientEmail: "jane@example.com",
            caseNumber: "CASE-1",
            requestedService: "PT",
            urgency: Referral.ValidUrgencies.Normal,
            notes: null,
            createdByUserId: null);

        typeof(Referral)
            .GetProperty("Provider")!
            .SetValue(referral, provider);

        return referral;
    }
}
