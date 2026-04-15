using Reports.Contracts.Adapters;
using Reports.Contracts.Guardrails;
using Reports.Contracts.Queue;

namespace Reports.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1")
            .WithTags("Health")
            .AllowAnonymous();

        group.MapGet("/health", () => Results.Ok(new
        {
            status    = "healthy",
            service   = "Reports Service",
            timestamp = DateTimeOffset.UtcNow,
        }));

        group.MapGet("/ready", async (
            IIdentityAdapter identity,
            ITenantAdapter tenant,
            IEntitlementAdapter entitlement,
            IAuditAdapter audit,
            IDocumentAdapter document,
            INotificationAdapter notification,
            IProductDataAdapter productData,
            IJobQueue queue,
            IGuardrailValidator guardrails,
            IConfiguration config) =>
        {
            var checks = new Dictionary<string, string>();

            checks["config_loaded"] = !string.IsNullOrEmpty(config["ReportsService:ServiceName"]) ? "ok" : "fail";

            checks["identity_adapter"] = await ProbeAdapter(() => identity.ValidateTokenAsync("probe")) ? "ok" : "fail";
            checks["tenant_adapter"] = await ProbeAdapter(() => tenant.IsTenantActiveAsync("probe")) ? "ok" : "fail";
            checks["entitlement_adapter"] = await ProbeAdapter(() => entitlement.CanAccessReportsAsync("probe", "probe")) ? "ok" : "fail";
            checks["audit_adapter"] = await ProbeAdapter(() => audit.RecordEventAsync("probe", "probe", "readiness", "probe")) ? "ok" : "fail";
            checks["document_adapter"] = await ProbeAdapter(() => document.RetrieveReportAsync("probe")) ? "ok" : "fail";
            checks["notification_adapter"] = await ProbeAdapter(() => notification.NotifyReportReadyAsync("probe", "probe", "probe", "probe")) ? "ok" : "fail";
            checks["product_data_adapter"] = await ProbeAdapter(() => productData.GetAvailableProductsAsync("probe")) ? "ok" : "fail";
            checks["job_queue"] = await ProbeAdapter(() => queue.GetPendingCountAsync()) ? "ok" : "fail";
            checks["guardrails"] = guardrails.ValidateExecutionLimits("probe", "probe").IsValid ? "ok" : "fail";

            var allOk = checks.Values.All(v => v == "ok");

            var response = new
            {
                status    = allOk ? "ready" : "degraded",
                service   = "Reports Service",
                checks,
                timestamp = DateTimeOffset.UtcNow,
            };

            return allOk ? Results.Ok(response) : Results.Json(response, statusCode: 503);
        });
    }

    private static async Task<bool> ProbeAdapter(Func<Task> probe)
    {
        try { await probe(); return true; }
        catch { return false; }
    }

    private static async Task<bool> ProbeAdapter<T>(Func<Task<T>> probe)
    {
        try { await probe(); return true; }
        catch { return false; }
    }
}
