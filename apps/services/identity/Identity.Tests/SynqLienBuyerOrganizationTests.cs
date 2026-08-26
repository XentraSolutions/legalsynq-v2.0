using Identity.Application.Interfaces;
using Identity.Api.Endpoints;
using Identity.Domain;
using Identity.Infrastructure.Data;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Identity.Tests;

public sealed class SynqLienBuyerOrganizationTests
{
    [Fact]
    public async Task CreateSynqLienBuyerOrganization_creates_lien_owner_org_idempotently()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var sourceBuyerOrgId = Guid.Parse("40000000-0000-0000-0000-000000000012");

        var firstResult = await AdminEndpointsLscc010.CreateSynqLienBuyerOrganization(
            new AdminEndpointsLscc010.CreateSynqLienBuyerOrgRequest(
                tenantId,
                sourceBuyerOrgId,
                "Capital Fund LLC",
                "buyer@capital.test"),
            db,
            CancellationToken.None);

        var firstStatus = Assert.IsAssignableFrom<IStatusCodeHttpResult>(firstResult);
        Assert.Equal(StatusCodes.Status201Created, firstStatus.StatusCode);
        Assert.True(await db.Tenants.AnyAsync(t => t.Id == tenantId));

        var org = await db.Organizations.SingleAsync();
        Assert.Equal(tenantId, org.TenantId);
        Assert.Equal(OrgType.LienOwner, org.OrgType);
        Assert.Equal("Capital Fund LLC", org.DisplayName);
        Assert.Contains(sourceBuyerOrgId.ToString("D"), org.Name);

        var secondResult = await AdminEndpointsLscc010.CreateSynqLienBuyerOrganization(
            new AdminEndpointsLscc010.CreateSynqLienBuyerOrgRequest(
                tenantId,
                sourceBuyerOrgId,
                "Capital Fund LLC",
                "buyer@capital.test"),
            db,
            CancellationToken.None);

