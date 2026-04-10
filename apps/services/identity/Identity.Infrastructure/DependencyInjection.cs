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
using System;

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

        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddScoped<ICapabilityService, CapabilityService>();
        services.AddScoped<AuthorizationService>();
        services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();

        services.AddScoped<IScopedAuthorizationService, ScopedAuthorizationService>();

        services.Configure<Route53DnsOptions>(configuration.GetSection("Route53"));
        services.AddSingleton<IDnsService, Route53DnsService>();

        services.Configure<TenantVerificationOptions>(configuration.GetSection("TenantVerification"));
        services.AddScoped<ITenantVerificationService, TenantVerificationService>();

        services.Configure<VerificationRetryOptions>(configuration.GetSection("VerificationRetry"));
        services.AddScoped<IVerificationRetryService, VerificationRetryService>();
        services.AddHostedService<VerificationRetryBackgroundService>();

        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();

        services.AddHttpClient("CareConnectInternal", client =>
        {
            var ccUrl = configuration["CareConnect:InternalUrl"] ?? "http://localhost:5003";
            client.BaseAddress = new Uri(ccUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddScoped<IProductProvisioningHandler, CareConnectProvisioningHandler>();
        services.AddScoped<IProductProvisioningService, ProductProvisioningService>();

        services.AddScoped<IProductRoleMapper, CareConnectRoleMapper>();
        services.AddScoped<IProductRoleResolutionService, ProductRoleResolutionService>();

        services.AddScoped<IAuditPublisher, AuditPublisher>();
        services.AddScoped<ITenantProductEntitlementService, TenantProductEntitlementService>();
        services.AddScoped<IUserProductAccessService, UserProductAccessService>();
        services.AddScoped<IUserRoleAssignmentService, UserRoleAssignmentService>();
        services.AddScoped<IAccessSourceQueryService, AccessSourceQueryService>();
        services.AddScoped<IEffectiveAccessService, EffectiveAccessService>();

        return services;
    }
}
