using Xenia.Application.Email;
using Xenia.Application.TenantContext;

namespace Xenia.Api.Endpoints;

/// <summary>
/// Tenant-scoped Email module settings endpoints.
///
/// GET  /email/settings      — returns current settings (creates defaults on first access)
/// PUT  /email/settings      — update settings
/// </summary>
public static class XeniaEmailSettingsEndpoints
{
    public static IEndpointRouteBuilder MapXeniaEmailSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/email/settings");

        // GET /email/settings
        group.MapGet("/", async (
            XeniaTenantContextAccessor tenantAccessor,
            IEmailSettingsService settingsService,
            CancellationToken ct) =>
        {
            var ctx = tenantAccessor.Current;
            if (ctx is null || !ctx.IsResolved)
                return Results.BadRequest(new { error = "Tenant context is required." });

            var settings = await settingsService.GetOrCreateAsync(ctx.TenantId, ct);
            return Results.Ok(settings);
        }).RequireAuthorization(XeniaPolicies.EmailRead);

        // PUT /email/settings
        group.MapPut("/", async (
            UpdateEmailSettingsRequest request,
            XeniaTenantContextAccessor tenantAccessor,
            IEmailSettingsService settingsService,
            CancellationToken ct) =>
        {
            var ctx = tenantAccessor.Current;
            if (ctx is null || !ctx.IsResolved)
                return Results.BadRequest(new { error = "Tenant context is required." });

            try
            {
                var updated = await settingsService.UpdateAsync(ctx.TenantId, ctx.ActorId, request, ct);
                return Results.Ok(updated);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Concurrency conflict"))
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization(XeniaPolicies.EmailManage);

        return app;
    }
}
