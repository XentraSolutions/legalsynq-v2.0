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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
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
        services.AddScoped<IPermissionService, PermissionService>();
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

        services.AddScoped<IAuditPublisher, AuditPublisher>();
        services.AddScoped<ITenantProductEntitlementService, TenantProductEntitlementService>();
        services.AddScoped<IUserProductAccessService, UserProductAccessService>();
        services.AddScoped<IUserRoleAssignmentService, UserRoleAssignmentService>();
        services.AddScoped<IAccessSourceQueryService, AccessSourceQueryService>();
        services.AddScoped<IEffectiveAccessService, EffectiveAccessService>();

        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IGroupMembershipService, GroupMembershipService>();
        services.AddScoped<IGroupProductAccessService, GroupProductAccessService>();
        services.AddScoped<IGroupRoleAssignmentService, GroupRoleAssignmentService>();

        services.Configure<PolicyCachingOptions>(configuration.GetSection("Authorization:PolicyCaching"));
        services.Configure<PolicyLoggingOptions>(configuration.GetSection("Authorization:PolicyLogging"));
        services.Configure<PolicyVersioningOptions>(configuration.GetSection("Authorization:PolicyVersioning"));
        services.AddSingleton<PolicyMetrics>();

        services.AddScoped<IAttributeProvider, DefaultAttributeProvider>();
        services.AddScoped<IPolicyEvaluationService, PolicyEvaluationService>();
        services.AddScoped<IPolicyResourceContextAccessor, HttpContextPolicyResourceContextAccessor>();

        AddPolicyInfrastructure(services, configuration);

        return services;
    }

    private static void AddPolicyInfrastructure(IServiceCollection services, IConfiguration configuration)
    {
        var cachingProvider = configuration["Authorization:PolicyCaching:Provider"] ?? "InMemory";
        var versioningProvider = configuration["Authorization:PolicyVersioning:Provider"] ?? "InMemory";
        var redisUrl = configuration["Authorization:Redis:Url"] ?? configuration["Redis:Url"] ?? "";
        var useRedis = string.Equals(cachingProvider, "Redis", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(versioningProvider, "Redis", StringComparison.OrdinalIgnoreCase);

        if (useRedis && !string.IsNullOrWhiteSpace(redisUrl))
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<RedisPolicyVersionProvider>>();
                try
                {
                    return ConnectionMultiplexer.Connect(redisUrl);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Redis connection failed — distributed policy features will use in-memory fallback");
                    throw;
                }
            });
        }

        if (string.Equals(versioningProvider, "Redis", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(redisUrl))
        {
            services.AddSingleton<IPolicyVersionProvider>(sp =>
            {
                try
                {
                    var redis = sp.GetRequiredService<IConnectionMultiplexer>();
                    return new RedisPolicyVersionProvider(redis, sp.GetRequiredService<ILogger<RedisPolicyVersionProvider>>());
                }
                catch
                {
                    return new InMemoryPolicyVersionProvider();
                }
            });
        }
        else
        {
            services.AddSingleton<IPolicyVersionProvider, InMemoryPolicyVersionProvider>();
        }

        if (string.Equals(cachingProvider, "Redis", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(redisUrl))
        {
            services.AddSingleton<IPolicyEvaluationCache>(sp =>
            {
                try
                {
                    var redis = sp.GetRequiredService<IConnectionMultiplexer>();
                    return new RedisPolicyEvaluationCache(redis, sp.GetRequiredService<ILogger<RedisPolicyEvaluationCache>>());
                }
                catch
                {
                    return new InMemoryPolicyEvaluationCache(sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>());
                }
            });
        }
        else
        {
            services.AddSingleton<IPolicyEvaluationCache, InMemoryPolicyEvaluationCache>();
        }
    }
}
