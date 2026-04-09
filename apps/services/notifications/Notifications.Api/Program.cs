using Microsoft.EntityFrameworkCore;
using Notifications.Api.Endpoints;
using Notifications.Api.Middleware;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
    try
    {
        db.Database.Migrate();
        app.Logger.LogInformation("Notifications database migrated successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Database migration failed (may need manual setup), ensuring database is created");
        try
        {
            db.Database.EnsureCreated();
            app.Logger.LogInformation("Notifications database created via EnsureCreated");
        }
        catch (Exception ex2)
        {
            app.Logger.LogError(ex2, "Database creation also failed - service will start but DB operations will fail");
        }
    }
}

app.UseMiddleware<RawBodyMiddleware>();
app.UseMiddleware<InternalTokenMiddleware>();
app.UseMiddleware<TenantMiddleware>();

app.MapHealthEndpoints();
app.MapNotificationEndpoints();
app.MapTemplateEndpoints();
app.MapGlobalTemplateEndpoints();
app.MapProviderEndpoints();
app.MapWebhookEndpoints();
app.MapBillingEndpoints();
app.MapContactEndpoints();
app.MapBrandingEndpoints();
app.MapInternalEndpoints();

var port = builder.Configuration["PORT"] ?? "5008";
app.Run($"http://0.0.0.0:{port}");