        var secondStatus = Assert.IsAssignableFrom<IStatusCodeHttpResult>(secondResult);
        Assert.Equal(StatusCodes.Status200OK, secondStatus.StatusCode);
        Assert.Equal(1, await db.Organizations.CountAsync());
    }

    [Fact]
    public async Task SelfRegisterSynqLienBuyer_rejects_existing_account_without_linking_access()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var tenant = Tenant.Rehydrate(tenantId, "synqlien-test", status: "Active");
        var org = Organization.Create(
            tenantId,
            "Capital Fund LLC [synqlien:40000000-0000-0000-0000-000000000012]",
            OrgType.LienOwner,
            "Capital Fund LLC");
        var existingUser = User.Create(
            tenantId,
            "buyer@capital.test",
            "hashed-existing-password",
            "Buyer",
            "Reviewer");

        db.Tenants.Add(tenant);
        db.Organizations.Add(org);
        db.Users.Add(existingUser);
        db.UserTenants.Add(UserTenant.Create(existingUser.Id, tenantId));
        await db.SaveChangesAsync();

        var result = await AdminEndpointsLscc010.SelfRegisterSynqLienBuyer(
            org.Id,
            new AdminEndpointsLscc010.SelfRegisterUserRequest(
                tenantId,
                "buyer@capital.test",
                "Password123!",
                "Buyer",
                "Reviewer",
                "+13105551212"),
            db,
            new TestPasswordHasher(),
            new ThrowingProductProvisioningService(),
            new ThrowingUserProductAccessService(),
            new NoOpAuditEventClient(),
            NullLoggerFactory.Instance,
            CancellationToken.None);

        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, status.StatusCode);
        Assert.Null(await db.Users.Where(u => u.Id == existingUser.Id).Select(u => u.Phone).SingleAsync());
        Assert.Empty(await db.UserOrganizationMemberships.ToListAsync());
        Assert.Empty(await db.UserProductAccessRecords.ToListAsync());
        Assert.Empty(await db.UserRoleAssignments.ToListAsync());
    }

    [Fact]
    public async Task SelfRegisterSynqLienBuyer_rejects_when_organization_already_has_active_member()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var tenant = Tenant.Rehydrate(tenantId, "synqlien-test", status: "Active");
        var org = Organization.Create(
            tenantId,
            "Capital Fund LLC [synqlien:40000000-0000-0000-0000-000000000012]",
            OrgType.LienOwner,
            "Capital Fund LLC");
        var existingMember = User.Create(
            tenantId,
            "existing-member@capital.test",
            "hashed-existing-password",
            "Existing",
            "Member");

        db.Tenants.Add(tenant);
        db.Organizations.Add(org);
        db.Users.Add(existingMember);
        db.UserTenants.Add(UserTenant.Create(existingMember.Id, tenantId));
        db.UserOrganizationMemberships.Add(
            UserOrganizationMembership.Create(existingMember.Id, org.Id, MemberRole.Member));
        await db.SaveChangesAsync();

        var result = await AdminEndpointsLscc010.SelfRegisterSynqLienBuyer(
            org.Id,
            new AdminEndpointsLscc010.SelfRegisterUserRequest(
                tenantId,
                "new-buyer@capital.test",
                "Password123!",
                "New",
                "Buyer",
                "+13105551212"),
            db,
            new TestPasswordHasher(),
            new ThrowingProductProvisioningService(),
            new ThrowingUserProductAccessService(),
            new NoOpAuditEventClient(),
            NullLoggerFactory.Instance,
            CancellationToken.None);

        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, status.StatusCode);
        Assert.False(await db.Users.AnyAsync(u => u.Email == "new-buyer@capital.test"));
        Assert.Single(await db.UserOrganizationMemberships.ToListAsync());
        Assert.Empty(await db.UserProductAccessRecords.ToListAsync());
        Assert.Empty(await db.UserRoleAssignments.ToListAsync());
    }

    private static IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase("synqlien-buyer-org-" + Guid.CreateVersion7())
            .Options;
        return new IdentityDbContext(options);
    }

    private sealed class TestPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed-" + password;

        public bool Verify(string password, string hash) => hash == Hash(password);
    }

    private sealed class ThrowingProductProvisioningService : IProductProvisioningService
    {
        public Task<ProvisionProductResult> ProvisionAsync(
            ProvisionProductRequest request,
            CancellationToken ct = default) =>
            throw new InvalidOperationException("Product provisioning should not run for an existing account.");
    }

    private sealed class ThrowingUserProductAccessService : IUserProductAccessService
    {
        public Task<List<UserProductAccess>> GetByTenantUserAsync(
            Guid tenantId,
            Guid userId,
            CancellationToken ct = default) =>
            throw new InvalidOperationException("User product access should not be read for this test.");

        public Task<UserProductAccess?> GetByTenantUserAndCodeAsync(
            Guid tenantId,
            Guid userId,
            string productCode,
            CancellationToken ct = default) =>
            throw new InvalidOperationException("User product access should not be read for this test.");

        public Task<UserProductAccess> GrantAsync(
            Guid tenantId,
            Guid userId,
            string productCode,
            Guid? actorUserId = null,
            CancellationToken ct = default) =>
            throw new InvalidOperationException("User product access should not be granted for an existing account.");

        public Task<bool> RevokeAsync(
            Guid tenantId,
            Guid userId,
            string productCode,
            Guid? actorUserId = null,
            CancellationToken ct = default) =>
            throw new InvalidOperationException("User product access should not be revoked for this test.");
    }

    private sealed class NoOpAuditEventClient : IAuditEventClient
    {
        public Task<IngestResult> IngestAsync(
            IngestAuditEventRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(new IngestResult(
                Accepted: true,
                AuditId: Guid.CreateVersion7().ToString(),
                RejectionReason: null,
                StatusCode: StatusCodes.Status202Accepted));

        public Task<BatchIngestResult> IngestBatchAsync(
            BatchIngestRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(new BatchIngestResult(Submitted: 0, Accepted: 0, Rejected: 0, Results: []));
    }
}
