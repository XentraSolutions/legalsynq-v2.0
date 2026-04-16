using System.Security.Claims;
using System.Text;
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Contracts;
using SynqComm.Api.Endpoints;
using SynqComm.Api.Middleware;
using SynqComm.Infrastructure;
using SynqComm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

const string ServiceName = "synqcomm";
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

builder.Services.AddSynqCommServices(builder.Configuration);

var app = builder.Build();

var env = app.Environment.EnvironmentName;
app.Logger.LogInformation("Starting {Service} {Version} in {Environment}", ServiceName, Version, env);

if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SynqCommDbContext>();
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
    });
})
.RequireAuthorization(Policies.AuthenticatedUser);

app.MapConversationEndpoints();
app.MapMessageEndpoints();
app.MapParticipantEndpoints();
app.MapAttachmentEndpoints();

app.Run();
