using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Flow.Api.Middleware;
using Flow.Api.Services;
using Flow.Application.Adapters.AuditAdapter;
using Flow.Application.Adapters.NotificationAdapter;
using Flow.Application.Events;
using Flow.Domain.Interfaces;
using Flow.Infrastructure;
using Flow.Infrastructure.Adapters;
using Flow.Infrastructure.Events;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// MVC + JSON
// ---------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .SelectMany(e => e.Value!.Errors.Select(err => err.ErrorMessage))
            .ToList();

        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new
        {
            error = "Validation failed.",
            errors
        });
    };
});

// ---------------------------------------------------------------------------
// Identity / JWT (LegalSynq Identity v2 conventions)
// ---------------------------------------------------------------------------
var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"] ?? string.Empty;

if (!string.IsNullOrWhiteSpace(signingKey))
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer              = jwtSection["Issuer"],
                ValidAudience            = jwtSection["Audience"],
                IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                RoleClaimType            = ClaimTypes.Role,
                ClockSkew                = TimeSpan.Zero
            };
        });
}
else
{
    // Allow boot in environments without a configured signing key. Protected
    // endpoints will reject requests because no token will validate.
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.AdminOnly, policy =>
        policy.RequireRole(Roles.PlatformAdmin));

    options.AddPolicy(Policies.PlatformOrTenantAdmin, policy =>
        policy.RequireRole(Roles.PlatformAdmin, Roles.TenantAdmin));

    // LS-FLOW-MERGE-P3 — product-capability policies for the product-facing
    // workflow endpoints. Each policy requires an authenticated user PLUS
    // either the corresponding permission claim OR product-role access on
    // the matching ProductCode. The "no permissions at all" fallback is
    // restricted to the Development environment so production tokens that
    // legitimately lack a capability claim cannot pass — see
    // merge-phase-3-notes.md.
    var allowMissingPermissions = builder.Environment.IsDevelopment();

    options.AddPolicy(Policies.CanSellLien, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  ctx.User.HasPermission(PermissionCodes.LienSell) ||
                  ctx.User.HasProductAccess(ProductCodes.SynqLiens) ||
                  (allowMissingPermissions && !ctx.User.GetPermissions().Any())));

    options.AddPolicy(Policies.CanReferCareConnect, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  ctx.User.HasPermission(PermissionCodes.ReferralCreate) ||
                  ctx.User.HasProductAccess(ProductCodes.SynqCareConnect) ||
                  (allowMissingPermissions && !ctx.User.GetPermissions().Any())));

    options.AddPolicy(Policies.CanReferFund, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  ctx.User.HasPermission(PermissionCodes.ApplicationRefer) ||
                  ctx.User.HasProductAccess(ProductCodes.SynqFund) ||
                  (allowMissingPermissions && !ctx.User.GetPermissions().Any())));
});

// ---------------------------------------------------------------------------
// Request context, tenant provider, infrastructure, application
// ---------------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();
// LS-FLOW-MERGE-P3 — adapt platform request context into Flow.Domain abstraction.
builder.Services.AddScoped<Flow.Domain.Interfaces.IFlowUserContext, Flow.Api.Services.FlowUserContext>();

builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddTenantProvider<ClaimsTenantProvider>();
builder.Services.AddApplicationServices();

// ---------------------------------------------------------------------------
// Platform integration adapters (audit + notifications) + internal events
// ---------------------------------------------------------------------------
builder.Services.AddFlowPlatformAdapters(builder.Configuration);

// ---------------------------------------------------------------------------
// CORS — environment-driven origins. Local dev defaults preserved in
// appsettings.json; higher environments must supply explicit origins.
// ---------------------------------------------------------------------------
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FlowCors", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            // No origins configured → no cross-origin access. Same-origin
            // (e.g., gateway-fronted) requests are unaffected.
            policy.DisallowCredentials();
        }
    });
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Pipeline ordering:
//   exception → routing → CORS → auth → authorization → tenant validation → endpoints
// ---------------------------------------------------------------------------
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseRouting();
app.UseCors("FlowCors");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantValidationMiddleware>();

app.MapControllers();
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/health"); // LS-FLOW-MERGE-P4: alias for unified product smoke checks

app.Run();
