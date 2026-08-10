using BuildingBlocks.Commerce;
using Contracts.Commerce;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Identity.Tests;

public class ProductProvisioningServiceTests
{
    [Fact]
    public async Task ProvisionAsync_Enable_GrantsDirectProductAccessToTenantOwner()
    {
        await using var db = BuildDbContext();

        var product = Product.Create("SynqFund", "SYNQ_FUND");
        var tenant = Tenant.Create("Acme Law", "acme-law");
        var org = Organization.Create(tenant.Id, "Acme Law", OrgType.LawFirm, displayName: "Acme Law");
        var owner = User.Create(tenant.Id, "owner@acme.test", "password-hash", "Owner", "User");

        tenant.SetOwner(owner.Id);

        db.Products.Add(product);
        db.Tenants.Add(tenant);
        db.Organizations.Add(org);
        db.Users.Add(owner);
        db.UserTenants.Add(UserTenant.Create(owner.Id, tenant.Id));
        await db.SaveChangesAsync();

        var userProductAccessService = new UserProductAccessService(
            db,
            new StubAuditPublisher(),
            NullLogger<UserProductAccessService>.Instance);

        var sut = new ProductProvisioningService(
            db,
            [],
            userProductAccessService,
            NullLogger<ProductProvisioningService>.Instance,
            new StubCommerceLifecycleNotifier());

        var result = await sut.ProvisionAsync(
            new ProvisionProductRequest(tenant.Id, product.Code, true),
            CancellationToken.None);

        Assert.True(result.Enabled);
        Assert.True(await db.UserProductAccessRecords.AnyAsync(a =>
            a.TenantId == tenant.Id &&
            a.UserId == owner.Id &&
            a.ProductCode == product.Code &&
            a.AccessStatus == AccessStatus.Granted));
    }

    [Fact]
    public async Task ProvisionAsync_Enable_DoesNotDuplicateOwnerProductAccess()
    {
        await using var db = BuildDbContext();

        var product = Product.Create("SynqFund", "SYNQ_FUND");
        var tenant = Tenant.Create("Acme Law", "acme-law");
        var org = Organization.Create(tenant.Id, "Acme Law", OrgType.LawFirm, displayName: "Acme Law");
        var owner = User.Create(tenant.Id, "owner@acme.test", "password-hash", "Owner", "User");

        tenant.SetOwner(owner.Id);

        db.Products.Add(product);
        db.Tenants.Add(tenant);
        db.Organizations.Add(org);
        db.Users.Add(owner);
        db.UserTenants.Add(UserTenant.Create(owner.Id, tenant.Id));
        db.UserProductAccessRecords.Add(UserProductAccess.Create(tenant.Id, owner.Id, product.Code));
        await db.SaveChangesAsync();

        var userProductAccessService = new UserProductAccessService(
            db,
            new StubAuditPublisher(),
            NullLogger<UserProductAccessService>.Instance);

        var sut = new ProductProvisioningService(
            db,
            [],
            userProductAccessService,
            NullLogger<ProductProvisioningService>.Instance,
            new StubCommerceLifecycleNotifier());

        await sut.ProvisionAsync(
            new ProvisionProductRequest(tenant.Id, product.Code, true),
            CancellationToken.None);

        var rows = await db.UserProductAccessRecords
            .Where(a => a.TenantId == tenant.Id && a.UserId == owner.Id && a.ProductCode == product.Code)
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal(AccessStatus.Granted, rows[0].AccessStatus);
    }

    [Fact]
    public async Task ProvisionAsync_Enable_BumpsAccessVersionForTenantMembersWhenEntitlementChanges()
    {
        await using var db = BuildDbContext();

        var product = Product.Create("Xenia", "SYNQ_AI");
        var tenant = Tenant.Create("Acme Law", "acme-law");
        var org = Organization.Create(tenant.Id, "Acme Law", OrgType.LawFirm, displayName: "Acme Law");
        var owner = User.Create(tenant.Id, "owner@acme.test", "password-hash", "Owner", "User");
        var member = User.Create(tenant.Id, "member@acme.test", "password-hash", "Member", "User");

        tenant.SetOwner(owner.Id);

        db.Products.Add(product);
        db.Tenants.Add(tenant);
        db.Organizations.Add(org);
        db.Users.AddRange(owner, member);
        db.UserTenants.AddRange(
            UserTenant.Create(owner.Id, tenant.Id),
            UserTenant.Create(member.Id, tenant.Id));
        await db.SaveChangesAsync();

        var ownerInitialAccessVersion = owner.AccessVersion;
        var memberInitialAccessVersion = member.AccessVersion;

        var userProductAccessService = new UserProductAccessService(
            db,
            new StubAuditPublisher(),
            NullLogger<UserProductAccessService>.Instance);

        var sut = new ProductProvisioningService(
            db,
            [],
            userProductAccessService,
            NullLogger<ProductProvisioningService>.Instance,
            new StubCommerceLifecycleNotifier());

        await sut.ProvisionAsync(
            new ProvisionProductRequest(tenant.Id, product.Code, true),
            CancellationToken.None);

        await db.Entry(owner).ReloadAsync();
        await db.Entry(member).ReloadAsync();

        Assert.True(owner.AccessVersion > ownerInitialAccessVersion);
        Assert.True(member.AccessVersion > memberInitialAccessVersion);
    }

    private static IdentityDbContext BuildDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"product-provisioning-{Guid.CreateVersion7()}")
            .Options;

        return new IdentityDbContext(options);
    }

    private sealed class StubAuditPublisher : IAuditPublisher
    {
        public void Publish(
            string eventType,
            string action,
            string description,
            Guid? tenantId,
            Guid? actorUserId = null,
            string? entityType = null,
            string? entityId = null,
            string? before = null,
            string? after = null,
            string? metadata = null,
            string? correlationId = null)
        {
        }
    }

    private sealed class StubCommerceLifecycleNotifier : ICommerceLifecycleNotifier
    {
        public Task NotifyAsync(CommerceLifecycleEvent ev, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
