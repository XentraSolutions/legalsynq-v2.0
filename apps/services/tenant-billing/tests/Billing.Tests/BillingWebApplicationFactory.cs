using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Billing.Api.Security;
using Billing.Infrastructure.Data;

namespace Billing.Tests;

/// <summary>
/// WebApplicationFactory for the Billing API. Each factory instance gets
/// its own InMemory database so test classes do not bleed state into each
/// other (the runtime registration uses a static InMemory name).
///
/// MS-BILL-SVC-003 added two cross-cutting requirements that this factory
/// satisfies for every test:
/// <list type="bullet">
///   <item>Every <c>/api/*</c> request needs the
///         <c>X-Internal-Token</c> header. The factory configures a fixed
///         test token via <c>BILLING_INTERNAL_TOKEN</c> equivalent (config
///         key <c>Billing:InternalToken</c>) and pre-populates the header
///         on every client created here.</item>
///   <item>Platform-template endpoints (<c>/api/invoice-templates/platform/*</c>)
///         are gated behind the <c>Billing:EnablePlatformTemplates</c>
///         flag and default to 404. Tests cover those routes too, so this
///         factory turns the flag ON for the test process.</item>
/// </list>
/// </summary>
public sealed class BillingWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestInternalToken = "test-internal-token";

    private readonly string _databaseName = $"billing-tests-{Guid.CreateVersion7():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Force the DependencyInjection fallback path (InMemory) in
                // case the host environment defines a real connection string.
                ["ConnectionStrings:DefaultConnection"] = string.Empty,
                ["ConnectionStrings:Billing"]           = string.Empty,

                // Internal-token middleware (MS-BILL-SVC-003).
                [RequireInternalTokenMiddleware.ConfigurationKey] = TestInternalToken,

                // Enable platform-template endpoints for tests that exercise
                // them. Production default is false; tests opt back in.
                [PlatformTemplatesGuardAttribute.ConfigurationKey] = "true",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the runtime-registered DbContext options with a per-
            // factory InMemory database to isolate tests.
            var optionsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<BillingDbContext>));
            if (optionsDescriptor is not null) services.Remove(optionsDescriptor);

            services.AddDbContext<BillingDbContext>(o => o.UseInMemoryDatabase(_databaseName));
        });
    }

    /// <summary>
    /// Override the default client factory so EVERY client returned from
    /// this factory carries the test internal-service token. Tests that
    /// call <c>CreateClient()</c> implicitly get a client that satisfies
    /// the <see cref="RequireInternalTokenMiddleware"/> gate. Tests that
    /// want to verify rejection (no token, wrong token) can remove or
    /// overwrite the header on their returned client before sending.
    /// </summary>
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add(
            RequireInternalTokenMiddleware.HeaderName,
            TestInternalToken);
    }

    /// <summary>
    /// Creates an HttpClient with both the internal-service token (added
    /// automatically by <see cref="ConfigureClient"/>) and the
    /// <c>X-Tenant-Id</c> header preset. The TenantResolutionMiddleware
    /// rejects any /api/* request without a tenant header, so every
    /// tenant-scoped integration test needs to scope itself up-front.
    /// </summary>
    public HttpClient CreateClientForTenant(Guid tenantId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(
            Billing.Api.Tenancy.TenantResolutionMiddleware.HeaderName,
            tenantId.ToString());
        return client;
    }
}
