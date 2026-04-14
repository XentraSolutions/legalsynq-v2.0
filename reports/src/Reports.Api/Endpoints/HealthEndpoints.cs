using Reports.Contracts.Adapters;
using Reports.Contracts.Queue;

namespace Reports.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new
        {
            status    = "healthy",
            service   = "Reports Service",
            timestamp = DateTimeOffset.UtcNow,
        }))
        .WithTags("Health")
        .AllowAnonymous();

        app.MapGet("/ready", (
            IIdentityAdapter identity,
            ITenantAdapter tenant,
            IEntitlementAdapter entitlement,
            IAuditAdapter audit,
            IDocumentAdapter document,
            INotificationAdapter notification,
            IProductDataAdapter productData,
            IJobQueue queue,
            IConfiguration config) =>
        {
            var checks = new Dictionary<string, string>
            {
                ["config_loaded"]       = config != null ? "ok" : "fail",
                ["identity_adapter"]    = identity != null ? "ok" : "fail",
                ["tenant_adapter"]      = tenant != null ? "ok" : "fail",
                ["entitlement_adapter"] = entitlement != null ? "ok" : "fail",
                ["audit_adapter"]       = audit != null ? "ok" : "fail",
                ["document_adapter"]    = document != null ? "ok" : "fail",
                ["notification_adapter"] = notification != null ? "ok" : "fail",
                ["product_data_adapter"] = productData != null ? "ok" : "fail",
                ["job_queue"]           = queue != null ? "ok" : "fail",
            };

            var allOk = checks.Values.All(v => v == "ok");

            return Results.Ok(new
            {
                status    = allOk ? "ready" : "degraded",
                service   = "Reports Service",
                checks,
                timestamp = DateTimeOffset.UtcNow,
            });
        })
        .WithTags("Health")
        .AllowAnonymous();
    }
}
