using System.Text;
using Billing.Api.Hosting;
using Billing.Api.LegalSynq;
using Billing.Api.OpenApi;
using Billing.Api.Security;
using Billing.Api.Tenancy;
using Billing.Infrastructure;
using Billing.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Billing API",
        Version = "v1",
        Description = "Monk Search Billing service — tenant-facing invoices, payments, statements, and templates."
    });

    // Internal-token header is required on every /api/* request. Surface
    // it in Swagger so the "Try it out" panel makes the contract explicit
    // for the BFF integrator.
    o.AddSecurityDefinition(InternalTokenOperationFilter.SchemeId, new OpenApiSecurityScheme
    {
        Name = RequireInternalTokenMiddleware.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description =
            "Internal shared secret between the Monk BFF and Billing. **Required on every /api/* request.** " +
            "Browser clients MUST NOT send this header — Billing.Api is a private internal microservice and is " +
            "never exposed directly to the browser. The BFF reads the value from a server-side secret and " +
            "injects it on every outbound call."
    });

    // Tenant header is required on every tenant-scoped /api/* request.
    o.AddSecurityDefinition("TenantHeader", new OpenApiSecurityScheme
    {
        Name = TenantResolutionMiddleware.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description =
            "Tenant identifier (GUID) used to scope every billing read and write. **Internal BFF-injected " +
            "header** — the Monk BFF derives the tenant from the validated IDM session and forwards it as " +
            "X-Tenant-Id. Browser clients MUST NOT send this header directly; the BFF is the only authorized " +
            "source of tenant identity for Billing."
    });

    // Apply per-operation security requirements so Swagger UI, generated
    // SDKs, and the snapshot contract all clearly mark /api/* operations
    // as requiring both internal-token and tenant-id headers. Health
    // endpoints (/health, /healthz) are minimal-API mapped and never
    // appear in the document.
    o.OperationFilter<InternalTokenOperationFilter>();
    o.OperationFilter<TenantHeaderOperationFilter>();

    // Strip /api/invoice-templates/platform/* from the generated document
    // when BILLING_ENABLE_PLATFORM_TEMPLATES is not "true" (the default).
    // Mirrors the runtime gate in PlatformTemplatesGuardAttribute so the
    // on-disk contract matches the surface that will actually serve.
    o.DocumentFilter<HideDisabledPlatformTemplateEndpointsDocumentFilter>();
});

builder.Services.AddBillingInfrastructure(builder.Configuration);

// Tenant resolution: HttpContextAccessor + scoped ITenantContext that pulls
// the tenant id parsed by TenantResolutionMiddleware from the request header.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpHeaderTenantContext>();

// ---- LS-INT-01: LegalSynq identity + dual-mode tenant context (additive, gated) ----
// Safe defaults: both Enabled=false → standalone X-Internal-Token + X-Tenant-Id
// pipeline is COMPLETELY UNCHANGED. When opted in:
//   LegalSynq:Identity:Enabled=true    → JWT bearer authentication registered
//   LegalSynq:TenantContext:Enabled=true → ITenantIdentityContextResolver wired
//     into TenantResolutionMiddleware; hierarchy: JWT claim → internal-service
//     header → X-Tenant-Id fallback.
builder.Services.Configure<LegalSynqIdentityOptions>(
    builder.Configuration.GetSection(LegalSynqIdentityOptions.SectionName));
builder.Services.Configure<LegalSynqTenantContextOptions>(
    builder.Configuration.GetSection(LegalSynqTenantContextOptions.SectionName));

var legalSynqIdentityEnabled = builder.Configuration.GetValue<bool>("LegalSynq:Identity:Enabled");
var legalSynqTenantEnabled = builder.Configuration.GetValue<bool>("LegalSynq:TenantContext:Enabled");

