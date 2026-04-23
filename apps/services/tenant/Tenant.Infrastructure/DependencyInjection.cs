using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tenant.Application.Interfaces;
using Tenant.Application.Services;
using Tenant.Infrastructure.Data;
using Tenant.Infrastructure.Repositories;

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

        services.AddScoped<ITenantRepository,  TenantRepository>();
        services.AddScoped<IBrandingRepository, BrandingRepository>();
        services.AddScoped<ITenantService,     TenantService>();
        services.AddScoped<IBrandingService,   BrandingService>();

        return services;
    }
}
