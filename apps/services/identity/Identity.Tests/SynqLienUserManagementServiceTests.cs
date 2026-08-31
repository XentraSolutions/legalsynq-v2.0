using BuildingBlocks.Authorization;
using AuthorizationProductCodes = BuildingBlocks.Authorization.ProductCodes;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.Tests;

public class SynqLienUserManagementServiceTests
{
    [Fact]
    public async Task InviteAndAccept_NewUser_AppliesProductAndRolesOnlyOnAcceptance()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.InviteAsync(
            fixture.Tenant.Id,
            fixture.Actor.Id,
            new SynqLienInviteCommand(
                "new-liens-user@example.com", "New", "User", "+15555550100",
                [ProductRoleCodes.SynqLienSeller]),
            CancellationToken.None);

        Assert.NotNull(result.InvitationId);
        Assert.False(result.AccessGrantedImmediately);
        Assert.False(await fixture.Db.UserProductAccessRecords.AnyAsync(a => a.UserId == result.UserId));

        var invitation = await fixture.Db.UserInvitations
            .Include(i => i.RoleGrants)
            .Include(i => i.User)
            .SingleAsync(i => i.Id == result.InvitationId);
        Assert.Equal(AuthorizationProductCodes.SynqLiens, invitation.ProductCode);
        Assert.Equal(ProductRoleCodes.SynqLienSeller, Assert.Single(invitation.RoleGrants).RoleCode);

        invitation.User.Activate();
        invitation.Accept();
        await fixture.Service.ApplyInvitationGrantsAsync(invitation.Id);
        await fixture.Db.SaveChangesAsync();

