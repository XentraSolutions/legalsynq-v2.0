using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.Tests;

public class AuthLoginTenantResolutionTests
{
    private static WebApplicationFactory<Program> BuildFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:IdentityDb"] = "Server=localhost;Database=identity_test_placeholder;",
                    ["Jwt:SigningKey"] = "test-only-signing-key-32-chars-padded-ok",
                    ["Jwt:Issuer"] = "test-issuer",
                    ["Jwt:Audience"] = "test-audience",
                    ["TenantService:ProvisioningSecret"] = "",
                });
            });

            builder.ConfigureTestServices(services =>
            {
                var hostedSvcs = services
                    .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
                    .ToList();
                foreach (var s in hostedSvcs) services.Remove(s);

                var dbDescriptors = services
                    .Where(d =>
                        d.ServiceType == typeof(IdentityDbContext) ||
                        d.ServiceType == typeof(DbContextOptions<IdentityDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions))
                    .ToList();
                foreach (var d in dbDescriptors) services.Remove(d);

                var dbName = "auth-login-tenant-resolution-test-" + Guid.CreateVersion7();
                services.AddDbContext<IdentityDbContext>(opts => opts.UseInMemoryDatabase(dbName));
            });
        });

    [Fact]
    public async Task Login_AllowsTenantResolvedByTenantId_WhenIdentityTenantStatusIsPending()
    {
        using var factory = BuildFactory();
        var seeded = await SeedPendingTenantLoginUserAsync(factory);

        using var scope = factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var response = await authService.LoginAsync(new LoginRequest(
            Email: seeded.Email,
            Password: seeded.Password,
            TenantCode: seeded.TenantCode,
            TenantId: seeded.TenantId));

        Assert.Equal(seeded.TenantId, response.User.TenantId);
    }

    [Fact]
    public async Task Login_ResolveByEmail_SelectsTenantWithCareConnectAccess_ForMultiTenantUser()
    {
        using var factory = BuildFactory();
        var seeded = await SeedMultiTenantCareConnectUserAsync(factory);

        using var scope = factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var response = await authService.LoginAsync(new LoginRequest(
            Email: seeded.Email,
            Password: seeded.Password,
            ResolveByEmail: true));

        Assert.Equal(seeded.CareConnectTenantId, response.User.TenantId);
        Assert.Contains("SYNQ_CARECONNECT:CARECONNECT_REFERRER", response.User.ProductRoles ?? []);
    }

    [Theory]
    [InlineData(" user@example.com")]
    [InlineData("user@example.com ")]
    [InlineData("  user@example.com  ")]
    public async Task Login_WithWhitespaceInEmail_ThrowsUnauthorized(string emailWithSpaces)
    {
        using var factory = BuildFactory();
        var seeded = await SeedActiveUserAsync(factory);

        using var scope = factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authService.LoginAsync(new LoginRequest(
                Email: emailWithSpaces,
                Password: seeded.Password,
                TenantCode: seeded.TenantCode)));
    }

    private static async Task<(Guid TenantId, string TenantCode, string Email, string Password)> SeedActiveUserAsync(
        WebApplicationFactory<Program> factory)
    {
        const string password = "Password123!";
        const string email = "user@example.com";

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var tenant = Tenant.Rehydrate(
            Guid.CreateVersion7(),
            $"active-{Guid.CreateVersion7():N}",
            displayName: "Active Tenant",
            status: ProvisioningStatus.Active.ToString());
        db.Tenants.Add(tenant);

        var role = Role.Create(
            tenant.Id,
            "TenantAdmin",
            description: "Test tenant admin role",
            isSystemRole: true,
            scope: RoleScopes.Tenant);
        db.Roles.Add(role);

        var user = User.Create(
            tenant.Id,
            email,
            passwordHasher.Hash(password),
            "Test",
            "User");
        db.Users.Add(user);
        db.UserTenants.Add(UserTenant.Create(user.Id, tenant.Id));
        db.ScopedRoleAssignments.Add(ScopedRoleAssignment.Create(
            user.Id,
            role.Id,
            ScopedRoleAssignment.ScopeTypes.Global,
            tenantId: tenant.Id));

        await db.SaveChangesAsync();
        return (tenant.Id, tenant.Code, email, password);
    }

    private static async Task<(Guid TenantId, string TenantCode, string Email, string Password)> SeedPendingTenantLoginUserAsync(
        WebApplicationFactory<Program> factory)
    {
        const string password = "Password123!";
        const string email = "tenant.user@example.com";

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var tenant = Tenant.Rehydrate(
            Guid.CreateVersion7(),
            $"tenant-{Guid.CreateVersion7():N}",
            displayName: "Pending Tenant",
            status: ProvisioningStatus.Pending.ToString(),
            subdomain: $"tenant-{Guid.CreateVersion7():N}");
        db.Tenants.Add(tenant);

        var role = Role.Create(
            tenant.Id,
            "TenantAdmin",
            description: "Test tenant admin role",
            isSystemRole: true,
            scope: RoleScopes.Tenant);
        db.Roles.Add(role);

        var user = User.Create(
            tenant.Id,
            email,
            passwordHasher.Hash(password),
            "Tenant",
            "User");
        db.Users.Add(user);
        db.UserTenants.Add(UserTenant.Create(user.Id, tenant.Id));
        db.ScopedRoleAssignments.Add(ScopedRoleAssignment.Create(
            user.Id,
            role.Id,
            ScopedRoleAssignment.ScopeTypes.Global,
            tenantId: tenant.Id));

        await db.SaveChangesAsync();
        return (tenant.Id, tenant.Code, email, password);
    }

    private static async Task<(Guid CareConnectTenantId, string Email, string Password)> SeedMultiTenantCareConnectUserAsync(
        WebApplicationFactory<Program> factory)
    {
        const string password = "Password123!";
        const string email = "multi.tenant@example.com";

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var homeTenant = Tenant.Create("Home Tenant", $"home-{Guid.CreateVersion7():N}");
        var ccTenant = Tenant.Create("CareConnect Tenant", $"cc-{Guid.CreateVersion7():N}");
        db.Tenants.AddRange(homeTenant, ccTenant);

        var homeOrg = Organization.Create(homeTenant.Id, "Home Internal", OrgType.Internal, displayName: "Home Internal");
        var ccOrg = Organization.Create(ccTenant.Id, "Referrer Firm", OrgType.LawFirm, displayName: "Referrer Firm");
        db.Organizations.AddRange(homeOrg, ccOrg);

        var ccProduct = Product.Create("SynqCareConnect", BuildingBlocks.Authorization.ProductCodes.SynqCareConnect);
        db.Products.Add(ccProduct);
        db.OrganizationProducts.Add(OrganizationProduct.Create(ccOrg.Id, ccProduct.Id));
        db.Set<TenantProduct>().Add(TenantProduct.Create(ccTenant.Id, ccProduct.Id));

        var ccRole = ProductRole.Create(
            ccProduct.Id,
            "CARECONNECT_REFERRER",
            "CareConnect Referrer");
        db.ProductRoles.Add(ccRole);

        var user = User.Create(
            homeTenant.Id,
            email,
            passwordHasher.Hash(password),
            "Multi",
            "Tenant");
        db.Users.Add(user);
        db.UserTenants.Add(UserTenant.Create(user.Id, homeTenant.Id));
        db.UserTenants.Add(UserTenant.Create(user.Id, ccTenant.Id));

        var homeMembership = UserOrganizationMembership.Create(user.Id, homeOrg.Id, MemberRole.Member);
        homeMembership.SetPrimary();
        var ccMembership = UserOrganizationMembership.Create(user.Id, ccOrg.Id, MemberRole.Member);
        db.UserOrganizationMemberships.AddRange(homeMembership, ccMembership);

        db.UserProductAccessRecords.Add(UserProductAccess.Create(
            ccTenant.Id,
            user.Id,
            BuildingBlocks.Authorization.ProductCodes.SynqCareConnect));
        db.UserRoleAssignments.Add(UserRoleAssignment.Create(
            ccTenant.Id,
            user.Id,
            "CARECONNECT_REFERRER",
            BuildingBlocks.Authorization.ProductCodes.SynqCareConnect));

        await db.SaveChangesAsync();
        return (ccTenant.Id, email, password);
    }
}
