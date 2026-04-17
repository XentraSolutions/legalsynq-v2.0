using System.Security.Claims;
using System.Text;
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using BuildingBlocks.FlowClient;
using Contracts;
using Liens.Api.Endpoints;
using Liens.Api.Middleware;
using Liens.Domain;
using Liens.Infrastructure;
using Liens.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

const string ServiceName = "liens";
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.AdminOnly, policy =>
        policy.RequireRole(Roles.PlatformAdmin));

    options.AddPolicy(Policies.PlatformOrTenantAdmin, policy =>
        policy.RequireRole(Roles.PlatformAdmin, Roles.TenantAdmin));
});

builder.Services.AddLiensServices(builder.Configuration);
// LS-FLOW-MERGE-P4 — shared Flow HTTP adapter (bearer pass-through, retry, 503 mapping).
builder.Services.AddFlowClient(builder.Configuration);

var app = builder.Build();

var env = app.Environment.EnvironmentName;
app.Logger.LogInformation("Starting {Service} {Version} in {Environment}", ServiceName, Version, env);

if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        db.Database.Migrate();
        app.Logger.LogInformation("Database migrations applied");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not apply migrations on startup");
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("ok", ServiceName)))
    .AllowAnonymous();

app.MapGet("/info", () =>
    Results.Ok(new InfoResponse(ServiceName, env, Version)))
    .AllowAnonymous();

app.MapGet("/context", (ICurrentRequestContext ctx) =>
{
    if (!ctx.IsAuthenticated)
        return Results.Unauthorized();

    return Results.Ok(new
    {
        authenticated = true,
        userId        = ctx.UserId,
        tenantId      = ctx.TenantId,
        tenantCode    = ctx.TenantCode,
        email         = ctx.Email,
        orgId         = ctx.OrgId,
        orgType       = ctx.OrgType,
        orgTypeId     = ctx.OrgTypeId,
        roles         = ctx.Roles,
        productRoles  = ctx.ProductRoles,
        permissions   = ctx.Permissions,
        isPlatformAdmin = ctx.IsPlatformAdmin,
        capabilities = new
        {
            sell = ctx.CanSellLiens(),
            manageInternal = ctx.CanManageLiensInternal(),
            resolved = ctx.GetLiensCapabilities(),
        },
    });
})
.RequireAuthorization(Policies.AuthenticatedUser);

app.MapLienEndpoints();
app.MapLienOfferEndpoints();
app.MapBillOfSaleEndpoints();
app.MapCaseEndpoints();
app.MapServicingEndpoints();
app.MapContactEndpoints();
// LS-FLOW-MERGE-P4 — product → Flow integration endpoints.
app.MapWorkflowEndpoints();

app.Run();
