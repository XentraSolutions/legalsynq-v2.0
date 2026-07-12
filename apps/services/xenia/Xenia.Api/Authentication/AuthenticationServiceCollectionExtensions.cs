using System.Security.Claims;
using System.Text;
using BuildingBlocks.Authentication.ServiceTokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Xenia.Api.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddXeniaAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var userSigningKey = jwtSection["SigningKey"] ?? string.Empty;
        var serviceTokenKey =
            Environment.GetEnvironmentVariable(ServiceTokenAuthenticationDefaults.SecretEnvVar)
            ?? configuration[$"{ServiceTokenOptions.SectionName}:SigningKey"]
            ?? string.Empty;

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = !string.IsNullOrWhiteSpace(jwtSection["Issuer"]),
                    ValidateAudience = !string.IsNullOrWhiteSpace(jwtSection["Audience"]),
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = !string.IsNullOrWhiteSpace(userSigningKey),
                    ValidIssuer = jwtSection["Issuer"],
                    ValidAudience = jwtSection["Audience"],
                    IssuerSigningKey = string.IsNullOrWhiteSpace(userSigningKey)
                        ? null
                        : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(userSigningKey)) { KeyId = ServiceTokenAuthenticationDefaults.UserTokenKeyId },
                    RoleClaimType = "role",
                    NameClaimType = "sub",
                    ClockSkew = TimeSpan.Zero,
                };
            })
            .AddJwtBearer(ServiceTokenAuthenticationDefaults.Scheme, options =>
            {
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = !string.IsNullOrWhiteSpace(serviceTokenKey),
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidIssuer = ServiceTokenAuthenticationDefaults.DefaultIssuer,
                    ValidAudiences = ["xenia-service", "legalsynq-services", ServiceTokenAuthenticationDefaults.DefaultAudience],
                    IssuerSigningKey = string.IsNullOrWhiteSpace(serviceTokenKey)
                        ? null
                        : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(serviceTokenKey)) { KeyId = ServiceTokenAuthenticationDefaults.ServiceTokenKeyId },
                    NameClaimType = "sub",
                    RoleClaimType = ClaimTypes.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var subject = context.Principal?.FindFirst("sub")?.Value;
                        if (string.IsNullOrWhiteSpace(subject) || !subject.StartsWith("service:", StringComparison.Ordinal))
                            context.Fail("Service token subject must start with 'service:'.");
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(XeniaPolicies.AuthenticatedUser, policy =>
                policy
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser());

            options.AddPolicy(XeniaPolicies.PlatformAdmin, policy =>
                policy
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                    .RequireRole("PlatformAdmin"));

            options.AddPolicy(XeniaPolicies.TenantAdminOrAbove, policy =>
                policy
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                    .RequireRole("PlatformAdmin", "TenantAdmin"));

            options.AddPolicy(XeniaPolicies.InternalService, policy =>
                policy
                    .AddAuthenticationSchemes(ServiceTokenAuthenticationDefaults.Scheme)
                    .RequireAuthenticatedUser());
        });

        return services;
    }
}
