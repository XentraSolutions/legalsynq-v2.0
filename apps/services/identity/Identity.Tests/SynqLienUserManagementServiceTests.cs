using BuildingBlocks.Authorization;
using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ProductCodes = BuildingBlocks.Authorization.ProductCodes;

namespace Identity.Tests;

public sealed class SynqLienUserManagementServiceTests
{
    [Fact]
    public async Task Invite_NewUser_PersistsOnlyPendingGrant()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.Service.InviteAsync(fixture.Scope, new(
            "new.user@example.com", "New", "User", "Operations", "Reviewer", fixture.ViewRole.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("INVITED", result.Value!.Outcome);
        var userId = result.Value.UserId;
        Assert.False((await fixture.Db.Users.SingleAsync(x => x.Id == userId)).IsActive);
        Assert.False(await fixture.Db.UserOrganizationMemberships.AnyAsync(x => x.UserId == userId));
        Assert.False(await fixture.Db.UserProductAccessRecords.AnyAsync(x => x.UserId == userId));
        Assert.False(await fixture.Db.SynqLienUserAccessRoleAssignments.AnyAsync(x => x.UserId == userId));
        var invitation = await fixture.Db.UserInvitations.SingleAsync(x => x.UserId == userId);
        Assert.Equal(fixture.ViewRole.Id, invitation.PendingAccessRoleId);
        Assert.Equal("Operations", invitation.PendingDepartment);
    }

    [Fact]
    public async Task Invite_WhenInvitationIsAlreadyPending_ReturnsStableConflict()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = new SynqLienInviteRequest(
            "pending@example.com", "Pending", "User", null, null, fixture.ViewRole.Id);
        Assert.True((await fixture.Service.InviteAsync(fixture.Scope, request, default)).IsSuccess);

        var duplicate = await fixture.Service.InviteAsync(fixture.Scope, request, default);

        Assert.Equal(SynqLienUserManagementError.Conflict, duplicate.Error);
        Assert.Equal("synqlien.invitation_pending", duplicate.Code);
    }