        var access = await fixture.Db.UserProductAccessRecords.SingleAsync(a => a.UserId == result.UserId);
        var role = await fixture.Db.UserRoleAssignments.SingleAsync(a => a.UserId == result.UserId);
        Assert.Equal(AccessStatus.Granted, access.AccessStatus);
        Assert.Equal(AuthorizationProductCodes.SynqLiens, access.ProductCode);
        Assert.Equal(ProductRoleCodes.SynqLienSeller, role.RoleCode);
        Assert.NotNull(Assert.Single(invitation.RoleGrants).AppliedAtUtc);
        Assert.Equal(1, invitation.User.AccessVersion);
    }

    [Fact]
    public async Task ReplaceRoles_ReplacesDirectLiensRoles_AndIncrementsVersionOnce()
    {
        await using var fixture = await Fixture.CreateAsync();
        var target = User.Create(
            fixture.Tenant.Id, "target@example.com", "hash", "Target", "User");
        fixture.Db.Users.Add(target);
        fixture.Db.UserTenants.Add(UserTenant.Create(target.Id, fixture.Tenant.Id));
        fixture.Db.UserProductAccessRecords.Add(UserProductAccess.Create(
            fixture.Tenant.Id, target.Id, AuthorizationProductCodes.SynqLiens));
        fixture.Db.UserRoleAssignments.Add(UserRoleAssignment.Create(
            fixture.Tenant.Id, target.Id, ProductRoleCodes.SynqLienSeller, AuthorizationProductCodes.SynqLiens));
        await fixture.Db.SaveChangesAsync();

        var detail = await fixture.Service.ReplaceRolesAsync(
            fixture.Tenant.Id,
            fixture.Actor.Id,
            target.Id,
            [ProductRoleCodes.SynqLienHolder],
            expectedAccessVersion: 0);

        Assert.Equal(1, detail.AccessVersion);
        Assert.Contains(detail.Roles, r => r.Code == ProductRoleCodes.SynqLienHolder);
        Assert.DoesNotContain(detail.Roles, r => r.Code == ProductRoleCodes.SynqLienSeller);
        Assert.Contains(await fixture.Db.UserRoleAssignments.ToListAsync(), r =>
            r.UserId == target.Id && r.RoleCode == ProductRoleCodes.SynqLienSeller &&
            r.AssignmentStatus == AssignmentStatus.Removed);
    }

    [Fact]
    public async Task SetAccess_WithStaleVersion_ReturnsPreconditionFailure()
    {
        await using var fixture = await Fixture.CreateAsync();
        var target = User.Create(
            fixture.Tenant.Id, "stale@example.com", "hash", "Stale", "User");
        target.IncrementAccessVersion();
        fixture.Db.Users.Add(target);
        fixture.Db.UserTenants.Add(UserTenant.Create(target.Id, fixture.Tenant.Id));
        await fixture.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SynqLienUserManagementException>(() =>
            fixture.Service.SetAccessAsync(
                fixture.Tenant.Id, fixture.Actor.Id, target.Id, true, expectedAccessVersion: 0));

        Assert.Equal(412, ex.StatusCode);
        Assert.Equal("CONCURRENCY_CONFLICT", ex.Code);
    }

    [Fact]
    public async Task SetAccess_WithOrganizationScopedRecord_RejectsTenantScopedMutation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var target = User.Create(
            fixture.Tenant.Id, "organization-user@example.com", "hash", "Organization", "User");
        fixture.Db.Users.Add(target);
        fixture.Db.UserTenants.Add(UserTenant.Create(target.Id, fixture.Tenant.Id));
        fixture.Db.UserProductAccessRecords.Add(UserProductAccess.Create(
            fixture.Tenant.Id,
            target.Id,
            AuthorizationProductCodes.SynqLiens,
            organizationId: Guid.CreateVersion7()));
        await fixture.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SynqLienUserManagementException>(() =>
            fixture.Service.SetAccessAsync(
                fixture.Tenant.Id, fixture.Actor.Id, target.Id, true, expectedAccessVersion: 0));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("ORGANIZATION_SCOPED_ACCESS_UNSUPPORTED", ex.Code);
    }

    [Fact]
    public async Task ApplyInvitationGrants_RevokedInvitation_DoesNotGrantAccess()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.Service.InviteAsync(
            fixture.Tenant.Id,
            fixture.Actor.Id,
            new SynqLienInviteCommand(
                "revoked-liens-user@example.com", "Revoked", "User", null,
                [ProductRoleCodes.SynqLienBuyer]));

        var invitation = await fixture.Db.UserInvitations.SingleAsync(i => i.Id == result.InvitationId);
        invitation.Revoke();
        await fixture.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SynqLienUserManagementException>(() =>
            fixture.Service.ApplyInvitationGrantsAsync(invitation.Id));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("INVITATION_NOT_ACCEPTED", ex.Code);
        Assert.False(await fixture.Db.UserProductAccessRecords.AnyAsync(a => a.UserId == result.UserId));
    }

    [Fact]
    public async Task ListUsers_ProductAdminRoleIsRecheckedAgainstCurrentIdentityState()
    {
        await using var fixture = await Fixture.CreateAsync();
        var product = await fixture.Db.Products.SingleAsync(p => p.Code == AuthorizationProductCodes.SynqLiens);
        var adminRole = await fixture.Db.ProductRoles.SingleAsync(r => r.Code == ProductRoleCodes.SynqLienUserAdmin);
        var permission = Permission.Create(product.Id, PermissionCodes.LienUserRead, "Read SynqLien Users");
        var standardAdmin = User.Create(
            fixture.Tenant.Id, "product-admin@example.com", "hash", "Product", "Admin");
        var assignment = UserRoleAssignment.Create(
            fixture.Tenant.Id,
            standardAdmin.Id,
            ProductRoleCodes.SynqLienUserAdmin,
            AuthorizationProductCodes.SynqLiens);

        fixture.Db.Permissions.Add(permission);
        fixture.Db.RolePermissionMappings.Add(RolePermissionMapping.Create(adminRole.Id, permission.Id));
        fixture.Db.Users.Add(standardAdmin);
        fixture.Db.UserTenants.Add(UserTenant.Create(standardAdmin.Id, fixture.Tenant.Id));
        fixture.Db.UserProductAccessRecords.Add(UserProductAccess.Create(
            fixture.Tenant.Id, standardAdmin.Id, AuthorizationProductCodes.SynqLiens));
        fixture.Db.UserRoleAssignments.Add(assignment);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.ListUsersAsync(
            fixture.Tenant.Id, standardAdmin.Id, null, null, null, 1, 25);

        assignment.Remove(fixture.Actor.Id);
        await fixture.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SynqLienUserManagementException>(() =>
            fixture.Service.ListUsersAsync(
                fixture.Tenant.Id, standardAdmin.Id, null, null, null, 1, 25));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal("LIENS_USER_MANAGEMENT_FORBIDDEN", ex.Code);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(
            IdentityDbContext db,
            Tenant tenant,
            User actor,
            SynqLienUserManagementService service)
        {
            Db = db;
            Tenant = tenant;
            Actor = actor;
            Service = service;
        }

        public IdentityDbContext Db { get; }
        public Tenant Tenant { get; }
        public User Actor { get; }
        public SynqLienUserManagementService Service { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase("synqlien-user-management-" + Guid.CreateVersion7())
                .Options;
            var db = new IdentityDbContext(options);

            var tenant = Tenant.Create("Liens Tenant", "liens-" + Guid.CreateVersion7().ToString("N"));
            var product = Product.Create("SynqLien", AuthorizationProductCodes.SynqLiens);
            var actor = User.Create(tenant.Id, "admin@example.com", "hash", "Liens", "Admin");
            var tenantAdminRole = Role.Create(
                tenant.Id, Roles.TenantAdmin, isSystemRole: true, scope: RoleScopes.Tenant);

            db.Tenants.Add(tenant);
            db.Products.Add(product);
            db.Set<TenantProduct>().Add(TenantProduct.Create(tenant.Id, product.Id));
            db.Users.Add(actor);
            db.UserTenants.Add(UserTenant.Create(actor.Id, tenant.Id));
            db.Roles.Add(tenantAdminRole);
            db.ScopedRoleAssignments.Add(ScopedRoleAssignment.Create(
                actor.Id, tenantAdminRole.Id, ScopedRoleAssignment.ScopeTypes.Global, tenantId: tenant.Id));

            foreach (var (code, name) in new[]
            {
                (ProductRoleCodes.SynqLienSeller, "Seller"),
                (ProductRoleCodes.SynqLienBuyer, "Buyer"),
                (ProductRoleCodes.SynqLienHolder, "Holder"),
                (ProductRoleCodes.SynqLienUserAdmin, "User Administrator"),
            })
                db.ProductRoles.Add(ProductRole.Create(product.Id, code, name));

            await db.SaveChangesAsync();

            var service = new SynqLienUserManagementService(
                db, new StubPasswordHasher(), new StubAuditPublisher());
            return new Fixture(db, tenant, actor, service);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;
        public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
    }

    private sealed class StubAuditPublisher : IAuditPublisher
    {
        public void Publish(
            string eventType, string action, string description, Guid? tenantId,
            Guid? actorUserId = null, string? entityType = null, string? entityId = null,
            string? before = null, string? after = null, string? metadata = null,
            string? correlationId = null) { }
    }
}
