using System.Text;
using Contracts;
using Identity.Api;
using Identity.Api.Endpoints;
using Identity.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

const string ServiceName = "identity";
const string Version = "v1";

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders()
    .AddConsole();

builder.Services.AddInfrastructure(builder.Configuration);

// ── JWT authentication (for GET /api/auth/me) ─────────────────────────────
// Identity.Api both ISSUES and VALIDATES JWTs.
// Validation here is used only for the /auth/me endpoint (called by the Next.js BFF).
// The gateway handles JWT validation for all other downstream service routes.
var jwtSection   = builder.Configuration.GetSection("Jwt");
var signingKey   = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
var issuer       = jwtSection["Issuer"]   ?? "legalsynq-identity";
var audience     = jwtSection["Audience"] ?? "legalsynq-platform";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // keep claim names as-is (sub, email, etc.)
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidIssuer              = issuer,
            ValidateAudience         = true,
            ValidAudience            = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.Zero,
            RoleClaimType            = System.Security.Claims.ClaimTypes.Role,
            NameClaimType            = System.Security.Claims.ClaimTypes.NameIdentifier,
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();
var env = app.Environment.EnvironmentName;

app.Logger.LogInformation("Starting {Service} {Version} in {Environment}", ServiceName, Version, env);

// ── Startup tasks (see StartupExtensions.cs) ─────────────────────────────
app.RunDevMigrations();
await app.RunPhaseGDiagnosticsAsync();
await app.SeedProductRolesAsync();
await app.RunDevFixupsAsync();

// ── Middleware pipeline ───────────────────────────────────────────────────
// UseAuthentication + UseAuthorization must precede all endpoint maps
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("ok", ServiceName)));

app.MapGet("/info", () =>
    Results.Ok(new InfoResponse(ServiceName, env, Version)));

app.MapTenantEndpoints();
app.MapProductEndpoints();
app.MapUserEndpoints();
app.MapAuthEndpoints();
app.MapTenantBrandingEndpoints();
app.MapAdminEndpoints();

app.Run();
