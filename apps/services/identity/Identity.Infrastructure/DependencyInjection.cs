using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Identity.Application;
using Identity.Application.Interfaces;
using Identity.Application.Services;
using Identity.Infrastructure.Auth;
using Identity.Infrastructure.Data;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Services;
using LegalSynq.AuditClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException("Connection string 'IdentityDb' not found.");

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 0))));

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        services.AddAuditEventClient(configuration);

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();

        // Capability-based authorization
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddScoped<ICapabilityService, CapabilityService>();
        services.AddScoped<AuthorizationService>();
        services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();

        // Phase I: scoped authorization service (real non-global scope checks)
        services.AddScoped<IScopedAuthorizationService, ScopedAuthorizationService>();

        services.Configure<Route53DnsOptions>(configuration.GetSection("Route53"));
        services.AddSingleton<IDnsService, Route53DnsService>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();

        return services;
    }
}
