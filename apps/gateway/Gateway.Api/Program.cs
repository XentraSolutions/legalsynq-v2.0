using System.Text;
using Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

const string ServiceName = "gateway";
const string Version = "v1";

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders()
    .AddConsole();

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Deny", policy =>
        policy.RequireAssertion(_ => false));
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
var env = app.Environment.EnvironmentName;

app.Logger.LogInformation("Starting {Service} v{Version} in {Environment}", ServiceName, Version, env);

// Security headers
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"]        = "DENY";
    ctx.Response.Headers["X-XSS-Protection"]       = "0";
    ctx.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("ok", ServiceName)))
    .AllowAnonymous();

app.MapGet("/info", () =>
    Results.Ok(new InfoResponse(ServiceName, env, Version)))
    .AllowAnonymous();

app.MapReverseProxy(pipeline =>
{
    // BLK-SEC-02-02: Public CareConnect tenant-header trust boundary enforcement.
    // For /careconnect/api/public/* paths:
    //   1. Strip any client-supplied X-Internal-Gateway-Secret (prevent forgery from direct callers).
    //   2. Inject the configured gateway origin marker so CareConnect can verify the request
    //      passed through this trusted YARP instance (Layer 1 defense).
    // Non-public and non-CareConnect routes are unaffected.
    pipeline.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path.StartsWithSegments("/careconnect/api/public"))
        {
            ctx.Request.Headers.Remove("X-Internal-Gateway-Secret");
            var secret = ctx.RequestServices
                .GetRequiredService<IConfiguration>()["PublicTrustBoundary:InternalRequestSecret"];
            if (!string.IsNullOrWhiteSpace(secret))
                ctx.Request.Headers["X-Internal-Gateway-Secret"] = secret;
        }
        await next();
    });
}).RequireAuthorization();

app.Run();
