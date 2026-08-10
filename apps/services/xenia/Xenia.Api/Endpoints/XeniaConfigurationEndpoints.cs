using Xenia.Application.Configuration;
using Xenia.Application.TenantContext;

namespace Xenia.Api.Endpoints;

public static class XeniaConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapXeniaConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/configuration").RequireAuthorization(XeniaPolicies.ConfigurationRead);

        // GET /configuration — returns non-secret configuration visible to the caller.
        // Secret values are always omitted (ConfigurationEntryDto.ConfigurationValue is null when IsSecret=true).
        group.MapGet("/", async (
            XeniaTenantContextAccessor tenantAccessor,
            IXeniaConfigurationService configService,
            string? @namespace,
            CancellationToken ct) =>
        {
            var tenantId = tenantAccessor.Current?.IsResolved == true
                ? tenantAccessor.Current.TenantId
                : (Guid?)null;

            var entries = await configService.GetVisibleConfigurationAsync(tenantId, @namespace, ct);

            return Results.Ok(new
            {
                entries,
                total = entries.Count,
                tenant_scoped = tenantId.HasValue,
                note = "Secret values are omitted. is_secret=true entries have null configuration_value.",
            });
        });

        return app;
    }
}
