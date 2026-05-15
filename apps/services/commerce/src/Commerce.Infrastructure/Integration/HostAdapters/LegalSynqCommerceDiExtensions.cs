using System.Text;
using Commerce.Application.Integration.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Commerce.Infrastructure.Integration.HostAdapters;

/// <summary>
/// LS-INT-01 — DI registration for LegalSynq identity integration in Commerce.
///
/// Call <see cref="AddLegalSynqCommerceIntegration"/> from
/// <c>Program.cs</c> when <c>LegalSynq:Identity:Enabled = true</c>.
/// When disabled the existing <see cref="LocalHostIdentityContextAccessor"/>
/// and <see cref="NoopHostTenantResolver"/> registrations remain unchanged.
/// </summary>
public static class LegalSynqCommerceDiExtensions
{
    /// <summary>
    /// Registers LegalSynq JWT identity adapters and adds JwtBearer authentication.
    ///
    /// Safe defaults: call is only reached when <c>LegalSynq:Identity:Enabled = true</c>;
    /// standalone mode is not affected.
    /// </summary>
    public static IServiceCollection AddLegalSynqCommerceIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LegalSynqIdentityOptions>(
            configuration.GetSection(LegalSynqIdentityOptions.SectionName));

        var opts = new LegalSynqIdentityOptions();
        configuration.GetSection(LegalSynqIdentityOptions.SectionName).Bind(opts);

        // Resolve signing key: env var takes priority over appsettings (never commit real keys).
        var signingKey = Environment.GetEnvironmentVariable("COMMERCE_LEGALSYNQ_SIGNING_KEY")
                      ?? opts.SigningKey;

        if (string.IsNullOrWhiteSpace(signingKey))
        {
            // Can't use ILoggerFactory here (services not built yet); use a startup warning message.
            Console.Error.WriteLine(
                "[Commerce][WARN] LegalSynq:Identity:Enabled=true but SigningKey is empty. " +
                "Set COMMERCE_LEGALSYNQ_SIGNING_KEY env var. JWT validation will reject all tokens.");
        }

        // Register concrete LegalSynq adapters.
        services.AddHttpContextAccessor();
        services.AddScoped<LegalSynqJwtHostIdentityContextAccessor>();

        // NoopHostTenantResolver is already registered as IHostTenantResolver by
        // AddCommerceInfrastructure(). Register it ALSO as its concrete type so
        // LegalSynqJwtHostTenantResolver can inject the EF-backed base resolver directly.
        services.AddScoped<NoopHostTenantResolver>();
        services.AddScoped<LegalSynqJwtHostTenantResolver>();

        // Override the stub registrations from AddCommerceInfrastructure().
        // The last Add* registration wins for GetRequiredService<T>().
        services.AddScoped<IHostIdentityContextAccessor>(sp =>
            sp.GetRequiredService<LegalSynqJwtHostIdentityContextAccessor>());

        services.AddScoped<IHostTenantResolver>(sp =>
            sp.GetRequiredService<LegalSynqJwtHostTenantResolver>());

        services.AddScoped<IHostIntegrationAdapter, LegalSynqCommerceHostIntegrationAdapter>();

        // JWT Bearer authentication.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwtOpts =>
            {
                jwtOpts.MapInboundClaims = false;
                jwtOpts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = opts.Issuer,
                    ValidateAudience = true,
                    ValidAudience = opts.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    IssuerSigningKey = string.IsNullOrWhiteSpace(signingKey)
                        ? new SymmetricSecurityKey(new byte[32])
                        : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                };
            });

        services.AddAuthorization();

        return services;
    }
}
