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
        await SchemaRenamer.RenameSchemaAsync(db, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Schema rename step failed — tables/columns may already be renamed");
    }

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

    // ── Migration coverage self-test ─────────────────────────────────────
    // Compares every EF-mapped column against the live schema and logs an
    // ERROR if any are missing. Catches the class of bug behind Task #58:
    // a migration committed without its [Migration] attribute (or otherwise
    // un-applied) leaves the EF model and the live schema out of sync,
    // which previously surfaced only as runtime "Unknown column" errors.
    try
    {
        await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
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

app.Run();
