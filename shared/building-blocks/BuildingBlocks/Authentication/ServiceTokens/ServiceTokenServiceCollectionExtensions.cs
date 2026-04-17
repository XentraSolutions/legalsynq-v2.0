using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Authentication.ServiceTokens;

/// <summary>
/// LS-FLOW-MERGE-P5 — DI helpers for service-token issuance (product
/// side) and validation (Flow side).
/// </summary>
public static class ServiceTokenServiceCollectionExtensions
{
    /// <summary>
    /// Register an <see cref="IServiceTokenIssuer"/> bound from the
    /// <c>ServiceTokens</c> configuration section. The shared secret is
    /// preferred from the <c>FLOW_SERVICE_TOKEN_SECRET</c> environment
    /// variable, then from <c>ServiceTokens:SigningKey</c>.
    /// </summary>
    public static IServiceCollection AddServiceTokenIssuer(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        services.AddOptions<ServiceTokenOptions>()
            .Bind(configuration.GetSection(ServiceTokenOptions.SectionName))
            .PostConfigure(o =>
            {
                if (string.IsNullOrWhiteSpace(o.SigningKey))
                {
                    o.SigningKey = Environment.GetEnvironmentVariable(
                        ServiceTokenAuthenticationDefaults.SecretEnvVar) ?? string.Empty;
                }
                if (string.IsNullOrWhiteSpace(o.ServiceName) || o.ServiceName == "unknown-service")
                {
                    o.ServiceName = serviceName;
                }
            });

        services.AddSingleton<IServiceTokenIssuer, ServiceTokenIssuer>();
        return services;
    }

    /// <summary>
    /// Add a second <see cref="JwtBearer"/> scheme (<see cref="ServiceTokenAuthenticationDefaults.Scheme"/>)
    /// that validates HS256 service tokens. Caller is responsible for
    /// having already called <c>AddAuthentication(...)</c> with the user
    /// scheme as the default.
    /// </summary>
    public static AuthenticationBuilder AddServiceTokenBearer(
        this AuthenticationBuilder builder,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(ServiceTokenOptions.SectionName);
        var signingKey = section["SigningKey"]
                         ?? Environment.GetEnvironmentVariable(ServiceTokenAuthenticationDefaults.SecretEnvVar)
                         ?? string.Empty;
        var issuer    = section["Issuer"]   ?? ServiceTokenAuthenticationDefaults.DefaultIssuer;
        var audience  = section["Audience"] ?? ServiceTokenAuthenticationDefaults.DefaultAudience;

        return builder.AddJwtBearer(ServiceTokenAuthenticationDefaults.Scheme, options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = !string.IsNullOrWhiteSpace(signingKey),
                ValidIssuer              = issuer,
                ValidAudience            = audience,
                IssuerSigningKey         = string.IsNullOrWhiteSpace(signingKey)
                    ? null
                    : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                RoleClaimType            = System.Security.Claims.ClaimTypes.Role,
                ClockSkew                = TimeSpan.FromSeconds(30)
            };
        });
    }
}
