using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Support.Api.Auth;

public static class AuthExtensions
{
    public const string TestScheme = "TestAuth";

    public static IServiceCollection AddSupportAuth(
        this IServiceCollection services,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        var defaultScheme = env.IsEnvironment("Testing")
            ? TestScheme
            : JwtBearerDefaults.AuthenticationScheme;

        var authBuilder = services.AddAuthentication(defaultScheme);

        if (env.IsEnvironment("Testing"))
        {
            authBuilder.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
                TestScheme, _ => { });
        }
        else
        {
            var jwtSection = config.GetSection("Authentication:Jwt");
            var authority = jwtSection["Authority"];
            var issuer = jwtSection["Issuer"];
            var audience = jwtSection["Audience"];
            var requireHttps = jwtSection.GetValue<bool?>("RequireHttpsMetadata") ?? !env.IsDevelopment();
            var symmetricKey = jwtSection["SymmetricKey"]; // local/test only

            // Fail closed: every non-Testing environment must explicitly choose
            // exactly one signing strategy. We never silently fall back to a
            // hardcoded key, and we never silently disable issuer/audience
            // validation. A misconfigured deployment should refuse to start
            // rather than accept attacker-minted tokens.
            var hasAuthority = !string.IsNullOrWhiteSpace(authority);
            var hasSymmetric = !string.IsNullOrWhiteSpace(symmetricKey);

            if (!hasAuthority && !hasSymmetric)
            {
                throw new InvalidOperationException(
                    "Authentication:Jwt is not configured. Set Authentication:Jwt:Authority " +
                    "(OIDC) or Authentication:Jwt:SymmetricKey (local/test). Refusing to " +
                    "start without a verified signing strategy.");
            }
            if (hasAuthority && hasSymmetric)
            {
                throw new InvalidOperationException(
                    "Authentication:Jwt configured with both Authority and SymmetricKey. " +
                    "Pick exactly one signing strategy.");
            }
            if (string.IsNullOrWhiteSpace(audience))
            {
                throw new InvalidOperationException(
                    "Authentication:Jwt:Audience is required and must be validated.");
            }
            if (string.IsNullOrWhiteSpace(issuer))
            {
                throw new InvalidOperationException(
                    "Authentication:Jwt:Issuer is required and must be validated.");
            }
            if (hasSymmetric && symmetricKey!.Length < 32)
            {
                // 256-bit minimum for HS256.
                throw new InvalidOperationException(
                    "Authentication:Jwt:SymmetricKey must be at least 32 bytes long.");
            }

            // Disable legacy claim-type mapping so "sub", "role", and "tenant_id"
            // survive intact as claim types after token validation.
            System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

            authBuilder.AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.Audience = audience;
                o.RequireHttpsMetadata = requireHttps;

                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = "role",
                    NameClaimType = "sub",
                };

                if (hasAuthority)
                {
                    // OIDC mode: let JwtBearer fetch the JWKS from the IdP and
                    // resolve signing keys by `kid` as normal. Do NOT pre-set
                    // any local key here, otherwise we would override the
                    // authority-discovered keys.
                    o.Authority = authority;
                }
                else
                {
                    // Local/test symmetric mode: use the configured key as the
                    // sole signing key. The resolver returns it regardless of
                    // `kid` so locally-minted tokens without a kid still
                    // validate, but the key MUST be present (checked above).
                    var signingKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(symmetricKey!));
                    o.TokenValidationParameters.IssuerSigningKey = signingKey;
                    o.TokenValidationParameters.IssuerSigningKeys = new[] { signingKey };
                    o.TokenValidationParameters.IssuerSigningKeyResolver =
                        (token, securityToken, kid, parameters) => new[] { signingKey };
                }
            });
        }

        services.AddAuthorization(opts =>
        {
            opts.AddPolicy(SupportPolicies.SupportRead,
                p => p.RequireAuthenticatedUser().RequireRole(SupportRoles.All));
            opts.AddPolicy(SupportPolicies.SupportWrite,
                p => p.RequireAuthenticatedUser().RequireRole(SupportRoles.All));
            opts.AddPolicy(SupportPolicies.SupportManage,
                p => p.RequireAuthenticatedUser().RequireRole(SupportRoles.Managers));
            opts.AddPolicy(SupportPolicies.SupportInternal,
                p => p.RequireAuthenticatedUser().RequireRole(SupportRoles.InternalStaff));
        });

        return services;
    }
}
