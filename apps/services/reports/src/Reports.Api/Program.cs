using System.Security.Claims;
using System.Text;
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Reports.Api.Configuration;
using Reports.Api.Endpoints;
using Reports.Api.Middleware;
using Reports.Application;
using Reports.Contracts.Configuration;
using Reports.Infrastructure;
using Reports.Worker.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ReportsServiceSettings>(
    builder.Configuration.GetSection(ReportsServiceSettings.SectionName));
builder.Services.Configure<MySqlSettings>(
    builder.Configuration.GetSection(MySqlSettings.SectionName));
builder.Services.Configure<AdapterSettings>(
    builder.Configuration.GetSection(AdapterSettings.SectionName));
builder.Services.Configure<AuditServiceSettings>(
    builder.Configuration.GetSection(AuditServiceSettings.SectionName));
builder.Services.Configure<EmailDeliverySettings>(
    builder.Configuration.GetSection(EmailDeliverySettings.SectionName));
builder.Services.Configure<SftpDeliverySettings>(
    builder.Configuration.GetSection(SftpDeliverySettings.SectionName));
builder.Services.Configure<StorageSettings>(
    builder.Configuration.GetSection(StorageSettings.SectionName));
builder.Services.Configure<LiensDataSettings>(
    builder.Configuration.GetSection(LiensDataSettings.SectionName));

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
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();

builder.Services.AddReportsApplication();
builder.Services.AddReportsInfrastructure(builder.Configuration);

builder.Services.AddHostedService<ReportWorkerService>();
builder.Services.AddHostedService<ScheduleWorkerService>();

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantValidationMiddleware>();

app.MapHealthEndpoints();
app.MapTemplateEndpoints();
app.MapAssignmentEndpoints();
app.MapOverrideEndpoints();
app.MapExecutionEndpoints();
app.MapExportEndpoints();
app.MapScheduleEndpoints();
app.MapViewEndpoints();
app.MapMetricsEndpoints();

app.MapGet("/health", () => Results.Redirect("/api/v1/health", permanent: true))
    .ExcludeFromDescription();
app.MapGet("/ready", () => Results.Redirect("/api/v1/ready", permanent: true))
    .ExcludeFromDescription();

app.Run();
