using Microsoft.OpenApi.Models;
using TenantBilling.Api.Hosting;
using TenantBilling.Api.Tenancy;
using TenantBilling.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Tenant Billing API",
        Version = "v1",
        Description = "Tenant Billing Service — billing domain foundation."
    });

    // Surface the tenant header on /api/* operations so the Swagger UI
    // "Try it out" panel knows it's required there. The operation filter
    // scopes the requirement to protected routes so /health is not falsely
    // shown as needing a tenant.
    o.AddSecurityDefinition("TenantHeader", new OpenApiSecurityScheme
    {
        Name = TenantResolutionMiddleware.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Tenant identifier (GUID) used to scope every billing read and write."
    });
    o.OperationFilter<TenantHeaderOperationFilter>();
});

builder.Services.AddTenantBillingInfrastructure(builder.Configuration);

// Tenant resolution: HttpContextAccessor + scoped ITenantContext that pulls
// the tenant id parsed by TenantResolutionMiddleware from the request header.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpHeaderTenantContext>();

// Invoice lifecycle scheduler. The hosted service self-disables when
// InvoiceLifecycle:OverdueJobEnabled is false (the default), so this is
// safe to register unconditionally — registration just makes the option
// available without spinning the loop.
builder.Services.Configure<InvoiceLifecycleOptions>(
    builder.Configuration.GetSection(InvoiceLifecycleOptions.SectionName));
builder.Services.AddHostedService<InvoiceOverdueHostedService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "Tenant Billing API v1"));
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "tenant-billing-api" }))
   .WithName("Health")
   .WithTags("Health");

// Must run before MapControllers so /api/* requests are short-circuited
// with HTTP 400 when the tenant header is missing or invalid.
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();

app.Run();

public partial class Program { }
