using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TenantBilling.Infrastructure.Data;

namespace TenantBilling.Tests;

/// <summary>
/// WebApplicationFactory for the Tenant Billing API. Each factory instance gets
/// its own InMemory database so test classes do not bleed state into each
/// other (the runtime registration uses a static InMemory name).
/// </summary>
public sealed class TenantBillingWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"tenant-billing-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Force the DependencyInjection fallback path (InMemory) in case
            // the host environment defines a real connection string.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = string.Empty
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the runtime-registered DbContext options with a per-
            // factory InMemory database to isolate tests.
            var optionsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<TenantBillingDbContext>));
            if (optionsDescriptor is not null) services.Remove(optionsDescriptor);

            services.AddDbContext<TenantBillingDbContext>(o => o.UseInMemoryDatabase(_databaseName));
        });
    }

    /// <summary>
    /// Creates an HttpClient with the X-Tenant-Id header preset. The
    /// TenantResolutionMiddleware (added in TBS-B02) rejects any
    /// /api/* request without this header with HTTP 400, so every
    /// integration test needs to scope itself to a tenant up-front.
    /// </summary>
    public HttpClient CreateClientForTenant(Guid tenantId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }
}
