using System.Security.Claims;
using System.Text;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Authorization;
using Contracts;
using Task.Api.Endpoints;
using Task.Api.Middleware;
using Task.Infrastructure;
using Task.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

const string ServiceName = "task";
const string Version     = "v1";

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders()
    .AddConsole();

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey  = jwtSection["SigningKey"]
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
            ClockSkew                = TimeSpan.Zero,
        };
    })
    // TASK-B05 (TASK-013) — second scheme for machine-to-machine service tokens.
    // Used exclusively on /api/tasks/internal/* endpoints.
    // Secret sourced from FLOW_SERVICE_TOKEN_SECRET env var (same shared secret
    // used by Flow, Notifications, and all other platform services).
    .AddServiceTokenBearer(builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.AdminOnly, policy =>
        policy.RequireRole(Roles.PlatformAdmin));

    options.AddPolicy(Policies.PlatformOrTenantAdmin, policy =>
        policy.RequireRole(Roles.PlatformAdmin, Roles.TenantAdmin));

    // TASK-B05 (TASK-013) — internal service-to-service endpoint gate.
    // Only accepts tokens with scheme=ServiceToken and role=service.
    // Rejects user JWTs, tokens with missing tenant claim, and unsigned tokens.
    options.AddPolicy("InternalService", policy =>
        policy
            .AddAuthenticationSchemes(ServiceTokenAuthenticationDefaults.Scheme)
            .RequireRole(ServiceTokenAuthenticationDefaults.ServiceRole));
});

builder.Services.AddTaskServices(builder.Configuration);

var app = builder.Build();
var env = app.Environment.EnvironmentName;

app.Logger.LogInformation(
    "Starting {Service} {Version} in {Environment}", ServiceName, Version, env);

try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TasksDbContext>();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Task database migrations applied successfully.");
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex,
        "Could not apply Task database migrations on startup — schema may be out of sync.");
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

app.MapTaskEndpoints();
app.MapTaskNoteEndpoints();
app.MapTaskStageEndpoints();
app.MapTaskStageTransitionEndpoints();
app.MapTaskGovernanceEndpoints();
app.MapTaskTemplateEndpoints();
app.MapTaskReminderEndpoints();
app.MapTaskFlowEndpoints();
app.MapTaskAnalyticsEndpoints();
app.MapTaskLinkedEntityEndpoints();

app.Run();
