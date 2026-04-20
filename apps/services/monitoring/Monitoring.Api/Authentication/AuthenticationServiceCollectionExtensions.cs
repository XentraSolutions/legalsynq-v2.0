using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Monitoring.Api.Authentication;

/// <summary>
/// Wires JWT Bearer authentication (RS256) into the Monitoring API.
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddMonitoringAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<JwtAuthenticationOptions>()
            .Bind(configuration.GetSection(JwtAuthenticationOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<JwtAuthenticationOptions>, JwtAuthenticationOptionsValidator>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });

        // Use IConfigureNamedOptions so JwtBearerOptions are populated from
        // the validated JwtAuthenticationOptions snapshot.
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

        services.AddAuthorization();

        return services;
    }

    private sealed class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
    {
        private readonly IOptions<JwtAuthenticationOptions> _options;
        private readonly ILogger<ConfigureJwtBearerOptions> _logger;

        public ConfigureJwtBearerOptions(
            IOptions<JwtAuthenticationOptions> options,
            ILogger<ConfigureJwtBearerOptions> logger)
        {
            _options = options;
            _logger = logger;
        }

        public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);

        public void Configure(string? name, JwtBearerOptions options)
        {
            if (name != JwtBearerDefaults.AuthenticationScheme)
            {
                return;
            }

            var jwt = _options.Value;

            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = jwt.RequireHttpsMetadata;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
                ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
            };

            if (!string.IsNullOrWhiteSpace(jwt.Authority))
            {
                // JWKS-based key resolution via OIDC discovery.
                options.Authority = jwt.Authority;
                _logger.LogInformation(
                    "JWT Bearer configured for issuer '{Issuer}', audience '{Audience}', authority discovery enabled.",
                    jwt.Issuer, jwt.Audience);
            }
            else if (!string.IsNullOrWhiteSpace(jwt.PublicKeyPem))
            {
                var rsa = RSA.Create();
                rsa.ImportFromPem(jwt.PublicKeyPem);
                options.TokenValidationParameters.IssuerSigningKey = new RsaSecurityKey(rsa);
                _logger.LogInformation(
                    "JWT Bearer configured for issuer '{Issuer}', audience '{Audience}', explicit RSA public key loaded.",
                    jwt.Issuer, jwt.Audience);
            }

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = ctx =>
                {
                    var log = ctx.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Monitoring.Api.Auth");
                    log.LogWarning(
                        "JWT authentication failed: {Reason}",
                        ctx.Exception.GetType().Name);
                    return Task.CompletedTask;
                },
                OnChallenge = ctx =>
                {
                    var log = ctx.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Monitoring.Api.Auth");
                    log.LogInformation(
                        "JWT challenge issued for {Path} (error={Error})",
                        ctx.Request.Path, string.IsNullOrEmpty(ctx.Error) ? "unauthorized" : ctx.Error);
                    return Task.CompletedTask;
                },
            };
        }
    }
}
