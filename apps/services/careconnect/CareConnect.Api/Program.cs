using System.Security.Claims;
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Api.Endpoints;
using CareConnect.Api.Middleware;
using CareConnect.Infrastructure;
using CareConnect.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            RoleClaimType            = ClaimTypes.Role
        };
    });

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.PlatformOrTenantAdmin, policy =>
        policy.RequireRole(Roles.PlatformAdmin, Roles.TenantAdmin));
});

// Infrastructure (DbContext + repositories + services)
builder.Services.AddInfrastructure(builder.Configuration);

// Request context
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();

var app = builder.Build();

// Auto-migrate in Development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CareConnectDbContext>();
    db.Database.Migrate();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// Health & info
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapGet("/info",   () => Results.Ok(new { service = "CareConnect", version = "1.0.0" })).AllowAnonymous();

// API endpoints
app.MapProviderEndpoints();
app.MapReferralEndpoints();
app.MapCategoryEndpoints();

app.Run();
