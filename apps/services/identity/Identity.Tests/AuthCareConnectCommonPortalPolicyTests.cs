using Identity.Application.DTOs;
using Identity.Application.Exceptions;
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

public class AuthCareConnectCommonPortalPolicyTests
{
    private const string PortalRestrictionMessage =
        "This account is not eligible to access the CareConnect portal.";

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

                var dbName = "auth-cc-common-portal-policy-test-" + Guid.CreateVersion7();
                services.AddDbContext<IdentityDbContext>(opts => opts.UseInMemoryDatabase(dbName));
            });
        });

    [Fact]
    public async Task Login_ResolveByEmail_AllowsReferrerOnly()
    {
        using var factory = BuildFactory();
        var seeded = await SeedCommonPortalUserAsync(factory, ["CARECONNECT_REFERRER"], systemRoles: []);

        using var scope = factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var response = await authService.LoginAsync(new LoginRequest(
            Email: seeded.Email,
            Password: seeded.Password,
            ResolveByEmail: true));

        Assert.Equal(seeded.TenantId, response.User.TenantId);
        Assert.Contains("SYNQ_CARECONNECT:CARECONNECT_REFERRER", response.User.ProductRoles ?? []);
    }

    [Fact]
    public async Task Login_ResolveByEmail_AllowsReceiverOnly()
    {
        using var factory = BuildFactory();
        var seeded = await SeedCommonPortalUserAsync(factory, ["CARECONNECT_RECEIVER"], systemRoles: []);

        using var scope = factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var response = await authService.LoginAsync(new LoginRequest(
            Email: seeded.Email,
            Password: seeded.Password,
            ResolveByEmail: true));

        Assert.Equal(seeded.TenantId, response.User.TenantId);
        Assert.Contains("SYNQ_CARECONNECT:CARECONNECT_RECEIVER", response.User.ProductRoles ?? []);
    }

    [Fact]
    public async Task Login_ResolveByEmail_AllowsReceiverAndReferrerOnly()
    {
        using var factory = BuildFactory();
        var seeded = await SeedCommonPortalUserAsync(
            factory,
            ["CARECONNECT_REFERRER", "CARECONNECT_RECEIVER"],
            systemRoles: []);

        using var scope = factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var response = await authService.LoginAsync(new LoginRequest(
            Email: seeded.Email,
            Password: seeded.Password,
            ResolveByEmail: true));

        Assert.Equal(seeded.TenantId, response.User.TenantId);
        Assert.Contains("SYNQ_CARECONNECT:CARECONNECT_REFERRER", response.User.ProductRoles ?? []);
        Assert.Contains("SYNQ_CARECONNECT:CARECONNECT_RECEIVER", response.User.ProductRoles ?? []);
    }

    [Fact]
    public async Task Login_ResolveByEmail_DeniesNetworkManagerPlusReferrer()
    {
        using var factory = BuildFactory();
        var seeded = await SeedCommonPortalUserAsync(
            factory,
            productRoles: ["CARECONNECT_REFERRER", "CARECONNECT_NETWORK_MANAGER"],
            systemRoles: []);

        using var scope = factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var ex = await Assert.ThrowsAsync<CareConnectPortalRoleRestrictedException>(() =>
            authService.LoginAsync(new LoginRequest(
                Email: seeded.Email,
                Password: seeded.Password,
                ResolveByEmail: true)));

        Assert.Equal(PortalRestrictionMessage, ex.Message);
    }

    [Theory]
    [InlineData("TenantAdmin")]
    [InlineData("PlatformAdmin")]
    public async Task Login_ResolveByEmail_DeniesSystemRoles(string systemRole)
    {
        using var factory = BuildFactory();
        var seeded = await SeedCommonPortalUserAsync(
            factory,
            productRoles: ["CARECONNECT_RECEIVER"],
            systemRoles: [systemRole]);

        using var scope = factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var ex = await Assert.ThrowsAsync<CareConnectPortalRoleRestrictedException>(() =>
            authService.LoginAsync(new LoginRequest(
                Email: seeded.Email,
                Password: seeded.Password,
                ResolveByEmail: true)));

        Assert.Equal(PortalRestrictionMessage, ex.Message);
    }

    [Fact]
    public async Task Login_ResolveByEmail_DeniesWhenNoCareConnectRole()
    {
        using var factory = BuildFactory();
        var seeded = await SeedCommonPortalUserAsync(factory, productRoles: [], systemRoles: []);

        using var scope = factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authService.LoginAsync(new LoginRequest(
                Email: seeded.Email,
                Password: seeded.Password,
                ResolveByEmail: true)));
    }

    private static async Task<(Guid TenantId, string Email, string Password)> SeedCommonPortalUserAsync(
        WebApplicationFactory<Program> factory,
        IReadOnlyCollection<string> productRoles,
        IReadOnlyCollection<string> systemRoles)
    {
        const string password = "Password123!";

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var tenant = Tenant.Create("CareConnect Tenant", $"cc-{Guid.CreateVersion7():N}");
        db.Tenants.Add(tenant);

        var orgType = productRoles.Contains("CARECONNECT_RECEIVER", StringComparer.OrdinalIgnoreCase)
            ? OrgType.Provider
            : OrgType.LawFirm;
        var org = Organization.Create(tenant.Id, "Common Portal Org", orgType, displayName: "Common Portal Org");
        db.Organizations.Add(org);

        var ccProduct = Product.Create("SynqCareConnect", BuildingBlocks.Authorization.ProductCodes.SynqCareConnect);
        db.Products.Add(ccProduct);
        db.OrganizationProducts.Add(OrganizationProduct.Create(org.Id, ccProduct.Id));
        db.Set<TenantProduct>().Add(TenantProduct.Create(tenant.Id, ccProduct.Id));

        var distinctProductRoles = productRoles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var roleCode in distinctProductRoles)
        {
            db.ProductRoles.Add(ProductRole.Create(ccProduct.Id, roleCode, roleCode.Replace('_', ' ')));
        }

        var email = $"common-portal-{Guid.CreateVersion7():N}@example.com";
        var user = User.Create(
            tenant.Id,
            email,
            passwordHasher.Hash(password),
            "Common",
            "Portal");
        db.Users.Add(user);
        db.UserTenants.Add(UserTenant.Create(user.Id, tenant.Id));

        var membership = UserOrganizationMembership.Create(user.Id, org.Id, MemberRole.Member);
        membership.SetPrimary();
        db.UserOrganizationMemberships.Add(membership);

        db.UserProductAccessRecords.Add(UserProductAccess.Create(
            tenant.Id,
            user.Id,
            BuildingBlocks.Authorization.ProductCodes.SynqCareConnect));

        foreach (var roleCode in distinctProductRoles)
        {
            db.UserRoleAssignments.Add(UserRoleAssignment.Create(
                tenant.Id,
                user.Id,
                roleCode,
                BuildingBlocks.Authorization.ProductCodes.SynqCareConnect));
        }

        foreach (var systemRole in systemRoles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var role = Role.Create(
                tenant.Id,
                systemRole,
                description: $"{systemRole} test role",
                isSystemRole: true,
                scope: systemRole.Equals("PlatformAdmin", StringComparison.OrdinalIgnoreCase)
                    ? RoleScopes.Platform
                    : RoleScopes.Tenant);
            db.Roles.Add(role);
            db.ScopedRoleAssignments.Add(ScopedRoleAssignment.Create(
                user.Id,
                role.Id,
                ScopedRoleAssignment.ScopeTypes.Global,
                tenantId: tenant.Id));
        }

        await db.SaveChangesAsync();
        return (tenant.Id, email, password);
    }
}