    [Fact]
    public async Task Invite_ExistingActiveOrganizationMember_GrantsImmediateScopedAccess()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.Service.InviteAsync(fixture.Scope, new(
            fixture.User.Email, fixture.User.FirstName, fixture.User.LastName, "Legal", "Analyst", fixture.ViewRole.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("ACCESS_GRANTED", result.Value!.Outcome);
        Assert.Empty(await fixture.Db.UserInvitations.Where(x => x.UserId == fixture.User.Id).ToListAsync());
        Assert.Equal(fixture.ViewRole.Id, await fixture.Db.SynqLienUserAccessRoleAssignments
            .Where(x => x.UserId == fixture.User.Id && x.IsActive).Select(x => x.RoleId).SingleAsync());
        Assert.True(await fixture.Db.UserRoleAssignments.AnyAsync(x => x.UserId == fixture.User.Id &&
            x.RoleCode == ProductRoleCodes.SynqLienSeller && x.AssignmentStatus == AssignmentStatus.Active));
    }

    [Fact]
    public async Task Get_UserFromAnotherOrganization_ReturnsNotFound()
    {
        await using var fixture = await Fixture.CreateAsync();
        var otherOrganization = Organization.Create(fixture.Tenant.Id, "Other", Identity.Domain.OrgType.LawFirm);
        var otherUser = User.Create(fixture.Tenant.Id, "other@example.com", "hash", "Other", "User");
        fixture.Db.AddRange(otherOrganization, otherUser,
            UserOrganizationMembership.Create(otherUser.Id, otherOrganization.Id, MemberRole.Member));
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.GetAsync(fixture.Scope, otherUser.Id, default);
        Assert.Equal(SynqLienUserManagementError.NotFound, result.Error);
    }

    [Fact]
    public async Task Deactivate_RevokesOnlyCurrentOrganizationSynqLienAccess()
    {
        await using var fixture = await Fixture.CreateAsync();
        var otherTenant = Tenant.Create("Other tenant", "other-tenant");
        fixture.Db.AddRange(otherTenant,
            UserProductAccess.Create(fixture.Tenant.Id, fixture.User.Id, ProductCodes.SynqCareConnect, fixture.Organization.Id),
            UserProductAccess.Create(otherTenant.Id, fixture.User.Id, ProductCodes.SynqLiens));
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.SetProductAccessAsync(fixture.Scope, fixture.User.Id, false, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(AccessStatus.Revoked, await fixture.Db.UserProductAccessRecords
            .Where(x => x.UserId == fixture.User.Id && x.TenantId == fixture.Tenant.Id && x.OrganizationId == fixture.Organization.Id && x.ProductCode == ProductCodes.SynqLiens)
            .Select(x => x.AccessStatus).SingleAsync());
        Assert.Equal(2, await fixture.Db.UserProductAccessRecords.CountAsync(x => x.UserId == fixture.User.Id && x.AccessStatus == AccessStatus.Granted));
    }

    [Fact]
    public async Task ReplaceRole_PreservesCommercialPersona()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.AddRange(
            UserRoleAssignment.Create(fixture.Tenant.Id, fixture.User.Id, ProductRoleCodes.SynqLienSeller, ProductCodes.SynqLiens, fixture.Organization.Id),
            SynqLienUserAccessRoleAssignment.Create(fixture.Tenant.Id, fixture.Organization.Id, fixture.User.Id, fixture.AdminRole.Id, fixture.Actor.Id));
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.ReplaceRoleAsync(fixture.Scope, fixture.User.Id, new(fixture.ViewRole.Id), default);

        Assert.True(result.IsSuccess);
        Assert.True(await fixture.Db.UserRoleAssignments.AnyAsync(x => x.UserId == fixture.User.Id && x.RoleCode == ProductRoleCodes.SynqLienSeller));
        Assert.Equal(fixture.ViewRole.Id, await fixture.Db.SynqLienUserAccessRoleAssignments
            .Where(x => x.UserId == fixture.User.Id && x.IsActive).Select(x => x.RoleId).SingleAsync());
    }

    [Fact]
    public async Task DeleteRole_InUse_ReturnsConflict()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.SynqLienUserAccessRoleAssignments.Add(SynqLienUserAccessRoleAssignment.Create(
            fixture.Tenant.Id, fixture.Organization.Id, fixture.User.Id, fixture.ViewRole.Id, fixture.Actor.Id));
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.DeleteRoleAsync(fixture.Scope, fixture.ViewRole.Id, default);
        Assert.Equal(SynqLienUserManagementError.Conflict, result.Error);
        Assert.Equal("synqlien.role_in_use", result.Code);
    }

    [Fact]
    public async Task List_SellerWithoutManagementRole_ReturnsForbidden()
    {
        await using var fixture = await Fixture.CreateAsync();
        var seller = User.Create(fixture.Tenant.Id, "seller@example.com", "hash", "Seller", "Only");
        fixture.Db.AddRange(seller,
            UserTenant.Create(seller.Id, fixture.Tenant.Id),
            UserOrganizationMembership.Create(seller.Id, fixture.Organization.Id, MemberRole.Member),
            UserProductAccess.Create(fixture.Tenant.Id, seller.Id, ProductCodes.SynqLiens, fixture.Organization.Id),
            UserRoleAssignment.Create(fixture.Tenant.Id, seller.Id, ProductRoleCodes.SynqLienSeller,
                ProductCodes.SynqLiens, fixture.Organization.Id));
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.ListAsync(
            new(fixture.Tenant.Id, fixture.Organization.Id, seller.Id, "test"),
            new(null, null, null, null, 1, 25, null), default);

        Assert.Equal(SynqLienUserManagementError.Forbidden, result.Error);
    }

