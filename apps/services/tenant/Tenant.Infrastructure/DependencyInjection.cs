using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tenant.Application.Interfaces;
using Tenant.Application.Services;
using Tenant.Infrastructure.Data;
using Tenant.Infrastructure.Repositories;
using Tenant.Infrastructure.Services;

namespace Tenant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TenantDb")
            ?? throw new InvalidOperationException("Connection string 'TenantDb' is not configured.");

        services.AddDbContext<TenantDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 0))));

        // ── Repositories ──────────────────────────────────────────────────────

        services.AddScoped<ITenantRepository,       TenantRepository>();
        services.AddScoped<IBrandingRepository,     BrandingRepository>();
        services.AddScoped<IDomainRepository,       DomainRepository>();
        services.AddScoped<IEntitlementRepository,  EntitlementRepository>();
        services.AddScoped<ICapabilityRepository,   CapabilityRepository>();
        services.AddScoped<ISettingRepository,      SettingRepository>();

        // ── Application services ──────────────────────────────────────────────

        services.AddScoped<ITenantService,          TenantService>();
        services.AddScoped<IBrandingService,        BrandingService>();
        services.AddScoped<IDomainService,          DomainService>();
        services.AddScoped<IResolutionService,      ResolutionService>();
        services.AddScoped<IEntitlementService,     EntitlementService>();
        services.AddScoped<ICapabilityService,      CapabilityService>();
        services.AddScoped<ISettingService,         SettingService>();
        services.AddScoped<IMigrationUtilityService, MigrationUtilityService>();
        services.AddScoped<ITenantSyncAdapter,       NoOpTenantSyncAdapter>();

        return services;
    }
}