if (legalSynqIdentityEnabled)
{
    var lsOpts = new LegalSynqIdentityOptions();
    builder.Configuration.GetSection(LegalSynqIdentityOptions.SectionName).Bind(lsOpts);

    var signingKey = Environment.GetEnvironmentVariable("BILLING_LEGALSYNQ_SIGNING_KEY")
                  ?? lsOpts.SigningKey;

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(jwtOpts =>
        {
            jwtOpts.MapInboundClaims = false;
            jwtOpts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = lsOpts.Issuer,
                ValidateAudience = true,
                ValidAudience = lsOpts.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                IssuerSigningKey = string.IsNullOrWhiteSpace(signingKey)
                    ? new SymmetricSecurityKey(new byte[32])
                    : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            };
        });

    builder.Services.AddAuthorization();
}

if (legalSynqTenantEnabled)
{
    builder.Services.AddScoped<ITenantIdentityContextResolver, LegalSynqJwtTenantContextResolver>();
}

// Invoice lifecycle scheduler. The hosted service self-disables when
// InvoiceLifecycle:OverdueJobEnabled is false (the default), so this is
// safe to register unconditionally — registration just makes the option
// available without spinning the loop.
builder.Services.Configure<InvoiceLifecycleOptions>(
    builder.Configuration.GetSection(InvoiceLifecycleOptions.SectionName));
builder.Services.AddHostedService<InvoiceOverdueHostedService>();

var app = builder.Build();

// ---------------------------------------------------------------------------
// EF migrations — gated behind environment to protect shared databases.
// Auto-migrate runs only when:
//   * ASPNETCORE_ENVIRONMENT is Development, OR
//   * BILLING_RUN_MIGRATIONS=true
// In every other case the service comes up without touching schema. Operators
// run `dotnet ef database update` (or set BILLING_RUN_MIGRATIONS=true once)
// against production databases on demand.
// ---------------------------------------------------------------------------
{
    var explicitFlag = Environment.GetEnvironmentVariable("BILLING_RUN_MIGRATIONS");
    var explicitOptIn = string.Equals(explicitFlag, "true", StringComparison.OrdinalIgnoreCase);
    var runMigrations = app.Environment.IsDevelopment() || explicitOptIn;

    using var scope = app.Services.CreateScope();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (runMigrations)
    {
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        if (db.Database.IsRelational())
        {
            startupLogger.LogInformation(
                "Billing: running EF Core migrations (environment={Env}, BILLING_RUN_MIGRATIONS={Flag}).",
                app.Environment.EnvironmentName,
                explicitFlag ?? "<unset>");
            db.Database.Migrate();
        }
        else
        {
            startupLogger.LogInformation(
                "Billing: skipping migrations — DbContext is using a non-relational provider (likely InMemory).");
        }
    }
    else
    {
        startupLogger.LogInformation(
            "Billing: skipping EF Core migrations (environment={Env}, BILLING_RUN_MIGRATIONS={Flag}). " +
            "Set BILLING_RUN_MIGRATIONS=true to opt in for this process.",
            app.Environment.EnvironmentName,
            explicitFlag ?? "<unset>");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "Billing API v1"));
}

// Health endpoints — bypass both internal-token and tenant middleware
// (they are mapped before either middleware runs in the pipeline, AND
// RequireInternalTokenMiddleware also short-circuits these paths
// defensively in case route ordering ever changes).
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "billing-api" }))
   .WithName("Health")
   .WithTags("Health");

app.MapGet("/healthz", () => Results.Ok(new { status = "ok", service = "billing-api" }))
   .WithName("Healthz")
   .WithTags("Health");

// LS-INT-01: JWT auth middleware — registered only when LegalSynq:Identity:Enabled=true.
// Health endpoints (/health, /healthz) are minimal-API mapped ABOVE this block,
// so they are never gated. Pipeline order: auth → internal-token → tenant resolution.
if (legalSynqIdentityEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

// Internal-token gate MUST come before TenantResolutionMiddleware so the
// tenant header is only ever read for trusted internal callers.
app.UseMiddleware<RequireInternalTokenMiddleware>();

// Must run before MapControllers so /api/* requests are short-circuited
// with HTTP 400 when the tenant header is missing or invalid.
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();

app.Run();

public partial class Program { }