    [Fact]
    public async Task CreateRole_CannotDelegatePermissionActorDoesNotHold()
    {
        await using var fixture = await Fixture.CreateAsync();
        var manager = User.Create(fixture.Tenant.Id, "manager@example.com", "hash", "Role", "Manager");
        var lienRead = Permission.Create(
            (await fixture.Db.Products.SingleAsync(x => x.Code == ProductCodes.SynqLiens)).Id,
            PermissionCodes.LienRead, "Read liens");
        var roleManager = SynqLienAccessRole.Create(
            fixture.Tenant.Id, fixture.Organization.Id, "Role Manager", null, false, fixture.Actor.Id);
        var rolesManage = await fixture.Db.Permissions.SingleAsync(x => x.Code == PermissionCodes.LienRolesManage);
        fixture.Db.AddRange(manager, lienRead, roleManager,
            UserTenant.Create(manager.Id, fixture.Tenant.Id),
            UserOrganizationMembership.Create(manager.Id, fixture.Organization.Id, MemberRole.Member),
            UserProductAccess.Create(fixture.Tenant.Id, manager.Id, ProductCodes.SynqLiens, fixture.Organization.Id),
            SynqLienAccessRolePermission.Create(roleManager.Id, rolesManage.Id),
            SynqLienUserAccessRoleAssignment.Create(
                fixture.Tenant.Id, fixture.Organization.Id, manager.Id, roleManager.Id, fixture.Actor.Id));
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.CreateRoleAsync(
            new(fixture.Tenant.Id, fixture.Organization.Id, manager.Id, "test"),
            new("Escalated", null, [PermissionCodes.LienRolesManage, PermissionCodes.LienRead]), default);

        Assert.Equal(SynqLienUserManagementError.Forbidden, result.Error);
    }

    [Fact]
    public async Task Invite_CannotAssignRoleWithPermissionsActorDoesNotHold()
    {
        await using var fixture = await Fixture.CreateAsync();
        var manager = User.Create(fixture.Tenant.Id, "invite.manager@example.com", "hash", "Invite", "Manager");
        var managerRole = SynqLienAccessRole.Create(
            fixture.Tenant.Id, fixture.Organization.Id, "Invitation Manager", null, false, fixture.Actor.Id);
        var invitationsManage = await fixture.Db.Permissions.SingleAsync(x => x.Code == PermissionCodes.LienInvitationsManage);
        fixture.Db.AddRange(manager, managerRole,
            UserTenant.Create(manager.Id, fixture.Tenant.Id),
            UserOrganizationMembership.Create(manager.Id, fixture.Organization.Id, MemberRole.Member),
            UserProductAccess.Create(fixture.Tenant.Id, manager.Id, ProductCodes.SynqLiens, fixture.Organization.Id),
            SynqLienAccessRolePermission.Create(managerRole.Id, invitationsManage.Id),
            SynqLienUserAccessRoleAssignment.Create(
                fixture.Tenant.Id, fixture.Organization.Id, manager.Id, managerRole.Id, fixture.Actor.Id));
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.InviteAsync(
            new(fixture.Tenant.Id, fixture.Organization.Id, manager.Id, "test"),
            new("escalated@example.com", "Escalated", "User", null, null, fixture.AdminRole.Id), default);

        Assert.Equal(SynqLienUserManagementError.Forbidden, result.Error);
        Assert.False(await fixture.Db.Users.AnyAsync(x => x.Email == "escalated@example.com"));
    }

