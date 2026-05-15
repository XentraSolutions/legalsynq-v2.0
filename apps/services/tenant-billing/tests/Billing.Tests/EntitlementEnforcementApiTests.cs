using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Billing.Api.Contracts;
using Billing.Api.Security;
using Billing.Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Billing.Infrastructure.Data;
using Xunit;

namespace Billing.Tests;

/// <summary>
/// TB-ENF-01 — integration tests with
/// <c>Billing:EntitlementEnforcement:Enabled=true</c>. Verifies that the
/// <see cref="RequireTenantBillingAccessAttribute"/> short-circuits attributed
/// write endpoints with HTTP 403 ProblemDetails based on the tenant's
/// profile + entitlement snapshot, while leaving reads + admin endpoints
/// reachable.
/// </summary>
public class EntitlementEnforcementApiTests
    : IClassFixture<EntitlementEnforcementApiTests.EnforcementFactory>
{
    private readonly EnforcementFactory _factory;
    public EntitlementEnforcementApiTests(EnforcementFactory f) => _factory = f;

    public sealed class EnforcementFactory : WebApplicationFactory<Program>
    {
        public const string TestInternalToken = "test-internal-token";
        private readonly string _databaseName = $"billing-enf-tests-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = string.Empty,
                    ["ConnectionStrings:Billing"] = string.Empty,
                    [RequireInternalTokenMiddleware.ConfigurationKey] = TestInternalToken,
                    [PlatformTemplatesGuardAttribute.ConfigurationKey] = "true",

                    // TB-ENF-01 — opt the test process into enforcement.
                    ["Billing:EntitlementEnforcement:Enabled"] = "true",
                });
            });
            builder.ConfigureServices(services =>
            {
                var d = services.SingleOrDefault(
                    x => x.ServiceType == typeof(DbContextOptions<BillingDbContext>));
                if (d is not null) services.Remove(d);
                services.AddDbContext<BillingDbContext>(o => o.UseInMemoryDatabase(_databaseName));
            });
        }

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            client.DefaultRequestHeaders.Add(
                RequireInternalTokenMiddleware.HeaderName, TestInternalToken);
        }

        public HttpClient CreateClientForTenant(Guid tenantId)
        {
            var c = CreateClient();
            c.DefaultRequestHeaders.Add(
                Billing.Api.Tenancy.TenantResolutionMiddleware.HeaderName,
                tenantId.ToString());
            return c;
        }
    }

    private static async Task SeedAsync(
        HttpClient c, Guid billingAccount,
        string status, string rec)
    {
        var prof = await c.PostAsJsonAsync("/api/tenant-billing/profiles",
            new CreateTenantBillingProfileRequest
            {
                BillingAccountId = billingAccount,
                Mode = TenantBillingMode.InternalOnly,
            });
        prof.StatusCode.Should().Be(HttpStatusCode.Created);
        var pr = (await prof.Content.ReadFromJsonAsync<TenantBillingProfileResponse>())!;
        (await c.PostAsync($"/api/tenant-billing/profiles/{pr.Id}/activate", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var snap = await c.PostAsJsonAsync("/api/tenant-billing/entitlements/apply",
            new ApplyEntitlementSnapshotRequestDto
            {
                BillingAccountId = billingAccount,
                SourceSystem = "commerce",
                EntitlementStatus = status,
                AccessRecommendation = rec,
            });
        snap.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static HttpRequestMessage CreateCustomerReq()
        => new(HttpMethod.Post, "/api/customers")
        {
            Content = JsonContent.Create(new CreateCustomerRequest
            {
                Name  = "Acme " + Guid.NewGuid().ToString("N")[..6],
                Email = $"acme+{Guid.NewGuid():N}@example.com",
            }),
        };

    [Fact]
    public async Task CustomerWrite_blocked_when_no_profile_exists_and_enforcement_on()
    {
        var c = _factory.CreateClientForTenant(Guid.NewGuid());

        // No profile → UnknownMode default = ReadOnly → CustomerWrite blocked.
        var resp = await c.SendAsync(CreateCustomerReq());
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(403);
        doc.RootElement.GetProperty("category").GetString().Should().Be("CustomerWrite");
        doc.RootElement.TryGetProperty("accessRecommendation", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Read_endpoint_passes_even_when_profile_missing()
    {
        var c = _factory.CreateClientForTenant(Guid.NewGuid());

        // Customer LIST has no [RequireTenantBillingAccess] attribute, so
        // it must pass regardless of the tenant's entitlement state.
        var resp = await c.GetAsync("/api/customers");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Allow_snapshot_lets_CustomerWrite_through()
    {
        var t = Guid.NewGuid(); var a = Guid.NewGuid();
        var c = _factory.CreateClientForTenant(t);
        await SeedAsync(c, a,
            TenantBillingEntitlementStatus.Enabled,
            TenantBillingAccessRecommendation.Allow);

        var resp = await c.SendAsync(CreateCustomerReq());
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Block_snapshot_blocks_CustomerWrite_with_403_problem_details()
    {
        var t = Guid.NewGuid(); var a = Guid.NewGuid();
        var c = _factory.CreateClientForTenant(t);
        await SeedAsync(c, a,
            TenantBillingEntitlementStatus.Enabled,
            TenantBillingAccessRecommendation.Block);

        var resp = await c.SendAsync(CreateCustomerReq());
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("accessRecommendation").GetString()
            .Should().Be(TenantBillingAccessRecommendation.Block);
    }

    [Fact]
    public async Task ReadOnly_snapshot_blocks_CustomerWrite_but_allows_Read()
    {
        var t = Guid.NewGuid(); var a = Guid.NewGuid();
        var c = _factory.CreateClientForTenant(t);
        await SeedAsync(c, a,
            TenantBillingEntitlementStatus.Enabled,
            TenantBillingAccessRecommendation.ReadOnly);

        var write = await c.SendAsync(CreateCustomerReq());
        write.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var read = await c.GetAsync("/api/customers");
        read.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProfileAdmin_endpoint_remains_reachable_even_when_blocked()
    {
        var t = Guid.NewGuid(); var a = Guid.NewGuid();
        var c = _factory.CreateClientForTenant(t);
        await SeedAsync(c, a,
            TenantBillingEntitlementStatus.Disabled,
            TenantBillingAccessRecommendation.Block);

        // Profile lifecycle endpoints are intentionally NOT attributed, so
        // an operator can always recover from a Block state by editing the
        // profile / re-applying a snapshot.
        var resp = await c.GetAsync("/api/tenant-billing/profiles");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
