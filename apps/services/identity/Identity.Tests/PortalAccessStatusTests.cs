using System.Net;
using System.Net.Http.Json;
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

public class PortalAccessStatusTests
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

                var dbName = "portal-access-status-test-" + Guid.CreateVersion7();
                services.AddDbContext<IdentityDbContext>(opts => opts.UseInMemoryDatabase(dbName));
            });
        });

    [Fact]
    public async Task PortalAccess_ReturnsExistingUserOtherTenant_WhenEmailExistsOutsideTargetTenant()
    {
        using var factory = BuildFactory();
        var targetTenantId = await SeedExistingCrossTenantUserAsync(factory, "lawyer@example.com");
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/internal/users/portal-access?tenantId={targetTenantId}&email=lawyer@example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PortalAccessStatusResponse>();
        Assert.NotNull(body);
        Assert.Equal("existing_user_other_tenant", body.Status);
    }

    [Fact]
    public async Task PortalAccess_ReturnsActiveInTenant_WhenEmailAlreadyHasTenantLawFirmAccess()
    {
        using var factory = BuildFactory();
        var targetTenantId = await SeedActiveTenantReferrerAsync(factory, "active@example.com");
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/internal/users/portal-access?tenantId={targetTenantId}&email=active@example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PortalAccessStatusResponse>();
        Assert.NotNull(body);
        Assert.Equal("active_in_tenant", body.Status);
    }

    [Fact]
    public async Task PortalAccess_ReturnsNoAccount_WhenEmailDoesNotExist()
    {
        using var factory = BuildFactory();
        var tenantId = await SeedTenantOnlyAsync(factory);
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/internal/users/portal-access?tenantId={tenantId}&email=missing@example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PortalAccessStatusResponse>();
        Assert.NotNull(body);
        Assert.Equal("no_account", body.Status);
    }

    [Fact]
    public async Task SelfRegister_LinksExistingUserByNormalizedEmail_WithoutCreatingDuplicateUser()
    {
        using var factory = BuildFactory();
        var password = "ExistingPassword123!";
        Guid targetOrgId;
        Guid targetTenantId;
        Guid existingUserId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var homeTenant = Tenant.Create("Home Tenant", $"home-{Guid.CreateVersion7():N}");
            var targetTenant = Tenant.Create("Target Tenant", $"target-{Guid.CreateVersion7():N}");
            db.Tenants.AddRange(homeTenant, targetTenant);

            var homeOrg = Organization.Create(homeTenant.Id, "Home Firm", OrgType.LawFirm, displayName: "Home Firm");
            var targetOrg = Organization.Create(targetTenant.Id, "Target Firm", OrgType.LawFirm, displayName: "Target Firm");
            db.Organizations.AddRange(homeOrg, targetOrg);

            var existingUser = User.Create(
                homeTenant.Id,
                "legacy.referrer@example.com",
                passwordHasher.Hash(password),
                "Legacy",
                "Referrer");
            SetEmailForTest(existingUser, "Legacy.Referrer@Example.com");

            db.Users.Add(existingUser);
            db.UserTenants.Add(UserTenant.Create(existingUser.Id, homeTenant.Id));

            var homeMembership = UserOrganizationMembership.Create(existingUser.Id, homeOrg.Id, MemberRole.Member);
            homeMembership.SetPrimary();
            db.UserOrganizationMemberships.Add(homeMembership);

            await db.SaveChangesAsync();

            targetOrgId = targetOrg.Id;
            targetTenantId = targetTenant.Id;
            existingUserId = existingUser.Id;
        }

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/organizations/{targetOrgId}/self-register",
            new
            {
                email = "legacy.referrer@example.com",
                password,
                firstName = "Legacy",
                lastName = "Referrer"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SelfRegisterResponse>();
        Assert.NotNull(body);
        Assert.Equal(existingUserId, body.UserId);
        Assert.False(body.IsNew);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var normalizedUserCount = await verifyDb.Users
            .CountAsync(u => u.Email.Trim().ToLower() == "legacy.referrer@example.com");
        Assert.Equal(1, normalizedUserCount);

        Assert.True(await verifyDb.UserTenants.AnyAsync(ut =>
            ut.UserId == existingUserId && ut.TenantId == targetTenantId && ut.IsActive));
        Assert.True(await verifyDb.UserOrganizationMemberships.AnyAsync(m =>
            m.UserId == existingUserId && m.OrganizationId == targetOrgId && m.IsActive));
    }

    private static async Task<Guid> SeedExistingCrossTenantUserAsync(WebApplicationFactory<Program> factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var homeTenant = Tenant.Create("Home Tenant", $"home-{Guid.CreateVersion7():N}");
        var targetTenant = Tenant.Create("Target Tenant", $"target-{Guid.CreateVersion7():N}");
        db.Tenants.AddRange(homeTenant, targetTenant);

        var homeOrg = Organization.Create(homeTenant.Id, "Home Firm", OrgType.LawFirm, displayName: "Home Firm");
        var targetOrg = Organization.Create(targetTenant.Id, "Target Firm", OrgType.LawFirm, displayName: "Target Firm");
        db.Organizations.AddRange(homeOrg, targetOrg);

        var user = User.Create(homeTenant.Id, email, "password-hash", "Lawyer", "User");
        db.Users.Add(user);
        db.UserTenants.Add(UserTenant.Create(user.Id, homeTenant.Id));

        var homeMembership = UserOrganizationMembership.Create(user.Id, homeOrg.Id, MemberRole.Member);
        homeMembership.SetPrimary();
        db.UserOrganizationMemberships.Add(homeMembership);

        await db.SaveChangesAsync();
        return targetTenant.Id;
    }

    private static async Task<Guid> SeedActiveTenantReferrerAsync(WebApplicationFactory<Program> factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var tenant = Tenant.Create("Active Tenant", $"active-{Guid.CreateVersion7():N}");
        db.Tenants.Add(tenant);

        var org = Organization.Create(tenant.Id, "Active Firm", OrgType.LawFirm, displayName: "Active Firm");
        db.Organizations.Add(org);

        var user = User.Create(tenant.Id, email, "password-hash", "Active", "User");
        db.Users.Add(user);
        db.UserTenants.Add(UserTenant.Create(user.Id, tenant.Id));

        var membership = UserOrganizationMembership.Create(user.Id, org.Id, MemberRole.Member);
        membership.SetPrimary();
        db.UserOrganizationMemberships.Add(membership);

        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private static async Task<Guid> SeedTenantOnlyAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var tenant = Tenant.Create("Empty Tenant", $"empty-{Guid.CreateVersion7():N}");
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private sealed record PortalAccessStatusResponse(string? Status);

    private sealed record SelfRegisterResponse(Guid UserId, bool IsNew);

    private static void SetEmailForTest(User user, string email)
    {
        typeof(User)
            .GetProperty(nameof(User.Email))!
            .SetValue(user, email);
    }
}