    [Fact]
    public async Task ReplaceRole_CannotAssignRoleWithPermissionsActorDoesNotHold()
    {
        await using var fixture = await Fixture.CreateAsync();
        var manager = User.Create(fixture.Tenant.Id, "user.manager@example.com", "hash", "User", "Manager");
        var managerRole = SynqLienAccessRole.Create(
            fixture.Tenant.Id, fixture.Organization.Id, "User Manager", null, false, fixture.Actor.Id);
        var usersManage = await fixture.Db.Permissions.SingleAsync(x => x.Code == PermissionCodes.LienUsersManage);
        fixture.Db.AddRange(manager, managerRole,
            UserTenant.Create(manager.Id, fixture.Tenant.Id),
            UserOrganizationMembership.Create(manager.Id, fixture.Organization.Id, MemberRole.Member),
            UserProductAccess.Create(fixture.Tenant.Id, manager.Id, ProductCodes.SynqLiens, fixture.Organization.Id),
            SynqLienAccessRolePermission.Create(managerRole.Id, usersManage.Id),
            SynqLienUserAccessRoleAssignment.Create(
                fixture.Tenant.Id, fixture.Organization.Id, manager.Id, managerRole.Id, fixture.Actor.Id));
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.ReplaceRoleAsync(
            new(fixture.Tenant.Id, fixture.Organization.Id, manager.Id, "test"),
            fixture.User.Id, new(fixture.AdminRole.Id), default);

        Assert.Equal(SynqLienUserManagementError.Forbidden, result.Error);
        Assert.False(await fixture.Db.SynqLienUserAccessRoleAssignments.AnyAsync(x =>
            x.UserId == fixture.User.Id && x.RoleId == fixture.AdminRole.Id && x.IsActive));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(IdentityDbContext db, Tenant tenant, Organization organization, User actor, User user,
            SynqLienAccessRole adminRole, SynqLienAccessRole viewRole, SynqLienUserManagementService service)
        {
            Db = db; Tenant = tenant; Organization = organization; Actor = actor; User = user;
            AdminRole = adminRole; ViewRole = viewRole; Service = service;
            Scope = new(tenant.Id, organization.Id, actor.Id, "test-correlation");
        }

        public IdentityDbContext Db { get; }
        public Tenant Tenant { get; }
        public Organization Organization { get; }
        public User Actor { get; }
        public User User { get; }
        public SynqLienAccessRole AdminRole { get; }
        public SynqLienAccessRole ViewRole { get; }
        public SynqLienManagementScope Scope { get; }
        public SynqLienUserManagementService Service { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase($"synqlien-user-management-{Guid.CreateVersion7()}").Options);
            var tenant = Tenant.Create("Tenant", "tenant");
            var actor = User.Create(tenant.Id, "owner@example.com", "hash", "Owner", "User");
            var user = User.Create(tenant.Id, "user@example.com", "hash", "User", "One");
            var organization = Organization.Create(tenant.Id, "Firm", Identity.Domain.OrgType.LawFirm);
            organization.SetOwner(actor.Id);
            var product = Product.Create("SynqLien", ProductCodes.SynqLiens);
            var usersView = Permission.Create(product.Id, PermissionCodes.LienUsersView, "View users");
            var usersManage = Permission.Create(product.Id, PermissionCodes.LienUsersManage, "Manage users");
            var invitationsManage = Permission.Create(product.Id, PermissionCodes.LienInvitationsManage, "Manage invitations");
            var rolesView = Permission.Create(product.Id, PermissionCodes.LienRolesView, "View roles");
            var rolesManage = Permission.Create(product.Id, PermissionCodes.LienRolesManage, "Manage roles");
            var admin = SynqLienAccessRole.Create(tenant.Id, organization.Id, "Administrator", null, true, actor.Id);
            var view = SynqLienAccessRole.Create(tenant.Id, organization.Id, "Custom Reviewer", null, false, actor.Id);

            db.AddRange(tenant, actor, user, organization, product, usersView, usersManage, invitationsManage, rolesView, rolesManage, admin, view,
                TenantProduct.Create(tenant.Id, product.Id),
                UserTenant.Create(actor.Id, tenant.Id), UserTenant.Create(user.Id, tenant.Id),
                UserOrganizationMembership.Create(actor.Id, organization.Id, MemberRole.Admin),
                UserOrganizationMembership.Create(user.Id, organization.Id, MemberRole.Member),
                UserProductAccess.Create(tenant.Id, actor.Id, ProductCodes.SynqLiens, organization.Id),
                UserProductAccess.Create(tenant.Id, user.Id, ProductCodes.SynqLiens, organization.Id));
            db.AddRange(new[] { usersView, usersManage, invitationsManage, rolesView, rolesManage }
                .Select(permission => SynqLienAccessRolePermission.Create(admin.Id, permission.Id)));
            db.Add(SynqLienUserAccessRoleAssignment.Create(tenant.Id, organization.Id, actor.Id, admin.Id, actor.Id));
            await db.SaveChangesAsync();

            return new Fixture(db, tenant, organization, actor, user, admin, view,
                new SynqLienUserManagementService(db, new StubPasswordHasher(), new StubAuditPublisher(), new NoOpNotificationsCacheClient()));
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";
        public bool Verify(string password, string hash) => hash == $"hashed:{password}";
    }

    private sealed class StubAuditPublisher : IAuditPublisher
    {
        public void Publish(string eventType, string action, string description, Guid? tenantId, Guid? actorUserId = null,
            string? entityType = null, string? entityId = null, string? before = null, string? after = null,
            string? metadata = null, string? correlationId = null) { }
    }
}
