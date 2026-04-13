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
        var renames = new (string Old, string New)[]
        {
            ("notifications",                    "ntf_notifications"),
            ("notification_attempts",            "ntf_notification_attempts"),
            ("templates",                        "ntf_templates"),
            ("template_versions",                "ntf_template_versions"),
            ("notification_events",              "ntf_notification_events"),
            ("recipient_contact_health",         "ntf_recipient_contact_health"),
            ("delivery_issues",                  "ntf_delivery_issues"),
            ("contact_suppressions",             "ntf_contact_suppressions"),
            ("tenant_billing_plans",             "ntf_tenant_billing_plans"),
            ("tenant_billing_rates",             "ntf_tenant_billing_rates"),
            ("tenant_rate_limit_policies",       "ntf_tenant_rate_limit_policies"),
            ("tenant_contact_policies",          "ntf_tenant_contact_policies"),
            ("tenant_brandings",                 "ntf_tenant_brandings"),
            ("usage_meter_events",               "ntf_usage_meter_events"),
            ("tenant_provider_configs",          "ntf_tenant_provider_configs"),
            ("tenant_channel_provider_settings", "ntf_tenant_channel_provider_settings"),
            ("provider_health",                  "ntf_provider_health"),
            ("provider_webhook_logs",            "ntf_provider_webhook_logs"),
        };

        var dbName = db.Database.GetDbConnection().Database;
        foreach (var (oldName, newName) in renames)
        {
            var checkSql = $@"
                SELECT COUNT(*) FROM information_schema.tables
                WHERE table_schema = '{dbName}' AND table_name = '{oldName}'";
            var checkNewSql = $@"
                SELECT COUNT(*) FROM information_schema.tables
                WHERE table_schema = '{dbName}' AND table_name = '{newName}'";

            using var cmd = db.Database.GetDbConnection().CreateCommand();
            await db.Database.OpenConnectionAsync();

            cmd.CommandText = checkSql;
            var oldExists = Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;

            cmd.CommandText = checkNewSql;
            var newExists = Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;

            if (oldExists && !newExists)
            {
                await db.Database.ExecuteSqlRawAsync($"RENAME TABLE `{oldName}` TO `{newName}`");
                app.Logger.LogInformation("Renamed table {Old} → {New}", oldName, newName);
            }
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Table prefix rename step failed — tables may already be renamed");
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
